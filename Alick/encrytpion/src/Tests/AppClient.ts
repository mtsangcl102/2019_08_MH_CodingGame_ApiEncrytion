import {IUser} from '../entities/IUser';
import {EncryptionService} from '../Services/EncryptionService';
import {IRespondData} from '../entities/IRespondData';
import {IRequestData} from '../entities/IRequestData';
import {ICard} from '../entities/ICard';
import {ApiError} from '../errors/ApiError';
import {ErrorCode} from '../states/ErrorCode';
import axios from 'axios';
import * as crypto from 'crypto';
import * as zlib from 'zlib';
import {isBuffer, isString} from "util";

interface ILastRequest {
  endPoint: string;
  data: IRequestData;
}

export class ApiClient {
  // services
  private _encryptionService: EncryptionService;
  public nonce: number = 0;
  public timeout: number;
  public version: string = '1.0';
  public versionKey: string = 'version-key';
  public lastRequest: ILastRequest | null;

  public localSendInvalidRequestData: boolean;
  public localSendInvalidSignBase64: boolean;
  public remoteSendInvalidPayload: boolean;
  public remoteTimeout: boolean;


  // for testing
  public triggerInvalidServerPayload = false;
  public triggerInvalidSignature = false;
  public triggerInvalidClientPayload = false;
  public triggerTimeout = false;

  // return from server
  public user: IUser;
  public cards: ICard[];
  public receivedTimestamp: number;
  public serverTimestamp: number;
  public token: string;

  constructor(public readonly host: string, public readonly deviceId: string, requestTimeout: number) {
    this._encryptionService = new EncryptionService();
    this.timeout = requestTimeout;
    this.nonce = Math.random() * (1 << 30) | 0;
  }

  public generateNonce(): Buffer {
    const nonceBuffer = Buffer.alloc(8);
    nonceBuffer.writeUInt32LE(this.nonce, 0);
    this.nonce += 1;
    return nonceBuffer;
  }

  public get currentTimestamp(): number {
    return +new Date() / 1000 | 0;
  }

  public get predictedServerTimestamp(): number {
    const timestamp = this.currentTimestamp;
    return this.serverTimestamp + (timestamp - this.receivedTimestamp);
  }

  public async login() {
    const data = {deviceId: this.deviceId};
    const respondData = await this._request('/users/login', data);
    this.token = respondData.token;
  }

  public async listCards() {
    const data = {};
    const respondData = await this._request('/cards/list', data);
    this.cards = respondData.body.cards;
    return this.cards;
  }

  public async createCard(monsterId: number) {
    const data = {monsterId};
    const respondData = await this._request('/cards/create', data);
    this.cards = respondData.body.cards;
    return respondData;
  }

  public async updateCard(cardId: number, exp: number) {
    const data = {cardId, exp};
    const respondData = await this._request('/cards/update', data);
    this.cards = respondData.body.cards;
    return respondData;
  }

  public async deleteCard(cardId: number) {
    const data = {cardId};
    const respondData = await this._request('/cards/delete', data);
    this.cards = respondData.body.cards;
    return respondData;
  }

  public get canResend(): boolean {
    return !!(this.lastRequest);
  }

  public async resend(): Promise<IRespondData> {
    if (this.canResend) {
      return await this._request(this.lastRequest.endPoint, this.lastRequest.data);
    }
  }

  public getCurrentUnixTime() {
    return Date.now() / 1000 | 0;
  }

  private updateUserData(responseData: IRespondData) {
    if (responseData.user) {
      this.user = responseData.user;
    }
  }

  private constructPostData(nonce: Buffer, data: IRequestData) {
    let cacheKey = crypto.createHash('md5').update(JSON.stringify(data)).digest().toString('base64');

    let payloadData = {
        version: this.version,
        versionKey: this.versionKey,
        timestamp: this.predictedServerTimestamp,
        session: this.user ? this.user.session : '',
        token: this.token ? this.token : '',
        cacheKey: cacheKey + (this.user ? this.user.session : ''),
      }
    ;
    Object.assign(payloadData, data);
    // Generate Payload (POST Data)

    let payload = this._encryptionService.encrypt(
      Buffer.from(JSON.stringify(payloadData)),
      nonce
    );

    if (this.localSendInvalidRequestData) {
      payload = Buffer.alloc(10);
    }
    return payload;
  }

  private constructGetData(nonce: Buffer, payload: any) {
    // Generate Parameter (GET Data)
    let md5String = crypto.createHash('md5').update(payload).digest();
    let messageToBeSign = Buffer.concat([
      nonce,
      md5String
    ]);
    let signedMessage = this._encryptionService.signMessage(messageToBeSign).toString('base64');

    if (this.localSendInvalidSignBase64) {
      signedMessage = 'INVALID_STRING';
    }

    let params = {
      version: this.version,
      versionKey: this.versionKey,
      timestamp: this.currentTimestamp,
      signedBase64: signedMessage,
      remoteTimeout: this.remoteTimeout,
      remoteSendInvalidPayload: this.remoteSendInvalidPayload,
      token: this.token ? this.token : ''
    };
    return params;
  }

  private async sendApiRequest(endPoint: string, nonce: Buffer, payload: Buffer, params: any) {
    let response = null;
    try {
      response = await axios.request({
        method: 'post',
        url: this.host + endPoint,
        responseType: 'arraybuffer',
        data: {
          payloadBase64: payload.toString('base64'),
          nonce,
        },
        params,
        timeout: this.timeout,
        transformResponse: (r: any) => {
          return r;
        }
      });
    } catch (e) {
      if (e.code === 'ECONNABORTED') {
        throw new ApiError(ErrorCode.Timeout, e.message);
      } else {
        console.log(e.code);
      }
    }
    return response;
  }

  private handleServerError(respondData: any) {
    try {
      let errorData = <IRespondData> JSON.parse(respondData.toString());
      if (errorData && errorData.errorCode) {
        this.receivedTimestamp = errorData.timestamp;
        this.serverTimestamp = errorData.timestamp;
        throw new ApiError(errorData.errorCode);
      }
    } catch (e) {
      if ((e instanceof ApiError))
        throw e;
    }
  }

  private getDecyptedData(nonce: Buffer, respondData: any) {
    try {
      let decryptedMessage = this._encryptionService.decrypt(respondData, nonce);
      let returnData = null;
      if (decryptedMessage) {
        let responseData = zlib.gunzipSync(decryptedMessage);
        returnData = <IRespondData> JSON.parse(responseData.toString());
        this.updateUserData(returnData);
        this.receivedTimestamp = returnData.timestamp;
        this.serverTimestamp = returnData.timestamp;

        this.lastRequest = null;
        return returnData;
      }
    } catch (e) {
      throw new ApiError(ErrorCode.FailedToDecryptServerPayload, e.message);
    }
  }

  private async _request(endPoint: string, data: IRequestData): Promise<IRespondData> {
    this.lastRequest = {
      endPoint,
      data
    };

    // Prepare Data
    let nonce = this.generateNonce();
    let payload = this.constructPostData(nonce, data);
    let params = this.constructGetData(nonce, payload);

    // Send Request
    let response = await this.sendApiRequest(endPoint, nonce, payload, params);
    const respondData = response.data;

    // Handle Server Error Message
    this.handleServerError(respondData);

    // Decrypt Message from Server
    return this.getDecyptedData(nonce, respondData);
  }
}

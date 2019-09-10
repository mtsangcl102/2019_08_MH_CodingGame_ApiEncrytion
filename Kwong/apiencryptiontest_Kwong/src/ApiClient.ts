import {IUser} from './entities/IUser';
import {EncryptionService} from './EncryptionService';
import {IRespondData} from './entities/IRespondData';
import {ApiError} from './errors/ApiError';
import {ErrorCode} from './states/ErrorCode';
import {ICard} from './entities/ICard';
import {IRequestData} from './entities/IRequestData';
import axios from 'axios';
import * as crypto from 'crypto';
import * as zlib from 'zlib';
import json = Mocha.reporters.json;

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

    // for testing
    // public triggerInvalidServerPayload = false;
    // public triggerInvalidSignature = false;
    // public triggerInvalidClientPayload = false;
    // public triggerTimeout = false;
    public remoteSendInvalidPayload  = false;
    public localSendInvalidSignBase64  = false;
    public localSendInvalidRequestData = false;
    public remoteTimeout = false;

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
            return await this._request(this.lastRequest.endPoint, this.lastRequest.data, true);
        }
    }

    private async _request(endPoint: string, data: IRequestData, isResend = false): Promise<IRespondData> {

        const instance = axios.create({
            responseType: 'arraybuffer',
            baseURL: this.host,
            timeout: this.timeout,
        });

        // POST Data
        data.version = this.version;
        data.versionKey = this.versionKey;
        if(!!(this.user) && !!(this.user.session)) data.session = this.user.session;
        data.timestamp = (!!(this.serverTimestamp)) ? this.serverTimestamp : this.currentTimestamp;
        if(!!(this.user) && typeof data.cacheKey === 'undefined') data.cacheKey = this.user.userId.toString() + this.user.session + this.nonce.toString();

        // Last Request
        this.lastRequest = {
            'data': data,
            'endPoint': endPoint
        };

        const nonceBuffer = this._encryptionService.generateNonce(this.nonce);
        this.nonce += 1;

        const payload = Buffer.from(JSON.stringify(data));
        const encryptedPayload = this._encryptionService.encrypt(payload, nonceBuffer, this.localSendInvalidRequestData);
        const encryptedBase64 = encryptedPayload.toString('base64');
        let post = {
            'payloadBase64': encryptedBase64
        };

        // Query Data
        const hashBuffer = crypto.createHash('md5').update(encryptedPayload).digest();
        const concatBuffer = Buffer.concat([nonceBuffer, Buffer.from(hashBuffer)], nonceBuffer.length + hashBuffer.length);
        const signedPayload = this._encryptionService.signMessage(concatBuffer, this.localSendInvalidSignBase64);
        const signedBase64 = signedPayload.toString('base64');
        this.token = this.token ? this.token : "";
        let query = {
            'signedBase64': signedBase64,
            'token': this.token,
            'remoteTimeout': this.remoteTimeout.toString(),
            'remoteSendInvalidPayload': this.remoteSendInvalidPayload.toString()
        };

        // API Call & Get Respond
        let result;
        await instance.post(endPoint, post, {params: query})
            .then(response => {
                result = response;
            })
            .catch(err => {
                switch(err.code){
                    case 'ECONNABORTED':
                        throw new ApiError(ErrorCode.Timeout);
                        break;
                }
            });

        if(result) {
            const isRespondEncrypted = result.headers['content-encoding'] == 'encrypted';
            let respondData: IRespondData;
            respondData = {};

            // Check if Encrypted Respond => Do Decrypt
            if (isRespondEncrypted) {
                try {
                    const decryptedRespond = zlib.gunzipSync(this._encryptionService.decrypt(result.data, nonceBuffer)).toString();
                    const jsonRespond = JSON.parse(decryptedRespond);

                    respondData.token = jsonRespond.token;
                    if (!!(jsonRespond.body) && typeof respondData.body === 'undefined')
                        respondData.body = jsonRespond.body;
                    // this.serverTimestamp = jsonRespond.timestamp;
                    this.receivedTimestamp = this.currentTimestamp;
                    this.user = jsonRespond.user;
                    if(!!(jsonRespond.isCache)) respondData.isCache = jsonRespond.isCache;
                    if(isResend) this.lastRequest = null;
                    return respondData;

                } catch (e) {
                    if (!!(e.errorCode)) throw new ApiError(e.errorCode);
                    else throw new ApiError(ErrorCode.FailedToDecryptServerPayload);

                }

            // Not Encrypted Respond
            } else {
                const jsonRespond = JSON.parse(result.data.toString());
                throw new ApiError(jsonRespond.errorCode);

            }
        }
    }

}
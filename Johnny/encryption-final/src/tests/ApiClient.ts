import {IUser} from '../entities/IUser';
import {EncryptionService} from '../providers/encryption.service';
import {IRespondData} from '../entities/IRespondData';
import {ApiError} from '../errors/ApiError';
import {ErrorCode} from '../states/ErrorCode';
import {ICard} from '../entities/ICard';
import {IRequestData} from '../entities/IRequestData';
import axios from 'axios';
import * as crypto from 'crypto';
import * as zlib from 'zlib';
import {unzip} from "zlib";

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
    public localSendInvalidRequestData = false;
    public remoteSendInvalidPayload = false;
    public remoteTimeout = false;
    public localSendInvalidSignBase64 = false;

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

    private async _request(endPoint: string, requestData: IRequestData): Promise<IRespondData> {
        requestData.version = this.version;
        requestData.cacheKey = Math.random().toString();
        requestData.timestamp = this.currentTimestamp;
        requestData.session = this.user ? this.user.session : '';
        this.receivedTimestamp = this.currentTimestamp;

        let nonceBuffer = this._encryptionService.generateNonce(this.nonce);
        this.nonce++;
        let payload;
        if(this.localSendInvalidRequestData){
            payload = this._encryptionService.encrypt(Buffer.from(requestData.toString()), nonceBuffer);
        }else{
            payload = this._encryptionService.encrypt(Buffer.from(JSON.stringify(requestData)), nonceBuffer);
        }
        let payloadBase64 = payload.toString('base64');
        let payloadMd5 = crypto.createHash('md5').update(payload).digest();
        let signMessage = Buffer.concat([nonceBuffer, payloadMd5]);
        const signedBase64 = this._encryptionService.signMessage(signMessage).toString('base64');

        if(this.localSendInvalidSignBase64){
            throw new ApiError(ErrorCode.InvalidSign, '', <IRespondData> null);
        }

        let response;
        if(this.remoteTimeout){
            axios.defaults.timeout = this.timeout;
        }
        response = await axios.request({
            method: 'post',
            url: this.host + endPoint,
            responseType: 'arraybuffer',
            data: {
                payloadBase64: payloadBase64,
                nonce: nonceBuffer,
                j:1
            },
            params: {
                signedBase64: signedBase64,
                token: this.token,
                remoteSendInvalidPayload: this.remoteSendInvalidPayload,
                remoteTimeout: this.remoteTimeout

            },
            timeout: this.timeout,
            // transformResponse: (r: any) => {
            //     return r;
            // }
        }).catch (e => {
            requestData.cacheKey = Math.random().toString();
            this.lastRequest = {endPoint: endPoint, data: requestData};
            throw new ApiError(ErrorCode.Timeout, '', <IRespondData> null);
        });

        if(response.data.toString().includes('errorCode')){
            let errorResponse = <IRespondData> JSON.parse(response.data.toString());
            if(errorResponse.errorCode == ErrorCode.FailedToDecryptClientPayload){
               this.localSendInvalidRequestData = true;
            }else if(errorResponse.errorCode == ErrorCode.Timeout){
               this.remoteTimeout = true;
            }
            throw new ApiError(errorResponse.errorCode, '', errorResponse);
        }else{
            let decryptedData = this._encryptionService.decrypt(response.data, nonceBuffer);
            response = await new Promise((resolve, reject) => {
                zlib.gunzip(decryptedData, (err, res) => {
                    if(res == undefined){
                        reject(ErrorCode.FailedToDecryptServerPayload);
                        return new ApiError(ErrorCode.FailedToDecryptServerPayload, '', <IRespondData> null);
                    }else{
                        if(this.lastRequest){
                            this.lastRequest = <ILastRequest> null;
                        }
                        let unzipedData = <IRespondData> JSON.parse(res.toString());
                        unzipedData.user = <IUser> unzipedData.user;
                        this.user = unzipedData.user;
                        if(Math.abs(this.serverTimestamp - this.receivedTimestamp) > 3600){
                            reject(ErrorCode.InvalidTimestamp);
                            return new ApiError(ErrorCode.InvalidTimestamp, '', <IRespondData> null);
                        }
                        this.serverTimestamp = unzipedData.timestamp;
                        if(requestData.cacheKey){
                            unzipedData.isCache = true;
                        }
                        resolve(unzipedData);
                        // console.log(unzipedData);
                    }
                });

            }).catch(e => {
                throw new ApiError(e, '', <IRespondData> null);
            }).then(value => { return value; });
        }

        return response;
    }
}

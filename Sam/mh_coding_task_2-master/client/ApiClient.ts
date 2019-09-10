import {IUser} from '../src/entities/IUser';
import EncryptionService from "../src/providers/encryption.service";
import {IRespondData} from '../src/entities/IRespondData';
import {ApiError} from '../src/errors/ApiError';
import {ErrorCode} from '../src/states/ErrorCode';
import {ICard} from '../src/entities/ICard';
import {IRequestData} from '../src/entities/IRequestData';
import axios from 'axios';
import * as zlib from 'zlib';
import * as crypto from 'crypto';

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
    public triggerInvalidServerPayload = false;
    public triggerInvalidSignature = false;
    public triggerInvalidClientPayload = false;
    public triggerTimeout = false;
    public localSendInvalidRequestData: boolean;
    public remoteSendInvalidPayload: boolean;
    public remoteTimeout: boolean;
    public localSendInvalidSignBase64: boolean;

    // return from server
    public user: IUser;
    public cards: ICard[];
    public receivedTimestamp: number;
    public serverTimestamp: number;
    public token: string;

    constructor(public readonly host: string, public readonly deviceId: string, requestTimeout: number) {
        this._encryptionService = new EncryptionService();
        this.timeout = requestTimeout;
        this.nonce = Math.random() * 256 * 256 * 256 | 0;
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
        this.user = respondData.user;
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

    private async _request(endPoint: string, data: IRequestData): Promise<IRespondData> {
        this.lastRequest = {endPoint, data};
        this.nonce++;
        // Constructing request
        const encryptionService: EncryptionService = new EncryptionService();
        const instance = axios.create({
            responseType: 'arraybuffer',
            baseURL: this.host,
        });
        data.version = this.version;
        data.versionKey = this.versionKey;
        data.session = this.user? this.user.session: null;
        data.timestamp = data.timestamp? data.timestamp: this.currentTimestamp;
        delete data.cacheKey;
        data.cacheKey = crypto.createHash('md5').update(JSON.stringify(data)).digest('hex');
        const payload = !this.localSendInvalidRequestData?
            encryptionService.encrypt(Buffer.from(JSON.stringify(data)),this.nonce):
            Buffer.from('123');
        const post = {
            payloadBase64: payload.toString('base64'),
            nonce: this.nonce,
            requestData: data,
        };
        const nonceBuffer = Buffer.alloc(8);
        nonceBuffer.writeUInt32LE(this.nonce, 0);
        const hash = crypto.createHash('md5').update(payload).digest();
        const signedBase64 = !this.localSendInvalidSignBase64?
            encryptionService.signMessage(Buffer.concat([nonceBuffer, hash])).toString('base64'):
            Buffer.from('123');
        const query = {
            signedBase64,
            token: this.token,
            remoteTimeout: this.remoteTimeout,
            remoteSendInvalidPayload: this.remoteSendInvalidPayload,
        };

        // Handling response
        const fetchResult = () => {
            return new Promise(
                async (resolve, reject)=>{
                    setTimeout(()=>reject(new ApiError(ErrorCode.Timeout)),this.timeout);
                    resolve(await instance.post(endPoint, post, {params: query}));
                });
        };
        const result: any = await fetchResult().catch(
            err => {
                throw err;
            }
        );
        const decryptedResultData = encryptionService.decrypt(result.data, this.nonce);
        const decodedBuffer = (gzippedBuffer) => {
            return new Promise(
                (resolve, reject)=>{
                    zlib.gunzip(gzippedBuffer, (err,data)=>{
                        if(!data){
                            try{
                                const errorBody = JSON.parse(result.data.toString());
                                reject(new ApiError(errorBody.errorCode));
                            }catch(err){
                                reject(new ApiError(ErrorCode.FailedToDecryptServerPayload));
                            }
                            return;
                        }
                        const payload = JSON.parse(data.toString());
                        if(this.serverTimestamp-this.currentTimestamp > 3600){
                            reject(new ApiError(ErrorCode.InvalidTimestamp));
                        }
                        resolve(payload);
                        return;
                    });
                });
        };
        return decodedBuffer(decryptedResultData).then(
            payload =>{
                this.lastRequest = null;
                return payload;
            }
        ).catch(
            err => {
                throw err;
            }
        );
    }

}

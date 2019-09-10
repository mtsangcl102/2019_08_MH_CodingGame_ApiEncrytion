import {IUser} from '../src/entities/IUser';
import {EncryptionService} from '../src/providers/encryption.service';
import {IRespondData} from '../src/entities/IRespondData';
import {ApiError} from '../src/errors/ApiError';
import {ErrorCode} from '../src/states/ErrorCode';
import {ICard} from '../src/entities/ICard';
import {IRequestData} from '../src/entities/IRequestData';
import axios from 'axios';
import * as crypto from 'crypto';
import * as zlib from 'zlib';
import {timeout} from "rxjs/operators";

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

    // return from server
    public user: IUser;
    public cards: ICard[];
    public receivedTimestamp: number;
    public serverTimestamp: number;
    public token: string;

    // self-define
    public localSendInvalidRequestData: boolean = false;
    public remoteSendInvalidPayload: boolean = false;
    public remoteTimeout: boolean = false;
    public localSendInvalidSignBase64: boolean = false;

    constructor(public readonly host: string, public readonly deviceId: string, requestTimeout: number) {
        this._encryptionService = new EncryptionService();
        this.timeout = requestTimeout;
        // this.nonce = Math.random() * 256 * 256 * 256 | 0;
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

    public generateNonce() {
        const nonceBuffer = Buffer.alloc(8);
        nonceBuffer.writeUInt32LE(this.nonce, 0);
        return nonceBuffer;
    }

    private async _request(endPoint: string, data: IRequestData): Promise<IRespondData> {
        // TODO: implement
        const host = 'https://nestjs-api-test.herokuapp.com';
        const instance = axios.create({
            responseType: 'arraybuffer',
            baseURL: host,
        });
        const nonceBuffer = this.generateNonce();
        this.nonce++;

        console.log(endPoint);
        console.log(data);
        if (endPoint == "/users/login") {
            // try {
                let requestData = {
                    version: this.version,
                    versionKey: this.versionKey,
                    timestamp: this.currentTimestamp,
                    deviceId: data.deviceId ? data.deviceId : ""
                };
                let payload = this._encryptionService.encrypt(Buffer.from(JSON.stringify(requestData)), nonceBuffer);
                if (this.localSendInvalidRequestData) {
                    payload = this._encryptionService.encrypt(Buffer.from(requestData.toString()), nonceBuffer);
                }
                let payloadBase64 = payload.toString("base64");
                let md5 = crypto.createHash('md5').update(payload).digest();
                let signedBase64 = this._encryptionService.signMessage(Buffer.concat([nonceBuffer, md5])).toString("base64");
                const post = {
                    payloadBase64: payloadBase64,
                    nonce: this.nonce
                };
                const query = {
                    signedBase64: signedBase64,
                    token: "",
                    remoteTimeout: this.remoteTimeout,
                    remoteSendInvalidPayload: this.remoteSendInvalidPayload
                };
                console.log(post);console.log(query);
                const sendTime = this.currentTimestamp;
                instance.defaults.timeout = this.timeout;
                const result = await instance.post('/users/login', post, {params: query}).catch(e => {
                    this.lastRequest = {endPoint: endPoint, data: data};
                    throw new ApiError(ErrorCode.Timeout, ErrorCode.Timeout.toString(), <IRespondData>null);
                });
                const returnData = result.data;
                // console.log(returnData.toString());

                // if ((this.currentTimestamp - sendTime) * 1000 > this.timeout) {
                //     this.lastRequest = {endPoint: endPoint, data: data};
                //     throw new ApiError(ErrorCode.Timeout, ErrorCode.Timeout.toString(), <IRespondData>null);
                // }

                if (returnData.toString().includes("errorCode")) {
                    let returnError = JSON.parse(returnData.toString());
                    console.log(returnError);
                    throw new ApiError(returnError.errorCode, returnError.errorCode, <IRespondData>returnError);
                }

                let returnResult: any;
                let respondPromise = await new Promise<IRespondData>(async (resolve, reject) => {
                    await zlib.gunzip(this._encryptionService.decrypt(returnData, nonceBuffer), (error, result) => {
                        if (result == undefined || result == null) {
                            reject(ErrorCode.FailedToDecryptServerPayload);
                            return new ApiError(ErrorCode.FailedToDecryptServerPayload, ErrorCode.FailedToDecryptServerPayload.toString(), <IRespondData>null);
                        }
                        const resultJson = JSON.parse(result.toString());
                        console.log(resultJson);
                        this.user = <IUser>resultJson.user;

                        resolve(resultJson);
                        returnResult = <IRespondData>resultJson;
                    });
                }).catch(e => {
                    throw new ApiError(e, e.toString(), <IRespondData>null);
                }).then(() => {
                    return returnResult;
                });
                return respondPromise;
            // } catch (e) {
            //     console.log(e);
            //     throw e;
            // }
        } else if (endPoint.startsWith("/cards/")) {
            let requestData = {
                version: this.version,
                versionKey: this.versionKey,
                timestamp: this.currentTimestamp,
                session: this.user.session ? this.user.session : "",
                cacheKey: data.cacheKey ? data.cacheKey : "",
                cardId: data.cardId ? data.cardId : "",
                monsterId: data.monsterId ? data.monsterId : "",
                exp: data.exp ? data.exp : ""
            };
            let payload = this._encryptionService.encrypt(Buffer.from(JSON.stringify(requestData)), nonceBuffer);
            let payloadBase64 = payload.toString("base64");
            let md5 = crypto.createHash('md5').update(payload).digest();
            let signedBase64 = this._encryptionService.signMessage(Buffer.concat([nonceBuffer, md5])).toString("base64");
            if (this.localSendInvalidSignBase64) {
                signedBase64 = Buffer.concat([nonceBuffer, md5]).toString("base64");
            }
            const post = {
                payloadBase64: payloadBase64,
                nonce: this.nonce
            };
            const query = {
                signedBase64: signedBase64,
                token: this.token,
                remoteTimeout: this.remoteTimeout,
                remoteSendInvalidPayload: this.remoteSendInvalidPayload
            };
            const sendTime = this.currentTimestamp;
            instance.defaults.timeout = this.timeout;
            const result = await instance.post(endPoint, post, {params: query}).catch(e => {
                this.lastRequest = {endPoint: endPoint, data: data};
                this.lastRequest.data.cacheKey = crypto.createHash('md5').update(payload).digest("hex");
                throw new ApiError(ErrorCode.Timeout, ErrorCode.Timeout.toString(), <IRespondData>null);
            });
            const returnData = result.data;

            // if ((this.currentTimestamp - sendTime) * 1000 > this.timeout) {
            // }

            if (returnData.toString().includes("errorCode")) {
                let returnError = JSON.parse(returnData.toString());
                console.log(returnError);
                throw new ApiError(returnError.errorCode, returnError.errorCode, <IRespondData>returnError);
            }

            let returnResult: any;
            let respondPromise = await new Promise<IRespondData>(async (resolve, reject) => {
                await zlib.gunzip(this._encryptionService.decrypt(returnData, nonceBuffer), (error, result) => {
                    if (result == undefined) {
                        reject(ErrorCode.FailedToDecryptServerPayload);
                        throw new ApiError(ErrorCode.FailedToDecryptServerPayload, ErrorCode.FailedToDecryptServerPayload.toString(), <IRespondData>null);
                    }
                    const resultJson = JSON.parse(result.toString());
                    console.log(resultJson);
                    if (resultJson.timestamp < this.serverTimestamp) {
                        reject(ErrorCode.InvalidTimestamp);
                        return new ApiError(ErrorCode.InvalidTimestamp, ErrorCode.InvalidTimestamp.toString(), <IRespondData>null);
                    }

                    resolve(resultJson);
                    if (data.cacheKey) {
                        resultJson.isCache = true;
                        this.lastRequest = null;
                    }
                    returnResult = <IRespondData>resultJson;
                });
            }).catch(e => {
                throw new ApiError(e, e.toString(), <IRespondData>null);
            }).then(() => {
                return returnResult;
            });
            return respondPromise;
        }
    }
}
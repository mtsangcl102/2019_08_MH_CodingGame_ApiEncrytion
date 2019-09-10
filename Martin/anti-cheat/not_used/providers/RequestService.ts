import {Injectable} from '@nestjs/common';
import {Response, Request} from 'express';
import {IRequestData} from '../entities/IRequestData';
import {EncryptionService} from './encryption.service';
import {ErrorCode} from '../states/ErrorCode';
import {IRespondData} from '../entities/IRespondData';
import {IUser} from '../entities/IUser';
import {UserService} from './user.service';
import {IRequestBody} from '../entities/IRequestBody';
import {ApiError} from '../errors/ApiError';
import * as crypto from 'crypto';
import * as zlib from 'zlib';

@Injectable()
export class RequestService {
    public logs = [];

    constructor(private readonly encryptionService: EncryptionService,
                private readonly userService: UserService) {

    }

    public addLog(type: string, url: string, data: any) {
        this.logs.unshift({
            type,
            dateTime: new Date(),
            baseUrl: url,
            data,
        });

        const max = 100;
        if (this.logs.length > max) {
            this.logs.slice(0, max);

        }
    }

    public async timeout(ms: number) {
        await new Promise(resolve => setTimeout(resolve, ms));
    }

    public canLock(user: IUser): boolean {
        if (user.lockTimestamp === 0) {
            return true;
        }

        // check if lock expired
        const currentTimestamp = +new Date() / 1000 | 0;
        const maxLockSeconds = 60;
        if (currentTimestamp >  user.lockTimestamp + maxLockSeconds) {
            return true;
        }

        return false;
    }

    public lock(user: IUser, signedBase64: string) {
        if (this.canLock(user)) {
            user.lockTimestamp = +new Date() / 1000 | 0;
            user.lockSignature = signedBase64;
        }
    }

    public unlock(user: IUser, signedBase64: string) {
        if (user.lockSignature === signedBase64) {
            user.lockTimestamp = 0;
        }
    }

    public verifyRequest(request: Request) {
        if (!request.query.signedBase64 || !request.body.payloadBase64) {
            throw new ApiError(ErrorCode.InvalidRequest);
        }
    }

    public verifyToken(token: string): IUser {
        const deviceId = this.encryptionService.verifyJwt(token);
        return this.userService.get(deviceId);
    }

    public verifyNonce(user: IUser, nonce: number): void {
        if (!nonce || nonce <= user.nonce) {
            throw new ApiError(ErrorCode.InvalidNonce);
        }
    }

    public async testingRemoteTimeout(query: any, message: IRequestBody) {
        if (query.remoteTimeout && query.remoteTimeout.toLocaleLowerCase() === 'true') {
            await this.timeout(5000);
        }
    }

    public openSigned(payloadBase64: string, signedBase64: string): number {
        let opened: Buffer;
        let digest: Buffer;
        let payload: Buffer;
        let signed: Buffer;

        try {
            payload = Buffer.from(payloadBase64, 'base64');
            signed = Buffer.from(signedBase64, 'base64');
            digest = crypto.createHash('md5').update(payload).digest();
            opened = this.encryptionService.signOpenMessage(signed);
        } catch (e) {
            throw new ApiError(ErrorCode.InvalidSign, e.message);
        }

        const nonce = opened.readInt32LE(0);
        const signedDigest = opened.slice(8);
        if (!digest.equals(signedDigest)) {
            throw new ApiError(ErrorCode.InvalidSign, `invalid digest. yours: ${signedDigest.toString('base64')}, expected: ${digest.toString('base64')}`);
        }

        return nonce;
    }

    public checkHasCache(cacheKey: string) {
        if (cacheKey) {
            const respondData = this.userService.getCache(cacheKey);
            if (respondData) {
                throw new ApiError(ErrorCode.HasCache, '', respondData);
            }
        }
    }

    public verifyVersion(version: string, versionKey: string) {
        if (version !== '1.0') {
            throw new ApiError(ErrorCode.InvalidVersion);
        }
    }

    public verifySession(user: IUser, session: string) {
        if (user.session !== session) {
            throw new ApiError(ErrorCode.InvalidSession);
        }
    }

    public verifyTimestamp(timestamp: number) {
        const currentTimestamp = (+new Date() / 1000 | 0);
        if (isNaN(timestamp)) {
            throw new ApiError(ErrorCode.InvalidTimestamp);
        } else {
            const diffTimestamp = Math.abs(timestamp - currentTimestamp);
            if (diffTimestamp > 3600) {
                throw new ApiError(ErrorCode.InvalidTimestamp);
            }
        }
    }

    public decryptData(payloadBase64: string, nonce: number): IRequestData {
        try {
            const input = new Buffer(payloadBase64, 'base64');
            const output = this.encryptionService.decrypt(input, nonce) as IRequestData;
            return JSON.parse(output.toString());
        } catch (err) {
            throw new ApiError(ErrorCode.FailedToDecryptClientPayload);
        }
    }

    public async sendData(response: Response, respondData: IRespondData) {
        const requestBody = response.req.body as IRequestBody;
        const cacheKey = requestBody.requestData.cacheKey;

        respondData.errorCode = 0;
        respondData.timestamp = +new Date() / 1000 | 0;
        respondData.user = requestBody.user;

        // save the cache
        if (cacheKey) {
            this.userService.setCache(cacheKey, respondData);
        }

        this.sendEncryptData(response, respondData, requestBody.nonce);
    }

    public sendEncryptData(response: Response, respondData: IRespondData, nonce: number) {
        const message = Buffer.from(JSON.stringify(respondData));
        const payload = this.encryptionService.encrypt(message, nonce);
        let gzippedPayload = zlib.gzipSync(payload);

        if (response.req.query.remoteSendInvalidPayload && response.req.query.remoteSendInvalidPayload.toLowerCase() === 'true') {
            gzippedPayload = Buffer.alloc(10);
        }

        response.setHeader('Content-Encoding', 'encrypted');
        response.send(gzippedPayload);
    }

    public sendRawError(response: Response, errorCode: ErrorCode) {
        const data = {
            errorCode: errorCode as number,
            timestamp: +new Date() / 1000 | 0,
        };
        response.send(data);
    }
}
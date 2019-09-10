import {Injectable, NestMiddleware} from '@nestjs/common';
import {Request, Response} from 'express';
import {IRequestBody} from '../entities/IRequestBody';
import {UserService} from '../providers/user.service';
import {RequestService} from '../providers/request.service';
import {ApiError} from '../errors/ApiError';
import {ErrorCode} from '../states/ErrorCode';

@Injectable()
export class ApiMiddleware implements NestMiddleware {
    constructor(private readonly userService: UserService,
                private readonly requestService: RequestService,
    ) {

    }

    async use(request: Request, response: Response, next: () => any) {
        const requestBody = request.body as IRequestBody;
        this.requestService.addLog('request', request.originalUrl, {requestBody});

        const query = request.query;
        const signedBase64 = query.signedBase64;
        const token = query.token;

        // verify client send enough things
        this.requestService.verifyRequest(request);

        // we validate the user identify first
        const user = this.requestService.verifyToken(token);
        requestBody.user = user;

        // get the nonce and verify sign
        const nonce = this.requestService.openSigned(requestBody.payloadBase64, signedBase64);
        requestBody.nonce = nonce;

        // we update the nonce after verify
        this.requestService.verifyNonce(user, nonce);
        this.userService.updateNonce(user, nonce);

        // decrypt
        const requestData = this.requestService.decryptData(requestBody.payloadBase64, nonce);
        requestBody.requestData = requestData;

        // verify data
        this.requestService.verifyVersion(requestData.version, requestData.versionKey);
        this.requestService.verifySession(user, requestData.session);
        this.requestService.verifyTimestamp(requestData.timestamp);
        this.requestService.checkHasCache(requestData.cacheKey);

        // lock for atomic request
        if (this.requestService.canLock(user)) {
            this.requestService.lock(user, signedBase64);

            await this.requestService.testingRemoteTimeout(query, requestBody);

            next();
            this.requestService.unlock(user, signedBase64);
        } else {
            throw new ApiError(ErrorCode.Locked);
        }

    }
}
import { Injectable, NestMiddleware } from '@nestjs/common';
import { Request, Response } from 'express';
import {UserService} from '../providers/user.service';
import {RequestService} from '../providers/request.service';
import {IRequestBody} from '../entities/IRequestBody';

@Injectable()
export class LoginMiddleware implements NestMiddleware {
    constructor(private readonly userService: UserService,
                private readonly requestService: RequestService,
    ) {

    }

    async use(request: Request, response: Response, next: () => any) {
        const requestBody = request.body as IRequestBody;

        this.requestService.addLog('request', request.originalUrl, {requestBody});

        const query = request.query;
        const signedBase64 = query.signedBase64;

        // verify client send enough things
        this.requestService.verifyRequest(request);

        // verify sign and get the nonce
        const nonce = this.requestService.openSigned(requestBody.payloadBase64, signedBase64);
        requestBody.nonce = nonce;

        // decrypt
        const requestData = this.requestService.decryptData(requestBody.payloadBase64, nonce);
        requestBody.requestData = requestData;

        // verify
        this.requestService.verifyVersion(requestData.version, requestData.versionKey);

        // testing
        await this.requestService.testingRemoteTimeout(query, requestBody);

        next();
    }
}
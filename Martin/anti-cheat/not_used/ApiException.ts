import {ArgumentsHost, Catch, ExceptionFilter, Injectable} from '@nestjs/common';
import {Response, Request} from 'express';
import {ApiError} from '../errors/ApiError';
import {RequestService} from '../providers/request.service';
import {ErrorCode} from '../states/ErrorCode';
import {IRequestBody} from '../entities/IRequestBody';
import {EncryptionService} from '../providers/encryption.service';

@Injectable()
@Catch(ApiError)
export class ApiException implements ExceptionFilter {
    constructor(private readonly requestService: RequestService, private readonly encryptionService: EncryptionService) {

    }

    async catch(exception: ApiError, host: ArgumentsHost) {
        console.log('catch api error new', exception.errorCode);

        // prepare objects
        const ctx = host.switchToHttp();
        const response = ctx.getResponse<Response>();
        const request = ctx.getRequest<Request>();
        const requestBody = request.body as IRequestBody;

        // log
        this.requestService.addLog('error', request.originalUrl, {error: exception.errorCode, errorMessage: exception.errorMessage});

        if (exception.errorCode === ErrorCode.HasCache) {
            exception.respondData.isCache = true;
            const payload = this.requestService.sendEncryptData(response, exception.respondData, requestBody.nonce);

        } else {
            this.requestService.sendRawError(response, exception.errorCode);
        }
    }
}
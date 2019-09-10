import {Body, Controller, Get, Post, Req, Res} from '@nestjs/common';
import {Request, Response} from 'express';
import {UserService} from '../providers/user.service';
import {EncryptionService} from '../providers/encryption.service';
import {IRequestBody} from '../entities/IRequestBody';
import {IRespondData} from '../entities/IRespondData';
import {ErrorCode} from '../states/ErrorCode';
import {RequestService} from '../providers/request.service';
import {ApiError} from '../errors/ApiError';

@Controller('users')
export class UsersController {
    constructor(private readonly userService: UserService,
                private readonly requestService: RequestService,
                private readonly encryptionService: EncryptionService,
    ) {
    }

    @Get('/')
    getLogin(): string {
        return '/users';
    }

    @Post('login')
    async index(@Req() request: Request, @Res() response: Response, @Body() requestBody: IRequestBody) {
        const query = request.query;
        const requestData = requestBody.requestData;

        // check is valid device id
        if (!requestData.deviceId || requestData.deviceId.length !== 32) {
            throw new ApiError(ErrorCode.InvalidDeviceId);
        }

        // login user
        const user = this.userService.login(requestData.deviceId);
        requestBody.user = user;
        this.userService.updateNonce(user, requestBody.nonce);
        this.userService.createSession(user);

        const respondData: IRespondData = {
            token: this.encryptionService.generateJwt(user.deviceId),
        };

        return await this.requestService.sendData(response, respondData);
    }
}
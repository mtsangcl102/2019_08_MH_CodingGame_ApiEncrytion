import {Body, Controller, Get, Post, Req, Res} from '@nestjs/common';
import {Request, Response} from 'express';
import {UserService} from '../providers/user.service';
import {ApiError} from '../errors/ApiError';
import {IRequestBody} from '../entities/IRequestBody';
import {RequestService} from '../providers/request.service';
import {EncryptionService} from '../providers/encryption.service';
import {IRespondData} from '../entities/IRespondData';

@Controller('cards')
export class CardsController {
    constructor(private readonly userService: UserService,
                private readonly requestService: RequestService,
                private readonly encryptionService: EncryptionService,
    ) {
    }

    @Get('/')
    index(@Req() request: Request): string {
        return '/cards';
    }

    @Post('list')
    async list(@Req() request: Request, @Res() response: Response, @Body() requestBody: IRequestBody) {
        const user = requestBody.user;
        const cards = this.userService.listCards(user.deviceId);
        const respondData: IRespondData = {
            body: {
                cards,
            },
        };
        return await this.requestService.sendData(response, respondData);
    }

    @Post('create')
    async create(@Req() request: Request, @Res() response: Response, @Body() requestBody: IRequestBody) {
        const user = requestBody.user;
        const requestData = requestBody.requestData;
        const [cards, card] = this.userService.createCard(user.deviceId, requestData.monsterId);
        const respondData: IRespondData = {
            body: {
                cards,
                card,
            },
        };
        return await this.requestService.sendData(response, respondData);
    }

    @Post('update')
    async update(@Req() request: Request, @Res() response: Response, @Body() requestBody: IRequestBody) {
        const user = requestBody.user;
        const requestData = requestBody.requestData;
        const [cards, card] = this.userService.updateCard(user.deviceId, requestData.cardId, requestData.exp);

        const respondData: IRespondData = {
            body: {
                cards,
                card,
            },
        };
        return await this.requestService.sendData(response, respondData);
    }

    @Post('delete')
    async delete(@Req() request: Request, @Res() response: Response, @Body() requestBody: IRequestBody) {
        const user = requestBody.user;
        const requestData = requestBody.requestData;
        const [cards, card] = this.userService.deleteCard(user.deviceId, requestData.cardId);

        const respondData: IRespondData = {
            body: {
                cards,
                card,
            },
        };
        return await this.requestService.sendData(response, respondData);
    }
}
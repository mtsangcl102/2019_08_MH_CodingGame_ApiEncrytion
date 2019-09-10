import {Injectable} from '@nestjs/common';
import {IUser} from '../entities/IUser';
import {ICard} from '../entities/ICard';
import {ApiError} from '../errors/ApiError';
import {ErrorCode} from '../states/ErrorCode';
import {IRespondData} from '../entities/IRespondData';

@Injectable()
export class UserService {
    private _users: Map<string, IUser> = new Map();
    private _cards: Map<string, ICard[]> = new Map();
    private _caches: Map<string, IRespondData> = new Map();
    private _totalUser: number = 0;

    constructor() {
        //
    }

    login(deviceId: string): IUser {
        let user = this._users.get(deviceId);
        if (!user) {
            this._totalUser++;
            user = {userId: this._totalUser, deviceId, nonce: 0, lockTimestamp: 0, session: ''};
            this._users.set(deviceId, user);
            this._cards.set(deviceId, []);
        }

        return user;
    }

    updateNonce(user: IUser, nonce: number) {
        user.nonce = nonce;
    }

    createSession(user: IUser) {
        user.session = Math.random().toString();
    }

    setCache(hash: string, respondData: IRespondData) {
        this._caches.set(hash, respondData);
    }

    getCache(hash: string): IRespondData | undefined {
        return this._caches.get(hash);
    }

    get(deviceId: string): IUser | undefined {
        return this._users.get(deviceId);
    }

    listCards(deviceId: string): ICard[] {
        return this._cards.get(deviceId);
    }

    createCard(deviceId: string, monsterId: number): [ICard[], ICard] {
        if (isNaN(monsterId) || monsterId <= 0) {
            throw  new ApiError(ErrorCode.InvalidMonsterId);
        }

        const cards = this.listCards(deviceId);
        const id = Math.max(0, ...cards.map(x => x.id)) + 1;
        const card: ICard = {
            id,
            monsterId,
            exp: 0,
        };
        cards.push(card);
        return [cards, card];
    }

    updateCard(deviceId: string, cardId: number, exp: number): [ICard[], ICard] {
        const cards = this.listCards(deviceId);
        const index = cards.findIndex(x => x.id === cardId);
        if (index >= 0) {
            const card = cards[index];
            card.exp = exp;
            return [cards, card];
        }

        throw new ApiError(ErrorCode.InvalidCardId);
    }

    deleteCard(deviceId: string, cardId: number): [ICard[], ICard] {
        const cards = this.listCards(deviceId);
        const index = cards.findIndex(x => x.id === cardId);
        if (index >= 0) {
            const card = cards[index];
            cards.splice(index, 1);
            return [cards, card];
        }

        throw new ApiError(ErrorCode.InvalidCardId);
    }
}
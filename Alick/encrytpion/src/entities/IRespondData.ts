import {IUser} from './IUser';
import {ICard} from './ICard';

export interface IRespondData {
    errorCode?: number;
    timestamp?: number;
    token?: string;
    user?: IUser;
    isCache?: boolean;
    body?: {
        cards?: ICard[],
        card?: ICard,
    };
}

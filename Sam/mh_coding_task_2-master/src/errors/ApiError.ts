import {ErrorCode} from '../states/ErrorCode';
import {IRespondData} from '../entities/IRespondData';

export class ApiError extends Error {
    constructor(public readonly errorCode: ErrorCode, public errorMessage: string = '', public respondData: IRespondData = null) {
        super(JSON.stringify({errorCode}));
        Object.setPrototypeOf(this, ApiError.prototype);
    }
}
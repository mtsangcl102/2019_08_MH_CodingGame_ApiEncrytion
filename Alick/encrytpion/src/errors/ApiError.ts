import {ErrorCode} from '../states/ErrorCode';
import {IRespondData} from '../entities/IRespondData';

export class ApiError extends Error {
    constructor(public readonly errorCode: ErrorCode, public errorMessage: string = '', public respondData: IRespondData = null) {
        super();

        // Set the prototype explicitly.
        Object.setPrototypeOf(this, ApiError.prototype);
    }
}

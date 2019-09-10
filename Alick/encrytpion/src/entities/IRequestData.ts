export interface IRequestData {
    // core
    version?: string;
    versionKey?: string;
    session?: string;
    timestamp?: number;
    cacheKey?: string;

    // api related
    deviceId?: string;
    cardId?: number;
    monsterId?: number;
    exp?: number;
}

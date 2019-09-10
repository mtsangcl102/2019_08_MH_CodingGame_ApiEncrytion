export interface IUser {
    deviceId: string;
    userId: number;
    nonce: number;
    session: string;
    lockTimestamp?: number;
    lockSignature?: string;
}
export enum ErrorCode {
    UnknownError = 1, // client error, not used
    Timeout = 2, // client error, checked
    FailedToDecryptServerPayload = 3, // client error, checked
    InvalidRequest = 4,
    FailedToDecryptClientPayload = 5, // checked
    InvalidSign = 6, // checked
    InvalidToken = 7, // checked
    InvalidNonce = 8, // checked
    InvalidSession = 9, // checked
    InvalidTimestamp = 10, // checked
    InvalidVersion = 11, // checked
    HasCache = 12, // checked
    Locked = 13, // checked
    InvalidDeviceId = 21, // checked
    InvalidMonsterId = 31, // checked
    InvalidCardId = 32, // checked
}

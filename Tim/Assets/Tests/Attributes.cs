using System;

namespace Tests
{
    public struct RequestData
    {
        public long timestamp;
        public string deviceId;
        public string session;
        public string version;
        public string versionKey;
        public string cacheKey;
        public int cardId;
        public int monsterId;
        public int exp;

    }

    [Serializable]
    public struct RespondData
    {
        public ErrorCode errorCode;
        public int timestamp;
        public bool isCache;
        public string token;
        public User user;
        public Body body;
    }

    [Serializable]
    public struct User
    {
        public int userId;
        public string deviceId;
        public string session;
    }

    [Serializable]
    public struct Body
    {
        public Card[] cards;
        public Card card;
    }

    [Serializable]
    public struct Card
    {
        public int id;
        public int monsterId;
        public int exp;
    }
    
    public enum ErrorCode 
    {
        UnknownError = 1, // client error, not used
        Timeout = 2, // client error, checked
        FailedToDecryptServerPayload = 3, // client error, checked
        InvalidRequest = 4, // checked
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
}
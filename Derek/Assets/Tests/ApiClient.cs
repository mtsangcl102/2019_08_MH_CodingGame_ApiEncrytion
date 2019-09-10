using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BestHTTP;
using BestHTTP.Decompression.Zlib;
using BestHTTP.Forms;
using BestHTTP.JSON;
using BestHTTP.SocketIO;
using unity.libsodium;
using UnityEngine;
using Random = System.Random;

namespace Tests
{
    public class LastRequest
    {
        public string EndPoint;
        public RequestData RequestData;
    }
    
    public class ApiClient
    {
        // service
        private EncryptionHelper _encryptionHelper = new EncryptionHelper();
        MD5 md5 = MD5.Create();
        public LastRequest LastRequest;
        public RespondData RespondData;
        
        // variables
        public long nonce = 1;
        public string Host;
        public string DeviceId;
        public string Version = "1.0";
        public string VersionKey = "any-derek-name";
        public int Timeout;
        public bool isSending = false;
        
        // for testing
        public bool remoteSendInvalidPayload = false;
        public bool localSendInvalidSignBase64 = false;
        public bool localSendInvalidRequestData = false;
        public bool remoteTimeout = false;
        
        // return from server
        public User user;
        public Card[] cards;
        public Card card;
        public long receivedTimestamp;
        public long serverTimestamp;
        public string token = "";
        
        public ApiClient(string host, string deviceId, int timeout)
        {
            Host = host;
            DeviceId = deviceId;
            Timeout = timeout;
            var random = new Random();
            nonce = random.Next(0, 1 << 30);
        }

        public long CurrentTimestamp
        {
            get { return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); }
        }
        
        public long PredictedServerTimestamp
        {
            get { return serverTimestamp + (CurrentTimestamp - receivedTimestamp);  }
        }
        
        
        public bool CanResend
        {
            get { return LastRequest != null && !isSending; }
        }

        public void Resend()
        {
            if (CanResend)
            {
                _request(LastRequest.EndPoint, LastRequest.RequestData);
            }
        }

        public void Login()
        {
            var requestData = new RequestData() {deviceId = DeviceId};
            _request("/users/login", requestData);
        }

        public void ListCards()
        {
            var requestData = new RequestData() {};
            _request("/cards/list", requestData);
        }

        public void CreateCard(int monsterid)
        {
            var requestData = new RequestData() {monsterId = monsterid};
            _request("/cards/create", requestData);
        }
        
        public void UpdateCard(int cardId, int exp)
        {
            var requestData = new RequestData() {cardId = cardId, exp = exp};
            _request("/cards/update", requestData);
        }
        
        public void DeleteCard(int cardId)
        {
            var requestData = new RequestData() {cardId = cardId};
            _request("/cards/delete", requestData);
        }

        public string getQuery(Dictionary<string, string> dict)
        {
            string query = string.Join("&", dict.Select(x => x.Key + "=" + Uri.EscapeDataString(x.Value)).ToArray());
            return query;
        }

        public void _request(string endPoint, RequestData requestData)
        {
            nonce++;
            
            if (String.IsNullOrEmpty(requestData.cacheKey))
            {
                requestData.cacheKey = $"512343564564{UnityEngine.Random.Range(0,10000)}";
            }
            
            requestData.timestamp = PredictedServerTimestamp;
            requestData.version = Version;
            requestData.versionKey = VersionKey;
            requestData.session = user.session;

            isSending = true;
            
            byte[] payload = _encryptionHelper.Encrypt(Encoding.ASCII.GetBytes(JsonUtility.ToJson(requestData)), nonce);
            RawJsonForm postForm = new RawJsonForm();
            if (localSendInvalidRequestData) payload[0] = 55;
            postForm.AddField("payloadBase64", Convert.ToBase64String(payload));
            

            Dictionary<string, string> dict = new Dictionary<string, string>();

            MD5 md5 = MD5.Create();
            string json = JsonUtility.ToJson(requestData);

            byte[] digest = md5.ComputeHash(payload);
            if (localSendInvalidSignBase64) digest[0] = 55;
            byte[] signMessage = BitConverter.GetBytes(nonce).Concat(digest).ToArray();
            byte[] signedMessage = _encryptionHelper.SignMessage(signMessage);
            
            dict.Add("signedBase64", Convert.ToBase64String(signedMessage));
            dict.Add("token", token);
            if (remoteSendInvalidPayload) dict.Add("remoteSendInvalidPayload", "true");
            if (remoteTimeout) dict.Add("remoteTimeout", "true");
//            dict.Add("remoteTimeout", remoteTimeout? "1":"0");
//            dict.Add("remoteSendInvalidPayload", remoteSendInvalidPayload? "1":"0");

            Uri url = new Uri($"{Host}{endPoint}?{getQuery(dict)}");
            HTTPRequest request = new HTTPRequest(url, HTTPMethods.Post, (originalRequest, response) => {
                                                                             if (response == null)
                                                                             {
                                                                                 RespondData = new RespondData() {errorCode = ErrorCode.Timeout};
                                                                             }
                                                                             else if (response.HasHeaderWithValue("Content-Encoding", "encrypted"))
                                                                             {
                                                                                 byte[] decrypted = _encryptionHelper.Decrypt(response.Data, nonce);

                                                                                 if (decrypted.Length <= 10)
                                                                                 {
                                                                                     RespondData = new RespondData() {errorCode = ErrorCode.FailedToDecryptServerPayload};
                                                                                     isSending = false;
                                                                                     return;
                                                                                 }

                                                                                 Stream rs = new MemoryStream(decrypted);
                                                                                 GZipStream stream = new GZipStream(rs, CompressionMode.Decompress);
                                                                                 MemoryStream us = new MemoryStream();
                                                                                 byte[] respondArr = new byte[decrypted.Length];
                                                                                 stream.CopyTo(us);
//                                                                                 stream.CopyTo(us);
                                                                                 RespondData = JsonUtility.FromJson<RespondData>(Encoding.UTF8.GetString(us.ToArray()));
                                                                                 
                                                                                 user = RespondData.user;
                                                                                 cards = RespondData.body.cards;
                                                                                 card = RespondData.body.card;

                                                                                 switch (endPoint)
                                                                                 {
                                                                                     case "/users/login":
                                                                                         serverTimestamp = receivedTimestamp = RespondData.timestamp;
                                                                                         token = RespondData.token;
                                                                                         user = RespondData.user;
                                                                                         break;
                                                                                     case "/cards/list":
                                                                                         cards = RespondData.body.cards;
                                                                                         card = RespondData.body.card;
                                                                                         
                                                                                         break;
                                                                                     case "/cards/create":
                                                                                         cards = RespondData.body.cards;
                                                                                         card = RespondData.body.card;
                                                                                         break;
                                                                                     case "/cards/update":
                                                                                         cards = RespondData.body.cards;
                                                                                         card = RespondData.body.card;
                                                                                         break;
                                                                                     case "/cards/delete":
                                                                                         cards = RespondData.body.cards;
                                                                                         card = RespondData.body.card;
                                                                                         break;
                                                                                 }
                                                                             }
                                                                             else
                                                                             {
                                                                                 RespondData = JsonUtility.FromJson<RespondData>(response.DataAsText);
                                                                             }
                                                                             isSending = false;
                                                                         });
            request.Timeout = TimeSpan.FromSeconds(Timeout);
            request.SetForm(postForm);
            request.Send();
            LastRequest = new LastRequest() {EndPoint = endPoint, RequestData = requestData};

        }
    }
}
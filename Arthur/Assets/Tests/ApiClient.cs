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
        public string VersionKey = "any-version-name";
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
            var requestData = new RequestData() {monsterId = monsterid, cacheKey = UnityEngine.Random.value.ToString()};
            _request("/cards/create", requestData);
        }
        
        public void UpdateCard(int cardId, int exp)
        {
            var requestData = new RequestData() {cardId = cardId, exp = exp, cacheKey = UnityEngine.Random.value.ToString()};
            _request("/cards/update", requestData);
        }
        
        public void DeleteCard(int cardId)
        {
            var requestData = new RequestData() {cardId = cardId, cacheKey = UnityEngine.Random.value.ToString()};
            _request("/cards/delete", requestData);
        }

        public string getQuery(Dictionary<string, string> dict)
        {
            string query = string.Join("&", dict.Select(x => x.Key + "=" + Uri.EscapeDataString(x.Value)).ToArray());
            return query;
        }

        public void _request(string endPoint, RequestData requestData)
        {
            isSending = true;

            requestData.version = Version;
            requestData.versionKey = VersionKey;
            requestData.deviceId = DeviceId;
            requestData.timestamp = PredictedServerTimestamp;
            requestData.session = user.session;
            
            
            Byte[] payload = _encryptionHelper.Encrypt(Encoding.UTF8.GetBytes(localSendInvalidRequestData ? "_" : "" + JsonUtility.ToJson(requestData)), ++nonce);
            string payloadBase64 = Convert.ToBase64String(payload);
            var postForm = new RawJsonForm();
            postForm.AddField("payloadBase64", payloadBase64);
            
            // signature
            Byte[] digest = md5.ComputeHash(payload);
            Byte[] nonceByte = BitConverter.GetBytes(nonce);
            Byte[] signMessage = nonceByte.Concat(digest).ToArray();
            string signedBase64 = Convert.ToBase64String(_encryptionHelper.SignMessage(signMessage));
            
            // query
            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add("signedBase64", localSendInvalidSignBase64 ? "_" : "" + signedBase64);
            if(token != "")
                queryDict.Add("token", token);
            if(remoteTimeout)
                queryDict.Add("remoteTimeout", "true");
            if(remoteSendInvalidPayload)
                queryDict.Add("remoteSendInvalidPayload", "true");
            var query = getQuery(queryDict);
            
            
            LastRequest = new LastRequest()
            {
                EndPoint = endPoint,
                RequestData = requestData
            };
            
            // send request
            var url = $"{Host}{endPoint}?{query}";
            HTTPRequest request = new HTTPRequest(new Uri(url), HTTPMethods.Post, (originalRequest, response) =>
            {
                if (response != null && response.IsSuccess)
                {
                    if (response.Headers.ContainsKey("content-encoding") && response.Headers["content-encoding"].Contains("encrypted"))
                    {
                        byte[] decrypted = _encryptionHelper.Decrypt(response.Data, nonce);
                        if(decrypted.Length > 10)
                        {
                            RespondData = JsonUtility.FromJson<RespondData>(
                                Encoding.UTF8.GetString(Decompress(decrypted)));


                            receivedTimestamp = RespondData.timestamp;
                            serverTimestamp = RespondData.timestamp;

                            if (RespondData.timestamp - requestData.timestamp > Timeout)
                                RespondData.errorCode = ErrorCode.Timeout;
                            else
                            {
                                user = RespondData.user;
                                if (Mathf.Abs(PredictedServerTimestamp - receivedTimestamp) > 3600)
                                    RespondData.errorCode = ErrorCode.InvalidTimestamp;
                                else
                                {

                                    cards = RespondData.body.cards;
                                    card = RespondData.body.card;
                                }

                                if (!String.IsNullOrEmpty(RespondData.token))
                                    token = RespondData.token;
                            }
                        }
                        else
                        {
                            RespondData = new RespondData() {errorCode = ErrorCode.FailedToDecryptServerPayload};
                        }
                    }
                    else
                    {
                        RespondData = JsonUtility.FromJson<RespondData>(response.DataAsText);
                    }
                }
                else
                {
                    RespondData = new RespondData() {errorCode = ErrorCode.Timeout};
                }

                isSending = false;
            });
            request.SetForm(postForm);
            request.Timeout = TimeSpan.FromSeconds(Timeout);
            request.Send();
        }
        
        public static byte[] Decompress(byte[] input)
        {
            using (var source = new MemoryStream(input))
            using (var zip = new GZipStream(source, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                zip.CopyTo(output);
                return output.ToArray();
            }
        }
    }
}
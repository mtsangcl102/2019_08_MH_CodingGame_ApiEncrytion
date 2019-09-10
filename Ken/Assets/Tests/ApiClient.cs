using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BestHTTP;
using BestHTTP.Decompression.Zlib;
using BestHTTP.Extensions;
using BestHTTP.Forms;
using BestHTTP.JSON;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Ocsp;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.Encoders;
using UnityEngine;
using System.Collections ;
using Random = System.Random;
using unity.libsodium;

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

        string _host = "https://nestjs-api-test.herokuapp.com";
        string _deviceId = "any-32-bytes-string-works-001-99";
        private int _requestTimeout = 20;
        private int _testTimeout = 21; // pls use 21
        private int _serverSimulateTimeout = 5;
        private int _shortRequestTimeout = 1;
        
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
                isSending = true;
                nonce = nonce + 1; 
                _request(LastRequest.EndPoint, LastRequest.RequestData);
            }
        }

        public void Login()
        {
            isSending = true;
            var requestData = new RequestData() {deviceId = DeviceId};
            _request("/users/login", requestData);
        }

        public void ListCards()
        {
            isSending = true;
            var requestData = new RequestData() {};
            nonce = nonce + 1;
            requestData.cacheKey = user.userId.ToString() + "" + nonce.ToString();
            _request("/cards/list", requestData);
        }

        public void CreateCard(int monsterid)
        {
            isSending = true;
            nonce = nonce + 1;
            var requestData = new RequestData() {monsterId = monsterid};
            requestData.cacheKey = user.userId.ToString() + "" + nonce.ToString();
            _request("/cards/create", requestData);
        }
        
        public void UpdateCard(int cardId, int exp)
        {
            isSending = true;
            nonce = nonce + 1;
            var requestData = new RequestData() {cardId = cardId, exp = exp};
            requestData.cacheKey = user.userId.ToString() + "" + nonce.ToString();
            _request("/cards/update", requestData);
        }
        
        public void DeleteCard(int cardId)
        {
            isSending = true;
            nonce = nonce + 1;
            var requestData = new RequestData() {cardId = cardId};
            requestData.cacheKey = user.userId.ToString() + "" + nonce.ToString();
            _request("/cards/delete", requestData);
        }

        public string getQuery(Dictionary<string, string> dict)
        {
            string query = string.Join("&", dict.Select(x => x.Key + "=" + Uri.EscapeDataString(x.Value)).ToArray());
            return query;
        }

        public void _request(string endPoint, RequestData requestData)
        {
            
            RespondData respondData = new RespondData();
            EncryptionHelper encryptionHelper = new EncryptionHelper();
            
            {
                requestData.version = Version;
                requestData.versionKey = VersionKey;
                requestData.timestamp = CurrentTimestamp;
                requestData.session = user.session;
                requestData.cacheKey = requestData.cacheKey;
                requestData.cardId = requestData.cardId;
                requestData.monsterId = requestData.monsterId;
                requestData.exp = requestData.exp;
                
                
                //login use only
                requestData.deviceId = DeviceId;
            
            }

            Byte[] payload = JsonUtility.ToJson(requestData).GetASCIIBytes();
            byte[] encryptData = encryptionHelper.Encrypt(payload, nonce, localSendInvalidRequestData);
            string payloadBase64 = Convert.ToBase64String(encryptData);
            var postForm = new RawJsonForm();
            postForm.AddField("payloadBase64", payloadBase64);
            
            // signature
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            Byte[] digest = md5.ComputeHash(encryptionHelper.Encrypt(payload,nonce, localSendInvalidRequestData));//new Byte[100]; // md5(payload);
            Byte[] nonceByte = BitConverter.GetBytes(nonce);//new Byte[8]; // nonce to byte
            Byte[] signMessage = nonceByte.Concat(digest).ToArray(); // nonceByte + digest
            string signedBase64 = Convert.ToBase64String(encryptionHelper.SignMessage(signMessage));
            if (localSendInvalidSignBase64)
            {
                signedBase64 = ":asdfjhasldkj" + signedBase64;
            }
            // query
            string tokenStr = "";
            if (string.IsNullOrEmpty(this.token))
            {
                this.token = "";
            }
            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add("signedBase64", signedBase64);
            queryDict.Add("token", this.token);
            queryDict.Add("remoteTimeout", this.remoteTimeout.ToString());
            queryDict.Add("remoteSendInvalidPayload", remoteSendInvalidPayload.ToString());
            var query = this.getQuery(queryDict);
            
            
            // send request
            var url = $"{_host}{endPoint}?{query}";
            HTTPRequest request = new HTTPRequest(new Uri(url), HTTPMethods.Post, (originalRequest, response) =>
            {

                bool contentEncrypted = false;
                switch (originalRequest.State)
                {
                    case HTTPRequestStates.ConnectionTimedOut:
                    case HTTPRequestStates.TimedOut:
                        this.RespondData.errorCode = ErrorCode.Timeout;
                    break;
                    
                    default:
                        if (response.Headers.ContainsKey("content-encoding"))
                        {
                            List<string> tmpOutput = response.Headers["content-encoding"];
                            if (tmpOutput.Contains("encrypted"))
                            {
                                contentEncrypted = true;
                            }
                        }
                    if(contentEncrypted)
                    {
                        try
                        {
                            var compressedStream = new MemoryStream(encryptionHelper.Decrypt(response.Data, nonce));
                            var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                            var resultStream = new MemoryStream();
                            zipStream.CopyTo(resultStream);
                            string outputString = Encoding.UTF8.GetString(resultStream.ToArray());
                            respondData = JsonUtility.FromJson<RespondData>(outputString);
                            this.receivedTimestamp = respondData.timestamp;

                            if ((this.CurrentTimestamp - this.serverTimestamp) > 3600 &&
                                !originalRequest.Uri.ToString().Contains("/login"))
                            {
                                this.RespondData.errorCode = ErrorCode.InvalidTimestamp;

                            }
                            else
                            {
                                this.RespondData = respondData;
                                if (!string.IsNullOrEmpty(respondData.token))
                                {
                                    this.token = respondData.token;
                                }

                                this.user = respondData.user;
                                this.card = respondData.body.card;
                                this.cards = respondData.body.cards;
                                this.serverTimestamp = respondData.timestamp;


                            }
                        }
                        catch (Exception exception)
                        {
                            this.RespondData.errorCode = ErrorCode.FailedToDecryptServerPayload;
                            isSending = false;
                        }

                    }
                    else
                    {   
                        try
                        {
                            string outputString = Encoding.UTF8.GetString(response.Data);
                            respondData = JsonUtility.FromJson<RespondData>(outputString);
                            this.RespondData.errorCode = respondData.errorCode;
                            isSending = false;

                        }
                        catch (Exception exception)
                        {
                            this.RespondData.errorCode = ErrorCode.FailedToDecryptServerPayload;
                            isSending = false;
                            
                        }
                    }
                    break;
                    
                }
               
                isSending = false;
            });
            
            request.SetForm(postForm);
            request.Timeout = new TimeSpan(0, 0, 0, this.Timeout);
            request.Send();
            
            
            LastRequest = new LastRequest();
            LastRequest.EndPoint = endPoint;
            LastRequest.RequestData = requestData;
            
        }
    }
}
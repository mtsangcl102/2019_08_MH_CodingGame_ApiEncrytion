using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BestHTTP;
using BestHTTP.Decompression.Zlib;
using BestHTTP.Forms;
using UnityEngine;
using Random = System.Random;
using System.Timers;

namespace Tests
{
    public class LastRequest
    {
        public string EndPoint;
        public RequestData RequestData;
    }
    
    public class ApiClient
    {
        private static System.Timers.Timer aTimer;
        
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
            var requestData = new RequestData() {session = user.session, cacheKey = user.userId+"_"+nonce.ToString()};
            _request("/cards/list", requestData);
        }

        public void CreateCard(int monsterid)
        {
            var requestData = new RequestData() {session = user.session, monsterId = monsterid, cacheKey = user.userId+"_"+nonce.ToString()};
            _request("/cards/create", requestData);
        }
        
        public void UpdateCard(int cardId, int exp)
        {
            var requestData = new RequestData() {session = user.session, cardId = cardId, exp = exp, cacheKey = user.userId+"_"+nonce.ToString()};
            _request("/cards/update", requestData);
        }
        
        public void DeleteCard(int cardId)
        {
            var requestData = new RequestData() {session = user.session, cardId = cardId, cacheKey = user.userId+"_"+nonce.ToString()};
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
            
            LastRequest = new LastRequest();
            LastRequest.EndPoint = endPoint;
            LastRequest.RequestData = requestData;
            
            long tmpNonce = nonce;

            //assign general request data
            requestData.version = Version;
            requestData.versionKey = VersionKey;
            requestData.timestamp = PredictedServerTimestamp;

            // post 
            Byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestData) );
            string payloadBase64 = Convert.ToBase64String( localSendInvalidRequestData?
                _encryptionHelper.Encrypt_Fake(payload, nonce) :
                _encryptionHelper.Encrypt(payload, nonce) );
            var postForm = new RawJsonForm();

            postForm.AddField("payloadBase64", payloadBase64);
            postForm.AddField("nonce", localSendInvalidRequestData? "0":nonce.ToString());

            // signature
            Byte[] digest = md5.ComputeHash( localSendInvalidRequestData?
                _encryptionHelper.Encrypt_Fake(payload, nonce) :
                _encryptionHelper.Encrypt(payload, nonce) );
            Byte[] nonceByte = BitConverter.GetBytes(nonce);
            Byte[] signMessage = new Byte[digest.Length + nonceByte.Length];
            Array.Copy(nonceByte, signMessage, nonceByte.Length);
            if (!localSendInvalidSignBase64)
                Array.Copy(digest, 0, signMessage, nonceByte.Length, digest.Length);
            string signedBase64 = Convert.ToBase64String( _encryptionHelper.SignMessage(signMessage) );
            
            // query
            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add("signedBase64", signedBase64);
            queryDict.Add("token", token);
            queryDict.Add("remoteTimeout", remoteTimeout? "true" : "false");
            queryDict.Add("remoteSendInvalidPayload", remoteSendInvalidPayload? "true" : "false" );
            var query = getQuery(queryDict);

            // send request
            var url = $"{Host}" + endPoint + $"?{query}";

            SetTimer(Timeout);
            RespondData = new RespondData();
            HTTPRequest request = new HTTPRequest(new Uri(url), HTTPMethods.Post, (originalRequest, response) =>
            {
                if (response.Data.Length == 10)
                {
                    RespondData.errorCode = RespondData.errorCode == 0? ErrorCode.FailedToDecryptServerPayload : RespondData.errorCode ;
                } else if (response.HasHeaderWithValue( "Content-Encoding", "encrypted" ) )
                {
                    var decrypted = _encryptionHelper.Decrypt(response.Data, tmpNonce); 
                    RespondData = JsonUtility.FromJson<RespondData>(Encoding.UTF8.GetString(Decompress(decrypted)));
                }
                else
                {
                    RespondData = JsonUtility.FromJson<RespondData>(response.DataAsText);
                }

                //assign user
                user.userId = RespondData.user.userId == 0 ? user.userId : RespondData.user.userId;
                user.deviceId = RespondData.user.deviceId == null ? user.deviceId : RespondData.user.deviceId;    
                user.session = RespondData.user.session == null ? user.session : RespondData.user.session;
                
                //assign other server return value
                cards = RespondData.body.cards == null ? cards : RespondData.body.cards ;
                card = RespondData.body.card;
                serverTimestamp = RespondData.timestamp;
                receivedTimestamp = CurrentTimestamp;
                token = RespondData.token == null ? token : RespondData.token;

                isSending = false;
            });
            request.SetForm(postForm);
            request.Send();

            nonce++;
        }
        
        private void SetTimer(float sec)
        {
            aTimer = new System.Timers.Timer(sec * 1000);
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }

        private void OnTimedEvent(System.Object source, ElapsedEventArgs e)
        {
            aTimer.Elapsed -= OnTimedEvent;
            RespondData.errorCode = ErrorCode.Timeout;
            isSending = false;
        }
        
        
        static byte[] Decompress(byte[] data)
        {
            using (var compressedStream = new MemoryStream(data))
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                zipStream.CopyTo(resultStream);
                return resultStream.ToArray();
            }
        }
    }
}
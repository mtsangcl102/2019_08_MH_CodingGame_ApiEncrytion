using System;
using System.Collections.Generic;
using System.IO;
//using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BestHTTP;
using BestHTTP.Decompression.Zlib;
using BestHTTP.Forms;
using UnityEngine;
using CompressionLevel = BestHTTP.Decompression.Zlib.CompressionLevel;
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
            get { return serverTimestamp + (CurrentTimestamp - receivedTimestamp); }
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
            var requestData = new RequestData() {deviceId = DeviceId, timestamp = PredictedServerTimestamp};
            _request("/users/login", requestData);
        }

        public void ListCards()
        {
            var requestData = new RequestData() {session = user.session, timestamp = PredictedServerTimestamp};
            _request("/cards/list", requestData);
        }

        public void CreateCard(int monsterid)
        {
            var requestData = new RequestData() {session = user.session, monsterId = monsterid, timestamp = PredictedServerTimestamp};
            _request("/cards/create", requestData);
        }

        public void UpdateCard(int cardId, int exp)
        {
            var requestData = new RequestData() {session = user.session, cardId = cardId, exp = exp, timestamp = PredictedServerTimestamp};
            _request("/cards/update", requestData);
        }

        public void DeleteCard(int cardId)
        {
            var requestData = new RequestData() {session = user.session, cardId = cardId, timestamp = PredictedServerTimestamp};
            _request("/cards/delete", requestData);
        }

        public string getQuery(Dictionary<string, string> dict)
        {
            foreach (var kvp in dict)
            {
                Debug.Log(kvp.Key+" " + kvp.Value);
            }
            string query = string.Join("&", dict.Select(x => x.Key + "=" + Uri.EscapeDataString(x.Value)).ToArray());
            return query;
        }

        public void _request(string endPoint, RequestData requestData)
        {
            LastRequest = new LastRequest() {EndPoint = endPoint, RequestData = requestData};
            
            // post 
            requestData.version = Version;
            requestData.versionKey = VersionKey;
            requestData.cacheKey = requestData.timestamp.ToString() + JsonUtility.ToJson(requestData);
            
            Byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestData)); // Json(requestData).toBytes();
            if (localSendInvalidRequestData) payload = new byte[payload.Length];

            Byte[] encryptedPayload = _encryptionHelper.Encrypt(payload, nonce);
            string payloadBase64 = Convert.ToBase64String(encryptedPayload);

            var postForm = new RawJsonForm();
            postForm.AddField("payloadBase64", payloadBase64);

            // signature
            Byte[] digest = md5.ComputeHash(encryptedPayload); // md5(payload);
            Byte[] nonceByte = BitConverter.GetBytes(nonce); // nonce to byte
            Byte[] signMessage = new byte[nonceByte.Length + digest.Length]; // nonceByte + digest
            if (!localSendInvalidSignBase64)
            {
                Array.Copy(nonceByte, signMessage, nonceByte.Length);
                Array.Copy(digest, 0, signMessage, nonceByte.Length, digest.Length);
            }

            string signedBase64 = Convert.ToBase64String(_encryptionHelper.SignMessage(signMessage));

            // query
            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add("signedBase64", signedBase64);
            queryDict.Add("token", token);
            queryDict.Add("remoteTimeout", remoteTimeout.ToString());
            queryDict.Add("remoteSendInvalidPayload", remoteSendInvalidPayload.ToString());
            var query = getQuery(queryDict);

            // send request
            var url = $"{Host}{endPoint}?{query}";
            Debug.Log(url);

            isSending = true;
            HTTPRequest request = new HTTPRequest(new Uri(url), HTTPMethods.Post, (originalRequest, response) =>
            {
                switch (originalRequest.State)
                {
                    case HTTPRequestStates.TimedOut:
                        Debug.Log("TimedOut");
                        this.RespondData = new RespondData();
                        RespondData.errorCode = ErrorCode.Timeout;
                        break;
                    default:
                        try
                        {
                            string data;
                            if (response.HasHeaderWithValue("Content-Encoding", "encrypted"))
                            {
                                var decrypt = _encryptionHelper.Decrypt(response.Data, nonce);
                                
                                using (MemoryStream memoryStream = new MemoryStream(decrypt))
                                using (GZipStream gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                                using (MemoryStream reader = new MemoryStream())
                                {
                                    gzipStream.CopyTo(reader);
                                    data = Encoding.UTF8.GetString(reader.ToArray());
                                }
                            }
                            else
                            {
                                Debug.Log("FAILED: " + response.DataAsText);
                                data = response.DataAsText;
                            }

                            this.RespondData = JsonUtility.FromJson<RespondData>(data);
                            if (endPoint == "/users/login")
                            {
                                user = RespondData.user;
                                token = RespondData.token ;
                            }
                            else
                            {
                                cards = RespondData.body.cards;
                                card = RespondData.body.card;
                            }
                            receivedTimestamp = RespondData.timestamp;
                            serverTimestamp = RespondData.timestamp;
                        }
                        catch (Exception e)
                        {
                            Debug.Log(e.ToString());
                            this.RespondData = new RespondData();
                            RespondData.errorCode = ErrorCode.FailedToDecryptServerPayload;
                        }
                        break;
                }

                isSending = false;
                nonce++;
            });
            request.Timeout = TimeSpan.FromMilliseconds(Timeout);
            request.SetForm(postForm);
            request.Send();

        }
    }
}
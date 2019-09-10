using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BestHTTP;
using BestHTTP.Decompression.Zlib;
using BestHTTP.Forms;
using NUnit.Framework;
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

        public static void CopyTo(Stream src, Stream dest)
        {
            byte[] bytes = new byte[4096];

            int cnt;

            while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
            {
                dest.Write(bytes, 0, cnt);
            }
        }

        public static byte[] Unzip(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(msi, CompressionMode.Decompress))
                {
                    //gs.CopyTo(mso);
                    CopyTo(gs, mso);
                }

                return mso.ToArray();
                //return Encoding.UTF8.GetString(mso.ToArray());
            }
        }

        public static string findHeader(HTTPResponse response, string key)
        {
            foreach (var entry in response.Headers)
            {
                if (entry.Key == key)
                {
                    return entry.Value[0];
                }
            }
            return "";
        }

        public void UpdateCards(Body body)
        {
            card = body.card;
            cards = (Tests.Card[])body.cards.Clone(); 
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
            var requestData = new RequestData() { deviceId = DeviceId };
            _request("/users/login", requestData);
        }

        public void ListCards()
        {
            var requestData = new RequestData() { };
            _request("/cards/list", requestData);
        }

        public void CreateCard(int monsterid)
        {
            var requestData = new RequestData() { monsterId = monsterid };
            _request("/cards/create", requestData);
        }

        public void UpdateCard(int cardId, int exp)
        {
            var requestData = new RequestData() { cardId = cardId, exp = exp };
            _request("/cards/update", requestData);
        }

        public void DeleteCard(int cardId)
        {
            var requestData = new RequestData() { cardId = cardId };
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

            isSending = true;
            LastRequest = new LastRequest();
            LastRequest.EndPoint = endPoint;
            LastRequest.RequestData = requestData;

            // post 
            requestData.version = Version;
            requestData.versionKey = VersionKey;
            requestData.session = user.session ;
            requestData.cacheKey = Convert.ToBase64String(md5.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestData))));
            requestData.timestamp = PredictedServerTimestamp;

            serverTimestamp = requestData.timestamp;

            string dataJson = localSendInvalidRequestData? "Invalid data":JsonUtility.ToJson(requestData);
            Byte[] payload = _encryptionHelper.Encrypt(Encoding.UTF8.GetBytes(dataJson), nonce) ;
            string payloadBase64 = Convert.ToBase64String(payload);
            var postForm = new RawJsonForm();
            postForm.AddField("payloadBase64", payloadBase64);


            // signature
            Byte[] digest = md5.ComputeHash(payload);
            string digestBase64 = Convert.ToBase64String(digest);
            Byte[] nonceByte = BitConverter.GetBytes(nonce);
            Byte[] signMessage = new Byte[digest.Length+nonceByte.Length];
            Array.Copy(nonceByte, 0, signMessage, 0, nonceByte.Length);
            Array.Copy(digest, 0, signMessage, nonceByte.Length, digest.Length);
            string signedBase64 = Convert.ToBase64String(_encryptionHelper.SignMessage(signMessage));

            // query
            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add("signedBase64", localSendInvalidSignBase64? "invalid":signedBase64);
            queryDict.Add("token", token);
            queryDict.Add("remoteSendInvalidPayload", remoteSendInvalidPayload ? "true":"");
            queryDict.Add("remoteTimeout", remoteTimeout ? "true" : "");
            var query = getQuery(queryDict);


            // send request
            var url = $"{Host}{endPoint}?{query}";
            RespondData respondData = new RespondData();
            HTTPRequest request = new HTTPRequest(new Uri(url), HTTPMethods.Post, (originalRequest, response) =>
            {
                isSending = false;

                if( response == null )
                {
                    RespondData = new RespondData();
                    RespondData.errorCode = ErrorCode.Timeout;
                }
                else 
                if (findHeader(response,"content-encoding") == "encrypted" )
                {
                    try
                    {
                        Byte[] decryptedData = Unzip(_encryptionHelper.Decrypt(response.Data, nonce));

                        string decryptedStr = Encoding.UTF8.GetString(decryptedData);
                        respondData = JsonUtility.FromJson<RespondData>(decryptedStr);

                        user = respondData.user;
                        if (respondData.token != null ) 
                            token = respondData.token;
                        receivedTimestamp = respondData.timestamp;
                        RespondData = respondData;
                        UpdateCards(respondData.body);
                    }
                    catch (Exception ex)
                    {
                        RespondData = new RespondData();
                        RespondData.errorCode = ErrorCode.FailedToDecryptServerPayload;
                    }
                    LastRequest = null;
                }
                else
                    RespondData = JsonUtility.FromJson<RespondData>(response.DataAsText);
            });
            request.Timeout = new TimeSpan(0,0, Timeout);
            request.SetForm(postForm);
            request.Send();

        }
    }
}
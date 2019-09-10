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

namespace Tests
{
    public class LastRequest
    {
        public string EndPoint;
        public RequestData RequestData;
        public bool IsResend;
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
                Debug.Log( "resend " + LastRequest.EndPoint );
                _request(LastRequest.EndPoint, LastRequest.RequestData, isResend : true );
            }
        }

        public void Login()
        {
            var requestData = new RequestData() {deviceId = DeviceId};
            _request("/users/login", requestData, false);
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

        public void _request(string endPoint, RequestData requestData, bool includeSession = true, bool isResend = false )
        {
            if (isSending)
            {
                return;
            }

            //settings
            isSending = true;
            LastRequest = new LastRequest();
            LastRequest.EndPoint = endPoint;
            LastRequest.RequestData = requestData;
            
            //add request data params
            requestData.timestamp = PredictedServerTimestamp;
            requestData.version = Version;
            requestData.versionKey = VersionKey;

            if (includeSession)
            {
                requestData.session = user.session;
            }

            nonce++;
            if (!isResend)
            {
                requestData.cacheKey = nonce.ToString();
            }
            else
            {
                LastRequest.IsResend = true;
            }
            
            // ------

            string json = JsonUtility.ToJson(requestData);
            
            if (localSendInvalidRequestData)
            {
                json = "hahahaha";
            }
            Debug.Log( json );
            
            // post 
            Byte[] payload = _encryptionHelper.StringToBytes(json );
            Byte[] payloadEncrypted = _encryptionHelper.Encrypt( payload, nonce ); 
            string payloadBase64 = _encryptionHelper.BytesToBase64( payloadEncrypted );
            var postForm = new RawJsonForm();
            postForm.AddField("payloadBase64", payloadBase64);
            
            // signature
            Byte[] nonceByte = _encryptionHelper.GetNonceBytes(nonce);
            Byte[] digest =  md5.ComputeHash(payloadEncrypted);
            Byte[] signMessage = new byte[8 + digest.Length];
            nonceByte.CopyTo( signMessage, 0 );
            digest.CopyTo( signMessage, 8 );
            Byte[] signedByte = _encryptionHelper.SignMessage( signMessage );
            string signedBase64 = _encryptionHelper.BytesToBase64( signedByte );
                
            // query
            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add("signedBase64", signedBase64);
            queryDict.Add("token", token);
            
            // testing
            if (remoteTimeout)
            {
                queryDict.Add("remoteTimeout", "true" );
            }

            if (remoteSendInvalidPayload)
            {
                queryDict.Add("remoteSendInvalidPayload", "true");
            }
            
            if (localSendInvalidSignBase64)
            {
                queryDict["signedBase64"] = "aaaaaa";
            }
            
            var query = getQuery(queryDict);
            
            
            // send request
            var url = $"{Host}{endPoint}?{query}";
            Debug.Log( url );
            HTTPRequest request = new HTTPRequest(new Uri(url), HTTPMethods.Post, OnRespond);
            request.SetForm(postForm);
            request.Timeout = TimeSpan.FromSeconds( Timeout );
            request.Send();
        }

        private void OnRespond(HTTPRequest request, HTTPResponse response)
        {
            isSending = false;

            string dataString;

            if (response == null )
            {
                Debug.Log("timeout");
                RespondData = new RespondData();
                RespondData.errorCode = ErrorCode.Timeout;
                return;
            }
            
            if ( response.Headers.ContainsKey("content-encoding") )
            {
                // normal flow
                try
                {
                    var decrypted = _encryptionHelper.Decrypt(response.Data, nonce);
                    var decompressed = _encryptionHelper.GZipDecompress(decrypted);
                    dataString = _encryptionHelper.BytesToString(decompressed);
                }
                catch ( Exception e )
                {
                    RespondData = new RespondData();
                    RespondData.errorCode = ErrorCode.FailedToDecryptServerPayload;
                    return;
                }
            }
            else
            {
                // error flow
                dataString = response.DataAsText; 
            }
            Debug.Log( dataString );
            
            RespondData = JsonUtility.FromJson<RespondData>(dataString);
            if (RespondData.errorCode == 0 )
            {
                _OnRespondSuccess(RespondData);
            }
            else
            {
                _OnRespondError(RespondData);
            }
        }

        private void _OnRespondSuccess( RespondData respondData)
        {
            if (LastRequest.EndPoint == "/users/login")
            {
                token = respondData.token;
                _UpdateUser( respondData.user );
            }
            else
            {
                Debug.Log("Success " + LastRequest.EndPoint );
                if (respondData.body.card.id > 0 )
                {
                    _UpdateCard(respondData.body.card);
                }
                if (respondData.body.cards != null )
                {
                    _UpdateCardList( respondData.body.cards );
                }
                RespondData.isCache = LastRequest.IsResend;
            }
            serverTimestamp = RespondData.timestamp;
            receivedTimestamp = CurrentTimestamp;
        }

        private void _OnRespondError(RespondData respondData)
        {
            Debug.Log(respondData.errorCode);
            if (respondData.errorCode == ErrorCode.HasCache)
            {
                _OnRespondSuccess(respondData);
            }
        }

        private void _UpdateUser(User respondUser)
        {
            user.userId = respondUser.userId;
            user.deviceId = respondUser.deviceId;
            user.session = respondUser.session;
        }

        private void _UpdateCard(Card respondCard)
        {
            card = respondCard;
        }
        
        private void _UpdateCardList(Card[] respondCards)
        {
            cards = respondCards;
        }
    }
}
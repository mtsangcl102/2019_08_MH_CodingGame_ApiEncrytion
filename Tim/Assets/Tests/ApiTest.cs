using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BestHTTP;
using BestHTTP.Forms;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace Tests
{
    public class ApiTest
    {
        string _host = "https://nestjs-api-test.herokuapp.com";
        string _deviceId = "any-32-bytes-string-works-001-99";
        private int _requestTimeout = 20;
        private int _testTimeout = 21; // pls use 21
        private int _serverSimulateTimeout = 5;
        private int _shortRequestTimeout = 1;

        public IEnumerator AssertApiError(ApiClient apiClient, ErrorCode errorCode)
        {
            yield return TestHelper.Timeout(() => !apiClient.isSending, _testTimeout);
            Assert.AreEqual(apiClient.RespondData.errorCode, errorCode);
        }
        
        public IEnumerator AssertLoginSuccess(ApiClient apiClient)
        {
            yield return TestHelper.Timeout(() => !apiClient.isSending, _testTimeout);
            Assert.IsNotEmpty(apiClient.token);
            Assert.AreEqual(apiClient.user.deviceId, _deviceId);
        }


        public IEnumerator AssertApiSuccess(ApiClient apiClient)
        {
            yield return TestHelper.Timeout(() => !apiClient.isSending, _testTimeout);
            Assert.AreEqual((int) apiClient.RespondData.errorCode, 0);
        }
        

        #region login

        [UnityTest]
        public IEnumerator Logic_Flow()
        {
            var apiClient = new ApiClient(_host, _deviceId, _requestTimeout);
            EncryptionHelper encryptionHelper = new EncryptionHelper();
            bool isSending = true;

            // post 
            Byte[] payload = new Byte[100]; // Json(requestData).toBytes();
            string payloadBase64 = "encryptWithChaCha(payload).toBase64()";
            var postForm = new RawJsonForm();
            postForm.AddField("payloadBase64", payloadBase64);
            
            // signature
            Byte[] digest = new Byte[100]; // md5(payload);
            Byte[] nonceByte = new Byte[8]; // nonce to byte
            Byte[] signMessage = new Byte[108]; // nonceByte + digest
            string signedBase64 = "signWithChaCha(signMessage).toBase64()";
                
            // query
            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add("signedBase64", signedBase64);
            queryDict.Add("token", "");
            var query = apiClient.getQuery(queryDict);
            
            
            // send request
            var url = $"{_host}/users/login?{query}";
            RespondData respondData = new RespondData();
            HTTPRequest request = new HTTPRequest(new Uri(url), HTTPMethods.Post, (originalRequest, response) =>
            {
                isSending = false;
                respondData = JsonUtility.FromJson<RespondData>(response.DataAsText);
            });
            request.SetForm(postForm);
            request.Send();
            
            yield return TestHelper.Timeout(() => !isSending, _testTimeout);
            Assert.AreEqual(respondData.errorCode, ErrorCode.InvalidSign);
        }

        [UnityTest]
        public IEnumerator Login_EmptyRequest_UnityWebRequest()
        {
            var url = $"{_host}/users/login";
            WWWForm form = new WWWForm();
            using (UnityWebRequest www = UnityWebRequest.Post(url, form))
            {
                yield return www.SendWebRequest();

                if (www.isNetworkError || www.isHttpError)
                {
                    Debug.Log(www.error);
                }
                else
                {
                    var respondData = JsonUtility.FromJson<RespondData>(www.downloadHandler.text);
                    Assert.AreEqual(respondData.errorCode, ErrorCode.InvalidRequest);
                }
            }
        }
        
        [UnityTest]
        public IEnumerator Login_Success()
        {
            var apiClient = new ApiClient(_host, _deviceId, _requestTimeout);
            apiClient.Login();
            yield return AssertLoginSuccess(apiClient);
            
            // list cards
            apiClient.ListCards();
            yield return AssertApiSuccess(apiClient);
        }
        
        [UnityTest]
        public IEnumerator Login_InvalidDeviceId()
        {
            var apiClient = new ApiClient(_host, "not 32 bytes", _requestTimeout);
            apiClient.Login();
            yield return AssertApiError(apiClient, ErrorCode.InvalidDeviceId);
        }

        [UnityTest]
        public IEnumerator Login_InvalidClientPayload()
        {
            var apiClient = new ApiClient(_host, _deviceId, _requestTimeout);
            apiClient.localSendInvalidRequestData = true;
            apiClient.Login();
            yield return AssertApiError(apiClient, ErrorCode.FailedToDecryptClientPayload);
        }
        
        [UnityTest]
        public IEnumerator Login_InvalidSeverPayload()
        {
            var apiClient = new ApiClient(_host, _deviceId, _requestTimeout);
            apiClient.remoteSendInvalidPayload = true;
            apiClient.Login();
            yield return AssertApiError(apiClient, ErrorCode.FailedToDecryptServerPayload);
        }
        
        [UnityTest]
        public IEnumerator Login_InvalidVersion()
        {
            var apiClient = new ApiClient(_host, _deviceId, _requestTimeout);
            apiClient.Version = "2.0";
            apiClient.Login();
            yield return AssertApiError(apiClient, ErrorCode.InvalidVersion);
        }
        
        [UnityTest]
        public IEnumerator Login_Timeout()
        {
            var apiClient = new ApiClient(_host, _deviceId, _requestTimeout);
            apiClient.remoteTimeout = true;
            apiClient.Timeout = _shortRequestTimeout;
            apiClient.Login();
            yield return AssertApiError(apiClient, ErrorCode.Timeout);
            yield return TestHelper.Timeout(_serverSimulateTimeout);
        }
        
        #endregion
        
        #region cards
        
        [UnityTest]
        public IEnumerator Card1_InvalidToken()
        {
            var apiClient = new ApiClient(_host, _deviceId, _requestTimeout);
            apiClient.Login();
            yield return AssertLoginSuccess(apiClient);

            apiClient.token = "invalid token";
            apiClient.ListCards();
            yield return AssertApiError(apiClient, ErrorCode.InvalidToken);
        }
        
        [UnityTest]
        public IEnumerator Card1_InvalidSession()
        {
            var apiClient = new ApiClient(_host, _deviceId, _requestTimeout);
            apiClient.Login();
            yield return AssertLoginSuccess(apiClient);

            apiClient.user.session = "invalid session";
            apiClient.ListCards();
            yield return AssertApiError(apiClient, ErrorCode.InvalidSession);
        }
        
        [UnityTest]
        public IEnumerator Card1_InvalidNonce()
        {
            var apiClient = new ApiClient(_host, _deviceId, _requestTimeout);
            apiClient.Login();
            yield return AssertLoginSuccess(apiClient);

            apiClient.nonce -= 1;
            apiClient.ListCards();
            yield return AssertApiError(apiClient, ErrorCode.InvalidNonce);
        }
        
        [UnityTest]
        public IEnumerator Card1_InvalidTimestamp()
        {
            var apiClient = new ApiClient(_host, _deviceId, _requestTimeout);
            apiClient.Login();
            yield return AssertLoginSuccess(apiClient);

            apiClient.serverTimestamp = apiClient.CurrentTimestamp - 4000;
            apiClient.ListCards();
            yield return AssertApiError(apiClient, ErrorCode.InvalidTimestamp);
        }
        
        [UnityTest]
        public IEnumerator Card1_InvalidSign()
        {
            var apiClient = new ApiClient(_host, _deviceId, _requestTimeout);
            apiClient.Login();
            yield return AssertLoginSuccess(apiClient);

            apiClient.localSendInvalidSignBase64 = true;
            apiClient.ListCards();
            yield return AssertApiError(apiClient, ErrorCode.InvalidSign);
        }

        [UnityTest]
        public IEnumerator Card1_InvalidCardId()
        {
            var apiClient = new ApiClient(_host, _deviceId, _requestTimeout);
            apiClient.Login();
            yield return AssertLoginSuccess(apiClient);

            apiClient.CreateCard(-1);
            yield return AssertApiError(apiClient, ErrorCode.InvalidMonsterId);

            apiClient.UpdateCard(100, 1000);
            yield return AssertApiError(apiClient, ErrorCode.InvalidCardId);
        }

        #endregion
        
        #region cards

        [UnityTest]
        public IEnumerator Card2_FullTest()
        {
            var apiClient = new ApiClient(_host, _deviceId, _requestTimeout);
            apiClient.Login();
            yield return AssertLoginSuccess(apiClient);
            
            // list cards
            apiClient.ListCards();
            yield return AssertApiSuccess(apiClient);
            
            // delete existing cards
            foreach (var card in apiClient.cards)
            {
                apiClient.DeleteCard(card.id);
                yield return AssertApiSuccess(apiClient);
            }
            Assert.AreEqual(apiClient.cards.Length, 0);
            
            // add some new card
            int newCard = 5;
            for (int i = 0; i < newCard; i++)
            {
                int monsterId = i + 1;
                apiClient.CreateCard(monsterId);
                yield return AssertApiSuccess(apiClient);
                Assert.AreEqual(apiClient.card.monsterId, monsterId);
            }
            
            // update all card
            foreach (var card in apiClient.cards)
            {
                int exp = card.id * 10;
                apiClient.UpdateCard(card.id, exp);
                yield return AssertApiSuccess(apiClient);
                Assert.AreEqual(apiClient.card.exp, exp);
            }
        }

        [UnityTest]
        public IEnumerator Card2_CacheTest()
        {
            var apiClient = new ApiClient(_host, _deviceId, _requestTimeout);
            apiClient.Login();
            yield return AssertLoginSuccess(apiClient);

            // trigger a timeout
            int monsterId = 100;
            apiClient.remoteTimeout = true;
            apiClient.Timeout = _shortRequestTimeout;
            apiClient.CreateCard(monsterId);
            yield return AssertApiError(apiClient, ErrorCode.Timeout);
            
            // restore timeout
            apiClient.remoteTimeout = false;
            apiClient.Timeout = _requestTimeout;
            
            // it should be locked since it's still processing last request
            Assert.IsTrue(apiClient.CanResend);
            apiClient.Resend();
            yield return AssertApiError(apiClient, ErrorCode.Locked);
            
//            wait for the lock to complete
            yield return TestHelper.Timeout(_serverSimulateTimeout);
            
            // resend, this wll return the cached result
            Assert.IsTrue(apiClient.CanResend);
            apiClient.Resend();
            yield return AssertApiSuccess(apiClient);
            Assert.AreEqual(apiClient.card.monsterId, monsterId);
            Assert.IsTrue(apiClient.RespondData.isCache);
        }

        #endregion
    }
}
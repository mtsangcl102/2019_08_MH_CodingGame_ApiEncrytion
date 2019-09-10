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
            Timeout = timeout ;
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
            requestData.session = user.session;
            _request("/cards/list", requestData);
        }

        public void CreateCard(int monsterid)
        {
            var requestData = new RequestData() {monsterId = monsterid};
            requestData.session = user.session;
            _request("/cards/create", requestData);
        }
        
        public void UpdateCard(int cardId, int exp)
        {
            var requestData = new RequestData() {cardId = cardId, exp = exp};
            requestData.session = user.session;
            _request("/cards/update", requestData);
        }
        
        public void DeleteCard(int cardId)
        {
            var requestData = new RequestData() {cardId = cardId};
            requestData.session = user.session;
            _request("/cards/delete", requestData);
        }

        public string getQuery(Dictionary<string, string> dict)
        {
            string query = string.Join("&", dict.Select(x => x.Key + "=" + Uri.EscapeDataString(x.Value)).ToArray());
            return query;
        }

		public void _request(string endPoint, RequestData requestData)
		{
			requestData.version = Version;
			requestData.versionKey = VersionKey;
			requestData.timestamp = CurrentTimestamp;
			
			if( serverTimestamp != 0 ){
				requestData.timestamp = serverTimestamp;
			}
			
			nonce++;
			EncryptionHelper encryptionHelper = new EncryptionHelper();
			isSending = true;
			
			//post
			Byte[] payload = Encoding.UTF8.GetBytes( JsonUtility.ToJson( requestData ) );
			
			if( localSendInvalidRequestData ){
				payload = new byte[ 10 ];
			}
			
			
			string payloadBase64 = Convert.ToBase64String( encryptionHelper.Encrypt( payload, nonce ) );
			

			
			var postForm = new RawJsonForm();
			postForm.AddField( "payloadBase64", payloadBase64 );
			
			//signature
			Byte[] digest = md5.ComputeHash( encryptionHelper.Encrypt( payload, nonce ) );
			Byte[] nonceByte = BitConverter.GetBytes( nonce );
			Byte[] signMessage = new Byte[ digest.Length + nonceByte.Length ];
			
			if( ! localSendInvalidSignBase64 )
			{
				int counter = 0;
				for( int i = 0; i < nonceByte.Length; i++ ){
					signMessage[ counter++ ] = nonceByte[ i ];
				}
				for( int i = 0; i < digest.Length; i++ ){
					signMessage[ counter++ ] = digest[ i ];
				}
			}
			
			string signedBase64 = Convert.ToBase64String( encryptionHelper.SignMessage( signMessage ) );
			
			//query
			Dictionary<string, string> queryDict = new Dictionary<string, string>();
			queryDict.Add( "signedBase64", signedBase64 );
			queryDict.Add( "token", token );
			
//			if( localSendInvalidRequestData )
//			{
//				Debug.Log("here");
				queryDict.Add( "remoteSendInvalidPayload", remoteSendInvalidPayload.ToString() );
			//}
			queryDict.Add( "remoteTimeout", remoteTimeout.ToString() );
			
			
			//Debug.LogWarning( P.T( queryDict ) ) ;
			var query = getQuery( queryDict );
			
			var url = $"{Host}{endPoint}?{query}";
			RespondData = new RespondData();
			
			LastRequest = new LastRequest();
			LastRequest.EndPoint = endPoint;
			LastRequest.RequestData = requestData;
			
			
            HTTPRequest request = new HTTPRequest(new Uri(url), HTTPMethods.Post, (originalRequest, response) =>
            {
                isSending = false;
                
				if( response == null )
				{
					RespondData = new RespondData();
					RespondData.errorCode = ErrorCode.Timeout;
					return;
				}

				
                
              //  Debug.LogWarning( response.DataAsText );
//				byte[] aa = Encoding.UTF8.GetBytes( response.DataAsText );
//				MemoryStream stream = new MemoryStream( encryptionHelper.Decrypt( aa, nonce ) );
//				
//				GZipStream gZipStream = new GZipStream( stream, CompressionMode.Decompress );
//				byte[] buffer = new byte[ 100000 ];
//				int numRead = gZipStream.Read( buffer, 0, aa.Length );
//				Array.Resize( ref buffer, numRead );
//				Debug.Log("unzipped " + Encoding.UTF8.GetString( buffer ) );
//				Debug.Log("unzipped " + Convert.ToBase64String( buffer ) );

				//decrypt first unzip second
				byte[] unzipped = response.Data;

//Debug.Log( P.T( response.Headers ) );
				if( response.Headers.TryGetValue( "content-encoding", out List<string> aaa ) )
				{
					Debug.Log("unzipped");
					unzipped = Decompress( encryptionHelper.Decrypt( response.Data, nonce ) );
				}
				
//				Debug.Log( "unzipped " + response.Data.Length +" "+ unzipped.Length );
//				Debug.Log("receiveJson " +String.Join( "", response.Data  )+" //"+ Encoding.UTF8.GetString( encryptionHelper.Decrypt( unzipped, nonce )) );

				if( response.Data .Length <= 10 )
				{
					RespondData = new RespondData();
					RespondData.errorCode = ErrorCode.FailedToDecryptServerPayload;
					LastRequest = null;
				}
				else
				{
					RespondData = JsonUtility.FromJson<RespondData>( Encoding.UTF8.GetString( unzipped ) );
					if( RespondData.token != null )
					{
						token = RespondData.token;
						user = RespondData.user;
					}
					
					cards = RespondData.body.cards;
					card = RespondData.body.card;
					receivedTimestamp = (long)RespondData.timestamp;
					
					if( RespondData.errorCode != ErrorCode.Locked ){
						LastRequest = null;
						RespondData.isCache = true;
					}
					
					Debug.Log("errorCode " + RespondData.errorCode +" "+receivedTimestamp +" "+ serverTimestamp +" "+ CurrentTimestamp +" "+ PredictedServerTimestamp  );
				}
            });
			request.Timeout = new TimeSpan( 0, 0, Timeout );
			request.SetForm( postForm );
			request.Send();
		}

		
	    byte[] Decompress(byte[] gzip)
		{
			// Create a GZIP stream with decompression mode.
			// ... Then create a buffer and write into while reading from the GZIP stream.
			using (GZipStream stream = new GZipStream(new MemoryStream(gzip),
				CompressionMode.Decompress))
			{
				const int size = 4096;
				byte[] buffer = new byte[size];
				using (MemoryStream memory = new MemoryStream())
				{
					int count = 0;
					do
					{
						count = stream.Read(buffer, 0, size);
						Debug.LogWarning("count " + count );
						if (count > 0)
						{
							memory.Write(buffer, 0, count);
						}
					}
					while (count > 0);
					return memory.ToArray();
				}
			}
		}
    }
    
    
    
}
//Encoding.UTF8.GetBytes
//Encoding.UTF8.GetString
//Convert.ToBase64String
//Convert.FromBase64String
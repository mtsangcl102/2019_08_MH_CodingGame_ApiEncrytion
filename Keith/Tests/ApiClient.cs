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

        public ApiClient( string host, string deviceId, int timeout )
        {
            Host = host;
            DeviceId = deviceId;
            Timeout = timeout;
            var random = new Random();
            nonce = random.Next( 0, 1 << 30 );
        }

        public long CurrentTimestamp
        {
            get
            {
                return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }

        public long PredictedServerTimestamp
        {
            get
            {
                return serverTimestamp + ( CurrentTimestamp - receivedTimestamp );
            }
        }


        public bool CanResend
        {
            get
            {
                return LastRequest != null && !isSending;
            }
        }

        public void Resend()
        {
            if( CanResend )
            {
                _request( LastRequest.EndPoint, LastRequest.RequestData );
            }
        }

        public void Login()
        {
            var requestData = new RequestData() { deviceId = DeviceId };
            _request( "/users/login", requestData );
        }

        public void ListCards()
        {
            var requestData = new RequestData() { };
            _request( "/cards/list", requestData );
        }

        public void CreateCard( int monsterid )
        {
            var requestData = new RequestData() { monsterId = monsterid };
            _request( "/cards/create", requestData );
        }

        public void UpdateCard( int cardId, int exp )
        {
            var requestData = new RequestData() { cardId = cardId, exp = exp };
            _request( "/cards/update", requestData );
        }

        public void DeleteCard( int cardId )
        {
            var requestData = new RequestData() { cardId = cardId };
            _request( "/cards/delete", requestData );
        }

        public string getQuery( Dictionary<string, string> dict )
        {
            string query = string.Join( "&", dict.Select( x => x.Key + "=" + Uri.EscapeDataString( x.Value ) ).ToArray() );
            return query;
        }

        public void _request( string endPoint, RequestData requestData )
        {
            nonce += 1;
            isSending = true;

            if( LastRequest == null || endPoint != LastRequest.EndPoint || !requestData.Equals( LastRequest.RequestData ) )
            {
                requestData.cacheKey = UnityEngine.Random.Range( 1000, 9999 ).ToString();
            }

            LastRequest = new LastRequest();
            LastRequest.EndPoint = endPoint;
            LastRequest.RequestData = requestData;

            requestData.version = Version;
            requestData.versionKey = VersionKey;
            requestData.timestamp = PredictedServerTimestamp;
            requestData.session = user.session ;
            //requestData.cacheKey

            // post 
            Byte[] payload = Encoding.UTF8.GetBytes( JsonUtility.ToJson( requestData ) ); // Json(requestData).toBytes(); new Byte[ 100 ]
            Byte[] encrypted = _encryptionHelper.Encrypt( payload, nonce );
            if( localSendInvalidRequestData )
            {
                encrypted = Encoding.UTF8.GetBytes( "1234" );
            }
            string payloadBase64 = Convert.ToBase64String( encrypted ); // "encryptWithChaCha(payload).toBase64()";
            var postForm = new RawJsonForm();
            postForm.AddField( "payloadBase64", payloadBase64 );

            // signature
            Byte[] digest = md5.ComputeHash( encrypted ); // md5(payload);new Byte[ 100 ]
            Byte[] nonceByte = BitConverter.GetBytes( nonce ); // nonce to byte new Byte[ 8 ]
            Byte[] signMessage = new Byte[ nonceByte.Length + digest.Length ]; // nonceByte + digest
            Buffer.BlockCopy( nonceByte, 0, signMessage, 0, nonceByte.Length );
            Buffer.BlockCopy( digest, 0, signMessage, nonceByte.Length, digest.Length );
            if( localSendInvalidSignBase64 )
            {
                signMessage = Encoding.UTF8.GetBytes( "1234" );
            }
            string signedBase64 = Convert.ToBase64String( _encryptionHelper.SignMessage( signMessage ) ); //"signWithChaCha(signMessage).toBase64()";

            // query
            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add( "signedBase64", signedBase64 );
            queryDict.Add( "token", token );
            queryDict.Add( "remoteTimeout", remoteTimeout.ToString() );
            queryDict.Add( "remoteSendInvalidPayload", remoteSendInvalidPayload.ToString() );
            var query = getQuery( queryDict );

            // send request
            var url = $"{Host}{endPoint}?{query}";
            RespondData respondData = new RespondData();
            HTTPRequest request = new HTTPRequest( new Uri( url ), HTTPMethods.Post, ( originalRequest, response ) =>
            {
                isSending = false;
                receivedTimestamp = CurrentTimestamp;

                if( response == null )
                {
                    this.RespondData = new RespondData();
                    RespondData.errorCode = ErrorCode.Timeout;
                    return;
                }
                if( response.Data.Length <= 10  )
                {
                    this.RespondData = new RespondData();
                    RespondData.errorCode = ErrorCode.FailedToDecryptServerPayload;
                    return;
                }
                //Debug.Log( response.HasHeaderWithValue( "Content-Encoding", "encrypted" ) );
                const int size = 4096;
                byte[] buffer = new byte[ size ];
                if( response.HasHeaderWithValue( "Content-Encoding", "encrypted" ) )
                {
                    byte[] data;

                    using( GZipStream stream = new GZipStream( new MemoryStream( _encryptionHelper.Decrypt( response.Data, nonce ) ), CompressionMode.Decompress ) )
                    {
                        using( MemoryStream memory = new MemoryStream() )
                        {
                            int count = 0;
                            do
                            {
                                count = stream.Read( buffer, 0, size );
                                if( count > 0 )
                                {
                                    memory.Write( buffer, 0, count );
                                }
                            }
                            while( count > 0 );
                            data = memory.ToArray();
                        }
                    }
                    respondData = JsonUtility.FromJson<RespondData>( Encoding.UTF8.GetString( buffer ) );
                }
                else
                {
                    respondData = JsonUtility.FromJson<RespondData>( response.DataAsText );
                }
                this.RespondData = respondData;
                if( respondData.errorCode == 0 )
                {
                    if( !String.IsNullOrEmpty( respondData.token ) ) this.token = respondData.token;
                    this.user = respondData.user;
                    this.card = respondData.body.card;
                    this.cards = respondData.body.cards;
                    this.serverTimestamp = respondData.timestamp;
                }
            } );
            request.SetForm( postForm );
            request.Timeout = TimeSpan.FromSeconds( Timeout );
            request.Send();
        }
    }
}
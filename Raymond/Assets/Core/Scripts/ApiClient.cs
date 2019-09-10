using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BestHTTP;
using BestHTTP.Decompression.Zlib;
using BestHTTP.Forms;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

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

    // public string Session = "";

    public ApiClient( string host , string deviceId , int timeout )
    {
        Host = host;
        DeviceId = deviceId;
        Timeout = timeout;
        var random = new Random();
        nonce = random.Next( 0 , 1 << 30 );
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
        if ( CanResend )
        {
            _request( LastRequest.EndPoint , LastRequest.RequestData );
        }
    }

    public void Login()
    {
        var requestData = new RequestData() { deviceId = DeviceId };
        _request( "/users/login" , requestData );
    }

    public void ListCards()
    {
        var requestData = new RequestData() { session = user.session , cacheKey = GUID.Generate().ToString()};
        _request( "/cards/list" , requestData );
    }

    public void CreateCard( int monsterid )
    {
        var requestData = new RequestData() { session = user.session , monsterId = monsterid, cacheKey = GUID.Generate().ToString() };
        _request( "/cards/create" , requestData );
    }

    public void UpdateCard( int cardId , int exp )
    {
        var requestData = new RequestData() { session = user.session , cardId = cardId , exp = exp , cacheKey = GUID.Generate().ToString()};
        _request( "/cards/update" , requestData );
    }

    public void DeleteCard( int cardId )
    {
        var requestData = new RequestData() { session = user.session , cardId = cardId, cacheKey = GUID.Generate().ToString()};
        _request( "/cards/delete" , requestData );
    }

    public string getQuery( Dictionary<string , string> dict )
    {
        string query = string.Join( "&" , dict.Select( x => x.Key + "=" + Uri.EscapeDataString( x.Value ) ).ToArray() );
        return query;
    }

    public void _request( string endPoint , RequestData requestData )
    {
        requestData.timestamp = PredictedServerTimestamp;
        requestData.version = Version;
        requestData.versionKey = VersionKey;

        LastRequest = new LastRequest();
        LastRequest.EndPoint = endPoint;
        LastRequest.RequestData = requestData;
        
        Byte[] payload = Encoding.UTF8.GetBytes( JsonUtility.ToJson( requestData ) );

        if ( localSendInvalidRequestData )
            Array.Clear( payload , 0 , payload.Length );
        
        payload = _encryptionHelper.Encrypt( payload , this.nonce );
        
        string payloadBase64 = Convert.ToBase64String( payload );
        
        var postForm = new RawJsonForm();
        postForm.AddField("payloadBase64", payloadBase64);
      
        // signature
        Byte[] digest = md5.ComputeHash( payload ); // md5(payload);
        Byte[] nonceByte = BitConverter.GetBytes( this.nonce ); // nonce to byte
        Byte[] signMessage = new Byte[ digest.Length + nonceByte.Length ]; // nonceByte + digest
            
        Array.Copy( nonceByte , signMessage , nonceByte.Length );
        Array.Copy( digest , 0 , signMessage , nonceByte.Length , digest.Length );
        
        signMessage = _encryptionHelper.SignMessage( signMessage );
        string signedBase64 = Convert.ToBase64String( signMessage );

        if ( localSendInvalidSignBase64 )
            signedBase64 = "1234";
        
        // query
        Dictionary<string, string> queryDict = new Dictionary<string, string>();
        queryDict.Add("signedBase64", signedBase64);
        queryDict.Add("token", token);
        
        if ( remoteTimeout ) 
            queryDict.Add( "remoteTimeout" , "true" );
        
        if ( remoteSendInvalidPayload )
            queryDict.Add( "remoteSendInvalidPayload" , "true" );
        
        var query = this.getQuery(queryDict);
            
        // send request
        var url = $"{Host}{endPoint}?{query}";

        var nonceSent = nonce;
        
        HTTPRequest request = new HTTPRequest(new Uri(url), HTTPMethods.Post, (originalRequest, response) =>
        {
            isSending = false;
       
            try
            {
                if ( response != null && response.IsSuccess )
                {
                    var isEncrypted = false;
                    if ( response.Headers.TryGetValue( "content-encoding" , out var isEncryptString ) )
                    {
                        if ( isEncryptString.Count > 0 && isEncryptString[0] == "encrypted" )
                        {
                            isEncrypted = true;
                        }
                    }
            
                    if ( isEncrypted )
                    {
                        // decrypt
                        var decrypted = _encryptionHelper.Decrypt( response.Data , nonceSent );

                        // decompress
                        var memoryStream = new MemoryStream( decrypted );
                        var toMemoryStream = new MemoryStream();
                        var gZipStream = new GZipStream( memoryStream , CompressionMode.Decompress );
                        gZipStream.CopyTo( toMemoryStream );
                        memoryStream.Close();
                        var decompressed = toMemoryStream.ToArray();

                        var text = Encoding.UTF8.GetString( decompressed );

                        // Debug.Log( $"response Data: {text}." );

                        RespondData = JsonUtility.FromJson<RespondData>( text );
                    }
                    else
                    {
                        RespondData = JsonUtility.FromJson<RespondData>(response.DataAsText);
                    }
            
                    if ( !string.IsNullOrEmpty( RespondData.token ) )
                        token = RespondData.token;

                    if ( RespondData.user.userId > 0 )
                        user = RespondData.user;

                    card = RespondData.body.card;
                    cards = RespondData.body.cards;

                    serverTimestamp = RespondData.timestamp;
                    receivedTimestamp = CurrentTimestamp;
                }
                else
                {
                    RespondData.errorCode = ErrorCode.Timeout;
                }
            }
            catch ( Exception e )
            {
                RespondData.errorCode = ErrorCode.FailedToDecryptServerPayload;
            }
      
        });
        
        // request.ConnectTimeout = TimeSpan.FromSeconds( Timeout );
        request.Timeout = TimeSpan.FromSeconds( Timeout );
        request.SetForm(postForm);
        
        request.Send();

        isSending = true;

        nonce++;
    }
}
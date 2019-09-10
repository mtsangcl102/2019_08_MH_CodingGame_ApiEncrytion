// Copyright (c) Mad Head Limited All Rights Reserved

using System;
using unity.libsodium;

public class EncryptionHelper
{
    private byte[] _encryptionKey;
    private byte[] _sigPublicKey;
    private byte[] _sigPrivateKey;
    
    public EncryptionHelper()
    {
        _encryptionKey = Convert.FromBase64String( EncryptionConfig.ChaChaEncryptionKey );
        _sigPublicKey = Convert.FromBase64String( EncryptionConfig.ChaChaSigPublicKey );
        _sigPrivateKey = Convert.FromBase64String( EncryptionConfig.ChaChaSigPrivateKey );
    }
    
    public byte[] Encrypt( byte[] message , long nonce )
    {
        var nonceByteArray = BitConverter.GetBytes( nonce );
        return StreamEncryption.EncryptChaCha20(message, nonceByteArray, _encryptionKey );
    }

    public byte[] Decrypt( byte[] encrypted , long nonce )
    {
        // var nonce = StreamEncryption.GenerateNonceChaCha20();
        // var key = StreamEncryption.GenerateKey();
        // var messageString = "Test message to encrypt";
        // var encrypted = StreamEncryption.EncryptChaCha20(messageString, nonce, key);
        // var decrypted = StreamEncryption.DecryptChaCha20(encrypted, nonce, key);

        var nonceByteArray = BitConverter.GetBytes( nonce );
        return StreamEncryption.DecryptChaCha20(encrypted, nonceByteArray, _encryptionKey );
        
        // public const string ChaChaEncryptionKey = "f2ZWDvhBXFSxKXpCl0wg9aEZnuXfA+B2c+5RU8wWbfQ=" ;
        // public const string ChaChaSigPublicKey = "6T7iL581iPWn1X/Nm8AD2PNRFDqGyGlRfslNy0SD63M=";
        // public const string ChaChaSigPrivateKey =
    }

    public byte[] SignOpenMessage( byte[] signedMessage )
    {
        return StreamEncryption.SignOpenMessage(signedMessage, _sigPublicKey );
    }

    public byte[] SignMessage( byte[] message )
    {
        return StreamEncryption.SignMessage(message, _sigPrivateKey );
    }


    // The crypto_sign_keypair() function randomly generates a secret key and a corresponding public key.
    // The public key is put into pk (crypto_sign_PUBLICKEYBYTES bytes) and the secret key into
    // sk (crypto_sign_SECRETKEYBYTES bytes).
    
    // The crypto_sign() function prepends a signature to a message m whose length is mlen bytes,
    // using the secret key sk.
    // The signed message, which includes the signature + a plain copy of the message, is put into sm,
    // and is crypto_sign_BYTES + mlen bytes long.
        
    // The crypto_sign_open() function checks that the signed message sm whose length is smlen bytes has
    // a valid signature for the public key pk.
        
}
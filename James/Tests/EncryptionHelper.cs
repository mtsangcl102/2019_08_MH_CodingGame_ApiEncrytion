using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using unity.libsodium;

public class EncryptionHelper
{
    public byte[] Encrypt( byte[] message, long nonce )
    {
            byte[] buffer = new byte[message.Length];
            int ret = NativeLibsodium.crypto_stream_chacha20_xor(buffer, message, message.Length, BitConverter.GetBytes( nonce ), Convert.FromBase64String( encryptionConfig.ChaChaEncryptionKey ) );

            if (ret != 0)
                throw new Exception("Error encrypting message.");

            return buffer;
    }
    
    public byte[] Decrypt( byte[] encrypted, long nonce )
    {
            byte[] buffer = new byte[encrypted.Length];
            int ret = NativeLibsodium.crypto_stream_chacha20_xor(buffer, encrypted, encrypted.Length, BitConverter.GetBytes( nonce ), Convert.FromBase64String( encryptionConfig.ChaChaEncryptionKey ) );

            if (ret != 0)
                throw new Exception("Error derypting message.");

            return buffer;
    }
    
    public byte[] SignMessage( byte[] hash )
    {
        byte[] buffer = new byte[ 100000 ];
        long bufferLength = 0;

        NativeLibsodium.crypto_sign(buffer, ref bufferLength, hash, hash.Length, Convert.FromBase64String( encryptionConfig.ChaChaSigPrivateKey ) );
        Array.Resize( ref buffer, (int)bufferLength );      
        return buffer;
        
    }
    
    public byte[] SignOpenMessage( byte[] signed )
    {
        byte[] buffer = new byte[ 100000 ];
        long bufferLength = 0;
        NativeLibsodium.crypto_sign_open(buffer, ref bufferLength, signed, signed.Length, Convert.FromBase64String( encryptionConfig.ChaChaSigPublicKey ) );
        Array.Resize( ref buffer, (int)bufferLength );      
        return buffer;
    }
    
}

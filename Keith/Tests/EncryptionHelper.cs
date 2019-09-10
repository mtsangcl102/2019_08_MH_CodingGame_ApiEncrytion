using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using unity.libsodium;
using System;

public class EncryptionHelper
{
    string JsonWebTokenKey = "unity-super-long-secret";
    string ChaChaEncryptionKey = "f2ZWDvhBXFSxKXpCl0wg9aEZnuXfA+B2c+5RU8wWbfQ=" ;
    string ChaChaSigPublicKey = "6T7iL581iPWn1X/Nm8AD2PNRFDqGyGlRfslNy0SD63M=" ;
    string ChaChaSigPrivateKey = "UDPW1Td0BfG4bFINTU85mrwRp9ipd2iqKg/0n8hkFWbpPuIvnzWI9afVf82bwAPY81EUOobIaVF+yU3LRIPrcw==" ;

    public byte[] Encrypt( byte[] message, long nonce )
    {
        byte[] buffer = new byte[ message.Length ];
        int ret = NativeLibsodium.crypto_stream_chacha20_xor( buffer, message, message.Length, BitConverter.GetBytes( nonce ), Convert.FromBase64String( ChaChaEncryptionKey ) );

        if( ret != 0 )
            throw new Exception( "Error encrypting message." );

        return buffer;
    }

    public byte[] Decrypt( byte[] encrypted, long nonce )
    {
        byte[] buffer = new byte[ encrypted.Length ];
        int ret = NativeLibsodium.crypto_stream_chacha20_xor( buffer, encrypted, encrypted.Length, BitConverter.GetBytes( nonce ), Convert.FromBase64String( ChaChaEncryptionKey ) );

        if( ret != 0 )
            throw new Exception( "Error encrypting message." );

        return buffer;
    }

    public byte[] SignOpenMessage( byte[] signed )
    {
        byte[] buffer = new byte[ signed.Length ];
        long bufferLength = signed.Length;
        NativeLibsodium.crypto_sign_open( buffer, ref bufferLength, signed, signed.Length, Convert.FromBase64String( ChaChaSigPublicKey ) );
        Array.Resize<byte>( ref buffer, (int)bufferLength );
        return buffer;
    }

    public byte[] SignMessage( byte[] hash )
    {
        byte[] buffer = new byte[ hash.Length + 64 ];
        long bufferLength = hash.Length + 64;
        NativeLibsodium.crypto_sign( buffer, ref bufferLength, hash, hash.Length, Convert.FromBase64String( ChaChaSigPrivateKey ) ) ;
        return buffer;
    }

    //[DllImport( DLL_NAME )]
    //public static extern int crypto_sign_keypair( byte[] publicKey, byte[] secretKey );

    //[DllImport( DLL_NAME )]
    //public static extern int crypto_sign( byte[] buffer, ref long bufferLength, byte[] message, long messageLength,
    //    byte[] key );

    //[DllImport( DLL_NAME )]
    //public static extern int crypto_sign_open( byte[] buffer, ref long bufferLength, byte[] signedMessage,
    //    long signedMessageLength, byte[] key );
}

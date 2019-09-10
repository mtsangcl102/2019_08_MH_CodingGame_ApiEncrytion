using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using unity.libsodium;

public class EncryptionHelper
{
/*
 *  JsonWebTokenKey: 'unity-super-long-secret',
 *  ChaChaEncryptionKey: 'f2ZWDvhBXFSxKXpCl0wg9aEZnuXfA+B2c+5RU8wWbfQ=',
 *  ChaChaSigPublicKey: '6T7iL581iPWn1X/Nm8AD2PNRFDqGyGlRfslNy0SD63M=',
 *  ChaChaSigPrivateKey: 'UDPW1Td0BfG4bFINTU85mrwRp9ipd2iqKg/0n8hkFWbpPuIvnzWI9afVf82bwAPY81EUOobIaVF+yU3LRIPrcw=='
 */
    
    private string JsonWebTokenKey;
    private byte[] ChaChaEncryptionKey;
    private byte[] ChaChaSigPublicKey;
    private byte[] ChaChaSigPrivateKey;

    public EncryptionHelper()
    {
        JsonWebTokenKey = "unity-super-long-secret";
        ChaChaEncryptionKey = Convert.FromBase64String("f2ZWDvhBXFSxKXpCl0wg9aEZnuXfA+B2c+5RU8wWbfQ=");
        ChaChaSigPublicKey = Convert.FromBase64String("6T7iL581iPWn1X/Nm8AD2PNRFDqGyGlRfslNy0SD63M=");
        ChaChaSigPrivateKey = Convert.FromBase64String("UDPW1Td0BfG4bFINTU85mrwRp9ipd2iqKg/0n8hkFWbpPuIvnzWI9afVf82bwAPY81EUOobIaVF+yU3LRIPrcw==");
    }
    
    
    public byte[] Encrypt(byte[] message, long nonce)
    {
        var byteNonce = BitConverter.GetBytes(nonce);
        return StreamEncryption.EncryptChaCha20(message, byteNonce, ChaChaEncryptionKey);
    }
    
    public byte[] Decrypt(byte[] message, long nonce)
    {
        var byteNonce = BitConverter.GetBytes(nonce);
        return StreamEncryption.DecryptChaCha20(message, byteNonce, ChaChaEncryptionKey);
    } 

    public byte[] SignMessage(byte[] hash)
    {
        long bufferSize = hash.Length + 64;
        byte[] buffer = new byte[bufferSize];

        NativeLibsodium.crypto_sign(buffer, ref bufferSize, hash, hash.Length, ChaChaSigPrivateKey);
        return buffer;
    }
    
    public byte[] SignOpenMessage(byte[] singed)
    {
        long bufferSize = singed.Length - 64;
        byte[] buffer = new byte[bufferSize];

        NativeLibsodium.crypto_sign_open(buffer, ref bufferSize, singed, singed.Length, ChaChaSigPublicKey);
        return buffer;
    }
}

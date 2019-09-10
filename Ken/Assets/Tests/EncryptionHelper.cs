using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using unity.libsodium;using System;
using System.Collections.Generic;
using BestHTTP.Extensions;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.Encoders;
using UnityEngine.iOS;
using UnityEngine.Windows;


public class EncryptionHelper
{
    
    private string JsonWebTokenKey = "unity-super-long-secret";
    private string ChaChaEncryptionKey = "f2ZWDvhBXFSxKXpCl0wg9aEZnuXfA+B2c+5RU8wWbfQ=";
    private string ChaChaEncryptionKey2 = "f2ZWDvhBXFSxKXpCl0wg9aEZn12fA+B2c+5RU8wWbfQ=";
    private string ChaChaSigPublicKey = "6T7iL581iPWn1X/Nm8AD2PNRFDqGyGlRfslNy0SD63M=";

    private string ChaChaSigPrivateKey =
        "UDPW1Td0BfG4bFINTU85mrwRp9ipd2iqKg/0n8hkFWbpPuIvnzWI9afVf82bwAPY81EUOobIaVF+yU3LRIPrcw==";

    public byte[] Encrypt(byte[] message, long nonce, bool isInvalidClientPayload = false)
    {
        byte[] messageVar = new byte[] { };
        if (isInvalidClientPayload)
        {
            messageVar = StreamEncryption.EncryptChaCha20(message, BitConverter.GetBytes(nonce), Convert.FromBase64String(ChaChaEncryptionKey2));
            
        }
        else
        {
            messageVar = StreamEncryption.EncryptChaCha20(message, BitConverter.GetBytes(nonce), Convert.FromBase64String(ChaChaEncryptionKey));
        }
        
        return messageVar;
    }
    
    public byte[] Decrypt(byte[] encryptedStr, long nonce)
    {
        byte[] decryptedVar = new byte[]{};
        decryptedVar = StreamEncryption.DecryptChaCha20(encryptedStr, BitConverter.GetBytes(nonce), Convert.FromBase64String(ChaChaEncryptionKey)) ;
        return decryptedVar;
    }

    public byte[] SignOpenMessage(byte[] signedVar)
    {
        byte[] value = new byte[signedVar.Length - 64];
        long valueLength = value.LongLength;
        
        NativeLibsodium.crypto_sign_open(value, ref valueLength, signedVar, signedVar.LongLength,
            System.Convert.FromBase64String(ChaChaSigPublicKey));
        return value;
    }
    
    public byte[] SignMessage(byte[] signedVar)
    {
        byte[] value = new byte[signedVar.Length + 64];
        long valueLength = value.Length;
        
        NativeLibsodium.crypto_sign(value, ref valueLength, signedVar, signedVar.Length,
            System.Convert.FromBase64String(ChaChaSigPrivateKey));
        
        return value;
    }
}

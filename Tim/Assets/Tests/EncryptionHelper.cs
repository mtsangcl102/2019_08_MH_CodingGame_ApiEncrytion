using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using unity.libsodium;
using System;
using System.Text;


public class EncryptionHelper 
{
    private const int crypto_sign_PUBLICKEYBYTES = 32;
    private const int crypto_sign_SECRETKEYBYTES = 64;
    private const int crypto_sign_BYTES = 64;

    private byte[] _ChaChaSigPublicKey = Convert.FromBase64String("6T7iL581iPWn1X/Nm8AD2PNRFDqGyGlRfslNy0SD63M=") ;
    private byte[] _ChaChaSigPrivateKey = Convert.FromBase64String("UDPW1Td0BfG4bFINTU85mrwRp9ipd2iqKg/0n8hkFWbpPuIvnzWI9afVf82bwAPY81EUOobIaVF+yU3LRIPrcw==") ;
    private byte[] _ChaChaEncryptionKey = Convert.FromBase64String("f2ZWDvhBXFSxKXpCl0wg9aEZnuXfA+B2c+5RU8wWbfQ=");

    public EncryptionHelper()
    {
        //NativeLibsodium.crypto_sign_keypair(_publicKey, _privateKey);
    }

    public byte[] Encrypt(byte[] msg, long nonce)
    {
        byte[] nonceBytes = BitConverter.GetBytes(nonce);

        byte[] encrypted = new byte[msg.Length];
        int ret = NativeLibsodium.crypto_stream_chacha20_xor(encrypted, msg, msg.Length, nonceBytes, _ChaChaEncryptionKey);

        if (ret != 0)
            throw new Exception("Error encrypting message.");

        return encrypted;
    }

    public byte[] SignMessage(byte[] msg)
    {
        long bufLength = crypto_sign_BYTES + msg.Length;
        byte[] signedMsg = new byte[bufLength];

        int rc = NativeLibsodium.crypto_sign(signedMsg, ref bufLength, msg, msg.Length, _ChaChaSigPrivateKey);

        if (rc != 0)
            throw new Exception("Error encrypting message.");

        Debug.Log("signed" + signedMsg);
        return signedMsg;
    }

    public byte[] Decrypt(byte[] msg, long nonce)
    {
        byte[] nonceBytes = BitConverter.GetBytes(nonce);

        byte[] decrypted = new byte[msg.Length];
        int ret = NativeLibsodium.crypto_stream_chacha20_xor(decrypted, msg, msg.Length, nonceBytes, _ChaChaEncryptionKey);

        if (ret != 0)
            throw new Exception("Error derypting message.");

        return decrypted;
    }

    public byte[] SignOpenMessage(byte[] msg)
    {
        long bufLength = msg.Length - crypto_sign_BYTES;
        byte[] unsignedMsg = new byte[bufLength];

        int rc = NativeLibsodium.crypto_sign_open(unsignedMsg, ref bufLength, msg, msg.Length, _ChaChaSigPublicKey);

        if (rc != 0)
            throw new Exception("Error encrypting message.");

        return unsignedMsg;
    }
}

using System;
using System.IO;
using unity.libsodium;
using UnityEngine;

public class EncryptionHelper
{
    private byte[] JsonWebTokenKey; 
    private byte[] ChaChaEncryptionKey;
    private byte[] ChaChaSigPublicKey; 
    private byte[] ChaChaSigPrivateKey; 
    
    public EncryptionHelper()
    {
//       JsonWebTokenKey = Convert.FromBase64String("unity-super-long-secret");
        ChaChaEncryptionKey = Convert.FromBase64String("f2ZWDvhBXFSxKXpCl0wg9aEZnuXfA+B2c+5RU8wWbfQ=");
        ChaChaSigPublicKey = Convert.FromBase64String("6T7iL581iPWn1X/Nm8AD2PNRFDqGyGlRfslNy0SD63M=");
        ChaChaSigPrivateKey = Convert.FromBase64String("UDPW1Td0BfG4bFINTU85mrwRp9ipd2iqKg/0n8hkFWbpPuIvnzWI9afVf82bwAPY81EUOobIaVF+yU3LRIPrcw==");
    }
    
    public byte[] Encrypt(byte[] message, long nonce)
    {
        var nonceByte = BitConverter.GetBytes(nonce);
        return StreamEncryption.EncryptChaCha20(message, nonceByte, ChaChaEncryptionKey);
    }

    public byte[] Decrypt(byte[] encrypted, long nonce)
    {
        var nonceByte = BitConverter.GetBytes(nonce);
        return StreamEncryption.DecryptChaCha20(encrypted, nonceByte, ChaChaEncryptionKey);
    }

    public byte[] SignOpenMessage(byte[] signed)
    {
        byte[] buffer = new byte[2048];
        long bufferLength = 2048;
        if (NativeLibsodium.crypto_sign_open(buffer, ref bufferLength, signed, signed.Length, ChaChaSigPublicKey) == 0)
        {
            Array.Resize(ref buffer, (int) bufferLength);
            return buffer;
        }
        else
        {
            Debug.LogError("Incorrect Signature");
            return null;
        }
    }

    public byte[] SignMessage(byte[] hash)
    {
        byte[] buffer = new byte[2048];
        long bufferLength = 2048;
        if (NativeLibsodium.crypto_sign(buffer, ref bufferLength, hash, hash.Length, ChaChaSigPrivateKey) == 0)
        {
            Array.Resize(ref buffer, (int) bufferLength);
            return buffer;
        }
        else
        {
            Debug.LogError("Incorrect Signature");
            return null;
        }
    }
}
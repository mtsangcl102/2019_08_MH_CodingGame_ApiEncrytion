using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using unity.libsodium;

namespace Tests
{
    /*
     *  crypto_sign_keypair
     *  crypto_sign
     *  crypto_sign_open
     *  crypto_stream_chacha20_xor
     */

    public class EncryptionHelper
    {
        public static string JsonWebTokenKey = "unity-super-long-secret";
        public static string ChaChaEncryptionKey = "f2ZWDvhBXFSxKXpCl0wg9aEZnuXfA+B2c+5RU8wWbfQ=";
        public static string ChaChaSigPublicKey = "6T7iL581iPWn1X/Nm8AD2PNRFDqGyGlRfslNy0SD63M=";
        public static string ChaChaSigPrivateKey = "UDPW1Td0BfG4bFINTU85mrwRp9ipd2iqKg/0n8hkFWbpPuIvnzWI9afVf82bwAPY81EUOobIaVF+yU3LRIPrcw==";
        
        public byte[] Encrypt(byte[] message, long nonce)
        {
            byte[] nonceByteArr = BitConverter.GetBytes(nonce);
            byte[] key = Convert.FromBase64String(ChaChaEncryptionKey);
            byte[] buffer = new byte[message.Length];
            int ret = NativeLibsodium.crypto_stream_chacha20_xor(buffer, message, message.Length, nonceByteArr, key);
            if (ret == 0) return buffer;
            return null;
        }

        public byte[] Decrypt(byte[] message, long nonce)
        {
            byte[] nonceByteArr = BitConverter.GetBytes(nonce);
            byte[] key = Convert.FromBase64String(ChaChaEncryptionKey);
            byte[] buffer = new byte[message.Length];
            int ret = NativeLibsodium.crypto_stream_chacha20_xor(buffer, message, message.Length, nonceByteArr, key);

            if (ret == 0) return buffer;
            return null;
        }

        public byte[] SignMessage(byte[] hash)
        {
            byte[] key = Convert.FromBase64String(ChaChaSigPrivateKey);
            byte[] buffer = new byte[hash.Length + 64];

            long bufferLength = 0;
            
            int ret = NativeLibsodium.crypto_sign(buffer, ref bufferLength, hash, hash.Length, key);
            if (ret == 0) return buffer;
            return null;
        }
        
        public byte[] SignOpenMessage(byte[] signed)
        {
            byte[] key = Convert.FromBase64String(ChaChaSigPublicKey);
            byte[] buffer = new byte[signed.Length-64];

            long bufferLength = 0;
            
            int ret = NativeLibsodium.crypto_sign_open(buffer, ref bufferLength, signed, signed.Length, key);
            if (ret == 0) return buffer;
            return null;
        }
    }
} 

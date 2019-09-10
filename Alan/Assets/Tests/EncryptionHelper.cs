using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using unity.libsodium;

namespace Tests
{
    public class EncryptionHelper
    {
        public string JsonWebTokenKey = "unity-super-long-secret";
        private const string ChaChaEncryptionKey = "f2ZWDvhBXFSxKXpCl0wg9aEZnuXfA+B2c+5RU8wWbfQ=";
        private const string ChaChaEncryptionKeyFake = "f1ZWDvhBXFSxKXpCl0wg9aEZnuXfA+B2c+5RU8wWbfQ=";
        private const string ChaChaSigPublicKey = "6T7iL581iPWn1X/Nm8AD2PNRFDqGyGlRfslNy0SD63M=";
        private const string ChaChaSigPrivateKey = "UDPW1Td0BfG4bFINTU85mrwRp9ipd2iqKg/0n8hkFWbpPuIvnzWI9afVf82bwAPY81EUOobIaVF+yU3LRIPrcw==";

        public byte[] Encrypt(byte[] message, long tmpNonce) {
            return StreamEncryption.EncryptChaCha20(message, BitConverter.GetBytes(tmpNonce), Convert.FromBase64String(ChaChaEncryptionKey));
        }

        public byte[] Decrypt(byte[] message, long tmpNonce) {
            return StreamEncryption.DecryptChaCha20(message, BitConverter.GetBytes(tmpNonce), Convert.FromBase64String(ChaChaEncryptionKey)) ;
        }
        
        public byte[] Encrypt_Fake(byte[] message, long tmpNonce) {
            return StreamEncryption.EncryptChaCha20(message, BitConverter.GetBytes(tmpNonce), Convert.FromBase64String(ChaChaEncryptionKeyFake));
        }

        public byte[] Decrypt_Fake(byte[] message, long tmpNonce) {
            return StreamEncryption.DecryptChaCha20(message, BitConverter.GetBytes(tmpNonce), Convert.FromBase64String(ChaChaEncryptionKeyFake)) ;
        }

        public byte[] SignMessage(byte[] unsigned) {
            if (NativeLibsodium.crypto_sign_keypair(Convert.FromBase64String(ChaChaSigPublicKey), Convert.FromBase64String(ChaChaSigPrivateKey)) != 0)
                throw new Exception("Wrong Key pair");
            
            byte[] buffer = new byte[unsigned.LongLength + 64];
            long bufferLength = buffer.LongLength;
            int ret = NativeLibsodium.crypto_sign(buffer, ref bufferLength, unsigned, unsigned.LongLength, 
                Convert.FromBase64String(ChaChaSigPrivateKey) );

            if (ret != 0)
                throw new Exception("Error signing message.");
            
            return buffer;
        }
        
        public byte[] SignOpenMessage(byte[] signed){
            if (NativeLibsodium.crypto_sign_keypair(Convert.FromBase64String(ChaChaSigPublicKey), Convert.FromBase64String(ChaChaSigPrivateKey)) != 0)
                throw new Exception("Wrong Key pair");
            
            byte[] buffer = new byte[signed.LongLength - 64];
            long bufferLength = buffer.LongLength;
            int ret = NativeLibsodium.crypto_sign_open(buffer, ref bufferLength, signed, signed.LongLength, 
                Convert.FromBase64String(ChaChaSigPublicKey) );

            if (ret != 0)
                throw new Exception("Error open signing message.");
            
            return buffer;
        }
        
    }
}

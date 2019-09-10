using System;
using System.Collections;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Tls;
using NUnit.Framework;
using unity.libsodium;
using UnityEngine;
using System.Text;
using System.IO;
using System.IO.Compression;

namespace Tests
{
    public class EncryptionHelper
    {
        private byte[] _crypteKey;
        private byte[] _publicKey;
        private byte[] _privateKey;
        private byte[] buffer;

        private GZipStream _gZipStream;
        
        public EncryptionHelper()
        {
            buffer = new byte[1000];
            NativeLibsodium.sodium_init();
            _SetKeys();
            //GenerateNewKey();
        }

        public void GenerateNewKey()
        {
            _crypteKey = StreamEncryption.GenerateKey();
//            NativeLibsodium.crypto_sign_keypair( _publicKey, _privateKey );
        }
        
        private void _SetKeys()
        {
            _crypteKey = Base64ToBytes(EncryptionConfig.ChaChaEncryptionKey);
            _publicKey = Base64ToBytes(EncryptionConfig.ChaChaSigPublicKey);
            _privateKey = Base64ToBytes(EncryptionConfig.ChaChaSigPrivateKey);
        }
        
        public byte[] Encrypt( byte[] message, long nonce )
        {
            return StreamEncryption.EncryptChaCha20(message, GetNonceBytes(nonce), _crypteKey);
        }
        
        public byte[] Decrypt( byte[] cipherText, long nonce )
        {
            return StreamEncryption.DecryptChaCha20(cipherText, GetNonceBytes(nonce), _crypteKey);
        }

        public byte[] SignOpenMessage(byte[] message )
        {
            long bufferLength = -1;
            NativeLibsodium.crypto_sign_open(buffer, ref bufferLength, message, message.Length, _publicKey);
            var result = new byte[bufferLength];
            Array.Copy( buffer, result, bufferLength );
            return result;
        }
        
        public byte[] SignMessage(byte[] message )
        {
            long bufferLength = -1;
            NativeLibsodium.crypto_sign(buffer, ref bufferLength, message, message.Length, _privateKey);
            var result = new byte[bufferLength];
            Array.Copy( buffer, result, bufferLength );
            return result;
        }

        public byte[] GetNonceBytes(long nonce)
        {
            return BitConverter.GetBytes(nonce);;
        }
        
        public byte[] StringToBytes(string s)
        {
            return Encoding.UTF8.GetBytes(s);
        }

        public string BytesToString(byte[] b)
        {
            return Encoding.UTF8.GetString( b );
        }
        
        public byte[] Base64ToBytes(string b64 )
        {
            return Convert.FromBase64String( b64 );
        }

        public string BytesToBase64(byte[] b)
        {
            return Convert.ToBase64String(b);
        }

        public byte[] GZipCompress(byte[] data)
        {
            
            using (var outStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(outStream, CompressionMode.Compress))
                {
                    using (var ms = new MemoryStream(data))
                    {
                        ms.CopyTo(gzipStream);
                    }
                }

                return outStream.ToArray();
            }
        }
        
        public byte[] GZipDecompress(byte[] data)
        {
            using (var compressedStream = new MemoryStream(data))
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                zipStream.CopyTo(resultStream);
                return resultStream.ToArray();
            }
        }
    }
}
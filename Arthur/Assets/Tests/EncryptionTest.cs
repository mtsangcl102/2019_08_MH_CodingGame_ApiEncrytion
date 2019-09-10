using System;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using unity.libsodium;
using UnityEngine;

namespace Tests
{
    public class EncryptionTest
    {
        private EncryptionHelper _encryptionHelper = new EncryptionHelper();

        [Test]
        public void Stream_Encryption_Basic()
        {
            var nonce = StreamEncryption.GenerateNonceChaCha20();
            var key = StreamEncryption.GenerateKey();
            var messageString = "Test message to encrypt";
            var encrypted = StreamEncryption.EncryptChaCha20(messageString, nonce, key);
            var decrypted = StreamEncryption.DecryptChaCha20(encrypted, nonce, key);
            Assert.AreEqual(messageString, Encoding.UTF8.GetString(decrypted));
        }

        [Test]
        public void Encryption_Basic()
        {
            long nonce = 10;
            var messageString = "Test message to encrypt";
            var message = Encoding.UTF8.GetBytes("Test message to encrypt");
            var encrypted = _encryptionHelper.Encrypt(message, nonce);
            var decrypted = _encryptionHelper.Decrypt(encrypted, nonce);
            Assert.AreEqual(messageString, Encoding.UTF8.GetString(decrypted));
        }


        struct TestMessage
        {
            public string name;
            public string company;
            public string address;
        }

        [Test]
        public void Encryption_Verify_With_Server()
        {
            long nonceValue = 256 * 256 * 256 + 1;
            var signedBase64 =
                "uEfMMnTDZOlvAMh6Hw38mVfvDJr7jkG4nVOLqiIuXrAmkse7awC5cbHVczLW4jOmzlsO3VX5ZpoFTV5lLhbeCHsibmFtZSI6InRlcmVuY2UiLCJjb21wYW55IjoibWFkaGVhZCIsImFkZHJlc3MiOiJzY2llbmNlcGFyayJ9";
            var encryptedBase64 =
                "vRVKqppXx+txj38ZjhfS83C39O3fzRpb3rk/vG8KMEuU28RNesE/QdG18L5UHFXv7fpbA3y1A5aQ55qeWt4=";
            var singed = Convert.FromBase64String(signedBase64);
            var encrypted = Convert.FromBase64String(encryptedBase64);

            var message = new TestMessage() {name = "terence", company = "madhead", address = "sciencepark"};
            var decrypted = _encryptionHelper.Decrypt(encrypted, nonceValue);
            var opened = _encryptionHelper.SignOpenMessage(singed);
            Assert.AreEqual(JsonUtility.ToJson(message), Encoding.UTF8.GetString(decrypted));
            Assert.AreEqual(JsonUtility.ToJson(message), Encoding.UTF8.GetString(opened));
        }

        [Test]
        // 1000 * 10 Loops: 14.7 seconds, md5 takes the most performance problem
        public void Encryption_Performance()
        {
            var message = new byte[1000 * 100];
            MD5 md5 = MD5.Create();

            var date = DateTime.Now;
            var loop = 1000 * 10; // should be 1000 * 10
            for (int i = 0; i < loop; i++)
            {
                var encrypted = _encryptionHelper.Encrypt(message, i);
                var decrypted = _encryptionHelper.Decrypt(encrypted, i);
                byte[] hash = md5.ComputeHash(message);
                var signed = _encryptionHelper.SignMessage(hash);
                var opened = _encryptionHelper.SignOpenMessage(signed);
//                Debug.Log($"{Convert.ToBase64String(hash)}");
//                Debug.Log($"{Convert.ToBase64String(opened)}");
            }

            Debug.Log($"Execution Time: {DateTime.Now - date}");
        }
    }
}
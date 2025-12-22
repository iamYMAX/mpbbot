using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TelegramGigaChatBot.Services
{
    public static class SecurityService
    {
        private const int KeySize = 256;
        private const int BlockSize = 128;

        public static string Encrypt(string plainText, string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            if (keyBytes.Length * 8 != KeySize)
            {
                throw new ArgumentException($"Key must be {KeySize} bits long.", nameof(key));
            }

            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.GenerateIV();
            var iv = aes.IV;

            using var encryptor = aes.CreateEncryptor(aes.Key, iv);
            using var memoryStream = new MemoryStream();
            using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
            {
                using (var streamWriter = new StreamWriter(cryptoStream))
                {
                    streamWriter.Write(plainText);
                }
            }

            var encryptedBytes = memoryStream.ToArray();
            var resultBytes = new byte[iv.Length + encryptedBytes.Length];
            Buffer.BlockCopy(iv, 0, resultBytes, 0, iv.Length);
            Buffer.BlockCopy(encryptedBytes, 0, resultBytes, iv.Length, encryptedBytes.Length);

            return Convert.ToBase64String(resultBytes);
        }

        public static string Decrypt(string cipherText, string key)
        {
            var fullCipher = Convert.FromBase64String(cipherText);
            var keyBytes = Encoding.UTF8.GetBytes(key);
            if (keyBytes.Length * 8 != KeySize)
            {
                throw new ArgumentException($"Key must be {KeySize} bits long.", nameof(key));
            }

            using var aes = Aes.Create();
            var iv = new byte[aes.BlockSize / 8];
            var cipher = new byte[fullCipher.Length - iv.Length];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            aes.Key = keyBytes;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var memoryStream = new MemoryStream(cipher);
            using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            using var streamReader = new StreamReader(cryptoStream);

            return streamReader.ReadToEnd();
        }
    }
}

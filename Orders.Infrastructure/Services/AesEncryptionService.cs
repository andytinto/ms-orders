using System.Security.Cryptography;
using System.Text;
using Orders.Application.Common.Interfaces;

namespace Orders.Infrastructure.Services
{
    public class AesEncryptionService : IEncryptionService
    {
        public string Encrypt(string plainText, string key, string? iv = null)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32).Substring(0, 32));
            if (!string.IsNullOrEmpty(iv))
            {
                aes.IV = Encoding.UTF8.GetBytes(iv.PadRight(16).Substring(0, 16));
            }
            else
            {
                aes.GenerateIV();
            }

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            var encrypted = ms.ToArray();
            var result = new byte[aes.IV.Length + encrypted.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);

            return Convert.ToBase64String(result);
        }

        public string Decrypt(string cipherText, string key, string? iv = null)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            try
            {
                var fullCipher = Convert.FromBase64String(cipherText);
                using var aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32).Substring(0, 32));

                byte[] cipherBytes;
                if (!string.IsNullOrEmpty(iv))
                {
                    aes.IV = Encoding.UTF8.GetBytes(iv.PadRight(16).Substring(0, 16));
                    cipherBytes = fullCipher;
                }
                else
                {
                    var ivBytes = new byte[16];
                    cipherBytes = new byte[fullCipher.Length - 16];
                    Buffer.BlockCopy(fullCipher, 0, ivBytes, 0, 16);
                    Buffer.BlockCopy(fullCipher, 16, cipherBytes, 0, cipherBytes.Length);
                    aes.IV = ivBytes;
                }

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(cipherBytes);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);
                return sr.ReadToEnd();
            }
            catch
            {
                return cipherText;
            }
        }

        public string DecryptDeterministic(string cipherText, Guid key, string baseKey)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;
            try
            {
                var keyBytes = key.ToByteArray();
                byte[] ivBytes = !string.IsNullOrEmpty(baseKey)
                    ? Convert.FromBase64String(baseKey)
                    : new byte[16];

                if (ivBytes.Length > 16)
                {
                    var truncated = new byte[16];
                    Buffer.BlockCopy(ivBytes, 0, truncated, 0, 16);
                    ivBytes = truncated;
                }

                using var aes = Aes.Create();
                aes.Key = keyBytes;
                aes.IV = ivBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                var buffer = Convert.FromBase64String(cipherText);
                using var decryptor = aes.CreateDecryptor();
                using var ms = new MemoryStream(buffer);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);
                return sr.ReadToEnd();
            }
            catch
            {
                return cipherText;
            }
        }
    }
}

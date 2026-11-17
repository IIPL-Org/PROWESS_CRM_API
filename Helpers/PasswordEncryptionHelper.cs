using System.Security.Cryptography;
using System.Text;

namespace PROWESS_CRM.Helpers
{
    /// <summary>
    /// AES-256 Symmetric Password Encryption &amp; Decryption Helper
    /// Allows passwords to be encrypted for storage AND decrypted back to plain text when needed.
    /// </summary>
    public static class PasswordEncryptionHelper
    {
        // 32-byte secret key for AES-256 encryption/decryption
        private const string SecretKey = "PayrollCRM_AES_Secret_Key_32Byte!"; 

        /// <summary>
        /// Encrypts a plain-text password into a Base64 AES-256 encrypted string
        /// </summary>
        /// <param name="plainText">Plain text password (e.g. "Admin@123")</param>
        /// <returns>Encrypted Base64 string</returns>
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            byte[] keyBytes = Encoding.UTF8.GetBytes(SecretKey);
            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length); // Prepend Initialization Vector (IV)

            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        /// <summary>
        /// Decrypts a Base64 AES-256 encrypted password string back to original plain text
        /// </summary>
        /// <param name="cipherText">Encrypted Base64 string</param>
        /// <returns>Original plain-text password</returns>
        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;

            try
            {
                byte[] fullCipher = Convert.FromBase64String(cipherText);
                byte[] keyBytes = Encoding.UTF8.GetBytes(SecretKey);

                using var aes = Aes.Create();
                aes.Key = keyBytes;

                byte[] iv = new byte[aes.BlockSize / 8];
                byte[] cipher = new byte[fullCipher.Length - iv.Length];

                Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
                Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(cipher);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);

                return sr.ReadToEnd();
            }
            catch (Exception ex)
            {
                // If it's not a valid AES Base64 string (e.g. plain text or invalid format), return error string
                return $"[Unable to decrypt: {ex.Message}]";
            }
        }
    }
}

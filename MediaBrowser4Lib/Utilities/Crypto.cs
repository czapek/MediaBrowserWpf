using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security.Cryptography;


namespace MediaBrowser4.Utilities
{
    public class Crypto
    {
        private static MD5CryptoServiceProvider md5Crypto;
        private static Encoder unicodeEncoder = System.Text.Encoding.Unicode.GetEncoder();
        private static Type defaultType = Type.GLOBAL;

        #region global key parameters

        // Passphrase from which a pseudo-random password will be derived. The
        // derived password will be used to generate the encryption key.
        // Passphrase can be any string. In this example we assume that this
        // passphrase is an ASCII string.
        private static string passPhrase = "Pas5pr@se";
        // Salt value used along with passphrase to generate password. Salt can
        // be any string. In this example we assume that salt is an ASCII string.
        private static string saltValue = "s@1tValue";
        // Hash algorithm used to generate password. Allowed values are: "MD5" and
        // "SHA1". SHA1 hashes are a bit slower, but more secure than MD5 hashes.
        private static string hashAlgorithm = "SHA1";
        // Number of iterations used to generate password. One or two iterations
        // should be enough.
        private static int passwordIterations = 2;
        // Initialization vector (or IV). This value is required to encrypt the
        // first block of plaintext data. For RijndaelManaged class IV must be 
        // exactly 16 ASCII characters long.
        private static string initVector = "@1B2c3D4e5F6g7H8";
        // Size of encryption key in bits. Allowed values are: 128, 192, and 256. 
        // Longer keys are more secure than shorter keys.
        private static int keySize = 256;

        #endregion

        /// <summary>
        /// Specifies what kind of key store is used.
        /// </summary>
        public enum Type
        {
            /// <summary>Use the hard coded Key.</summary>			
            GLOBAL,
            /// <summary>Use the user key store of the OS.</summary>			
            USER,
            /// <summary>Use the main key store of the OS.</summary>			
            WORKSTATION,
            /// <summary>Default is the Global type.</summary>
            DEFAULT
        }

        public static void GetMD5Value(MediaBrowser4.Objects.MediaItem mItem, int maxlength)
        {
            mItem.Md5Value = GetMD5fromFile(mItem.FileObject, maxlength);
        }

        public static string GetMD5fromFile(FileInfo fileInf, int maxlength)
        {
            try
            {
                string md5Key = "";

                if (md5Crypto == null)
                {
                    md5Crypto = new MD5CryptoServiceProvider();
                }

                byte[] hash = md5Crypto.ComputeHash(GetBytesFromFile(fileInf, maxlength));
                foreach (byte b in hash)
                {
                    md5Key += b.ToString("x");
                }
                return md5Key;
            }
            catch
            {
                return "";
            }
        }

        public static byte[] GetBytesFromFile(FileInfo file, int maxlength)
        {
            byte[] buffer;

            if (file.Length > maxlength)
            {
                buffer = new byte[maxlength];
                FileStream fileStream = new FileStream(file.FullName,
                        FileMode.Open, FileAccess.Read, FileShare.Read, maxlength);

                fileStream.Read(buffer, 0, maxlength);
                fileStream.Close();
            }
            else
            {
                buffer = File.ReadAllBytes(file.FullName);
            }


            return buffer;
        }

        public static string Decrypt(string text, Type encryptionType)
        {
            string result = null;

            if (encryptionType == Type.DEFAULT)
            {
                encryptionType = defaultType;
            }

            switch (encryptionType)
            {
                case Type.GLOBAL:
                    result = Crypto.Decrypt(text,
                        passPhrase,
                        saltValue,
                        hashAlgorithm,
                        passwordIterations,
                        initVector,
                        keySize);
                    break;
                case Type.USER:
                    result = null;
                    break;
                case Type.WORKSTATION:
                    result = null;
                    break;
            }

            return result;
        }

        public static string Encrypt(string text, Type encryptionType)
        {
            string result = null;

            if (encryptionType == Type.DEFAULT)
            {
                encryptionType = defaultType;
            }

            switch (encryptionType)
            {
                case Type.GLOBAL:
                    result = Crypto.Encrypt(text,
                        passPhrase,
                        saltValue,
                        hashAlgorithm,
                        passwordIterations,
                        initVector,
                        keySize);
                    break;
                case Type.USER:
                    result = null;
                    break;
                case Type.WORKSTATION:
                    result = null;
                    break;
            }

            return result;
        }

        public static string Encrypt(string plainText,
            string passPhrase,
            string saltValue,
            string hashAlgorithm,
            int passwordIterations,
            string initVector,
            int keySize)
        {
            if (plainText == null || plainText.Length == 0)
                return "";

            byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
            byte[] saltValueBytes = Encoding.ASCII.GetBytes(saltValue);
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);

            PasswordDeriveBytes password = new PasswordDeriveBytes(
                passPhrase,
                saltValueBytes,
                hashAlgorithm,
                passwordIterations);

            byte[] keyBytes = password.GetBytes(keySize / 8);
            RijndaelManaged symmetricKey = new RijndaelManaged();
            symmetricKey.Mode = CipherMode.CBC;

            ICryptoTransform encryptor = symmetricKey.CreateEncryptor(
                keyBytes,
                initVectorBytes);

            MemoryStream memoryStream = new MemoryStream();

            CryptoStream cryptoStream = new CryptoStream(memoryStream,
                encryptor,
                CryptoStreamMode.Write);

            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
            cryptoStream.FlushFinalBlock();
            byte[] cipherTextBytes = memoryStream.ToArray();

            memoryStream.Close();
            cryptoStream.Close();

            return Convert.ToBase64String(cipherTextBytes);
        }


        public static string Decrypt(string cipherText, string passPhrase,
            string saltValue,
            string hashAlgorithm,
            int passwordIterations,
            string initVector,
            int keySize)
        {
            if (cipherText == null || cipherText.Length == 0)
                return "";

            byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
            byte[] saltValueBytes = Encoding.ASCII.GetBytes(saltValue);
            byte[] cipherTextBytes = Convert.FromBase64String(cipherText);

            PasswordDeriveBytes password = new PasswordDeriveBytes(
                passPhrase,
                saltValueBytes,
                hashAlgorithm,
                passwordIterations);

            byte[] keyBytes = password.GetBytes(keySize / 8);
            RijndaelManaged symmetricKey = new RijndaelManaged();
            symmetricKey.Mode = CipherMode.CBC;

            ICryptoTransform decryptor = symmetricKey.CreateDecryptor(
                keyBytes,
                initVectorBytes);

            MemoryStream memoryStream = new MemoryStream(cipherTextBytes);

            CryptoStream cryptoStream = new CryptoStream(memoryStream,
                decryptor,
                CryptoStreamMode.Read);

            byte[] plainTextBytes = new byte[cipherTextBytes.Length];

            int decryptedByteCount = cryptoStream.Read(plainTextBytes,
                0,
                plainTextBytes.Length);

            memoryStream.Close();
            cryptoStream.Close();

            return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
        }
    }
}

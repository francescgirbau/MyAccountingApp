using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MyAccountingApp.Core.Vault;

public class VaultService : IVaultService
{
    private readonly string _metaFilePath;
    private readonly object _lock = new();
    private byte[]? _derivedKey;
    private bool _isUnlocked;

    public VaultService(string dataDirectory)
        : this(dataDirectory, enabled: true)
    {
    }

    public VaultService(string dataDirectory, bool enabled)
    {
        this.IsEnabled = enabled;
        Directory.CreateDirectory(dataDirectory);
        this._metaFilePath = Path.Combine(dataDirectory, "vault.meta");
    }

    public bool IsEnabled { get; }

    public bool IsInitialized
    {
        get
        {
            lock (this._lock)
            {
                return File.Exists(this._metaFilePath);
            }
        }
    }

    public bool IsUnlocked
    {
        get
        {
            lock (this._lock)
            {
                return this._isUnlocked && this._derivedKey != null;
            }
        }
    }

    public void Initialize(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be empty.", nameof(password));
        }

        lock (this._lock)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            int iterations = 100_000;
            byte[] key = DeriveKey(password, salt, iterations);

            // Create verifier encrypted with key
            byte[] verifierPlaintext = Encoding.UTF8.GetBytes("MY_ACCOUNTING_APP_VAULT_OK");
            byte[] encryptedVerifier = EncryptWithKey(verifierPlaintext, key);

            VaultMetadata meta = new()
            {
                SaltBase64 = Convert.ToBase64String(salt),
                Iterations = iterations,
                EncryptedVerifierBase64 = Convert.ToBase64String(encryptedVerifier),
            };

            string json = JsonSerializer.Serialize(meta);
            File.WriteAllText(this._metaFilePath, json);

            this._derivedKey = key;
            this._isUnlocked = true;
        }
    }

    public bool Unlock(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        lock (this._lock)
        {
            if (!File.Exists(this._metaFilePath))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(this._metaFilePath);
                VaultMetadata? meta = JsonSerializer.Deserialize<VaultMetadata>(json);
                if (meta == null)
                {
                    return false;
                }

                byte[] salt = Convert.FromBase64String(meta.SaltBase64);
                byte[] key = DeriveKey(password, salt, meta.Iterations);
                byte[] encryptedVerifier = Convert.FromBase64String(meta.EncryptedVerifierBase64);

                byte[] decryptedVerifier = DecryptWithKey(encryptedVerifier, key);
                string verifierText = Encoding.UTF8.GetString(decryptedVerifier);

                if (verifierText == "MY_ACCOUNTING_APP_VAULT_OK")
                {
                    this._derivedKey = key;
                    this._isUnlocked = true;
                    return true;
                }
            }
            catch
            {
                // Decryption failure or invalid format
            }

            return false;
        }
    }

    public void Lock()
    {
        lock (this._lock)
        {
            if (this._derivedKey != null)
            {
                Array.Clear(this._derivedKey, 0, this._derivedKey.Length);
                this._derivedKey = null;
            }

            this._isUnlocked = false;
        }
    }

    public byte[] Encrypt(byte[] plaintext)
    {
        lock (this._lock)
        {
            if (!this.IsUnlocked || this._derivedKey == null)
            {
                throw new InvalidOperationException("Vault is locked or not initialized.");
            }

            return EncryptWithKey(plaintext, this._derivedKey);
        }
    }

    public byte[] Decrypt(byte[] ciphertext)
    {
        lock (this._lock)
        {
            if (!this.IsUnlocked || this._derivedKey == null)
            {
                throw new InvalidOperationException("Vault is locked or not initialized.");
            }

            return DecryptWithKey(ciphertext, this._derivedKey);
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt, int iterations)
    {
        using Rfc2898DeriveBytes pbkdf2 = new(password, salt, iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32); // 256-bit key for AES
    }

    private static byte[] EncryptWithKey(byte[] plaintext, byte[] key)
    {
        using AesGcm aesGcm = new(key, 16);
        byte[] nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize];
        byte[] ciphertext = new byte[plaintext.Length];

        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        // Format: [Nonce (12 bytes)] [Tag (16 bytes)] [Ciphertext]
        byte[] result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);

        return result;
    }

    private static byte[] DecryptWithKey(byte[] ciphertextBytes, byte[] key)
    {
        // 12 bytes nonce + 16 bytes tag = 28 bytes minimum
        if (ciphertextBytes.Length < 28)
        {
            throw new CryptographicException("Invalid ciphertext length.");
        }

        using AesGcm aesGcm = new(key, 16);
        byte[] nonce = new byte[12];
        byte[] tag = new byte[16];
        int cipherLen = ciphertextBytes.Length - 28;
        byte[] ciphertext = new byte[cipherLen];

        Buffer.BlockCopy(ciphertextBytes, 0, nonce, 0, 12);
        Buffer.BlockCopy(ciphertextBytes, 12, tag, 0, 16);
        Buffer.BlockCopy(ciphertextBytes, 28, ciphertext, 0, cipherLen);

        byte[] plaintext = new byte[cipherLen];
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }

    private class VaultMetadata
    {
        public string SaltBase64 { get; set; } = string.Empty;
        public int Iterations { get; set; }
        public string EncryptedVerifierBase64 { get; set; } = string.Empty;
    }
}

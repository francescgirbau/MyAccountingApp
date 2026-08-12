using System.Text;
using MyAccountingApp.Core.Vault;

namespace MyAccountingApp.Core.Persistence;

public static class EncryptedJsonFileStorage
{
    public static string ReadText(string filePath, IVaultService? vaultService)
    {
        string encPath = filePath + ".enc";

        if (vaultService != null && vaultService.IsInitialized)
        {
            if (vaultService.IsUnlocked)
            {
                if (File.Exists(filePath) && !File.Exists(encPath))
                {
                    string plainJson = File.ReadAllText(filePath);
                    byte[] encrypted = vaultService.Encrypt(Encoding.UTF8.GetBytes(plainJson));
                    File.WriteAllBytes(encPath, encrypted);
                    File.Copy(filePath, filePath + ".bak", overwrite: true);
                    File.Delete(filePath);
                }

                if (File.Exists(encPath))
                {
                    byte[] encryptedBytes = File.ReadAllBytes(encPath);
                    byte[] decryptedBytes = vaultService.Decrypt(encryptedBytes);
                    return Encoding.UTF8.GetString(decryptedBytes);
                }

                return string.Empty;
            }
            else
            {
                throw new InvalidOperationException("Vault is locked. Please unlock to access data.");
            }
        }

        if (File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }

        return string.Empty;
    }

    public static void WriteText(string filePath, string jsonContent, IVaultService? vaultService)
    {
        string encPath = filePath + ".enc";

        if (vaultService != null && vaultService.IsInitialized)
        {
            if (!vaultService.IsUnlocked)
            {
                throw new InvalidOperationException("Vault is locked. Please unlock to modify data.");
            }

            byte[] encrypted = vaultService.Encrypt(Encoding.UTF8.GetBytes(jsonContent));
            string tempEncPath = encPath + ".tmp";
            File.WriteAllBytes(tempEncPath, encrypted);
            File.Move(tempEncPath, encPath, overwrite: true);
            return;
        }

        string tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, jsonContent);
        File.Move(tempPath, filePath, overwrite: true);
    }
}

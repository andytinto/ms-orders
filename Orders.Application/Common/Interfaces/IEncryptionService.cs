namespace Orders.Application.Common.Interfaces
{
    public interface IEncryptionService
    {
        string Encrypt(string plainText, string key, string? iv = null);
        string Decrypt(string cipherText, string key, string? iv = null);
        string DecryptDeterministic(string cipherText, Guid key, string baseKey);
    }
}

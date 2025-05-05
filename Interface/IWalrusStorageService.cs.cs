namespace Prismon.Api.Interface;

public interface IBlobStorageService
{
    Task<string> StoreBlobAsync(string walletPublicKey, byte[] data, string fileName, StoreBlobOptions options, string transactionId);
    Task<HttpResponseMessage> RetrieveBlobAsync(string walletPublicKey, string blobId, string transactionId);
    Task<bool> CertifyBlobAvailabilityAsync(string blobId);
}

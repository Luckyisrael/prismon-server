using Microsoft.Extensions.Logging;
using Solnet.Rpc;
using Solnet.Rpc.Core.Http;
using Solnet.Rpc.Messages;
using Solnet.Rpc.Models;
using Solnet.Rpc.Types;
using Solnet.Wallet;
using Solnet.Wallet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Prismon.Api.Interface;

public class BlobStorageService : IBlobStorageService
{
    private readonly IRpcClient _rpcClient;
    private readonly HttpClient _httpClient;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly string _publisherUrl = "https://publisher.walrus-testnet.walrus.space/v1/blobs";
    private readonly string _aggregatorUrl = "https://aggregator.walrus-testnet.walrus.space";

    public BlobStorageService(IRpcClient rpcClient, HttpClient httpClient, ILogger<BlobStorageService> logger)
    {
        _rpcClient = rpcClient;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> StoreBlobAsync(string walletPublicKey, byte[] data, string fileName, StoreBlobOptions options, string transactionId)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrEmpty(walletPublicKey) || data.Length == 0 || string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(transactionId))
                throw new ArgumentException("Invalid input parameters");
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            // Verify Solana transaction
            await VerifyTransaction(walletPublicKey, transactionId, $"Prismon:store:{fileName}");

            // Build query parameters dynamically
            var queryParams = new List<string>();
            if (options.Epochs != 1) // Only include if non-default
                queryParams.Add($"epochs={options.Epochs}");
            if (!string.IsNullOrEmpty(options.SendObjectTo))
                queryParams.Add($"send_object_to={Uri.EscapeDataString(options.SendObjectTo)}");
            if (options.Deletable) // Only include if true
                queryParams.Add("deletable=true");
            var queryString = queryParams.Any() ? $"?{string.Join("&", queryParams)}" : "";
            var requestUrl = $"{_publisherUrl}{queryString}";

            _logger.LogDebug("Sending PUT request to {Url} with data length {DataLength}", requestUrl, data.Length);

            // Send PUT request to store blob
            var content = new ByteArrayContent(data);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            var response = await _httpClient.PutAsync(requestUrl, content);

            // Check response
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Walrus API returned {StatusCode}: {ResponseBody}", response.StatusCode, responseBody);
                throw new HttpRequestException($"Walrus API returned {response.StatusCode}: {responseBody}");
            }

            // Parse response
            var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(await response.Content.ReadAsStringAsync())
                ?? throw new Exception("Invalid response from Blob API");

            string blobId;
            if (result.TryGetValue("newlyCreated", out var newlyCreated))
            {
                var blobObject = newlyCreated.GetProperty("blobObject");
                blobId = blobObject.GetProperty("blobId").GetString()
                    ?? throw new Exception("Blob ID not found in response");
            }
            else if (result.TryGetValue("alreadyCertified", out var alreadyCertified))
            {
                blobId = alreadyCertified.GetProperty("blobId").GetString()
                    ?? throw new Exception("Blob ID not found in response");
            }
            else
            {
                throw new Exception("Unexpected response format from Blob API");
            }

            _logger.LogInformation("Stored blob {BlobId} for {Wallet}", blobId, walletPublicKey);
            return blobId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing blob for {Wallet}", walletPublicKey);
            throw;
        }
    }

   public async Task<HttpResponseMessage> RetrieveBlobAsync(string walletPublicKey, string blobId, string transactionId)
{
    try
    {
        // Validate inputs
        if (string.IsNullOrEmpty(walletPublicKey))
            throw new ArgumentException("Wallet public key cannot be null or empty", nameof(walletPublicKey));
        if (string.IsNullOrEmpty(blobId))
            throw new ArgumentException("Blob ID cannot be null or empty", nameof(blobId));
        if (string.IsNullOrEmpty(transactionId))
            throw new ArgumentException("Transaction ID cannot be null or empty", nameof(transactionId));

        // Verify Solana transaction (if required)
        //await VerifyTransaction(walletPublicKey, transactionId, $"Prismon:retrieve:{blobId}");

        // Construct the correct URL path (/v1/blobs/{blobId})
        var response = await _httpClient.GetAsync($"{_aggregatorUrl}/v1/blobs/{blobId}");
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Successfully retrieved blob {BlobId} for wallet {Wallet}", 
            blobId, walletPublicKey);
        return response;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving blob {BlobId} for wallet {Wallet}", blobId, walletPublicKey);
        throw;
    }
}

    public async Task<bool> CertifyBlobAvailabilityAsync(string blobId)
    {
        try
        {
            if (string.IsNullOrEmpty(blobId))
                throw new ArgumentException("Blob ID cannot be null or empty", nameof(blobId));

            // Send PUT request with empty data to check certification
            var response = await _httpClient.PutAsync($"{_publisherUrl}", new ByteArrayContent(Array.Empty<byte>()));
            response.EnsureSuccessStatusCode();

            // Parse response
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Blob certification API response: {Response}", responseContent);

            var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent);
            if (result == null)
                throw new Exception("Invalid response from Blob API: empty or invalid JSON");

            bool isAvailable = result.ContainsKey("alreadyCertified") &&
                result["alreadyCertified"].TryGetProperty("blobId", out var blobIdProp) &&
                blobIdProp.GetString() == blobId;

            _logger.LogInformation("Blob {BlobId} availability certification status: {IsAvailable}", blobId, isAvailable);
            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error certifying availability for blob {BlobId}", blobId);
            throw;
        }
    }

    private async Task VerifyTransaction(string walletPublicKey, string transactionId, string expectedMemo)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(walletPublicKey))
                throw new ArgumentException("Wallet public key cannot be null or empty", nameof(walletPublicKey));
            if (string.IsNullOrWhiteSpace(transactionId))
                throw new ArgumentException("Transaction ID cannot be null or empty", nameof(transactionId));
            if (string.IsNullOrWhiteSpace(expectedMemo))
                throw new ArgumentException("Expected memo cannot be null or empty", nameof(expectedMemo));

            _logger.LogDebug("Verifying transaction {TxId} for wallet {Wallet} with expected memo: {Memo}",
                transactionId, walletPublicKey, expectedMemo);

            // Validate public key format
            PublicKey pubKey;
            try
            {
                pubKey = new PublicKey(walletPublicKey);
                if (!pubKey.IsOnCurve())
                {
                    _logger.LogError("Public key {Wallet} is not a valid Solana key", walletPublicKey);
                    throw new ArgumentException("Invalid wallet public key format", nameof(walletPublicKey));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invalid wallet public key format: {Wallet}", walletPublicKey);
                throw new ArgumentException("Invalid wallet public key format", nameof(walletPublicKey), ex);
            }

            // Fetch transaction with retries
            //RequestResult<TransactionContentInfo>? 
            var txResult = await _rpcClient.GetTransactionAsync(transactionId, Commitment.Confirmed);
            const int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    //var txResult = await _rpcClient.GetTransactionAsync(transactionId, Commitment.Confirmed);
                    if (txResult != null && txResult.WasSuccessful && txResult.Result != null)
                        break;

                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning("Attempt {Attempt}/{MaxRetries}: Transaction {TxId} not found or unconfirmed. Retrying...",
                            attempt, maxRetries, transactionId);
                        await Task.Delay(1000 * attempt); // Exponential backoff
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Attempt {Attempt}/{MaxRetries}: Error fetching transaction {TxId}",
                        attempt, maxRetries, transactionId);
                    if (attempt == maxRetries)
                        throw new Exception($"Failed to fetch transaction {transactionId} after {maxRetries} attempts", ex);

                    await Task.Delay(1000 * attempt);
                }
            }

            if (txResult == null || !txResult.WasSuccessful || txResult.Result == null)
            {
                _logger.LogError("Transaction {TxId} not found or unconfirmed after {MaxRetries} retries",
                    transactionId, maxRetries);
                throw new Exception($"Transaction {transactionId} not found or unconfirmed");
            }

            var tx = txResult.Result;

            // Check transaction age (prevent replays)
            var blockTime = tx.BlockTime ?? 0;
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Transaction too old check (more than 5 minutes old)
            if (currentTime - blockTime > 300)
            {
                _logger.LogError("Transaction {TxId} is too old: BlockTime {BlockTime}, CurrentTime {CurrentTime}, Age {AgeSeconds}s",
                    transactionId, blockTime, currentTime, currentTime - blockTime);
                throw new Exception($"Transaction is too old (created {currentTime - blockTime} seconds ago)");
            }

            // Transaction from the future check (more than 60 seconds in the future)
            if (blockTime - currentTime > 60)
            {
                _logger.LogError("Transaction {TxId} has future timestamp: BlockTime {BlockTime}, CurrentTime {CurrentTime}, Difference {DiffSeconds}s",
                    transactionId, blockTime, currentTime, blockTime - currentTime);
                throw new Exception("Transaction has invalid future timestamp");
            }

            _logger.LogDebug("Transaction {TxId} age verification passed: BlockTime {BlockTime}, CurrentTime {CurrentTime}, Age {AgeSeconds}s",
                transactionId, blockTime, currentTime, currentTime - blockTime);

            // Verify signer
            bool isSigned = false;
            int numRequiredSignatures = tx.Transaction.Message.Header.NumRequiredSignatures;
            var accountKeys = tx.Transaction.Message.AccountKeys;

            if (accountKeys == null || accountKeys.Length == 0)
            {
                _logger.LogError("Transaction {TxId} has no account keys", transactionId);
                throw new Exception("Transaction has no account keys");
            }

            _logger.LogDebug("Transaction {TxId} has {Count} account keys, {RequiredSigs} required signatures",
                transactionId, accountKeys.Length, numRequiredSignatures);

            for (int i = 0; i < accountKeys.Length && i < numRequiredSignatures; i++)
            {
                var accountKey = accountKeys[i];
                if (string.IsNullOrEmpty(accountKey))
                {
                    _logger.LogWarning("Empty account key at index {Index} in transaction {TxId}", i, transactionId);
                    continue;
                }

                try
                {
                    var accountPubKey = new PublicKey(accountKey);
                    _logger.LogDebug("Checking signer at index {Index}: {AccountKey}", i, accountKey);

                    // Compare public keys correctly
                    if (accountPubKey.Equals(pubKey))
                    {
                        isSigned = true;
                        _logger.LogDebug("Found matching signer: {AccountKey} at index {Index}", accountKey, i);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Invalid account key format at index {Index}: {AccountKey}", i, accountKey);
                }
            }

            if (!isSigned)
            {
                var signers = accountKeys.Take(numRequiredSignatures).Where(k => !string.IsNullOrEmpty(k));
                _logger.LogError("Transaction {TxId} not signed by wallet {Wallet}. Signers: {Signers}",
                    transactionId, walletPublicKey, string.Join(", ", signers));
                throw new Exception($"Transaction not signed by provided wallet {walletPublicKey}");
            }

            // Verify memo instruction
            var memoProgramId = new PublicKey("MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr");
            bool foundMemo = false;

            if (tx.Transaction.Message.Instructions == null || tx.Transaction.Message.Instructions.Length == 0)
            {
                _logger.LogError("Transaction {TxId} has no instructions", transactionId);
                throw new Exception("Transaction has no instructions");
            }

            foreach (var instruction in tx.Transaction.Message.Instructions)
            {
                if (instruction == null || instruction.ProgramIdIndex >= accountKeys.Length)
                {
                    _logger.LogWarning("Invalid instruction in transaction {TxId}: null or program ID index out of bounds", transactionId);
                    continue;
                }

                try
                {
                    var programId = accountKeys[instruction.ProgramIdIndex];
                    var programPubKey = new PublicKey(programId);

                    // Check if this is a memo instruction
                    if (programPubKey.Equals(memoProgramId))
                    {
                        _logger.LogDebug("Found memo program instruction in transaction {TxId}", transactionId);

                        // The data in Solnet is already decoded from the wire format, but may need Base58 decoding
                        if (string.IsNullOrEmpty(instruction.Data))
                        {
                            _logger.LogWarning("Empty memo data in transaction {TxId}", transactionId);
                            continue;
                        }

                        string memoText;

                        try
                        {
                            // First try: Assume data is Base58-encoded bytes
                            byte[] decodedData = Encoders.Base58.DecodeData(instruction.Data);
                            memoText = Encoding.UTF8.GetString(decodedData);
                            _logger.LogDebug("Decoded memo as Base58: {MemoText}", memoText);
                        }
                        catch (Exception)
                        {
                            // Second try: Treat as plain text if Base58 decoding fails
                            memoText = instruction.Data;
                            _logger.LogDebug("Using memo data as plain text: {MemoText}", memoText);
                        }

                        if (memoText == expectedMemo)
                        {
                            foundMemo = true;
                            _logger.LogDebug("Matching memo found: {Memo}", memoText);
                            break;
                        }
                        else
                        {
                            _logger.LogWarning("Found memo but did not match. Expected: '{ExpectedMemo}', Found: '{ActualMemo}'",
                                expectedMemo, memoText);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing instruction in transaction {TxId}. RawData: {RawData}",
                        transactionId, instruction.Data);
                }
            }

            if (!foundMemo)
            {
                _logger.LogError("Expected memo '{ExpectedMemo}' not found in transaction {TxId}", expectedMemo, transactionId);
                throw new Exception($"Expected memo '{expectedMemo}' not found in transaction");
            }

            _logger.LogInformation("Successfully verified transaction {TxId} for wallet {Wallet} with memo {Memo}",
                transactionId, walletPublicKey, expectedMemo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction verification failed for wallet {Wallet}, tx {TxId}", walletPublicKey, transactionId);
            throw new Exception($"Transaction verification failed: {ex.Message}", ex);
        }
    }
}

public class StoreBlobOptions
{
    public uint Epochs { get; set; } = 1; // Default: 1 epoch
    public string? SendObjectTo { get; set; } // Optional Sui address
    public bool Deletable { get; set; } = false; // Default: false
}
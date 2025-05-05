using Prismon.Api.Interface;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Models;
using Solnet.Rpc.Types;
using Solnet.Wallet;
using Solnet.Wallet.Bip39;
using Solnet.Wallet.Utilities;
using Solnet.Metaplex.NFT;
using Solnet.Metaplex;
using Solnet.Metaplex.NFT.Library;
using System.Text.Json;
using Solnet.Raydium.Client;
using Solnet.Raydium.Types;
using Solnet.Pumpfun;

using Solnet.Ore;
using Solnet.Ore.Accounts;
using Solnet.Ore.Models;
using Solnet.Programs.Utilities;
using PDALookup = Solnet.Ore.PDALookup;
using Microsoft.Extensions.Options;
using Prismon.Api.Models;
using Solnet.Rpc.Core.Sockets;
using System.Text;
using Solnet.Rpc.Messages;


namespace Prismon.Api.Services;

public class SolanaService : ISolanaService
{
    private readonly IRpcClient _rpcClient;
    private readonly ILogger<SolanaService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SolanaConfig _config;
    
    //private readonly IStreamingRpcClient _streamingRpcClient;


    public SolanaService(/**IStreamingRpcClient streamingRpcClient,*/ IRpcClient rpcClient, IOptions<SolanaConfig> config, ILogger<SolanaService> logger)
    {
        _rpcClient = rpcClient ?? throw new ArgumentNullException(nameof(rpcClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = new HttpClient { BaseAddress = new Uri("https://quote-api.jup.ag/v6/") };
        //_streamingRpcClient = streamingRpcClient;
    }

    public async Task<ulong> GetBalanceAsync(string publicKey)
    {
        try
        {
            var balance = await _rpcClient.GetBalanceAsync(publicKey);
            if (!balance.WasSuccessful)
            {
                throw new Exception($"Failed to get balance: {balance.Reason}");
            }
            return balance.Result.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching balance for {PublicKey}", publicKey);
            throw;
        }
    }

    public async Task<List<TokenAccounts>> GetTokenAccountsAsync(string publicKey)
    {
        try
        {
            var accounts = await _rpcClient.GetTokenAccountsByOwnerAsync(publicKey);
            if (!accounts.WasSuccessful)
            {
                throw new Exception($"Failed to get token accounts: {accounts.Reason}");
            }

            return accounts.Result.Value.Select(a => new TokenAccounts
            {
                Mint = a.Account.Data.Parsed.Info.Mint,
                Amount = a.Account.Data.Parsed.Info.TokenAmount.AmountUlong
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching token accounts for {PublicKey}", publicKey);
            throw;
        }
    }

    public async Task<string> TransferAsync(string fromPublicKey, string toPublicKey, ulong amount, string signature)
    {
        try
        {
            var fromPubKey = new PublicKey(fromPublicKey);
            var toPubKey = new PublicKey(toPublicKey);
            var signatureBytes = Encoders.Base58.DecodeData(signature);

            // Build the transaction and get the message for verification
            var txBuilder = new TransactionBuilder()
                .AddInstruction(SystemProgram.Transfer(fromPubKey, toPubKey, amount));
            var message = txBuilder.CompileMessage(); // Call CompileMessage on TransactionBuilder

            if (!fromPubKey.Verify(message, signatureBytes))
            {
                throw new Exception("Invalid signature");
            }

            // Submit the pre-signed transaction
            var txHash = await _rpcClient.SendTransactionAsync(signatureBytes);
            if (!txHash.WasSuccessful)
            {
                throw new Exception($"Transfer failed: {txHash.Reason}");
            }

            _logger.LogInformation("Transfer successful: {Signature}", txHash.Result);
            return txHash.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring {Amount} from {From} to {To}", amount, fromPublicKey, toPublicKey);
            throw;
        }
    }
    public async Task<string> MintAsync(string authorityPublicKey, string mint, ulong amount, string signature)
    {
        try
        {
            var authority = new PublicKey(authorityPublicKey);
            var mintPubKey = new PublicKey(mint);
            var signatureBytes = Encoders.Base58.DecodeData(signature);

            // Verify signature for minting instruction
            var txBuilder = new TransactionBuilder()
                .AddInstruction(TokenProgram.MintTo(mintPubKey, authority, amount, authority));
            //.Build(new List<Account> { new Account(authority.KeyBytes, null) });
            var message = txBuilder.CompileMessage();

            if (!authority.Verify(message, signatureBytes))
            {
                throw new Exception("Invalid signature");
            }

            // Submit the pre-signed transaction
            var txHash = await _rpcClient.SendTransactionAsync(signatureBytes);
            if (!txHash.WasSuccessful)
            {
                throw new Exception($"Mint failed: {txHash.Reason}");
            }

            _logger.LogInformation("Mint successful: {Signature}", txHash.Result);
            return txHash.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error minting {Amount} of {Mint} by {Authority}", amount, mint, authorityPublicKey);
            throw;
        }
    }

    public async Task<object> GetTransactionAsync(string signature)
    {
        try
        {
            var tx = await _rpcClient.GetTransactionAsync(signature);
            if (!tx.WasSuccessful)
            {
                throw new Exception($"Failed to get transaction: {tx.Reason}");
            }
            return tx.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching transaction {Signature}", signature);
            throw;
        }
    }

    public async Task<WalletCreationResponse> CreateWalletAsync()
    {
        try
        {
            // Generate a new wallet with a 12-word mnemonic
            var wallet = new Wallet(WordCount.Twelve, WordList.English);
            var account = wallet.Account;

            var response = new WalletCreationResponse
            {
                PublicKey = account.PublicKey.Key,
                PrivateKey = Encoders.Base58.EncodeData(account.PrivateKey.KeyBytes),
                Mnemonic = string.Join(" ", wallet.Mnemonic.Words)
            };

            _logger.LogInformation("Generated new Solana wallet for dApp: {PublicKey}", response.PublicKey);
            return await Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Solana wallet for dApp");
            throw;
        }
    }

    /*
      // --- Metaplex NFT Creation ---
     public async Task<string> MintNFTAsync(string walletPublicKey, string name, string symbol, string uri, int sellerFeeBasisPoints, List<Creator> creators, string signature, bool isProgrammable = false, string? collectionMint = null)
     {
         try
         {
             var walletPubKey = new PublicKey(walletPublicKey);
             var signatureBytes = Encoders.Base58.DecodeData(signature);
             var metaplexClient = new MetadataClient(_rpcClient);
             var mintAccount = new Account(); // New keypair for NFT mint

             // Build metadata as per Metaplex example
             var metadata = new Metadata
             {
                 name = name,
                 symbol = symbol,
                 uri = uri,
                 sellerFeeBasisPoints = (uint)sellerFeeBasisPoints,
                 creators = creators,
                 collection = collectionMint != null ? new Collection(new PublicKey(collectionMint)) : null
             };

             // Since CreateNFT requires an Account with private key, but we're using a pre-signed signature,
             // we need to build the transaction manually and verify the signature
             var blockHash = await _rpcClient.GetLatestBlockHashAsync();
             if (!blockHash.WasSuccessful) throw new Exception("Failed to get recent block hash");

             var minBalanceForExemptionMint = await _rpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.MintAccountDataSize);
             if (!minBalanceForExemptionMint.WasSuccessful) throw new Exception("Failed to get rent exemption for mint");

             var txBuilder = new TransactionBuilder()
                 .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                 .SetFeePayer(walletPubKey)
                 .AddInstruction(SystemProgram.CreateAccount(
                     walletPubKey,
                     mintAccount.PublicKey,
                     minBalanceForExemptionMint.Result,
                     TokenProgram.MintAccountDataSize,
                     TokenProgram.ProgramIdKey))
                 .AddInstruction(TokenProgram.InitializeMint(
                     mintAccount.PublicKey,
                     0, // NFTs have 0 decimals
                     walletPubKey,
                     walletPubKey))
                 .AddInstruction(metaplexClient.CreateMetadataAccount(
                     mintAccount.PublicKey,
                     walletPubKey,
                     walletPubKey,
                     metadata,
                     isProgrammable ? TokenStandard.ProgrammableNonFungible : TokenStandard.NonFungible));

             var message = txBuilder.CompileMessage();
             if (!walletPubKey.Verify(message, signatureBytes))
                 throw new Exception("Invalid signature");

             var txHash = await _rpcClient.SendTransactionAsync(signatureBytes);
             if (!txHash.WasSuccessful)
                 throw new Exception($"NFT mint failed: {txHash.Reason}");

             _logger.LogInformation("NFT minted: {MintKey}, Signature: {Signature}", mintAccount.PublicKey, txHash.Result);
             return txHash.Result; // Return transaction hash
         }
         catch (Exception ex)
         {
             _logger.LogError(ex, "Error minting NFT for {Wallet}", walletPublicKey);
             throw;
         }
     }
     // --- Metaplex NFT Metadata Update ---
     public async Task<string> UpdateNFTMetadataAsync(string mintPublicKey, string name, string symbol, string uri, int sellerFeeBasisPoints, List<Creator> creators, string signature)
     {
         try
         {
             var mintPubKey = new PublicKey(mintPublicKey);
             var signatureBytes = Encoders.Base58.DecodeData(signature);
             var metaplexClient = new MetadataClient(_rpcClient);

             var blockHash = await _rpcClient.GetLatestBlockHashAsync();
             if (!blockHash.WasSuccessful) throw new Exception("Failed to get recent block hash");

             var metadata = new Metadata
             {
                 name = name,
                 symbol = symbol,
                 uri = uri,
                 sellerFeeBasisPoints = (uint)sellerFeeBasisPoints,
                 creators = creators
             };

             var txBuilder = new TransactionBuilder()
                 .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                 .SetFeePayer(mintPubKey) // Assuming mint owner pays fees
                 .AddInstruction(metaplexClient.UpdateMetadataAccount(
                     mintPubKey,
                     mintPubKey, // Authority is the original creator
                     metadata));

             var message = txBuilder.CompileMessage();
             if (!mintPubKey.Verify(message, signatureBytes))
                 throw new Exception("Invalid signature");

             var txHash = await _rpcClient.SendTransactionAsync(signatureBytes);
             if (!txHash.WasSuccessful)
                 throw new Exception($"Metadata update failed: {txHash.Reason}");

             _logger.LogInformation("NFT metadata updated: {MintKey}, Signature: {Signature}", mintPublicKey, txHash.Result);
             return txHash.Result;
         }
         catch (Exception ex)
         {
             _logger.LogError(ex, "Error updating NFT metadata for {Mint}", mintPublicKey);
             throw;
         }
     }
     // --- Metaplex Collection Creation ---
     public async Task<string> CreateCollectionAsync(string walletPublicKey, string name, string symbol, string uri, string signature)
     {
         try
         {
             var walletPubKey = new PublicKey(walletPublicKey);
             var signatureBytes = Encoders.Base58.DecodeData(signature);
             var metaplexClient = new MetadataClient(_rpcClient);
             var collectionMint = new Account();

             var blockHash = await _rpcClient.GetLatestBlockHashAsync();
             if (!blockHash.WasSuccessful) throw new Exception("Failed to get recent block hash");

             var minBalanceForExemptionMint = await _rpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.MintAccountDataSize);
             if (!minBalanceForExemptionMint.WasSuccessful) throw new Exception("Failed to get rent exemption for mint");

             var metadata = new Metadata
             {
                 name = name,
                 symbol = symbol,
                 uri = uri,
                 sellerFeeBasisPoints = 0,
                 creators = new List<Creator> { new Creator(walletPubKey, 100, true) }
             };

             var txBuilder = new TransactionBuilder()
                 .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                 .SetFeePayer(walletPubKey)
                 .AddInstruction(SystemProgram.CreateAccount(
                     walletPubKey,
                     collectionMint.PublicKey,
                     minBalanceForExemptionMint.Result,
                     TokenProgram.MintAccountDataSize,
                     TokenProgram.ProgramIdKey))
                 .AddInstruction(TokenProgram.InitializeMint(
                     collectionMint.PublicKey,
                     0,
                     walletPubKey,
                     walletPubKey))
                 .AddInstruction(metaplexClient.CreateMetadataAccount(
                     collectionMint.PublicKey,
                     walletPubKey,
                     walletPubKey,
                     metadata,
                     TokenStandard.NonFungible,
                     isCollection: true));

             var message = txBuilder.CompileMessage();
             if (!walletPubKey.Verify(message, signatureBytes))
                 throw new Exception("Invalid signature");

             var txHash = await _rpcClient.SendTransactionAsync(signatureBytes);
             if (!txHash.WasSuccessful)
                 throw new Exception($"Collection creation failed: {txHash.Reason}");

             _logger.LogInformation("Collection created: {MintKey}, Signature: {Signature}", collectionMint.PublicKey, txHash.Result);
             return txHash.Result;
         }
         catch (Exception ex)
         {
             _logger.LogError(ex, "Error creating collection for {Wallet}", walletPublicKey);
             throw;
         }
     }
    */

    // --- Jupiter Swap ---
    public async Task<string> SwapTokensAsync(string walletPublicKey, string fromTokenMint, string toTokenMint, ulong amount, string signature)
    {
        try
        {
            var walletPubKey = new PublicKey(walletPublicKey);
            var signatureBytes = Encoders.Base58.DecodeData(signature);

            // Get quote from Jupiter API
            var quoteResponse = await _httpClient.GetAsync($"quote?inputMint={fromTokenMint}&outputMint={toTokenMint}&amount={amount}&slippageBps=50");
            if (!quoteResponse.IsSuccessStatusCode)
                throw new Exception("Failed to fetch Jupiter quote");

            var quoteJson = await quoteResponse.Content.ReadAsStringAsync();
            var quote = JsonSerializer.Deserialize<JupiterQuote>(quoteJson);

            // Get serialized transaction from Jupiter
            var swapResponse = await _httpClient.PostAsJsonAsync("swap", new
            {
                quoteResponse = quote,
                userPublicKey = walletPublicKey,
                wrapAndUnwrapSol = true
            });
            if (!swapResponse.IsSuccessStatusCode)
                throw new Exception("Failed to fetch Jupiter swap transaction");

            var swapJson = await swapResponse.Content.ReadAsStringAsync();
            var swapData = JsonSerializer.Deserialize<JupiterSwapResponse>(swapJson);
            var txBytes = Convert.FromBase64String(swapData.swapTransaction);

            // Verify client-provided signature matches the swap transaction
            var tx = Transaction.Deserialize(txBytes);
            if (!walletPubKey.Verify(tx.CompileMessage(), signatureBytes))
                throw new Exception("Invalid signature");

            // Submit transaction
            var txHash = await _rpcClient.SendTransactionAsync(signatureBytes);
            if (!txHash.WasSuccessful)
                throw new Exception($"Swap failed: {txHash.Reason}");

            _logger.LogInformation("Swap completed: {FromMint} -> {ToMint}, Amount: {Amount}, Signature: {Signature}", fromTokenMint, toTokenMint, amount, txHash.Result);
            return txHash.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error swapping tokens for {Wallet}", walletPublicKey);
            throw;
        }
    }

    public async Task<string> CreateTokenAsync(string walletPublicKey, int decimals, ulong initialSupply, bool freezeAuthorityEnabled, string signature)
    {
        try
        {
            var walletPubKey = new PublicKey(walletPublicKey);
            var signatureBytes = Encoders.Base58.DecodeData(signature);
            var mintAccount = new Account();
            var initialAccount = new Account();

            if (decimals < 0 || decimals > 9)
                throw new ArgumentException("Decimals must be between 0 and 9");

            var blockHash = await _rpcClient.GetLatestBlockHashAsync();
            if (!blockHash.WasSuccessful) throw new Exception("Failed to get recent block hash");

            var minBalanceForExemptionMint = await _rpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.MintAccountDataSize);
            var minBalanceForExemptionAcc = await _rpcClient.GetMinimumBalanceForRentExemptionAsync(TokenProgram.TokenAccountDataSize);
            if (!minBalanceForExemptionMint.WasSuccessful || !minBalanceForExemptionAcc.WasSuccessful)
                throw new Exception("Failed to get rent exemptions");

            var txBuilder = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(walletPubKey)
                .AddInstruction(SystemProgram.CreateAccount(
                    walletPubKey,
                    mintAccount.PublicKey,
                    minBalanceForExemptionMint.Result,
                    TokenProgram.MintAccountDataSize,
                    TokenProgram.ProgramIdKey))
                .AddInstruction(TokenProgram.InitializeMint(
                    mintAccount.PublicKey,
                    decimals,
                    walletPubKey,
                    freezeAuthorityEnabled ? walletPubKey : null))
                .AddInstruction(SystemProgram.CreateAccount(
                    walletPubKey,
                    initialAccount.PublicKey,
                    minBalanceForExemptionAcc.Result,
                    TokenProgram.TokenAccountDataSize,
                    TokenProgram.ProgramIdKey))
                .AddInstruction(TokenProgram.InitializeAccount(
                    initialAccount.PublicKey,
                    mintAccount.PublicKey,
                    walletPubKey))
                .AddInstruction(TokenProgram.MintTo(
                    mintAccount.PublicKey,
                    initialAccount.PublicKey,
                    initialSupply,
                    walletPubKey));

            var message = txBuilder.CompileMessage();
            if (!walletPubKey.Verify(message, signatureBytes))
                throw new Exception("Invalid signature");

            var txHash = await _rpcClient.SendTransactionAsync(signatureBytes);
            if (!txHash.WasSuccessful)
                throw new Exception($"Token creation failed: {txHash.Reason}");

            _logger.LogInformation("Token created: {MintKey}, InitialSupply: {Supply}, Signature: {Signature}", mintAccount.PublicKey, initialSupply, txHash.Result);
            return mintAccount.PublicKey.Key; // Return mint address
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating token for {Wallet}", walletPublicKey);
            throw;
        }
    }

    // --- Raydium Swap ---
    public async Task<string> RaydiumSwapAsync(string walletPublicKey, string poolAddress, ulong amountInLamports, ulong minimumAmountOut, OrderSide orderSide, string signature)
    {
        try
        {
            var walletPubKey = new PublicKey(walletPublicKey);
            var signatureBytes = Encoders.Base58.DecodeData(signature);
            var raydiumClient = new RaydiumAmmClient(_rpcClient);

            var txResult = await raydiumClient.SendSwapAsync(
                new PublicKey(poolAddress),
                amountInLamports,
                minimumAmountOut,
                orderSide,
                new Account(walletPubKey.KeyBytes, null),
                new Account(walletPubKey.KeyBytes, null) // Trader as signer
            );

            if (!txResult.WasSuccessful)
                throw new Exception($"Raydium swap failed: {txResult.RawRpcResponse}");

            _logger.LogInformation("Raydium swap completed: {Pool}, Amount: {Amount}, Signature: {Signature}", poolAddress, amountInLamports, txResult.Result);
            return txResult.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Raydium swap for {Wallet}", walletPublicKey);
            throw;
        }
    }


    // --- Pump.fun Buy ---
    public async Task<string> PumpfunBuyAsync(string walletPublicKey, string tokenMint, decimal solAmount, int slippagePercent, string signature)
    {
        try
        {
            var walletPubKey = new PublicKey(walletPublicKey);
            var signatureBytes = Encoders.Base58.DecodeData(signature);
            var pumpfunClient = new PumpfunClient(_rpcClient, new Account(walletPubKey.KeyBytes, null));

            var txResult = await pumpfunClient.Buy(tokenMint, solAmount, slippagePercent);
            if (string.IsNullOrEmpty(txResult))
                throw new Exception("Pump.fun buy failed");

            _logger.LogInformation("Pump.fun buy completed: {Token}, SOL: {Amount}, Signature: {Signature}", tokenMint, solAmount, signature);
            return txResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Pump.fun buy for {Wallet}", walletPublicKey);
            throw;
        }
    }


    // --- Pump.fun Sell ---
    public async Task<string> PumpfunSellAsync(string walletPublicKey, string tokenMint, decimal amount, string signature)
    {
        try
        {
            var walletPubKey = new PublicKey(walletPublicKey);
            var signatureBytes = Encoders.Base58.DecodeData(signature);
            var pumpfunClient = new PumpfunClient(_rpcClient, new Account(walletPubKey.KeyBytes, null));

            var txResult = await pumpfunClient.Sell(tokenMint, amount);
            if (string.IsNullOrEmpty(txResult))
                throw new Exception("Pump.fun sell failed");

            _logger.LogInformation("Pump.fun sell completed: {Token}, Amount: {Amount}, Signature: {Signature}", tokenMint, amount, signature);
            return txResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Pump.fun sell for {Wallet}", walletPublicKey);
            throw;
        }
    }

    // --- Ore Open Proof ---
    public async Task<(string ProofAddress, ulong Balance)> OreOpenProofAsync(string walletPublicKey, string signature)
    {
        try
        {
            var walletPubKey = new PublicKey(walletPublicKey);
            var signatureBytes = Encoders.Base58.DecodeData(signature);
            var oreClient = new OreClient(_config.RpcUrl);
            var miner = new Account(walletPubKey.KeyBytes, null);

            await oreClient.OpenProof(miner, miner, miner);
            var proofRequest = await oreClient.GetProofAccountAsync(PDALookup.FindProofPDA(miner.PublicKey).address);
            if (proofRequest == null || proofRequest.ParsedResult == null)
                throw new Exception("Failed to open Ore proof");

            var proof = proofRequest.ParsedResult;
            _logger.LogInformation("Ore proof opened: {ProofAddress}, Balance: {Balance}", proofRequest.WasSuccessful.ToString(), proof.Balance);
            return (proofRequest.WasSuccessful.ToString(), proof.Balance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening Ore proof for {Wallet}", walletPublicKey);
            throw;
        }
    }

    // --- Ore Mine and Claim ---
    public async Task<string> OreMineAndClaimAsync(string walletPublicKey, byte[] digest, byte[] nonce, ulong amountToClaim, string signature)
    {
        try
        {
            var walletPubKey = new PublicKey(walletPublicKey);
            var signatureBytes = Encoders.Base58.DecodeData(signature);
            var oreClient = new OreClient(_config.RpcUrl);
            var miner = new Account(walletPubKey.KeyBytes, null);
            var tokenAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(walletPubKey, PDALookup.FindMintPDA());

            var solution = new Solution { Digest = digest, Nonce = nonce };
            await oreClient.MineOre(miner, solution);
            var txResult = await oreClient.ClaimOre(miner, tokenAccount, amountToClaim);

            _logger.LogInformation("Ore mined and claimed: {Amount}, Signature: {Signature}", amountToClaim, signature);
            return signature; // Return original signature as confirmation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mining/claiming Ore for {Wallet}", walletPublicKey);
            throw;
        }
    }

    /**
    public async Task<SubscriptionState> SubscribeToWalletTransactionsAsync(string walletPublicKey, Action<TransactionUpdate> callback)
    {
        try
        {
            var pubKey = new PublicKey(walletPublicKey);
            var subscriptionState = await _streamingRpcClient.SubscribeAccountInfoAsync(
                pubKey,
                async (accountInfo, context) =>
                {
                    try
                    {
                        // Fetch recent signatures for the wallet
                        var signatures = await _rpcClient.GetSignaturesForAddressAsync(pubKey, 1, commitment: Commitment.Confirmed);
                        if (signatures?.Result?.Count > 0)
                        {
                            var signature = signatures.Result[0].Signature;
                            var tx = await _rpcClient.GetTransactionAsync(signature, Commitment.Confirmed);
                            if (tx?.Result != null)
                            {
                                var update = ParseTransaction(tx.Result, walletPublicKey);
                                callback(update);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing account update for {Wallet}", walletPublicKey);
                    }
                },
                Commitment.Confirmed
            );

            _logger.LogInformation("Subscribed to account updates for Wallet {Wallet}, SubscriptionId: {Id}",
                walletPublicKey, subscriptionState);
            return subscriptionState;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to account updates for Wallet {Wallet}", walletPublicKey);
            throw;
        }
    }

    public void UnsubscribeFromWalletTransactions(SubscriptionState subscriptionState)
    {
        try
        {
            _streamingRpcClient.UnsubscribeAsync(subscriptionState).GetAwaiter().GetResult();
            _logger.LogInformation("Unsubscribed from transactions, SubscriptionId: {Id}", subscriptionState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing from {Id}", subscriptionState);
        }
    }

    private TransactionUpdate ParseTransaction(TransactionMetaSlotInfo tx, string walletPublicKey)
    {
        var update = new TransactionUpdate
        {
            Signature = tx.Transaction.Signatures.FirstOrDefault() ?? string.Empty,
            WalletPublicKey = walletPublicKey,
            Timestamp = tx.BlockTime.HasValue ?
                DateTimeOffset.FromUnixTimeSeconds(tx.BlockTime.Value).UtcDateTime :
                DateTime.UtcNow,
            Status = tx.Meta?.Error == null ? "Confirmed" : "Failed"
        };

        // Todo Add More uses for raydium and etc

        return update;
    }
    */

}

// DTOs for Jupiter API
public class JupiterQuote
{
    public string InputMint { get; set; }
    public string OutputMint { get; set; }
    public string InAmount { get; set; }
    public string OutAmount { get; set; }
}

public class JupiterSwapResponse
{
    public string swapTransaction { get; set; }
}
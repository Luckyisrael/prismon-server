using System.Collections.Generic;
using System.Threading.Tasks;
using Prismon.Api.Models;
using Solnet.Metaplex.NFT.Library;
using Solnet.Raydium.Types;
using Solnet.Rpc.Core.Sockets;

namespace Prismon.Api.Interface;

public interface ISolanaService
{
    Task<ulong> GetBalanceAsync(string publicKey);
    Task<List<TokenAccounts>> GetTokenAccountsAsync(string publicKey);
    Task<string> TransferAsync(string fromPublicKey, string toPublicKey, ulong amount, string signature);
    Task<string> MintAsync(string authorityPublicKey, string mint, ulong amount, string signature);
    Task<object> GetTransactionAsync(string signature);
    Task<WalletCreationResponse> CreateWalletAsync(); // Added for dApp wallet creation
 //  Task<string> MintNFTAsync(string walletPublicKey, string name, string symbol, string uri, int sellerFeeBasisPoints, List<Creator> creators, string signature, bool isProgrammable = false, string? collectionMint = null);
   // Task<string> UpdateNFTMetadataAsync(string mintPublicKey, string name, string symbol, string uri, int sellerFeeBasisPoints, List<Creator> creators, string signature);
  //  Task<string> CreateCollectionAsync(string walletPublicKey, string name, string symbol, string uri, string signature);
    Task<string> CreateTokenAsync(string walletPublicKey, int decimals, ulong initialSupply, bool freezeAuthorityEnabled, string signature);

    // Jupiter Swap Method
    Task<string> SwapTokensAsync(string walletPublicKey, string fromTokenMint, string toTokenMint, ulong amount, string signature);
    Task<string> RaydiumSwapAsync(string walletPublicKey, string poolAddress, ulong amountInLamports, ulong minimumAmountOut, OrderSide orderSide, string signature);
    Task<string> PumpfunBuyAsync(string walletPublicKey, string tokenMint, decimal solAmount, int slippagePercent, string signature);
    Task<string> PumpfunSellAsync(string walletPublicKey, string tokenMint, decimal amount, string signature);
    Task<(string ProofAddress, ulong Balance)> OreOpenProofAsync(string walletPublicKey, string signature);
    Task<string> OreMineAndClaimAsync(string walletPublicKey, byte[] digest, byte[] nonce, ulong amountToClaim, string signature);

    //Task<SubscriptionState> SubscribeToWalletTransactionsAsync(string walletPublicKey, Action<TransactionUpdate> callback);
    //void UnsubscribeFromWalletTransactions(SubscriptionState subscriptionState);
}


public class TokenAccounts
{
    public string Mint { get; set; } = string.Empty;
    public ulong Amount { get; set; }
}

public class WalletCreationResponse
{
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty; // Base58-encoded
    public string Mnemonic { get; set; } = string.Empty;   // 12-word phrase
}
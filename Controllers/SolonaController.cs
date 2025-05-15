using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prismon.Api.Data;
using Prismon.Api.Interface;
using Prismon.Api.Models;
using Prismon.Api.Services;
using Solnet.Metaplex.NFT.Library;
using Solnet.Raydium.Types;
using Solnet.Wallet;
using System.Security.Claims;
using System.Net.Http;
using AspNetCoreRateLimit;

namespace Prismon.Api.Controllers;

[ApiController]
[Route("devApi/[controller]")]
public class SolanaController : ControllerBase
{
    private readonly ISolanaService _solanaService;
    private readonly PrismonDbContext _dbContext;
    private readonly SolanaConfig _config;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<ISolanaService> _logger;
    private readonly ISoarService _soarService;

    private readonly IClientPolicyStore _clientPolicyStore;

    public SolanaController(ISoarService soarService, IClientPolicyStore clientPolicyStore, ISolanaService solanaService, PrismonDbContext dbContext, IOptions<SolanaConfig> config, IBlobStorageService blobStorageService, ILogger<ISolanaService> logger)
    {
        _blobStorageService = blobStorageService;
        _solanaService = solanaService;
        _dbContext = dbContext;
        _config = config.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clientPolicyStore = clientPolicyStore;
        _soarService = soarService;
    }

    private async Task<App?> GetAppFromApiKey()
    {
        var apiKey = Request.Headers["X-API-Key"].ToString(); // Convert StringValues to string
        return await _dbContext.Apps.FirstOrDefaultAsync(a => a.ApiKey == apiKey);

    }

    
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance([FromQuery] string walletPublicKey)
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized("Invalid API key");

        var balance = await _solanaService.GetBalanceAsync(walletPublicKey);
        return Ok(new { balance });
    }

    
    [HttpGet("token-accounts")]
    public async Task<IActionResult> GetTokenAccounts()
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized("Invalid API key");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id.ToString() == userId && u.AppId == app.Id);
        if (user?.WalletPublicKey == null) return BadRequest("No wallet connected");

        var accounts = await _solanaService.GetTokenAccountsAsync(user.WalletPublicKey);
        return Ok(accounts);
    }

    
    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request)
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized("Invalid API key");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id.ToString() == userId && u.AppId == app.Id);
        if (user?.WalletPublicKey == null) return BadRequest("No wallet connected");

        var signature = await _solanaService.TransferAsync(user.WalletPublicKey, request.ToPublicKey, request.Amount, request.Signature);
        return Ok(new { signature });
    }

    
    [HttpPost("mint")]
    public async Task<IActionResult> Mint([FromBody] MintRequest request)
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized("Invalid API key");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id.ToString() == userId && u.AppId == app.Id);
        if (user?.WalletPublicKey == null) return BadRequest("No wallet connected");

        var signature = await _solanaService.MintAsync(user.WalletPublicKey, request.Mint, request.Amount, request.Signature);
        return Ok(new { signature });
    }

    
    [HttpGet("transaction")]
    public async Task<IActionResult> GetTransaction([FromQuery] string signature)
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized("Invalid API key");

        var transaction = await _solanaService.GetTransactionAsync(signature);
        return Ok(transaction);
    }

    
    [HttpPost("create-wallet")]
    public async Task<IActionResult> CreateWallet()
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized("Invalid API key");

        var wallet = await _solanaService.CreateWalletAsync();
        return Ok(wallet);
    }

    /*
       // --- Metaplex Endpoints ---
    
    [HttpPost("nft/mint")]
    public async Task<IActionResult> MintNFT([FromBody] MintNFTRequest request)
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized("Invalid API key");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id.ToString() == userId && u.AppId == app.Id);
        if (user?.WalletPublicKey == null) return BadRequest("No wallet connected");

        var creators = request.Creators.Select(c => new Creator(new PublicKey(c.Address), c.Share, c.Verified)).ToList();
        var signature = await _solanaService.MintNFTAsync(user.WalletPublicKey, request.Name, request.Symbol, request.Uri, request.SellerFeeBasisPoints, creators, request.Signature, request.IsProgrammable, request.CollectionMint);
        return Ok(new { mint = signature });
    }

      
    [HttpPut("nft/update")]
    public async Task<IActionResult> UpdateNFTMetadata([FromBody] UpdateNFTRequest request)
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized("Invalid API key");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id.ToString() == userId && u.AppId == app.Id);
        if (user?.WalletPublicKey == null) return BadRequest("No wallet connected");

        var creators = request.Creators.Select(c => new Creator(new PublicKey(c.Address), c.Share, c.Verified)).ToList();
        var signature = await _solanaService.UpdateNFTMetadataAsync(request.MintPublicKey, request.Name, request.Symbol, request.Uri, request.SellerFeeBasisPoints, creators, request.Signature);
        return Ok(new { signature });
    }


    
    [HttpPost("nft/collection")]
    public async Task<IActionResult> CreateCollection([FromBody] CreateCollectionRequest request)
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized("Invalid API key");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id.ToString() == userId && u.AppId == app.Id);
        if (user?.WalletPublicKey == null) return BadRequest("No wallet connected");

        var signature = await _solanaService.CreateCollectionAsync(user.WalletPublicKey, request.Name, request.Symbol, request.Uri, request.Signature);
        return Ok(new { collectionMint = signature });
    }

    **/
    // --- Jupiter Swap Endpoint ---
    
    [HttpPost("swap")]
    public async Task<IActionResult> SwapTokens([FromBody] SwapRequest request)
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized("Invalid API key");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id.ToString() == userId && u.AppId == app.Id);
        if (user?.WalletPublicKey == null) return BadRequest("No wallet connected");

        var signature = await _solanaService.SwapTokensAsync(user.WalletPublicKey, request.FromTokenMint, request.ToTokenMint, request.Amount, request.Signature);
        return Ok(new { signature });
    }

    
    [HttpPost("token/create")]
    public async Task<IActionResult> CreateToken([FromBody] CreateTokenRequest request)
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized("Invalid API key");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id.ToString() == userId && u.AppId == app.Id);
        if (user?.WalletPublicKey == null) return BadRequest("No wallet connected");

        var mint = await _solanaService.CreateTokenAsync(
            user.WalletPublicKey,
            request.Decimals,
            request.InitialSupply,
            request.FreezeAuthorityEnabled,
            request.Signature
        );
        return Ok(new { mint });
    }

    
    [HttpPost("raydium/swap")]
    public async Task<IActionResult> RaydiumSwap([FromBody] RaydiumSwapRequest request)
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized("Invalid API key");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogError("No user ID in JWT");
            return Unauthorized("Invalid user session");
        }

        _logger.LogDebug("Looking up DAppUser for UserId {UserId}, AppId {AppId}", userId, app.Id);
        Guid userGuid;
        if (!Guid.TryParse(userId, out userGuid))
        {
            _logger.LogError("Invalid UserId format: {UserId}", userId);
            return BadRequest("Invalid user ID format");
        }

        var user = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id == userGuid && u.AppId == app.Id);
        if (user == null)
        {
            var userAnyApp = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id == userGuid);
            _logger.LogWarning(
                "No DAppUser found for UserId {UserId}, AppId {AppId}. User exists with different AppId: {Exists}, AppId: {OtherAppId}",
                userId, app.Id, userAnyApp != null, userAnyApp?.AppId
            );
            return BadRequest("User not found for this app. Please connect a wallet via /users/connect-wallet.");
        }

        if (user.WalletPublicKey == null)
        {
            _logger.LogWarning("No wallet connected for UserId {UserId}", userId);
            return BadRequest("No wallet connected");
        }

        var signature = await _solanaService.RaydiumSwapAsync(
            user.WalletPublicKey,
            request.PoolAddress,
            request.AmountInLamports,
            request.MinimumAmountOut,
            request.OrderSide,
            request.Signature
        );
        return Ok(new { signature, cluster = _config.Cluster });
    }

    
    [HttpPost("pumpfun/buy")]
    public async Task<IActionResult> PumpfunBuy([FromBody] PumpfunBuyRequest request)
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized("Invalid API key");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogError("No user ID in JWT");
            return Unauthorized("Invalid user session");
        }

        _logger.LogDebug("Looking up DAppUser for UserId {UserId}, AppId {AppId}", userId, app.Id);
        Guid userGuid;
        if (!Guid.TryParse(userId, out userGuid))
        {
            _logger.LogError("Invalid UserId format: {UserId}", userId);
            return BadRequest("Invalid user ID format");
        }

        var user = await _dbContext.DAppUsers
            .FirstOrDefaultAsync(u => u.Id == userGuid && u.AppId == app.Id);
        if (user == null)
        {
            // Debug: Check if user exists with different AppId
            var userAnyApp = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id == userGuid);
            _logger.LogWarning(
                "No DAppUser found for UserId {UserId}, AppId {AppId}. User exists with different AppId: {Exists}, AppId: {OtherAppId}",
                userId, app.Id, userAnyApp != null, userAnyApp?.AppId
            );
            return BadRequest("User not found for this app. Please connect a wallet via /users/connect-wallet.");
        }

        if (user.WalletPublicKey == null)
        {
            _logger.LogWarning("No wallet connected for UserId {UserId}, WalletPublicKey is null", userId);
            return BadRequest("No wallet connected. Please connect a wallet via /users/connect-wallet.");
        }

        var signature = await _solanaService.PumpfunBuyAsync(
            user.WalletPublicKey,
            request.TokenMint,
            request.SolAmount,
            request.SlippagePercent,
            request.Signature
        );
        return Ok(new { signature, cluster = _config.Cluster });
    }

    
    [HttpPost("pumpfun/sell")]
    public async Task<IActionResult> PumpfunSell([FromBody] PumpfunSellRequest request)
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized("Invalid API key");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogError("No user ID in JWT");
            return Unauthorized("Invalid user session");
        }

        _logger.LogDebug("Looking up DAppUser for UserId {UserId}, AppId {AppId}", userId, app.Id);
        Guid userGuid;
        if (!Guid.TryParse(userId, out userGuid))
        {
            _logger.LogError("Invalid UserId format: {UserId}", userId);
            return BadRequest("Invalid user ID format");
        }

        var user = await _dbContext.DAppUsers
            .FirstOrDefaultAsync(u => u.Id == userGuid && u.AppId == app.Id);
        if (user == null)
        {
            // Debug: Check if user exists with different AppId
            var userAnyApp = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id == userGuid);
            _logger.LogWarning(
                "No DAppUser found for UserId {UserId}, AppId {AppId}. User exists with different AppId: {Exists}, AppId: {OtherAppId}",
                userId, app.Id, userAnyApp != null, userAnyApp?.AppId
            );
            return BadRequest("User not found for this app. Please connect a wallet via /users/connect-wallet.");
        }

        if (user.WalletPublicKey == null)
        {
            _logger.LogWarning("No wallet connected for UserId {UserId}, WalletPublicKey is null", userId);
            return BadRequest("No wallet connected. Please connect a wallet via /users/connect-wallet.");
        }


        var signature = await _solanaService.PumpfunSellAsync(
            user.WalletPublicKey,
            request.TokenMint,
            request.Amount,
            request.Signature
        );
        return Ok(new { signature, cluster = _config.Cluster });
    }

    
    [HttpPost("ore/open-proof")]
    public async Task<IActionResult> OreOpenProof([FromBody] OreOpenProofRequest request)
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized("Invalid API key");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id.ToString() == userId && u.AppId == app.Id);
        if (user?.WalletPublicKey == null) return BadRequest("No wallet connected");

        var (proofAddress, balance) = await _solanaService.OreOpenProofAsync(user.WalletPublicKey, request.Signature);
        return Ok(new { proofAddress, balance, cluster = _config.Cluster });
    }

    
    [HttpPost("ore/mine-claim")]
    public async Task<IActionResult> OreMineAndClaim([FromBody] OreMineClaimRequest request)
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized("Invalid API key");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id.ToString() == userId && u.AppId == app.Id);
        if (user?.WalletPublicKey == null) return BadRequest("No wallet connected");

        var signature = await _solanaService.OreMineAndClaimAsync(
            user.WalletPublicKey,
            request.Digest,
            request.Nonce,
            request.AmountToClaim,
            request.Signature
        );
        return Ok(new { signature, cluster = _config.Cluster });
    }


    [HttpPost("blob/store")]
    public async Task<IActionResult> StoreBlob([FromBody] StoreBlobRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var app = await GetAppFromApiKey();
        if (app == null)
        {
            _logger.LogWarning("Invalid API key: {ApiKey}", Request.Headers["X-API-Key"]);
            return Unauthorized("Invalid API key");
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogError("No user ID in JWT");
            return Unauthorized("Invalid user session");
        }

        _logger.LogDebug("Looking up DAppUser for UserId {UserId}, AppId {AppId}", userId, app.Id);
        Guid userGuid;
        if (!Guid.TryParse(userId, out userGuid))
        {
            _logger.LogError("Invalid UserId format: {UserId}", userId);
            return BadRequest("Invalid user ID format");
        }

        var user = await _dbContext.DAppUsers
            .FirstOrDefaultAsync(u => u.Id == userGuid && u.AppId == app.Id);
        if (user == null)
        {
            // Debug: Check if user exists with different AppId
            var userAnyApp = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id == userGuid);
            _logger.LogWarning(
                "No DAppUser found for UserId {UserId}, AppId {AppId}. User exists with different AppId: {Exists}, AppId: {OtherAppId}",
                userId, app.Id, userAnyApp != null, userAnyApp?.AppId
            );
            return BadRequest("User not found for this app. Please connect a wallet via /users/connect-wallet.");
        }

        if (user.WalletPublicKey == null)
        {
            _logger.LogWarning("No wallet connected for UserId {UserId}, WalletPublicKey is null", userId);
            return BadRequest("No wallet connected. Please connect a wallet via /users/connect-wallet.");
        }

        try
        {
            if (request.Data == null || request.Data.Length == 0 || string.IsNullOrEmpty(request.FileName) || string.IsNullOrEmpty(request.TransactionId))
                return BadRequest("Invalid request parameters");

            // Decode base64 data
            byte[] data = Convert.FromBase64String(request.Data);

            // Store blob
            string blobId = await _blobStorageService.StoreBlobAsync(user.WalletPublicKey, data, request.FileName, request.Options, request.TransactionId);
            return Ok(new { BlobId = blobId, Cluster = "testnet" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }


    [HttpGet("blob/retrieve/{blobId}")]
    public async Task<IActionResult> RetrieveBlob(
    string blobId,
    [FromQuery] string transactionId,
    [FromQuery] string? contentDisposition = null,
    [FromQuery] string? contentType = null,
    [FromQuery] string? contentEncoding = null,
    [FromQuery] string? contentLanguage = null,
    [FromQuery] string? contentLocation = null)
    {
        if (string.IsNullOrEmpty(blobId) || string.IsNullOrEmpty(transactionId))
            return BadRequest("BlobId and TransactionId are required");

        var app = await GetAppFromApiKey();
        if (app == null)
        {
            _logger.LogWarning("Invalid API key: {ApiKey}", Request.Headers["X-API-Key"]);
            return Unauthorized("Invalid API key");
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogError("No user ID in JWT");
            return Unauthorized("Invalid user session");
        }

        _logger.LogDebug("Looking up DAppUser for UserId {UserId}, AppId {AppId}", userId, app.Id);
        if (!Guid.TryParse(userId, out var userGuid))
        {
            _logger.LogError("Invalid UserId format: {UserId}", userId);
            return BadRequest("Invalid user ID format");
        }

        var user = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id == userGuid && u.AppId == app.Id);
        if (user == null)
        {
            var userAnyApp = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id == userGuid);
            _logger.LogWarning(
                "No DAppUser found for UserId {UserId}, AppId {AppId}. User exists with different AppId: {Exists}, AppId: {OtherAppId}",
                userId, app.Id, userAnyApp != null, userAnyApp?.AppId
            );
            return BadRequest("User not found for this app. Please connect a wallet via /users/connect-wallet.");
        }

        if (user.WalletPublicKey == null)
        {
            _logger.LogWarning("No wallet connected for UserId {UserId}", userId);
            return BadRequest("No wallet connected");
        }

        try
        {
            // Create HttpClient with optional headers
            using var httpClient = new HttpClient();

            // Add Sui object ID headers if provided
            if (!string.IsNullOrEmpty(contentDisposition))
                httpClient.DefaultRequestHeaders.Add("content-disposition", contentDisposition);
            if (!string.IsNullOrEmpty(contentType))
                httpClient.DefaultRequestHeaders.Add("content-type", contentType);
            if (!string.IsNullOrEmpty(contentEncoding))
                httpClient.DefaultRequestHeaders.Add("content-encoding", contentEncoding);
            if (!string.IsNullOrEmpty(contentLanguage))
                httpClient.DefaultRequestHeaders.Add("content-language", contentLanguage);
            if (!string.IsNullOrEmpty(contentLocation))
                httpClient.DefaultRequestHeaders.Add("content-location", contentLocation);

            var response = await _blobStorageService.RetrieveBlobAsync(user.WalletPublicKey, blobId, transactionId);

            // Create a FileStreamResult to properly handle file downloads with headers
            var content = await response.Content.ReadAsByteArrayAsync();
            var result = new FileContentResult(content, response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");

            // Copy relevant headers from the original response
            foreach (var header in response.Content.Headers)
            {
                if (header.Key.ToLower() != "content-type") // We already set Content-Type
                {
                    result.FileDownloadName = header.Key.ToLower() == "content-disposition"
                        ? GetFileNameFromContentDisposition(header.Value.FirstOrDefault())
                        : null;

                    Response.Headers.Add(header.Key, header.Value.ToArray());
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving blob {BlobId} for user {UserId}", blobId, userId);
            return StatusCode(500, new { Error = "An error occurred while retrieving the blob" });
        }
    }

    private string? GetFileNameFromContentDisposition(string? contentDisposition)
    {
        if (string.IsNullOrEmpty(contentDisposition)) return null;

        var match = System.Text.RegularExpressions.Regex.Match(contentDisposition, @"filename\*?=['""]?(?:UTF-\d['""]*)?([^;]*?)['""]?;?");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        match = System.Text.RegularExpressions.Regex.Match(contentDisposition, @"filename=['""]?([^'""]*)['""]?");
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }


    [HttpGet("blob/certify/{blobId}")]
    public async Task<IActionResult> CertifyBlob(string blobId)
    {
        if (string.IsNullOrEmpty(blobId))
            return BadRequest("BlobId is required");

        try
        {
            var isAvailable = await _blobStorageService.CertifyBlobAvailabilityAsync(blobId);
            return Ok(new { isAvailable });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error certifying blob {BlobId}", blobId);
            return StatusCode(500, "Failed to certify blob");
        }
    }


    [HttpGet("debug/wallet")]
    public IActionResult DebugWallet()
    {
        var walletAddress = User.FindFirst("walletAddress")?.Value;
        return Ok(new { walletAddress = walletAddress });
    }

    [HttpPost("soar/player")]
    
    public async Task<IActionResult> InitializePlayer([FromBody] InitializePlayerRequest request, [FromQuery] string? programId = null)
    {
        try
        {
            var signature = await _soarService.InitializePlayerAsync(request.UserPublicKey, request.Username, request.NftMeta, programId);
            return Ok(new { TransactionSignature = signature });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize player for {UserPublicKey}", request.UserPublicKey);
            return StatusCode(500, "Error initializing player profile");
        }
    }

    [HttpPost("soar/leaderboard")]
    
    public async Task<IActionResult> CreateLeaderboard([FromBody] CreateLeaderboardRequest request, [FromQuery] string? programId = null)
    {
        try
        {
            var signature = await _soarService.CreateLeaderboardAsync(
                request.GamePublicKey,
                request.Description,
                request.NftMeta,
                request.ScoresToRetain,
                request.IsAscending,
                programId
            );
            return Ok(new { TransactionSignature = signature });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create leaderboard for {GamePublicKey}", request.GamePublicKey);
            return StatusCode(500, "Error creating leaderboard");
        }
    }

    [HttpPost("soar/score")]
    
    public async Task<IActionResult> SubmitScore([FromBody] SubmitScoreRequest request, [FromQuery] string? programId = null)
    {
        try
        {
            var signature = await _soarService.SubmitScoreAsync(
                request.PlayerPublicKey,
                request.GamePublicKey,
                request.LeaderboardPublicKey,
                request.Score,
                programId
            );
            return Ok(new { TransactionSignature = signature });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit score for {PlayerPublicKey}", request.PlayerPublicKey);
            return StatusCode(500, "Error submitting score");
        }
    }

    [HttpPost("soar/achievement")]
    
    public async Task<IActionResult> CreateAchievement([FromBody] CreateAchievementRequest request, [FromQuery] string? programId = null)
    {
        try
        {
            var signature = await _soarService.CreateAchievementAsync(
                request.GamePublicKey,
                request.Title,
                request.Description,
                request.NftMeta,
                programId
            );
            return Ok(new { TransactionSignature = signature });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create achievement for {GamePublicKey}", request.GamePublicKey);
            return StatusCode(500, "Error creating achievement");
        }
    }

    [HttpPost("soar/claim-achievement")]
    
    public async Task<IActionResult> ClaimAchievement([FromBody] ClaimAchievementRequest request, [FromQuery] string? programId = null)
    {
        try
        {
            var signature = await _soarService.ClaimAchievementAsync(
                request.PlayerPublicKey,
                request.GamePublicKey,
                request.AchievementPublicKey,
                programId
            );
            return Ok(new { TransactionSignature = signature });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to claim achievement for {PlayerPublicKey}", request.PlayerPublicKey);
            return StatusCode(500, "Error claiming achievement");
        }
    }
    [HttpPost("soar/claim-reward")]
    
    public async Task<IActionResult> ClaimReward([FromBody] ClaimRewardRequest request, [FromQuery] string? programId = null)
    {
        try
        {
            var signature = await _soarService.ClaimRewardAsync(
                request.PlayerPublicKey,
                request.GamePublicKey,
                request.LeaderboardPublicKey,
                programId
            );
            return Ok(new { TransactionSignature = signature });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to claim reward for {PlayerPublicKey}", request.PlayerPublicKey);
            return StatusCode(500, "Error claiming reward");
        }
    }

    [HttpGet("soar/player")]
    
    public async Task<IActionResult> GetPlayerProfile([FromQuery] string playerPublicKey, [FromQuery] string? programId = null)
    {
        try
        {
            var profile = await _soarService.GetPlayerProfileAsync(playerPublicKey, programId);
            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get player profile for {PlayerPublicKey}", playerPublicKey);
            return StatusCode(500, "Error retrieving player profile");
        }
    }

}

public class TransferRequest
{
    public string ToPublicKey { get; set; } = string.Empty;
    public ulong Amount { get; set; }
    public string Signature { get; set; } = string.Empty; // Client-signed
}

public class MintRequest
{
    public string Mint { get; set; } = string.Empty;
    public ulong Amount { get; set; }
    public string Signature { get; set; } = string.Empty; // Client-signed
}

// Request DTOs
public class MintNFTRequest
{
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public int SellerFeeBasisPoints { get; set; } // 500 = 5%
    public List<CreatorRequest> Creators { get; set; } = new();
    public string Signature { get; set; } = string.Empty;
    public bool IsProgrammable { get; set; }
    public string? CollectionMint { get; set; }
}

public class UpdateNFTRequest
{
    public string MintPublicKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public int SellerFeeBasisPoints { get; set; }
    public List<CreatorRequest> Creators { get; set; } = new();
    public string Signature { get; set; } = string.Empty;
}

public class CreateCollectionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

public class CreatorRequest
{
    public string Address { get; set; } = string.Empty;
    public byte Share { get; set; } // 0-100
    public bool Verified { get; set; }
}

public class SwapRequest
{
    public string FromTokenMint { get; set; } = string.Empty;
    public string ToTokenMint { get; set; } = string.Empty;
    public ulong Amount { get; set; }
    public string Signature { get; set; } = string.Empty;
}

public class CreateTokenRequest
{
    public int Decimals { get; set; } // 0-9
    public ulong InitialSupply { get; set; } // In smallest units
    public bool FreezeAuthorityEnabled { get; set; } // Retain or renounce freeze authority
    public string Signature { get; set; } = string.Empty; // Client-signed
}

// Request DTOs
public class RaydiumSwapRequest
{
    public string PoolAddress { get; set; } = string.Empty;
    public ulong AmountInLamports { get; set; }
    public ulong MinimumAmountOut { get; set; }
    public OrderSide OrderSide { get; set; }
    public string Signature { get; set; } = string.Empty;
}

public class PumpfunBuyRequest
{
    public string TokenMint { get; set; } = string.Empty;
    public decimal SolAmount { get; set; }
    public int SlippagePercent { get; set; }
    public string Signature { get; set; } = string.Empty;
}

public class PumpfunSellRequest
{
    public string TokenMint { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Signature { get; set; } = string.Empty;
}

public class OreOpenProofRequest
{
    public string Signature { get; set; } = string.Empty;
}

public class OreMineClaimRequest
{
    public byte[] Digest { get; set; } = Array.Empty<byte>();
    public byte[] Nonce { get; set; } = Array.Empty<byte>();
    public ulong AmountToClaim { get; set; }
    public string Signature { get; set; } = string.Empty;
}

public class StoreBlobRequest
{
    public string Data { get; set; } = string.Empty; // Base64-encoded data
    public string FileName { get; set; } = string.Empty;
    public StoreBlobOptions Options { get; set; } = new StoreBlobOptions();
    public string TransactionId { get; set; } = string.Empty;
}

// wallet one with 10 solana: FeLAPfbAuXzRzFFKHAwgjaMgaL32X3fEoKSBbQJbCcCs
//wallet 2 with empty solana: 59GoseeZAEfXyACESjp7Gfbf6ufVVHBwKi6G9id25jBf
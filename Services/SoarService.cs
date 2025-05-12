using Solana.Unity.Rpc;
using Solana.Unity.Soar.Program;
using Solana.Unity.Wallet;
using Solana.Unity.Programs;
using Solana.Unity.Rpc.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Prismon.Api.Models;
using Solana.Unity.Soar.Types;

namespace Prismon.Api.Services
{
   public interface ISoarService
    {
        Task<string> InitializePlayerAsync(string userPublicKey, string username, string nftMeta, string? programId = null);
        Task<string> CreateLeaderboardAsync(string gamePublicKey, string description, string nftMeta, int scoresToRetain, bool isAscending, string? programId = null);
        Task<string> SubmitScoreAsync(string playerPublicKey, string gamePublicKey, string leaderboardPublicKey, long score, string? programId = null);
        Task<string> CreateAchievementAsync(string gamePublicKey, string title, string description, string nftMeta, string? programId = null);
        Task<string> ClaimAchievementAsync(string playerPublicKey, string gamePublicKey, string achievementPublicKey, string? programId = null);
        Task<string> ClaimRewardAsync(string playerPublicKey, string gamePublicKey, string leaderboardPublicKey, string? programId = null);
        Task<PlayerProfileDto> GetPlayerProfileAsync(string playerPublicKey, string? programId = null);
    }

    public class SoarService : ISoarService
    {
        private readonly IRpcClient _rpcClient;
        private readonly ILogger<SoarService> _logger;
        private readonly PublicKey _soarProgramId;
        private readonly string _defaultProgramId;

        public SoarService(IConfiguration config, ILogger<SoarService> logger)
        {
            _rpcClient = ClientFactory.GetClient(config["Solana:RpcUrl"] ?? "https://api.mainnet-beta.solana.com");
            _soarProgramId = new PublicKey(config["Soar:ProgramId"] ?? "SoarNNzwQHMwcfdkdLc6kvbkoMSxcHy89gTHrjhJYkk");
            _logger = logger;
        }

        public async Task<string> InitializePlayerAsync(string userPublicKey, string username, string nftMeta, string? programId = null)
        {
            try
            {
                var payer = new PublicKey(userPublicKey);
                var userPda = SoarPda.PlayerPda(payer);
                var tx = new Transaction
                {
                    FeePayer = payer,
                    Instructions = new List<TransactionInstruction>(),
                    RecentBlockHash = (await _rpcClient.GetRecentBlockHashAsync()).Result.Value.Blockhash
                };

                var accounts = new InitializePlayerAccounts
                {
                    Payer = payer,
                    User = userPda,
                    SystemProgram = SystemProgram.ProgramIdKey
                };
                var initUserIx = SoarProgram.InitializePlayer(accounts, username, new PublicKey(nftMeta), _soarProgramId);
                tx.Add(initUserIx);

                var signature = await _rpcClient.SendTransactionAsync(tx.Serialize());
                _logger.LogInformation("Player profile initialized for {UserPublicKey}, Tx: {Signature}", userPublicKey, signature.Result);
                return signature.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize player for {UserPublicKey}", userPublicKey);
                throw;
            }
        }

        public async Task<string> CreateLeaderboardAsync(string gamePublicKey, string description, string nftMeta, int scoresToRetain, bool isAscending, string? programId = null)
        {
            try
            {
                var programIdKey = new PublicKey(programId ?? _defaultProgramId);
                var game = new PublicKey(gamePublicKey);
                var payer = new PublicKey(gamePublicKey);

                var gameAccountInfo = await _rpcClient.GetAccountInfoAsync(game);
                if (!gameAccountInfo.WasSuccessful || gameAccountInfo.Result?.Value == null)
                    throw new Exception("Game account not found");
                var gameAccount = SoarAccountDeserializer.DeserializeGameAccount(gameAccountInfo.Result.Value);

                var id = gameAccount.LeaderboardCount + 1;
                // Fix: Use manual LeaderboardPda derivation
                var leaderboard = SoarPdaExtensions.LeaderboardPda(game, id, programIdKey);
                // Fix: Use manual LeaderboardTopEntriesPda derivation
                var topEntries = SoarPdaExtensions.LeaderboardTopEntriesPda(leaderboard, programIdKey);

                var tx = new Transaction
                {
                    FeePayer = payer,
                    Instructions = new List<TransactionInstruction>(),
                    RecentBlockHash = (await _rpcClient.GetRecentBlockHashAsync()).Result.Value.Blockhash
                };

                var leaderboardMeta = new RegisterLeaderBoardInput
                {
                    Description = description,
                    NftMeta = new PublicKey(nftMeta),
                    ScoresToRetain = (byte)scoresToRetain,
                    //IsAscending = isAscending,
                    //AllowMultipleScores = false
                };

                var accounts = new AddLeaderboardAccounts
                {
                    Authority = payer,
                    Payer = payer,
                    Game = game,
                    Leaderboard = leaderboard,
                    TopEntries = topEntries,
                    SystemProgram = SystemProgram.ProgramIdKey
                };
                var addLeaderboardIx = SoarProgram.AddLeaderboard(accounts, leaderboardMeta, programIdKey);
                tx.Add(addLeaderboardIx);

                var signature = await _rpcClient.SendTransactionAsync(tx.Serialize());
                _logger.LogInformation("Leaderboard created for {GamePublicKey}, Tx: {Signature}", gamePublicKey, signature.Result);
                return signature.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create leaderboard for {GamePublicKey}", gamePublicKey);
                throw;
            }
        }

        public async Task<string> SubmitScoreAsync(string playerPublicKey, string gamePublicKey, string leaderboardPublicKey, long score, string? programId = null)
        {
            try
            {
                var programIdKey = new PublicKey(programId ?? _defaultProgramId);
                var player = new PublicKey(playerPublicKey);
                var game = new PublicKey(gamePublicKey);
                var leaderboard = new PublicKey(leaderboardPublicKey);
                var playerAccount = SoarPda.PlayerPda(player, programIdKey);
                var playerScores = SoarPda.PlayerScoresPda(playerAccount, leaderboard, programIdKey);
                // Fix: Use manual LeaderboardTopEntriesPda derivation
                var topEntries = SoarPdaExtensions.LeaderboardTopEntriesPda(leaderboard, programIdKey);

                var tx = new Transaction
                {
                    FeePayer = player,
                    Instructions = new List<TransactionInstruction>(),
                    RecentBlockHash = (await _rpcClient.GetRecentBlockHashAsync()).Result.Value.Blockhash
                };

                if (!await IsPdaInitialized(playerScores))
                {
                    var registerPlayerAccounts = new RegisterPlayerAccounts
                    {
                        Payer = player,
                        User = player,
                        PlayerAccount = playerAccount,
                        Game = game,
                        Leaderboard = leaderboard,
                        NewList = playerScores,
                        SystemProgram = SystemProgram.ProgramIdKey
                    };
                    var registerPlayerIx = SoarProgram.RegisterPlayer(registerPlayerAccounts, programIdKey);
                    tx.Add(registerPlayerIx);
                }

                var submitScoreAccounts = new SubmitScoreAccounts
                {
                    Authority = player,
                    Payer = player,
                    PlayerAccount = playerAccount,
                    Game = game,
                    Leaderboard = leaderboard,
                    PlayerScores = playerScores,
                    TopEntries = topEntries,
                    SystemProgram = SystemProgram.ProgramIdKey
                };
                var submitScoreIx = SoarProgram.SubmitScore(submitScoreAccounts, (ulong)score, programIdKey);
                tx.Add(submitScoreIx);

                var signature = await _rpcClient.SendTransactionAsync(tx.Serialize());
                _logger.LogInformation("Score submitted for {PlayerPublicKey}, Leaderboard: {LeaderboardPublicKey}, Tx: {Signature}", playerPublicKey, leaderboardPublicKey, signature.Result);
                return signature.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit score for {PlayerPublicKey}", playerPublicKey);
                throw;
            }
        }

        public async Task<string> CreateAchievementAsync(string gamePublicKey, string title, string description, string nftMeta, string? programId = null)
        {
            try
            {
                var programIdKey = new PublicKey(programId ?? _defaultProgramId); // Support optional programId
                var game = new PublicKey(gamePublicKey);
                var payer = new PublicKey(gamePublicKey);
                // Fix: Use manual AchievementPda derivation from SoarPdaExtensions
                var achievementPda = SoarPdaExtensions.AchievementPda(game, title, programIdKey);

                var tx = new Transaction
                {
                    FeePayer = payer,
                    Instructions = new List<TransactionInstruction>(),
                    RecentBlockHash = (await _rpcClient.GetRecentBlockHashAsync()).Result.Value.Blockhash
                };

                var accounts = new AddAchievementAccounts
                {
                    Authority = payer,
                    Payer = payer,
                    Game = game,
                    NewAchievement = achievementPda,
                    SystemProgram = SystemProgram.ProgramIdKey
                };
                var addAchievementIx = SoarProgram.AddAchievement(accounts, title, description, new PublicKey(nftMeta), programIdKey);
                tx.Add(addAchievementIx);

                var signature = await _rpcClient.SendTransactionAsync(tx.Serialize());
                _logger.LogInformation("Achievement created for {GamePublicKey}, Title: {Title}, Tx: {Signature}", gamePublicKey, title, signature.Result);
                return signature.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create achievement for {GamePublicKey}", gamePublicKey);
                throw;
            }
        }

        public async Task<string> ClaimAchievementAsync(string playerPublicKey, string gamePublicKey, string achievementPublicKey, string? programId = null)
        {
            try
            {
                var player = new PublicKey(playerPublicKey);
                var game = new PublicKey(gamePublicKey);
                var achievement = new PublicKey(achievementPublicKey);
                var playerAccount = SoarPda.PlayerPda(player);

                var tx = new Transaction
                {
                    FeePayer = player,
                    Instructions = new List<TransactionInstruction>(),
                    RecentBlockHash = (await _rpcClient.GetRecentBlockHashAsync()).Result.Value.Blockhash
                };

                var accounts = new UnlockPlayerAchievementAccounts
                {
                    Payer = player,
                    Authority = player,
                    PlayerAccount = playerAccount,
                    Game = game,
                    Achievement = achievement,
                    SystemProgram = SystemProgram.ProgramIdKey
                };
                var unlockAchievementIx = SoarProgram.UnlockPlayerAchievement(accounts, _soarProgramId);
                tx.Add(unlockAchievementIx);

                var signature = await _rpcClient.SendTransactionAsync(tx.Serialize());
                _logger.LogInformation("Achievement claimed for {PlayerPublicKey}, Achievement: {AchievementPublicKey}, Tx: {Signature}", playerPublicKey, achievementPublicKey, signature.Result);
                return signature.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to claim achievement for {PlayerPublicKey}", playerPublicKey);
                throw;
            }
        }

        public async Task<string> ClaimRewardAsync(string playerPublicKey, string gamePublicKey, string leaderboardPublicKey, string? programId = null)
        {
            try
            {
                var programIdKey = new PublicKey(programId ?? _defaultProgramId);
                var player = new PublicKey(playerPublicKey);
                var game = new PublicKey(gamePublicKey);
                var leaderboard = new PublicKey(leaderboardPublicKey);
                var playerAccount = SoarPda.PlayerPda(player, programIdKey);
                var playerScores = SoarPda.PlayerScoresPda(playerAccount, leaderboard, programIdKey);
                // Fix: Use manual LeaderboardTopEntriesPda derivation
                var topEntries = SoarPdaExtensions.LeaderboardTopEntriesPda(leaderboard, programIdKey);
                var vault = SoarPdaExtensions.VaultPda(game, programIdKey); // Note: VaultPda is not provided, see below

                var leaderboardInfo = await _rpcClient.GetAccountInfoAsync(leaderboard);
                if (!leaderboardInfo.WasSuccessful || leaderboardInfo.Result?.Value == null)
                    throw new Exception("Leaderboard account not found");
                var soar = SoarAccountDeserializer.DeserializeLeaderboardAccount(leaderboardInfo.Result.Value);

                var tx = new Transaction
                {
                    FeePayer = player,
                    Instructions = new List<TransactionInstruction>(),
                    RecentBlockHash = (await _rpcClient.GetRecentBlockHashAsync()).Result.Value.Blockhash
                };

                var accounts = new ClaimNftRewardAccounts
                {
                    Payer = player,
                    User = player,
                    //Receiver = player,
                    Game = game,
                    //Vault = vault,
                    //LeaderboardInfo = leaderboard,
                    //SoarGame = soar.Game,
                    //SoarLeaderboard = soar.LeaderboardField,
                    //SoarPlayerAccount = playerAccount,
                    //SoarPlayerScores = playerScores,
                    //SoarTopEntries = topEntries,
                    TokenProgram = programIdKey,
                    SystemProgram = SystemProgram.ProgramIdKey
                };

                var claimRewardIx = SoarProgram.ClaimNftReward(accounts, programIdKey);
                tx.Add(claimRewardIx);

                var signature = await _rpcClient.SendTransactionAsync(tx.Serialize());
                _logger.LogInformation("Reward claimed for {PlayerPublicKey}, Leaderboard: {LeaderboardPublicKey}, Tx: {Signature}", playerPublicKey, leaderboardPublicKey, signature.Result);
                return signature.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to claim reward for {PlayerPublicKey}", playerPublicKey);
                throw;
            }
        }

        public async Task<PlayerProfileDto> GetPlayerProfileAsync(string playerPublicKey, string? programId = null)
        {
            try
            {
                var playerPda = SoarPda.PlayerPda(new PublicKey(playerPublicKey));
                var accountInfo = await _rpcClient.GetAccountInfoAsync(playerPda);
                if (!accountInfo.WasSuccessful || accountInfo.Result?.Value == null)
                    throw new Exception("Player account not found");

                var parsedProfile = SoarAccountDeserializer.DeserializePlayerAccount(accountInfo.Result.Value);
                var profileDto = new PlayerProfileDto
                {
                    PublicKey = playerPublicKey,
                    Username = parsedProfile.Username,
                    NftMeta = parsedProfile.NftMeta?.ToString() ?? string.Empty,
                    Achievements = parsedProfile.Achievements?.Select(a => a.ToString()).ToList() ?? new List<string>(),
                    Scores = parsedProfile.Scores?.Select(s => new PlayerScoreDto
                    {
                        LeaderboardPublicKey = s.Leaderboard.ToString(),
                        Score = (long)s.Value
                    }).ToList() ?? new List<PlayerScoreDto>()
                };

                _logger.LogInformation("Player profile retrieved for {PlayerPublicKey}", playerPublicKey);
                return profileDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get player profile for {PlayerPublicKey}", playerPublicKey);
                throw;
            }
        }

        private async Task<bool> IsPdaInitialized(PublicKey pda)
        {
            var accountInfo = await _rpcClient.GetAccountInfoAsync(pda);
            return accountInfo.WasSuccessful && accountInfo.Result?.Value != null;
        }
    }
}
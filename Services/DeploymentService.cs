using Microsoft.EntityFrameworkCore;
using Prismon.Api.Data;
using Prismon.Api.Models;
using Solnet.KeyStore;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Models;
using Solnet.Wallet;

namespace Prismon.Api.Services;

public class DeploymentService : IDeploymentService
{
    private readonly PrismonDbContext _dbContext;
    private readonly ILogger<DeploymentService> _logger;
    private readonly IConfiguration _configuration;

    public DeploymentService(PrismonDbContext dbContext, ILogger<DeploymentService> logger, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _logger = logger;
        _configuration = configuration;
    }

    // Mock Deployment (unchanged)
    public async Task<DeploymentResponse> DeployDAppAsync(App app)
    {
        if (!string.IsNullOrEmpty(app.ProgramId))
        {
            return new DeploymentResponse
            {
                Succeeded = false,
                Message = "App is already deployed"
            };
        }

        try
        {
            var client = ClientFactory.GetClient(Cluster.DevNet);
            var programId = "MockProgram" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var endpoint = $"https://prismon-api-{app.Id}.mockendpoint.com";

            app.ProgramId = programId;
            app.DeployedEndpoint = endpoint;
            app.DeployedAt = DateTime.UtcNow;

            _dbContext.Apps.Update(app);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Deployed mock dApp for app {AppId}: {ProgramId}", app.Id, programId);

            return new DeploymentResponse
            {
                Succeeded = true,
                Message = "dApp deployed successfully (mock)",
                ProgramId = programId,
                DeployedEndpoint = endpoint
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy mock dApp for app {AppId}", app.Id);
            return new DeploymentResponse
            {
                Succeeded = false,
                Message = $"Deployment failed: {ex.Message}"
            };
        }
    }

//TOdo delete and handle real deployment
    public Task<DeploymentResponse> DeployDAppRealAsync(App app)
    {
        throw new NotImplementedException();
    }

    // Real Solana Deployment

    /**    public async Task<DeploymentResponse> DeployDAppRealAsync(App app)
        {
            if (!string.IsNullOrEmpty(app.ProgramId))
            {
                return new DeploymentResponse
                {
                    Succeeded = false,
                    Message = "App is already deployed"
                };
            }

            try
            {
                // Load Solana keypair from config (stored as JSON)
                var keyStoreJson = _configuration["Solana:KeyStoreJson"];
                if (string.IsNullOrEmpty(keyStoreJson))
                {
                    throw new InvalidOperationException("Solana keypair not configured");
                }

                var keyStore = new SolanaKeyStoreService();
                var wallet = keyStore.RestoreKeystoreFromFile(keyStoreJson); // Assumes JSON string or file path
                var account = new Account(wallet.PrivateKey);

                // Ensure account has SOL (airdrop if needed on devnet)
                var client = ClientFactory.GetClient(Cluster.DevNet);
                var balance = await client.GetBalanceAsync(account.PublicKey);
                if (balance.Result < 1_000_000_000) // 1 SOL in lamports
                {
                    var airdrop = await client.RequestAirdropAsync(account.PublicKey, 1_000_000_000);
                    if (!airdrop.WasRequestAirdropSuccessful())
                    {
                        throw new Exception("Airdrop failed");
                    }
                    await Task.Delay(5000); // Wait for airdrop confirmation
                }

                // Load compiled .so file (assumes it’s in a config path or embedded)
                var soFilePath = _configuration["Solana:ProgramSoPath"];
                if (string.IsNullOrEmpty(soFilePath) || !File.Exists(soFilePath))
                {
                    throw new InvalidOperationException("Solana program .so file not found");
                }
                var programBuffer = File.ReadAllBytes(soFilePath);

                // Deploy the program
                var programAccount = new Account();
                var tx = await ProgramDeployment.DeployAsync(client, programBuffer, account, programAccount);
                var signature = await client.SendTransactionAsync(tx);

                if (!signature.WasSuccessful)
                {
                    throw new Exception($"Deployment transaction failed: {signature.RawRpcResponse}");
                }

                // Mock endpoint (replace with real backend later)
                var endpoint = $"https://prismon-api-{app.Id}.mockendpoint.com";

                // Update app
                app.ProgramId = programAccount.PublicKey.ToString();
                app.DeployedEndpoint = endpoint;
                app.DeployedAt = DateTime.UtcNow;

                _dbContext.Apps.Update(app);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Deployed real dApp for app {AppId}: {ProgramId}", app.Id, app.ProgramId);

                return new DeploymentResponse
                {
                    Succeeded = true,
                    Message = "dApp deployed successfully (real)",
                    ProgramId = app.ProgramId,
                    DeployedEndpoint = endpoint
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deploy real dApp for app {AppId}", app.Id);
                return new DeploymentResponse
                {
                    Succeeded = false,
                    Message = $"Deployment failed: {ex.Message}"
                };
            }
        } **/
}
namespace Prismon.Api.Models
{
    public class SolanaConfig
    {
        public string Cluster { get; set; } = "Devnet";
        public string RpcUrl { get; set; } = "https://api.devnet.solana.com";
    }
}
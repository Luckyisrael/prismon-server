public enum SolanaCluster
{
    Devnet,
    Testnet,
    Mainnet
}

public class SolanaConfig
{
    public SolanaCluster Cluster { get; set; }
    public string RpcUrl { get; set; } = string.Empty;
    public string WebSocketUrl { get; set; } = string.Empty;
}
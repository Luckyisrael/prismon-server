using Solana.Unity.Wallet;

namespace Prismon.Api.Models
{
    public class GameAccount
    {
        public uint LeaderboardCount { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<PublicKey> Authorities { get; set; } = new List<PublicKey>();
    }

    public class PlayerAccount
    {
        public string Username { get; set; } = string.Empty;
        public PublicKey? NftMeta { get; set; }
        public List<PublicKey> Achievements { get; set; } = new List<PublicKey>();
        public List<PlayerScore> Scores { get; set; } = new List<PlayerScore>();
    }

    public class PlayerScore
    {
        public PublicKey Leaderboard { get; set; }
        public ulong Value { get; set; }
    }

    public class LeaderboardAccount
    {
        public PublicKey Game { get; set; }
        public PublicKey LeaderboardField { get; set; }
        public PublicKey TopEntries { get; set; }
        public string Description { get; set; } = string.Empty;
        public PublicKey? NftMeta { get; set; }
        public uint ScoresToRetain { get; set; }
        public bool IsAscending { get; set; }
    }
}
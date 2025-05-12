namespace Prismon.Api.Models
{
    public class PlayerProfileDto
    {
        public string PublicKey { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string NftMeta { get; set; } = string.Empty;
        public List<string> Achievements { get; set; } = new List<string>();
        public List<PlayerScoreDto> Scores { get; set; } = new List<PlayerScoreDto>();
    }

    public class PlayerScoreDto
    {
        public string LeaderboardPublicKey { get; set; } = string.Empty;
        public long Score { get; set; }
    }

    public class InitializePlayerRequest
    {
        public string UserPublicKey { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string NftMeta { get; set; } = string.Empty;
    }

    public class CreateLeaderboardRequest
    {
        public string GamePublicKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string NftMeta { get; set; } = string.Empty;
        public int ScoresToRetain { get; set; }
        public bool IsAscending { get; set; }
    }

    public class SubmitScoreRequest
    {
        public string PlayerPublicKey { get; set; } = string.Empty;
        public string GamePublicKey { get; set; } = string.Empty;
        public string LeaderboardPublicKey { get; set; } = string.Empty;
        public long Score { get; set; }
    }

    public class CreateAchievementRequest
    {
        public string GamePublicKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string NftMeta { get; set; } = string.Empty;
    }

    public class ClaimAchievementRequest
    {
        public string PlayerPublicKey { get; set; } = string.Empty;
        public string GamePublicKey { get; set; } = string.Empty;
        public string AchievementPublicKey { get; set; } = string.Empty;
    }

    public class ClaimRewardRequest
    {
        public string PlayerPublicKey { get; set; } = string.Empty;
        public string GamePublicKey { get; set; } = string.Empty;
        public string LeaderboardPublicKey { get; set; } = string.Empty;
    }
}
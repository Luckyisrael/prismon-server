using Solana.Unity.Wallet;
using System.Collections.Generic;
using System.Text;

namespace Prismon.Api.Services
{
    public static class SoarPdaExtensions
    {
        public static PublicKey LeaderboardPda(PublicKey game, long id, PublicKey programId)
        {
            var seeds = new List<byte[]>
            {
                Encoding.UTF8.GetBytes("leaderboard"),
                game.KeyBytes,
                BitConverter.GetBytes(id)
            };
            PublicKey.TryFindProgramAddress(seeds, programId, out PublicKey address, out byte _);
            return address;
        }

        public static PublicKey LeaderboardTopEntriesPda(PublicKey leaderboard, PublicKey programId)
        {
            var seeds = new List<byte[]>
            {
                Encoding.UTF8.GetBytes("top_entries"),
                leaderboard.KeyBytes
            };
            PublicKey.TryFindProgramAddress(seeds, programId, out PublicKey address, out byte _);
            return address;
        }

        public static PublicKey VaultPda(PublicKey game, PublicKey programId)
        {
            var seeds = new List<byte[]>
            {
                Encoding.UTF8.GetBytes("vault"),
                game.KeyBytes
            };
            PublicKey.TryFindProgramAddress(seeds, programId, out PublicKey address, out byte _);
            return address;
        }

        public static PublicKey AchievementPda(PublicKey game, string title, PublicKey programId)
        {
            var seeds = new List<byte[]>
            {
                Encoding.UTF8.GetBytes("achievement"),
                game.KeyBytes,
                Encoding.UTF8.GetBytes(title)
            };
            PublicKey.TryFindProgramAddress(seeds, programId, out PublicKey address, out byte _);
            return address;
        }

        public static PublicKey PlayerAchievementPda(PublicKey player, PublicKey achievement, PublicKey programId)
        {
            var seeds = new List<byte[]>
            {
                Encoding.UTF8.GetBytes("player_achievement"),
                player.KeyBytes,
                achievement.KeyBytes
            };
            PublicKey.TryFindProgramAddress(seeds, programId, out PublicKey address, out byte _);
            return address;
        }
    }
}
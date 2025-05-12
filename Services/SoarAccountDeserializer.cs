using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using System;
using System.Collections.Generic;
using System.Linq;
using Prismon.Api.Models;

namespace Prismon.Api.Services
{
    public static class SoarAccountDeserializer
    {
        public static GameAccount DeserializeGameAccount(AccountInfo accountInfo)
        {
            if (accountInfo?.Data == null || accountInfo.Data.Count == 0)
                throw new ArgumentException("Invalid game account data");

            var data = Convert.FromBase64String(accountInfo.Data[0]);
            var reader = new BorshReader(data);

            return new GameAccount
            {
                LeaderboardCount = reader.ReadUInt32(),
                Title = reader.ReadString(),
                Description = reader.ReadString(),
                Authorities = reader.ReadPublicKeyList()
            };
        }

        public static PlayerAccount DeserializePlayerAccount(AccountInfo accountInfo)
        {
            if (accountInfo?.Data == null || accountInfo.Data.Count == 0)
                throw new ArgumentException("Invalid player account data");

            var data = Convert.FromBase64String(accountInfo.Data[0]);
            var reader = new BorshReader(data);

            return new PlayerAccount
            {
                Username = reader.ReadString(),
                NftMeta = reader.ReadOptionalPublicKey(),
                Achievements = reader.ReadPublicKeyList(),
                Scores = reader.ReadList(() => new PlayerScore
                {
                    Leaderboard = new PublicKey(reader.ReadPublicKey()),
                    Value = reader.ReadUInt64()
                })
            };
        }

        public static LeaderboardAccount DeserializeLeaderboardAccount(AccountInfo accountInfo)
        {
            if (accountInfo?.Data == null || accountInfo.Data.Count == 0)
                throw new ArgumentException("Invalid leaderboard account data");

            var data = Convert.FromBase64String(accountInfo.Data[0]);
            var reader = new BorshReader(data);

            return new LeaderboardAccount
            {
                Game = new PublicKey(reader.ReadPublicKey()),
                LeaderboardField = new PublicKey(reader.ReadPublicKey()),
                TopEntries = new PublicKey(reader.ReadPublicKey()),
                Description = reader.ReadString(),
                NftMeta = reader.ReadOptionalPublicKey(),
                ScoresToRetain = reader.ReadUInt32(),
                IsAscending = reader.ReadBoolean()
            };
        }
    }

    internal class BorshReader
    {
        private readonly byte[] _data;
        private int _position;

        public BorshReader(byte[] data)
        {
            _data = data;
            _position = 0;
        }

        public uint ReadUInt32()
        {
            var value = BitConverter.ToUInt32(_data, _position);
            _position += 4;
            return value;
        }

        public ulong ReadUInt64()
        {
            var value = BitConverter.ToUInt64(_data, _position);
            _position += 8;
            return value;
        }

        public bool ReadBoolean()
        {
            var value = _data[_position] != 0;
            _position += 1;
            return value;
        }

        public string ReadString()
        {
            var length = ReadUInt32();
            var bytes = _data.Skip(_position).Take((int)length).ToArray();
            _position += (int)length;
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        public byte[] ReadPublicKey()
        {
            var bytes = _data.Skip(_position).Take(32).ToArray();
            _position += 32;
            return bytes;
        }

        public PublicKey? ReadOptionalPublicKey()
        {
            var hasValue = ReadBoolean();
            return hasValue ? new PublicKey(ReadPublicKey()) : null;
        }

        public List<PublicKey> ReadPublicKeyList()
        {
            var length = ReadUInt32();
            var list = new List<PublicKey>();
            for (var i = 0; i < length; i++)
                list.Add(new PublicKey(ReadPublicKey()));
            return list;
        }

        public List<T> ReadList<T>(Func<T> readItem)
        {
            var length = ReadUInt32();
            var list = new List<T>();
            for (var i = 0; i < length; i++)
                list.Add(readItem());
            return list;
        }
    }
}
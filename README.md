## Prismon: A Solana Developer Platform

### Link to our official SDK 
https://lucky-israel.gitbook.io/prismon-docs/getting-started/quickstart

### Link to our official website
https://prismondev.vercel.app

### Example Project fully powered by Prismon
https://prismon-sentiment-analysis.vercel.app/

Prismon is a powerful, developer-friendly platform for building Solana-based applications, with seamless integration of MagicBlock's SOAR (Solana On-Chain Achievements and Rankings), Solana integrations, AI integration, Pyth Network Integration, Decentralised storage with walrus protocol. Designed to empower developers in Nigeria and globally, Prismon provides real-time, scalable APIs for Solana blockchain interactions, including gamification features like leaderboards, achievements, player profiles, and automated rewards distribution. With affordable pricing via Paystack (NGN/USD), robust rate limiting, and low-latency execution powered by MagicBlock's Ephemeral Rollups, Prismon is the go-to solution for gaming, DeFi, and Web3 developers.

![License](https://img.shields.io/badge/License-MIT-blue.svg)
![Build Status](https://img.shields.io/badge/Build-Passing-green.svg)
![Twitter](https://img.shields.io/twitter/follow/iluckyisrael.svg?style=social)

## 🌟 Features

- **Wallet Authentication**: Sign up, Login a user with your solana wallet.
- **Solana Integration**: Query wallet balances, send transactions, and interact with Solana programs via our Typescript SDK
- **Decentralised Storage**: Interact with Walrus-protocol for storing data, enabling developers to store and retrieve data (blobs) on the blockchain.
- **Artificail Intelligence**: We provide a simple interface for interacting with AI models through the Prismon platform. This documentation covers the invokeAI and registerModel methods for executing AI inferences and managing AI model configurations
- **Pyth Network**: We provide and easy to use methods for getting or streaming live crypto prices.
- **User Tiers**:
  - **Free**: 10,000 API calls/month.
  - **Premium**: 100,000 API calls/month (₦45,000 or $30 via Paystack).
  - **Enterprise**: Custom quotas for high-volume users.
- **Rate Limiting**: Enforces quotas per user/app, with Redis caching for performance.
- **Payment Integration**: Paystack supports NGN/USD payments for tier upgrades, making Prismon accessible in Nigeria and globally.
- **API Usage Tracking**: Logs calls in SQLite, with automated cleanup of old records (ApiUsageCleanupService).
- **Scalability**: Redis caching and in-memory fallback ensure high throughput.

### MagicBlock SOAR Integration

Prismon leverages SOAR to bring gamification to Solana dApps, with low-latency execution via Ephemeral Rollups (<50ms).

- **Leaderboards**: Create and manage on-chain leaderboards to rank players by scores (e.g., game wins, DeFi trades).
- **Achievements**: Define and award achievements for user milestones (e.g., "Stake 100 SOL" or "Win 10 matches").
- **Player Profiles**: Initialize and query persistent profiles, unifying progress across games or dApps.
- **Rewards Distribution**: Automate NFT or token rewards for top players or achievement holders, with gas-efficient CPI (Cross-Program Invocation).
- **Real-Time APIs**: SOAR's CPI ensures <50ms latency for leaderboard updates and rewards, powered by MagicBlock's infrastructure.

### Use Cases

- **Gaming Leaderboards**: Nigerian esports studios can rank players in real-time (e.g., PvP racing game), with affordable NGN payments.
- **DeFi Achievements**: Global protocols award NFTs for staking or trading milestones, boosting user engagement.
- **Cross-Game Profiles**: Developers unify player data across Solana dApps, ensuring data ownership.
- **Automated Rewards**: Esports platforms distribute SOL or NFTs to top players, with on-chain transparency.

## 🛠️ Getting Started

### Prerequisites

- .NET 8 SDK
- Redis (optional, for caching)
- SQLite (included via EF Core)
- Paystack Account (for payment integration)
- Solana testnet/mainnet RPC endpoint (e.g., https://api.testnet.solana.com)
- MagicBlock SOAR 

## 📚 API Documentation

**Full Docs**: Swagger UI (available when running locally).

### https://prismon-api-b2aeetbkezhwdhg3.southafricanorth-01.azurewebsites.net/api/swagger/index.html

### Architecture

- **Framework**: .NET 8, ASP.NET Core.
- **Database**: SQLite (PrismonDbContext) for users, apps, and API usage.
- **Caching**: Redis with in-memory fallback for rate limiting.
- **Payments**: Paystack for tier upgrades (NGN/USD).
- **Solana SDKs**:
  - `Solana.Unity.Soar` for SOAR interactions.
  - `Solana.Unity.Programs`, `Solana.Unity.Rpc`, `Solana.Unity.Wallet` for Solana blockchain.
- **Rate Limiting**: Custom `RateLimitMiddleware` enforces user-tier quotas.
- **Cleanup**: `ApiUsageCleanupService` removes old usage records (>30 days).

### SOAR Integration

- **Features**:
  - Leaderboards: `AddLeaderboard`, `SubmitScore`.
  - Achievements: `AddAchievement`, `UnlockPlayerAchievement`.
  - Profiles: `InitializePlayer`, `GetPlayerProfile`.
  - Rewards: `ClaimNftReward` (fungible token support planned).
- **Low Latency**: SOAR's CPI leverages MagicBlock's Ephemeral Rollups for <50ms execution.
- **Deserialization**: Custom `SoarAccountDeserializer` parses on-chain game, player, and leaderboard accounts (Borsh).

### Scalability

- **Redis Caching**: Minimizes database load for rate limiting (`Prismon_User_*` keys).
- **Ephemeral Rollups**: Auto-scale SOAR operations, reducing Solana mainnet dependency.
- **Horizontal Scaling**: Deploy multiple API instances behind a load balancer.

## 🤝 Contributing

We welcome contributions! Follow these steps:

1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/your-feature`.
3. Commit changes: `git commit -m "Add your feature"`.
4. Push to the branch: `git push origin feature/your-feature`.
5. Open a pull request.

## 📜 License

This project is licensed under the [MIT License](LICENSE).

## 🙌 Acknowledgements

- **Pyth Network**: For providing crypto prices in real time
- **WalrusProtocol**: For providing decentralised storage.
- **MagicBlock**: For SOAR and Ephemeral Rollups, enabling real-time, composable Solana dApps.
- **Solana**: For a high-performance blockchain.
- **Paystack**: For seamless NGN/USD payments, empowering Nigerian developers.
- **Solana.Unity SDK**: For robust Solana integration.

## 📬 Contact

- **Twitter**: [@iluckyisrael](https://twitter.com/iluckyisrael)
- **Email**: luckyisrael4real@gmail.com
- **MagicBlock**: Reach out to [@magicblock](https://twitter.com/magicblock) for SOAR collaboration!

**Star this repo to support our mission, and let's build the future of Web3 together!** 🚀




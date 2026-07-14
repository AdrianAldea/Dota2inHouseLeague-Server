# Dota 2 In-House League Bot

A comprehensive Discord bot and management system for organizing and running Dota 2 in-house leagues. This project automates match creation, team balancing, player ranking, and match result tracking.

## 📋 Project Overview

This application provides a complete solution for managing competitive Dota 2 in-house leagues through Discord integration. It includes:

- **Discord Bot Integration**: Real-time match updates and player interactions via Discord
- **Match Management**: Automated lobby creation, player assignment, and result tracking
- **Player Ranking System**: Dynamic player ratings and skill-based matchmaking
- **Web Dashboard**: Angular-based frontend for viewing league statistics and player profiles
- **Desktop Player Client**: Cross-platform Avalonia app for players to sign in with Steam, queue, and track their stats
- **Backend API**: RESTful API built with .NET Aspire for scalability

## 🏗️ Architecture

The project is organized as a multi-project solution with the following components:

### Backend Services (.NET C#)

- **IHLDota2Bot**: Core backend API service
  - Player and match management
  - Dota 2 API integration
  - Database context and data access
  
- **IHLDota2Bot.Discord**: Discord bot service
  - Discord.Net integration
  - Real-time notifications
  - Player commands and interactions

- **IHLDota2Bot.Common**: Shared libraries
  - Common models and utilities
  - Shared configuration

- **IHLDota2Bot.ServiceDefaults**: Service configuration
  - Default service setup
  - Logging and observability configuration

- **IHLDota2Bot.AppHost**: .NET Aspire host
  - Service orchestration and deployment
  - Development environment setup

### Frontend

- **IHLDota2BotFrontEnd**: Angular web application
  - League statistics and leaderboards
  - Player profiles and match history
  - Real-time updates

### Desktop Client

- **IHLDota2Bot.Desktop**: Avalonia (MVVM) cross-platform player client
  - Steam sign-in (no Discord required) backed by a player JWT
  - Join/leave the queue, mark ready, and track personal stats
  - Leaderboard, match history, and profile views
  - See [`IHLDota2Bot.Desktop/PUBLISHING.md`](IHLDota2Bot.Desktop/PUBLISHING.md) for distribution

## 🛠️ Technology Stack

### Backend
- **.NET 10**: API and core services
- **Entity Framework Core 10**: Database ORM
- **SQL Server**: Primary database (run as a container by Aspire)
- **.NET Aspire**: Service orchestration
- **Discord.Net**: Discord integration
- **SteamKit2**: Steam / Dota 2 Game Coordinator integration

### Frontend
- **Angular 21+**: Web framework
- **TypeScript**: Language
- **Vitest**: Unit testing

### Desktop
- **Avalonia 12**: Cross-platform .NET UI framework
- **CommunityToolkit.Mvvm**: MVVM source generators
- **JWT (HMAC-SHA256)**: Player session tokens issued after Steam login

### APIs & Integrations
- **Steam API**: Player and match data
- **Steam OpenID**: Player sign-in for the desktop client
- **Discord API**: Bot interactions
- **Dota 2 API**: Game information and match data

## 🚀 Getting Started

### Prerequisites

- .NET 10 SDK
- Node.js 22+ and npm
- Docker (Aspire runs SQL Server as a container)
- Discord bot token
- Steam account credentials (bot + pool accounts)

### Setup Instructions

#### 1. Clone the Repository

```bash
git clone https://github.com/AdrianAldea/dota2ihnhouseleague.git
cd dota2ihnhouseleague
```

#### 2. Configure Secrets

All credentials are read from configuration but **must never be committed**. The
`appsettings.json` files ship with empty placeholders only — supply real values
via [.NET user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
(local dev) or environment variables (deployment). The database connection string
is provided automatically by the Aspire AppHost, so you don't set it by hand.

**Game Manager (`IHLDota2Bot`)**

```bash
cd IHLDota2Bot
dotnet user-secrets set "Steam:BotAccount:Username" "your_steam_bot_username"
dotnet user-secrets set "Steam:BotAccount:Password" "your_steam_bot_password"
# Shared secret that gates the admin API (must match the Discord side below).
dotnet user-secrets set "GameManager:AdminApiKey" "a_long_random_string"
```

**Discord bot (`IHLDota2Bot.Discord`)**

```bash
cd IHLDota2Bot.Discord
dotnet user-secrets set "Discord:Token" "your_discord_bot_token"
dotnet user-secrets set "Discord:ChannelId" "your_channel_id"
# Optional: register slash commands in one guild for instant availability
# (when 0/unset they are registered globally, which can take up to an hour).
# See IHLDota2Bot.Discord/SLASH_COMMANDS.md for the full guide.
dotnet user-secrets set "Discord:GuildId" "your_guild_id"
# Same value as GameManager:AdminApiKey so admin commands authenticate.
dotnet user-secrets set "SteamClient:ApiKey" "a_long_random_string"
```

**Player authentication (`IHLDota2Bot`)**

Players sign in through Steam from the desktop client; the backend issues a signed
JWT for the session. Set a stable signing key so tokens survive a server restart:

```bash
cd IHLDota2Bot
dotnet user-secrets set "PlayerJwt:SigningKey" "another_long_random_string"
```

The equivalent environment variables use `__` as the section separator, e.g.
`Discord__Token`, `GameManager__AdminApiKey`, `Steam__BotAccount__Password`,
`PlayerJwt__SigningKey`.

> If `GameManager:AdminApiKey` is left empty the admin endpoints are **unsecured**
> (a startup warning is logged). Always set it in any shared/production environment.

> If `PlayerJwt:SigningKey` is left empty an ephemeral per-process key is generated
> (a startup warning is logged), so dev works without secrets — but issued tokens
> stop being valid after a server restart. Always set it in shared/production
> environments, and serve the backend over **HTTPS** there.

Optional tuning (safe defaults, no secret): `League:PlayersPerLobby` controls how
many players fill a lobby (default `2` for testing; set to `10` for full 5v5).

#### 3. Backend Setup

The whole backend (Game Manager API, Discord bot, and a containerized SQL
Server) is orchestrated by the .NET Aspire AppHost. Running the AppHost starts
every service and wires up service discovery and the database automatically.

```bash
# Navigate to the Aspire host
cd IHLDota2Bot.AppHost

# Restore dependencies
dotnet restore

# Start the distributed application (services + SQL Server container)
dotnet run
```

The database schema is created and kept up to date automatically on startup via
EF Core migrations (databases originally created by the older `EnsureCreated`
path are adopted into migration control transparently). Service URLs and ports
are assigned by Aspire; open the **Aspire dashboard** printed in the console to
see each service's endpoint.

#### 4. Frontend Setup

```bash
cd IHLDota2BotFrontEnd

# Install dependencies
npm install

# Start development server
npm start
```

The frontend will be available at `http://localhost:4200`

#### 5. Desktop Client (optional)

The Avalonia player client is registered with the AppHost but uses **explicit start**
— it appears in the Aspire dashboard and only launches when you click **Start**, so a
GUI window doesn't pop up on every `dotnet run`. When started this way it picks up the
Game Manager's address automatically via the `IHL_API_BASE` environment variable.

To run it standalone, point it at the backend and run the project:

```bash
cd IHLDota2Bot.Desktop

# Optional: override the backend URL (defaults to appsettings.json → ApiBaseUrl)
export IHL_API_BASE="http://localhost:5193"

dotnet run
```

Players sign in with Steam; new Steam users are prompted for a display name to
register a league account. To build a distributable executable, see
[`IHLDota2Bot.Desktop/PUBLISHING.md`](IHLDota2Bot.Desktop/PUBLISHING.md).

## 📦 Project Structure

```
dota2ihnhouseleague/
├── IHLDota2Bot.AppHost/          # .NET Aspire host configuration
├── IHLDota2Bot.Common/           # Shared models, DTOs, and utilities
├── IHLDota2Bot.Desktop/          # Avalonia desktop player client
├── IHLDota2Bot.Discord/          # Discord bot service
├── IHLDota2Bot.ServiceDefaults/  # Service configuration defaults
├── IHLDota2Bot/                  # Main API service (Game Manager)
├── IHLDota2BotFrontEnd/          # Angular web application
├── *.Tests / *.IntegrationTests/ # xUnit test projects
├── IHLDota2Bot.slnx              # Solution file
├── ProjectDiagram.drawio         # Architecture diagram
├── REFACTORING_PLAN.md           # Phased improvement roadmap
└── aspire.config.json            # Aspire configuration
```

## 🔑 Key Features

### Player Management
- Steam account linking
- Player ranking and rating system
- Match history and statistics
- Player profiles with performance metrics

### Match Management
- Automated lobby creation
- Fair team balancing using MMR
- Real-time match status updates
- Result submission and validation

### Discord Integration
- In-game notifications
- Match queue commands
- Player statistics commands
- Real-time match updates in designated channels
- Every command available both as `!`-prefixed text and as a Discord slash
  command (`/queue`, `/help`, …) with built-in argument validation — see the
  [slash commands guide](IHLDota2Bot.Discord/SLASH_COMMANDS.md) for examples,
  setup, and how it works

### Web Dashboard
- Leaderboards and rankings
- Player statistics and profiles
- Match history and replays
- League statistics and trends

### Desktop Player Client
- Steam sign-in with a session JWT (no Discord account required)
- Self-service registration for new Steam users
- Queue, ready-up, and personal stats from the desktop
- Leaderboard, match history, and profile views
- Cross-platform builds (Windows, macOS, Linux) via Avalonia

## 🧪 Testing

### Backend Tests

```bash
dotnet test
```

### Frontend Tests

Unit tests run on [Vitest](https://vitest.dev/) via the Angular build system
(jsdom environment):

```bash
cd IHLDota2BotFrontEnd
npm test
```

### Frontend Linting

```bash
cd IHLDota2BotFrontEnd
npm run lint
```

## 📝 API Documentation

In the Development environment, Swagger/OpenAPI documentation is exposed by each
backend service at its `/swagger` endpoint. The exact host and port are assigned
by Aspire — find each service's URL in the Aspire dashboard and append
`/swagger`. This provides interactive documentation for all API endpoints.

## 🐳 Docker Support

The Game Manager API (`IHLDota2Bot/Dockerfile`) and Discord bot
(`IHLDota2Bot.Discord/Dockerfile`) each ship a multi-stage, Linux-based
.NET 10 Dockerfile. Build an image from the repository root, for example:

```bash
docker build -f IHLDota2Bot/Dockerfile -t ihl-gamemanager .
docker build -f IHLDota2Bot.Discord/Dockerfile -t ihl-discord .
```

For local development, prefer running the Aspire AppHost (see Backend Setup),
which orchestrates the services and their dependencies for you. Producing
deployment manifests is handled by Aspire's publishing tooling rather than a
hand-maintained `docker-compose.yml`. See `.dockerignore` for excluded files.

## 🔐 Security Considerations

- Sensitive credentials should be stored in environment variables
- Use appropriate database authentication and encryption
- Discord bot token should be kept secure and not committed to version control
- Steam API key should be restricted to authorized IP ranges
- The API has three trust tiers: public reads (leaderboard, queue status,
  stats), player actions (JWT `me`-endpoints — identity always comes from the
  token), and service routes (every id-based mutation + `/admin/*`), which
  require the `X-Api-Key` shared by the Discord bot and admin panel. Players'
  desktop clients never hold that key.
- Player-facing endpoints support opt-in per-IP rate limiting — set
  `PlayerRateLimit:PermitLimit` (e.g. `60`) in production.
- See `PRODUCTION_PLAN.md` for the full production hardening checklist.

## 🤝 Contributing

Contributions are welcome! Please ensure:

1. Code follows C# and Angular style guidelines
2. New features include appropriate tests
3. Commit messages are descriptive
4. Pull requests reference related issues

## 📄 License

This project is currently not licensed. Please contact the repository owner for licensing information.

## 📞 Support & Contact

For issues, feature requests, or questions:
- Open an issue on the GitHub repository
- Contact the maintainer: [AdrianAldea](https://github.com/AdrianAldea)

## 🗺️ Roadmap

- [ ] Enhanced matchmaking algorithm
- [ ] Tournament mode support
- [ ] Advanced statistics and analytics
- [ ] Mobile application
- [ ] Multi-region support
- [ ] Custom league configurations

---

**Last Updated**: June 2026

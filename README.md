# Dota 2 In-House League Bot

A comprehensive Discord bot and management system for organizing and running Dota 2 in-house leagues. This project automates match creation, team balancing, player ranking, and match result tracking.

## 📋 Project Overview

This application provides a complete solution for managing competitive Dota 2 in-house leagues through Discord integration. It includes:

- **Discord Bot Integration**: Real-time match updates and player interactions via Discord
- **Match Management**: Automated lobby creation, player assignment, and result tracking
- **Player Ranking System**: Dynamic player ratings and skill-based matchmaking
- **Web Dashboard**: Angular-based frontend for viewing league statistics and player profiles
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

### APIs & Integrations
- **Steam API**: Player and match data
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

#### 2. Configure Environment Variables

Create or update configuration files with required credentials:

```json
{
  "Steam": {
    "ApiKey": "your_steam_api_key"
  },
  "Discord": {
    "BotToken": "your_discord_bot_token",
    "ServiceName": "discord-service"
  },
  "ConnectionStrings": {
    "Dota2inHouseLeagueDB": "Server=your_server;Database=Dota2IHL;Trusted_Connection=true;"
  }
}
```

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

The database schema is created automatically on startup (currently via EF Core
`EnsureCreated` — migrating to versioned EF Core migrations is tracked in
`REFACTORING_PLAN.md`). Service URLs and ports are assigned by Aspire; open the
**Aspire dashboard** printed in the console to see each service's endpoint.

#### 4. Frontend Setup

```bash
cd IHLDota2BotFrontEnd

# Install dependencies
npm install

# Start development server
npm start
```

The frontend will be available at `http://localhost:4200`

## 📦 Project Structure

```
dota2ihnhouseleague/
├── IHLDota2Bot.AppHost/          # .NET Aspire host configuration
├── IHLDota2Bot.Common/           # Shared models and utilities
├── IHLDota2Bot.Discord/          # Discord bot service
├── IHLDota2Bot.ServiceDefaults/  # Service configuration defaults
├── IHLDota2Bot/                  # Main API service
├── IHLDota2BotFrontEnd/          # Angular web application
├── IHLDota2Bot.slnx              # Solution file
├── ProjectDiagram.drawio         # Architecture diagram
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

### Web Dashboard
- Leaderboards and rankings
- Player statistics and profiles
- Match history and replays
- League statistics and trends

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

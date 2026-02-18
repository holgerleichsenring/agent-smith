# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files for restore caching
COPY AgentSmith.sln ./
COPY src/AgentSmith.Domain/AgentSmith.Domain.csproj src/AgentSmith.Domain/
COPY src/AgentSmith.Contracts/AgentSmith.Contracts.csproj src/AgentSmith.Contracts/
COPY src/AgentSmith.Application/AgentSmith.Application.csproj src/AgentSmith.Application/
COPY src/AgentSmith.Infrastructure/AgentSmith.Infrastructure.csproj src/AgentSmith.Infrastructure/
COPY src/AgentSmith.Host/AgentSmith.Host.csproj src/AgentSmith.Host/
COPY tests/AgentSmith.Tests/AgentSmith.Tests.csproj tests/AgentSmith.Tests/

RUN dotnet restore

# Copy everything and publish
COPY . .
RUN dotnet publish src/AgentSmith.Host -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Install git for LibGit2Sharp operations
RUN apt-get update && apt-get install -y --no-install-recommends git && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
COPY config/ ./config/

ENTRYPOINT ["dotnet", "AgentSmith.Host.dll"]

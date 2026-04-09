# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY . .
RUN dotnet restore
# Minimal change: Pointing directly to the project file to avoid solution-level warnings
RUN dotnet publish PortfolioManager.Api.csproj -c Release -o out

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Minimal change: Suppress interactive prompts and install necessary Linux dependencies
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y --no-install-recommends \
    libicu-dev \
    tzdata \
    && rm -rf /var/lib/apt/lists/*

# Fix for globalization in .NET 9
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app/out .

# Minimal change: Explicitly ensuring the Data folder exists in the root for CSV parsing
COPY --from=build /app/Data ./Data 

ENTRYPOINT ["dotnet", "PortfolioManager.Api.dll"]
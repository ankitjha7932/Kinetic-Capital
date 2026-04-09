# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Fix Status 139: Install ICU (Globalization) and tzdata (Timezones)
RUN apt-get update && apt-get install -y \
    libicu-dev \
    tzdata \
    && rm -rf /var/lib/apt/lists/*

# Fix for globalization in .NET 9
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app/out .

# Ensure Data folder is copied for your CSV FaceValue logic
# If your Data folder is inside the project folder:
# COPY --from=build /app/PortfolioManager.Api/Data ./Data

ENTRYPOINT ["dotnet", "PortfolioManager.Api.dll"]
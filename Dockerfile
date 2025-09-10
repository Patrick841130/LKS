# LKS COIN Mainnet Explorer - Production Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Create logs directory
RUN mkdir -p /app/logs

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/LksBrothers.Explorer/LksBrothers.Explorer.csproj", "src/LksBrothers.Explorer/"]
COPY ["src/LksBrothers.Core/LksBrothers.Core.csproj", "src/LksBrothers.Core/"]
COPY ["src/LksBrothers.StateManagement/LksBrothers.StateManagement.csproj", "src/LksBrothers.StateManagement/"]
COPY ["src/LksBrothers.Consensus/LksBrothers.Consensus.csproj", "src/LksBrothers.Consensus/"]
COPY ["src/LksBrothers.Dex/LksBrothers.Dex.csproj", "src/LksBrothers.Dex/"]

# Restore dependencies
RUN dotnet restore "src/LksBrothers.Explorer/LksBrothers.Explorer.csproj"

# Copy source code
COPY . .

# Build application
WORKDIR "/src/src/LksBrothers.Explorer"
RUN dotnet build "LksBrothers.Explorer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LksBrothers.Explorer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Copy static files
COPY demo-explorer.html ./wwwroot/

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "LksBrothers.Explorer.dll"]

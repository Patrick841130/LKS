FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8001
EXPOSE 9001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/LksBrothers.Firedancer/LksBrothers.Firedancer.csproj", "src/LksBrothers.Firedancer/"]
COPY ["src/LksBrothers.Core/LksBrothers.Core.csproj", "src/LksBrothers.Core/"]
COPY ["src/LksBrothers.Consensus/LksBrothers.Consensus.csproj", "src/LksBrothers.Consensus/"]
RUN dotnet restore "src/LksBrothers.Firedancer/LksBrothers.Firedancer.csproj"
COPY . .
WORKDIR "/src/src/LksBrothers.Firedancer"
RUN dotnet build "LksBrothers.Firedancer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LksBrothers.Firedancer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LksBrothers.Firedancer.dll"]

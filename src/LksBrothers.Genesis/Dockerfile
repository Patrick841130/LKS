FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/LksBrothers.Genesis/LksBrothers.Genesis.csproj", "src/LksBrothers.Genesis/"]
COPY ["src/LksBrothers.Core/LksBrothers.Core.csproj", "src/LksBrothers.Core/"]
RUN dotnet restore "src/LksBrothers.Genesis/LksBrothers.Genesis.csproj"
COPY . .
WORKDIR "/src/src/LksBrothers.Genesis"
RUN dotnet build "LksBrothers.Genesis.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LksBrothers.Genesis.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LksBrothers.Genesis.dll"]

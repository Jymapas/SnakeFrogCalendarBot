FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SnakeFrogCalendarBot/SnakeFrogCalendarBot.csproj", "SnakeFrogCalendarBot/"]
RUN dotnet restore "SnakeFrogCalendarBot/SnakeFrogCalendarBot.csproj"
COPY . .
WORKDIR "/src/SnakeFrogCalendarBot"
RUN dotnet build "./SnakeFrogCalendarBot.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SnakeFrogCalendarBot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SnakeFrogCalendarBot.dll"]

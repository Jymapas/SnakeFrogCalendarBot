FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/SnakeFrogCalendarBot.Worker/SnakeFrogCalendarBot.Worker.csproj", "src/SnakeFrogCalendarBot.Worker/"]
COPY ["src/SnakeFrogCalendarBot.Application/SnakeFrogCalendarBot.Application.csproj", "src/SnakeFrogCalendarBot.Application/"]
COPY ["src/SnakeFrogCalendarBot.Infrastructure/SnakeFrogCalendarBot.Infrastructure.csproj", "src/SnakeFrogCalendarBot.Infrastructure/"]
COPY ["src/SnakeFrogCalendarBot.Domain/SnakeFrogCalendarBot.Domain.csproj", "src/SnakeFrogCalendarBot.Domain/"]
RUN dotnet restore "src/SnakeFrogCalendarBot.Worker/SnakeFrogCalendarBot.Worker.csproj"
COPY . .
WORKDIR "/src/src/SnakeFrogCalendarBot.Worker"
RUN dotnet build "./SnakeFrogCalendarBot.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SnakeFrogCalendarBot.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SnakeFrogCalendarBot.Worker.dll"]

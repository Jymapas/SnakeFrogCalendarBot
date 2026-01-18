FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/SnakeFrogCalendarBot.Worker/SnakeFrogCalendarBot.Worker.csproj", "src/SnakeFrogCalendarBot.Worker/"]
COPY ["src/SnakeFrogCalendarBot.Application/SnakeFrogCalendarBot.Application.csproj", "src/SnakeFrogCalendarBot.Application/"]
COPY ["src/SnakeFrogCalendarBot.Infrastructure/SnakeFrogCalendarBot.Infrastructure.csproj", "src/SnakeFrogCalendarBot.Infrastructure/"]
COPY ["src/SnakeFrogCalendarBot.Domain/SnakeFrogCalendarBot.Domain.csproj", "src/SnakeFrogCalendarBot.Domain/"]
RUN dotnet restore "src/SnakeFrogCalendarBot.Worker/SnakeFrogCalendarBot.Worker.csproj"

COPY . .
WORKDIR "/src/src/SnakeFrogCalendarBot.Worker"
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata locales iputils-ping \
    && rm -rf /var/lib/apt/lists/* \
    && sed -i 's/# ru_RU.UTF-8 UTF-8/ru_RU.UTF-8 UTF-8/' /etc/locale.gen \
    && locale-gen

ENV TZ=Europe/Moscow \
    LANG=ru_RU.UTF-8 \
    LC_ALL=ru_RU.UTF-8 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

VOLUME ["/data"]

WORKDIR /app

COPY --from=build /app/publish ./

COPY src/SnakeFrogCalendarBot.Worker/run.sh ./run.sh
RUN chmod +x ./run.sh

ENTRYPOINT ["./run.sh"]

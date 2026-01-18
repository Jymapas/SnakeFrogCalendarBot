#!/usr/bin/env bash
set -euo pipefail

# Использование:
#   ./redeploy.sh [путь_к_проекту] [имя_образа]
# Примеры:
#   ./redeploy.sh
#   ./redeploy.sh /opt/SnakeFrogCalendarBot snakefrogcalendarbot:latest

PROJECT_DIR="${1:-.}"
IMAGE="${2:-snakefrogcalendarbot:latest}"
COMPOSE="docker compose"

if ! command -v docker >/dev/null 2>&1; then
  echo "Ошибка: docker не найден в PATH."
  exit 1
fi

pushd "$PROJECT_DIR" >/dev/null

# Проверка наличия необходимых файлов
if [ ! -f "docker-compose.yml" ]; then
  echo "Ошибка: docker-compose.yml не найден в $PROJECT_DIR"
  exit 1
fi

if [ ! -f "src/SnakeFrogCalendarBot.Worker/Dockerfile" ]; then
  echo "Ошибка: Dockerfile не найден в $PROJECT_DIR/src/SnakeFrogCalendarBot.Worker/Dockerfile"
  exit 1
fi

echo "[1/3] Останавливаю стек..."
$COMPOSE down || true

echo "[2/3] Собираю образ без кеша: $IMAGE ..."
$COMPOSE build --no-cache bot

echo "[3/3] Поднимаю стек в фоне..."
$COMPOSE up -d

popd >/dev/null
echo "Готово ✅"

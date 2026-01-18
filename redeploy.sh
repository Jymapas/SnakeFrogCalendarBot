#!/usr/bin/env bash
set -euo pipefail

# Использование:
#   ./redeploy.sh [путь_к_проекту] [имя_образа] [--no-cache]
# Примеры:
#   ./redeploy.sh                    # быстрая сборка с кешем
#   ./redeploy.sh --no-cache         # полная пересборка без кеша (из текущей директории)
#   ./redeploy.sh . snakefrogcalendarbot:latest --no-cache  # полная пересборка с указанием всех параметров
#   ./redeploy.sh /opt/SnakeFrogCalendarBot  # с указанием пути

COMPOSE="docker compose"
NO_CACHE=""

# Обработка параметров: проверяем наличие --no-cache
ARGS=()
for arg in "$@"; do
  if [ "$arg" = "--no-cache" ]; then
    NO_CACHE="--no-cache"
  else
    ARGS+=("$arg")
  fi
done

PROJECT_DIR="${ARGS[0]:-.}"
IMAGE="${ARGS[1]:-snakefrogcalendarbot:latest}"

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

if [ "$NO_CACHE" = "--no-cache" ]; then
  echo "[2/3] Собираю образ без кеша: $IMAGE ..."
  $COMPOSE build --no-cache bot
else
  echo "[2/3] Собираю образ (с использованием кеша): $IMAGE ..."
  $COMPOSE build bot
fi

echo "[3/3] Поднимаю стек в фоне..."
$COMPOSE up -d

popd >/dev/null
echo "Готово ✅"

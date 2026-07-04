#!/usr/bin/env bash
set -euo pipefail

# Бэкап базы данных PostgreSQL через pg_dump внутри Docker-контейнера.
#
# Использование:
#   ./backup.sh                         # из директории проекта
#   ./backup.sh /opt/SnakeFrogCalendarBot
#
# Переменные окружения (опциональные):
#   BACKUP_DIR    — куда складывать дампы (по умолчанию <project>/backups)
#   KEEP_DAYS     — сколько дней хранить (по умолчанию 7)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="${1:-"$(dirname "$SCRIPT_DIR")"}"

# Загружаем переменные из .env
ENV_FILE="$PROJECT_DIR/.env"
if [ ! -f "$ENV_FILE" ]; then
  echo "Ошибка: файл .env не найден в $PROJECT_DIR"
  exit 1
fi
# shellcheck disable=SC1091
set -o allexport
source "$ENV_FILE"
set +o allexport

BACKUP_DIR="${BACKUP_DIR:-"$PROJECT_DIR/backups"}"
KEEP_DAYS="${KEEP_DAYS:-7}"
TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
FILENAME="snakefrog_${TIMESTAMP}.dump.gz"

mkdir -p "$BACKUP_DIR"

echo "[1/3] Создаю дамп базы данных ${POSTGRES_DB}..."
docker compose -f "$PROJECT_DIR/docker-compose.yml" exec -T postgres \
  pg_dump \
    --username="${POSTGRES_USER}" \
    --dbname="${POSTGRES_DB}" \
    --format=custom \
  | gzip > "$BACKUP_DIR/$FILENAME"

SIZE=$(du -sh "$BACKUP_DIR/$FILENAME" | cut -f1)
echo "[2/3] Сохранено: $BACKUP_DIR/$FILENAME ($SIZE)"

echo "[3/3] Удаляю дампы старше ${KEEP_DAYS} дней..."
find "$BACKUP_DIR" -maxdepth 1 -name "snakefrog_*.dump.gz" -mtime "+${KEEP_DAYS}" -delete

REMAINING=$(find "$BACKUP_DIR" -maxdepth 1 -name "snakefrog_*.dump.gz" | wc -l | tr -d ' ')
echo "Готово. Дампов в хранилище: $REMAINING"

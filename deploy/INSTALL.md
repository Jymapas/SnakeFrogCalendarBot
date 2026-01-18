# Инструкция по установке и настройке автозапуска на Ubuntu

## Предварительные требования

1. Установлен Docker и Docker Compose
2. Проект скопирован на сервер (например, в `/opt/SnakeFrogCalendarBot`)

## Шаги установки

### 1. Подготовка проекта

```bash
# Перейти в директорию проекта
cd /opt/SnakeFrogCalendarBot

# Убедиться, что файл .env настроен
nano .env
```

**Важно**: В файле `.env` должны быть заданы следующие переменные для PostgreSQL:
- `POSTGRES_DB` - имя базы данных
- `POSTGRES_USER` - имя пользователя
- `POSTGRES_PASSWORD` - пароль (обязательно, не может быть пустым)
- `POSTGRES_PORT` - порт (по умолчанию 5432)

Пример минимальной конфигурации PostgreSQL в `.env`:
```env
POSTGRES_DB=calendarbot
POSTGRES_USER=calendarbot
POSTGRES_PASSWORD=your_secure_password_here
POSTGRES_PORT=5432
```

**Примечание**: Если вы хотите использовать PostgreSQL без пароля (только для разработки), установите:
```env
POSTGRES_HOST_AUTH_METHOD=trust
```

### 2. Сборка Docker-образов

```bash
# Собрать образы
docker compose build

# Или если используете готовый образ
docker compose pull
```

### 3. Установка systemd service

```bash
# Скопировать service файл в systemd
sudo cp deploy/snakefrogcalendarbot.service /etc/systemd/system/

# Отредактировать путь к проекту, если он отличается от /opt/SnakeFrogCalendarBot
sudo nano /etc/systemd/system/snakefrogcalendarbot.service

# Перезагрузить systemd для применения изменений
sudo systemctl daemon-reload

# Включить автозапуск
sudo systemctl enable snakefrogcalendarbot.service

# Запустить сервис
sudo systemctl start snakefrogcalendarbot.service
```

### 4. Проверка статуса

```bash
# Проверить статус сервиса
sudo systemctl status snakefrogcalendarbot.service

# Посмотреть логи
sudo journalctl -u snakefrogcalendarbot.service -f

# Или логи Docker-контейнеров
docker compose logs -f
```

## Управление сервисом

```bash
# Запустить
sudo systemctl start snakefrogcalendarbot

# Остановить
sudo systemctl stop snakefrogcalendarbot

# Перезапустить
sudo systemctl restart snakefrogcalendarbot

# Проверить статус
sudo systemctl status snakefrogcalendarbot

# Отключить автозапуск
sudo systemctl disable snakefrogcalendarbot
```

## Обновление приложения

### Способ 1: Использование скрипта redeploy.sh (рекомендуется)

```bash
cd /opt/SnakeFrogCalendarBot

# Остановить systemd сервис (если используется)
sudo systemctl stop snakefrogcalendarbot

# Обновить код (git pull или копирование файлов)
git pull  # или другой способ обновления

# Запустить скрипт пересборки и деплоя
./redeploy.sh

# Запустить systemd сервис обратно (если используется)
sudo systemctl start snakefrogcalendarbot
```

### Способ 2: Ручная пересборка

```bash
cd /opt/SnakeFrogCalendarBot

# Остановить сервис
sudo systemctl stop snakefrogcalendarbot

# Обновить код (git pull или копирование файлов)
git pull  # или другой способ обновления

# Пересобрать образы (если изменился код)
docker compose build

# Или просто перезапустить с существующими образами
sudo systemctl restart snakefrogcalendarbot

# Запустить сервис
sudo systemctl start snakefrogcalendarbot
```

### Использование redeploy.sh

Скрипт `redeploy.sh` автоматически:
1. Останавливает текущий стек
2. Собирает образ без кеша
3. Поднимает стек заново

Параметры:
- `./redeploy.sh` - использует текущую директорию и образ `snakefrogcalendarbot:latest`
- `./redeploy.sh /opt/SnakeFrogCalendarBot` - указать путь к проекту
- `./redeploy.sh /opt/SnakeFrogCalendarBot snakefrogcalendarbot:latest` - указать путь и имя образа

## Диагностика проблем

### Проблема: PostgreSQL не запускается

Если контейнер `snakefrogcalendarbot-postgres` завершается с ошибкой:

```bash
# Проверить логи PostgreSQL
docker compose logs postgres

# Проверить, занят ли порт 5432
sudo netstat -tulpn | grep 5432
# или
sudo ss -tulpn | grep 5432

# Проверить переменные окружения в .env
cat .env | grep POSTGRES
```

**Возможные решения:**

1. **Порт занят**: Измените `POSTGRES_PORT` в `.env` на другой порт (например, `5433`)
2. **Неправильные переменные**: Убедитесь, что в `.env` заданы:
   - `POSTGRES_DB`
   - `POSTGRES_USER`
   - `POSTGRES_PASSWORD` (может быть пустым)
   - `POSTGRES_PORT` (по умолчанию 5432)
3. **Проблемы с volume**: Удалите volume и создайте заново:
   ```bash
   docker compose down -v
   docker compose up -d
   ```

### Проблема: Бот не запускается

```bash
# Проверить логи бота
docker compose logs bot

# Проверить статус контейнеров
docker compose ps
```

## Важные замечания

1. **Путь к проекту**: Убедитесь, что путь в `snakefrogcalendarbot.service` (по умолчанию `/opt/SnakeFrogCalendarBot`) соответствует реальному расположению проекта
2. **Права доступа**: Убедитесь, что пользователь, от имени которого запускается systemd, имеет права на выполнение `docker-compose`
3. **Docker Compose**: Если используется новая версия Docker с встроенным Compose (плагин), замените в service файле:
   - `ExecStart=/usr/bin/docker-compose up -d` → `ExecStart=/usr/bin/docker compose up -d`
   - `ExecStop=/usr/bin/docker-compose down` → `ExecStop=/usr/bin/docker compose down`
4. **Сеть**: Контейнер будет ждать подключения к сети перед запуском (проверка через `ping api.telegram.org`)
5. **Логи**: Логи приложения сохраняются в `./logs/` директории проекта

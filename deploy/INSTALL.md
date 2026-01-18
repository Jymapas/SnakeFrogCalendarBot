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

## Важные замечания

1. **Путь к проекту**: Убедитесь, что путь в `snakefrogcalendarbot.service` (по умолчанию `/opt/SnakeFrogCalendarBot`) соответствует реальному расположению проекта
2. **Права доступа**: Убедитесь, что пользователь, от имени которого запускается systemd, имеет права на выполнение `docker-compose`
3. **Docker Compose**: Если используется новая версия Docker с встроенным Compose (плагин), замените в service файле:
   - `ExecStart=/usr/bin/docker-compose up -d` → `ExecStart=/usr/bin/docker compose up -d`
   - `ExecStop=/usr/bin/docker-compose down` → `ExecStop=/usr/bin/docker compose down`
4. **Сеть**: Контейнер будет ждать подключения к сети перед запуском (проверка через `ping api.telegram.org`)
5. **Логи**: Логи приложения сохраняются в `./logs/` директории проекта

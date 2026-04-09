# Настройка nginx + DuckDNS + Let's Encrypt на Orange Pi

## Требования

- Orange Pi с Ubuntu/Debian
- Публичный IP (белый IP у роутера)
- Порты 80 и 443 проброшены на Orange Pi в роутере

---

## Шаг 1: Бесплатный домен через DuckDNS

1. Зайди на [duckdns.org](https://www.duckdns.org) → войди через Telegram/GitHub/Google
2. Создай субдомен, например `snakefrog` → получишь `snakefrog.duckdns.org`
3. Скопируй токен (строка вида `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`)

**Автообновление IP на Orange Pi:**

```bash
# Создай скрипт
mkdir -p ~/duckdns
cat > ~/duckdns/duck.sh << 'EOF'
echo url="https://www.duckdns.org/update?domains=snakefrog&token=YOUR_TOKEN&ip=" | curl -k -o ~/duckdns/duck.log -K -
EOF

chmod +x ~/duckdns/duck.sh

# Добавь в cron (обновление каждые 5 минут)
crontab -e
# Добавить строку:
*/5 * * * * ~/duckdns/duck.sh >/dev/null 2>&1
```

Замени `snakefrog` на свой субдомен, `YOUR_TOKEN` на токен.

**Проверка:**
```bash
~/duckdns/duck.sh && cat ~/duckdns/duck.log
# Должно вернуть: OK
```

---

## Шаг 2: Установка nginx

```bash
sudo apt update
sudo apt install -y nginx
sudo systemctl enable nginx
sudo systemctl start nginx
```

---

## Шаг 3: SSL-сертификат через Certbot

```bash
sudo apt install -y certbot python3-certbot-nginx

# Получить сертификат (nginx должен быть запущен, порт 80 открыт)
sudo certbot --nginx -d snakefrog.duckdns.org
```

Certbot спросит email и условия лицензии. После успеха сертификат будет в `/etc/letsencrypt/live/snakefrog.duckdns.org/`.

**Автопродление** настраивается автоматически через systemd timer. Проверить:
```bash
sudo systemctl status certbot.timer
```

---

## Шаг 4: Конфигурация nginx

```bash
sudo nano /etc/nginx/sites-available/snakefrog
```

Содержимое файла:

```nginx
server {
    listen 80;
    server_name snakefrog.duckdns.org;
    # Certbot добавит сюда редирект на HTTPS автоматически
}

server {
    listen 443 ssl;
    server_name snakefrog.duckdns.org;

    # SSL — заполняется certbot автоматически
    ssl_certificate     /etc/letsencrypt/live/snakefrog.duckdns.org/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/snakefrog.duckdns.org/privkey.pem;
    include             /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam         /etc/letsencrypt/ssl-dhparams.pem;

    # Проксируем API бота
    location /api/ {
        proxy_pass         http://localhost:8080;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

```bash
# Активировать конфиг
sudo ln -s /etc/nginx/sites-available/snakefrog /etc/nginx/sites-enabled/

# Проверить синтаксис
sudo nginx -t

# Применить
sudo systemctl reload nginx
```

---

## Шаг 5: Проброс портов в роутере

В настройках роутера (обычно `192.168.1.1`):

| Внешний порт | Внутренний IP  | Внутренний порт | Протокол |
|---|---|---|---|
| 80  | IP Orange Pi | 80  | TCP |
| 443 | IP Orange Pi | 443 | TCP |

Узнать IP Orange Pi в сети:
```bash
hostname -I
```

---

## Шаг 6: Обновить .env на сервере

```env
MINI_APP_URL=https://snakefrog.duckdns.org
MINI_APP_ALLOWED_ORIGIN=https://USERNAME.github.io
```

Перезапустить бота:
```bash
docker compose up -d --build
# или
sudo systemctl restart snakefrogcalendarbot
```

---

## Шаг 7: Обновить VITE_API_URL в Mini App

В `mini-app/.env.production`:
```
VITE_API_URL=https://snakefrog.duckdns.org
```

После этого задеплоить Mini App (вручную через GitHub Actions или через команду `/deploy_miniapp` в боте).

---

## Проверка

```bash
# API доступен по HTTPS
curl -X POST https://snakefrog.duckdns.org/api/events \
  -H "Content-Type: application/json" \
  -H "Authorization: tma invalid" \
  -d '{}'
# Должен вернуть 401

# SSL-сертификат валиден
curl -vI https://snakefrog.duckdns.org/api/events 2>&1 | grep -E "SSL|subject|issuer|expire"
```

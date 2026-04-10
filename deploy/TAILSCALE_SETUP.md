# Настройка Tailscale Funnel на Orange Pi

Tailscale Funnel открывает локальный порт наружу по HTTPS без белого IP и проброса портов.

---

## Шаг 1: Установить Tailscale

```bash
curl -fsSL https://tailscale.com/install.sh | sh
sudo tailscale up
```

При первом запуске в терминале появится ссылка — открой её в браузере и войди/зарегистрируйся.

---

## Шаг 2: Включить HTTPS и Funnel в настройках аккаунта

1. Зайди на [login.tailscale.com/admin/dns](https://login.tailscale.com/admin/dns)
2. Включи **HTTPS Certificates** (кнопка "Enable HTTPS")
3. Зайди на [login.tailscale.com/admin/acls](https://login.tailscale.com/admin/acls)
4. Убедись что в policy есть разрешение на Funnel:
   ```json
   "nodeAttrs": [
     {
       "target": ["autogroup:member"],
       "attr":   ["funnel"]
     }
   ]
   ```
   Если строки нет — добавь и сохрани.

---

## Шаг 3: Запустить Funnel

```bash
sudo tailscale funnel --bg 8080
```

Флаг `--bg` запускает в фоне и сохраняет настройку после перезагрузки.

Tailscale выведет публичный URL вида:
```
https://orange-pi.tail1234ab.ts.net
```

Запомни этот URL — он понадобится для `.env`.

**Проверить статус:**
```bash
sudo tailscale funnel status
```

**Остановить:**
```bash
sudo tailscale funnel --bg=false 8080
```

---

## Шаг 4: Обновить .env на сервере

```env
MINI_APP_URL=https://orange-pi.tail1234ab.ts.net
MINI_APP_ALLOWED_ORIGIN=https://USERNAME.github.io
```

Перезапустить бота:
```bash
docker compose up -d
```

---

## Шаг 5: Обновить VITE_API_URL в Mini App

В `mini-app/.env.production`:
```
VITE_API_URL=https://orange-pi.tail1234ab.ts.net
```

После этого задеплоить Mini App через GitHub Actions (вручную или через `/deploy_miniapp` в боте).

---

## Проверка

```bash
# Должен вернуть 401 (значит API доступен)
curl -s -o /dev/null -w "%{http_code}" \
  -X POST https://orange-pi.tail1234ab.ts.net/api/events \
  -H "Content-Type: application/json" \
  -H "Authorization: tma invalid" \
  -d '{}'
```

---

## Автозапуск

`tailscale funnel --bg` сохраняет настройку — Funnel поднимается автоматически при перезагрузке Orange Pi вместе со службой `tailscaled`.

Убедиться что служба включена:
```bash
sudo systemctl enable tailscaled
```

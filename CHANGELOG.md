# Changelog

All notable changes to this project will be documented in this file.

## [1.1.0] - 2026-07-05

### Fixed

- Mini App persistent tokens now survive service restarts. Previously, static keyboard button URLs stopped working after a reboot (OrangePI power outage) because tokens were stored in memory only. Tokens are now derived deterministically via HMAC-SHA256(bot_token, user_id) and pre-warmed at startup for all allowed user IDs, eliminating 401 errors on the first request after restart.
- systemd service now declares `time-sync.target` dependency to ensure the bot starts only after the system clock is synchronized.

## [1.0.0] - 2026-06-01

### Added

- Telegram bot for managing family calendar events and birthdays
- One-off and yearly recurring events with file attachments
- Daily, weekly, and monthly digest posts with auto-refresh on new event creation
- Russian date parsing with NodaTime timezone support
- FSM-based conversation flows for add/edit/delete operations
- Telegram Mini App (TypeScript + Vite) for quick event and birthday creation
- Mini App backend HTTP API with Telegram initData (HMAC-SHA256) and one-time token authentication
- GitHub Actions deployment for Mini App to GitHub Pages
- Tailscale Funnel integration for HTTPS access without port forwarding
- PostgreSQL persistence via EF Core with auto-migration on startup
- Quartz.NET scheduled digest jobs (daily 09:00, weekly Sun 21:00, monthly last day 18:00)
- Monthly digest auto-pin with cleanup of Telegram system pin notifications

using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SnakeFrogCalendarBot.Worker.Telegram;

public static class ReplyKeyboards
{
    public static ReplyKeyboardMarkup MainKeyboard(string? miniAppUrl = null, string? token = null)
    {
        var hasMiniApp = !string.IsNullOrWhiteSpace(miniAppUrl);
        var tokenParam = !string.IsNullOrWhiteSpace(token) ? $"&token={token}" : "";

        return new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                hasMiniApp
                    ? new KeyboardButton("➕ Событие") { WebApp = new WebAppInfo { Url = $"{miniAppUrl}?form=event{tokenParam}" } }
                    : new KeyboardButton("➕ Событие"),
                hasMiniApp
                    ? new KeyboardButton("➕ День рождения") { WebApp = new WebAppInfo { Url = $"{miniAppUrl}?form=birthday{tokenParam}" } }
                    : new KeyboardButton("➕ День рождения"),
            },
            new[]
            {
                new KeyboardButton("📅 События"),
                new KeyboardButton("🎂 Дни рождения")
            },
            new[]
            {
                new KeyboardButton("📅 На неделю"),
                new KeyboardButton("📅 На месяц")
            },
            new[]
            {
                new KeyboardButton("✏️ Редактировать"),
                new KeyboardButton("🗑 Удалить")
            },
            new[]
            {
                new KeyboardButton("❌ Скрыть клавиатуру"),
                new KeyboardButton("↩️ Отмена")
            }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };
    }

    public static ReplyKeyboardRemove RemoveKeyboard()
    {
        return new ReplyKeyboardRemove();
    }
}

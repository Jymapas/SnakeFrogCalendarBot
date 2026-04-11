using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SnakeFrogCalendarBot.Worker.Telegram;

public static class ReplyKeyboards
{
    public static ReplyKeyboardMarkup MainKeyboard(string? miniAppUrl = null)
    {
        KeyboardButton[] addRow = string.IsNullOrWhiteSpace(miniAppUrl)
            ? new[]
            {
                new KeyboardButton("➕ Событие"),
                new KeyboardButton("➕ День рождения")
            }
            : new[]
            {
                KeyboardButton.WithWebApp("➕ Событие", new WebAppInfo { Url = $"{miniAppUrl}?form=event" }),
                KeyboardButton.WithWebApp("➕ День рождения", new WebAppInfo { Url = $"{miniAppUrl}?form=birthday" })
            };

        return new ReplyKeyboardMarkup(new[]
        {
            addRow,
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

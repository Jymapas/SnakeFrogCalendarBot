using Telegram.Bot.Types.ReplyMarkups;

namespace SnakeFrogCalendarBot.Worker.Telegram;

public static class ReplyKeyboards
{
    public static ReplyKeyboardMarkup MainKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                new KeyboardButton("➕ Событие"),
                new KeyboardButton("➕ День рождения")
            },
            new[]
            {
                new KeyboardButton("📅 События"),
                new KeyboardButton("🎂 Дни рождения")
            },
            new[]
            {
                new KeyboardButton("✏️ Редактировать"),
                new KeyboardButton("🗑 Удалить")
            },
            new[]
            {
                new KeyboardButton("❌ Скрыть клавиатуру")
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

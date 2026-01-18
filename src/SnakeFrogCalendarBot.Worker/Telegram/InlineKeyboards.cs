using Telegram.Bot.Types.ReplyMarkups;

namespace SnakeFrogCalendarBot.Worker.Telegram;

public static class InlineKeyboards
{
    public static InlineKeyboardMarkup MainMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📅 События", "menu:events"),
                InlineKeyboardButton.WithCallbackData("🎂 Дни рождения", "menu:birthdays")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("➕ Добавить событие", "cmd:event_add"),
                InlineKeyboardButton.WithCallbackData("➕ Добавить день рождения", "cmd:birthday_add")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📋 Список событий", "cmd:event_list"),
                InlineKeyboardButton.WithCallbackData("📋 Список дней рождения", "cmd:birthday_list")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✏️ Редактировать событие", "cmd:event_edit"),
                InlineKeyboardButton.WithCallbackData("✏️ Редактировать день рождения", "cmd:birthday_edit")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🗑 Удалить событие", "cmd:event_delete"),
                InlineKeyboardButton.WithCallbackData("🗑 Удалить день рождения", "cmd:birthday_delete")
            }
        });
    }

    public static InlineKeyboardMarkup EventsMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("➕ Добавить событие", "cmd:event_add"),
                InlineKeyboardButton.WithCallbackData("📋 Список событий", "cmd:event_list")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✏️ Редактировать", "cmd:event_edit"),
                InlineKeyboardButton.WithCallbackData("🗑 Удалить", "cmd:event_delete")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔙 Главное меню", "menu:main")
            }
        });
    }

    public static InlineKeyboardMarkup BirthdaysMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("➕ Добавить день рождения", "cmd:birthday_add"),
                InlineKeyboardButton.WithCallbackData("📋 Список дней рождения", "cmd:birthday_list")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✏️ Редактировать", "cmd:birthday_edit"),
                InlineKeyboardButton.WithCallbackData("🗑 Удалить", "cmd:birthday_delete")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔙 Главное меню", "menu:main")
            }
        });
    }
}

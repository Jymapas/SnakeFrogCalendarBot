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

    public static InlineKeyboardMarkup MonthSelectionKeyboard()
    {
        var monthNames = new[]
        {
            "Январь", "Февраль", "Март", "Апрель",
            "Май", "Июнь", "Июль", "Август",
            "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"
        };

        var buttons = new List<List<InlineKeyboardButton>>();
        
        // Группируем месяцы по 3 в ряд
        for (int i = 0; i < monthNames.Length; i += 3)
        {
            var row = new List<InlineKeyboardButton>();
            for (int j = 0; j < 3 && i + j < monthNames.Length; j++)
            {
                var monthNumber = i + j + 1;
                row.Add(InlineKeyboardButton.WithCallbackData(
                    monthNames[i + j], 
                    $"birthday_list_month:{monthNumber}"));
            }
            buttons.Add(row);
        }

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup MonthSelectionKeyboardForEdit()
    {
        var monthNames = new[]
        {
            "Январь", "Февраль", "Март", "Апрель",
            "Май", "Июнь", "Июль", "Август",
            "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"
        };

        var buttons = new List<List<InlineKeyboardButton>>();
        
        // Группируем месяцы по 3 в ряд
        for (int i = 0; i < monthNames.Length; i += 3)
        {
            var row = new List<InlineKeyboardButton>();
            for (int j = 0; j < 3 && i + j < monthNames.Length; j++)
            {
                var monthNumber = i + j + 1;
                row.Add(InlineKeyboardButton.WithCallbackData(
                    monthNames[i + j], 
                    $"birthday_edit_month:{monthNumber}"));
            }
            buttons.Add(row);
        }

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup EventMonthSelectionKeyboardForEdit()
    {
        var monthNames = new[]
        {
            "Январь", "Февраль", "Март", "Апрель",
            "Май", "Июнь", "Июль", "Август",
            "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"
        };

        var buttons = new List<List<InlineKeyboardButton>>();
        
        // Группируем месяцы по 3 в ряд
        for (int i = 0; i < monthNames.Length; i += 3)
        {
            var row = new List<InlineKeyboardButton>();
            for (int j = 0; j < 3 && i + j < monthNames.Length; j++)
            {
                var monthNumber = i + j + 1;
                row.Add(InlineKeyboardButton.WithCallbackData(
                    monthNames[i + j], 
                    $"event_edit_month:{monthNumber}"));
            }
            buttons.Add(row);
        }

        return new InlineKeyboardMarkup(buttons);
    }
}

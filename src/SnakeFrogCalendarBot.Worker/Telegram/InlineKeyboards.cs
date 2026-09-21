using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SnakeFrogCalendarBot.Worker.Telegram;

public static class InlineKeyboards
{
    public static InlineKeyboardMarkup MainMenu(string? miniAppUrl = null)
    {
        var rows = new List<InlineKeyboardButton[]>
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📅 События", "menu:events"),
                InlineKeyboardButton.WithCallbackData("🎂 Дни рождения", "menu:birthdays")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📅 На неделю", "event_view_week:0"),
                InlineKeyboardButton.WithCallbackData("📅 На месяц", "event_view_month:0")
            },
        };

        if (!string.IsNullOrWhiteSpace(miniAppUrl))
        {
            rows.Add(new[]
            {
                new InlineKeyboardButton("➕ Добавить событие")
                    { WebApp = new WebAppInfo { Url = $"{miniAppUrl}?form=event" } },
                new InlineKeyboardButton("🎂 Добавить ДР")
                    { WebApp = new WebAppInfo { Url = $"{miniAppUrl}?form=birthday" } },
            });
        }
        else
        {
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("➕ Добавить событие", "cmd:event_add"),
                InlineKeyboardButton.WithCallbackData("➕ Добавить день рождения", "cmd:birthday_add")
            });
        }

        rows.AddRange(new[]
        {
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

        return new InlineKeyboardMarkup(rows);
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
                InlineKeyboardButton.WithCallbackData("📅 На неделю", "event_view_week:0"),
                InlineKeyboardButton.WithCallbackData("📅 На месяц", "event_view_month:0")
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

    public static InlineKeyboardMarkup MonthSelectionKeyboardForDelete()
    {
        var monthNames = new[]
        {
            "Январь", "Февраль", "Март", "Апрель",
            "Май", "Июнь", "Июль", "Август",
            "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"
        };

        var buttons = new List<List<InlineKeyboardButton>>();

        for (int i = 0; i < monthNames.Length; i += 3)
        {
            var row = new List<InlineKeyboardButton>();
            for (int j = 0; j < 3 && i + j < monthNames.Length; j++)
            {
                var monthNumber = i + j + 1;
                row.Add(InlineKeyboardButton.WithCallbackData(
                    monthNames[i + j],
                    $"birthday_delete_month:{monthNumber}"));
            }
            buttons.Add(row);
        }

        return new InlineKeyboardMarkup(buttons);
    }

    /// <summary>
    /// Выбор типа повторения на шаге создания события.
    /// </summary>
    public static InlineKeyboardMarkup EventKindKeyboard()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("📌 Разовое", "event_kind:OneOff") },
            new[] { InlineKeyboardButton.WithCallbackData("🎉 Ежегодно", "event_kind:Yearly") },
            new[] { InlineKeyboardButton.WithCallbackData("🔁 Еженедельно", "event_kind:Weekly") },
            new[] { InlineKeyboardButton.WithCallbackData("⏱ Раз в N периодов", "event_kind:Interval") }
        });
    }

    /// <summary>
    /// Мультивыбор дней недели: бит 0 — понедельник, бит 6 — воскресенье.
    /// </summary>
    public static InlineKeyboardMarkup WeekdaysKeyboard(int daysOfWeekMask)
    {
        var names = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };
        var buttons = new List<List<InlineKeyboardButton>>();

        for (var i = 0; i < names.Length; i += 4)
        {
            var row = new List<InlineKeyboardButton>();
            for (var j = 0; j < 4 && i + j < names.Length; j++)
            {
                var bit = i + j;
                var selected = (daysOfWeekMask & (1 << bit)) != 0;
                row.Add(InlineKeyboardButton.WithCallbackData(
                    selected ? $"✅ {names[bit]}" : names[bit],
                    $"event_weekday:{bit}"));
            }

            buttons.Add(row);
        }

        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("Готово", "event_weekdays_done")
        });

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup IntervalUnitKeyboard()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Дни", "event_interval_unit:Days"),
                InlineKeyboardButton.WithCallbackData("Недели", "event_interval_unit:Weeks")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Месяцы", "event_interval_unit:Months"),
                InlineKeyboardButton.WithCallbackData("Годы", "event_interval_unit:Years")
            }
        });
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

        // Повторяющиеся события не привязаны к одному месяцу — им отдельная корзина.
        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("🔁 Повторяющиеся", "event_edit_month:0")
        });

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup EventMonthSelectionKeyboardForDelete()
    {
        var monthNames = new[]
        {
            "Январь", "Февраль", "Март", "Апрель",
            "Май", "Июнь", "Июль", "Август",
            "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"
        };

        var buttons = new List<List<InlineKeyboardButton>>();

        for (int i = 0; i < monthNames.Length; i += 3)
        {
            var row = new List<InlineKeyboardButton>();
            for (int j = 0; j < 3 && i + j < monthNames.Length; j++)
            {
                var monthNumber = i + j + 1;
                row.Add(InlineKeyboardButton.WithCallbackData(
                    monthNames[i + j],
                    $"event_delete_month:{monthNumber}"));
            }
            buttons.Add(row);
        }

        // Повторяющиеся события не привязаны к одному месяцу — им отдельная корзина.
        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("🔁 Повторяющиеся", "event_delete_month:0")
        });

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup EventMonthSelectionKeyboardForList()
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
                    $"event_list_month:{monthNumber}"));
            }
            buttons.Add(row);
        }

        // Повторяющиеся события не привязаны к одному месяцу — им отдельная корзина.
        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("🔁 Повторяющиеся", "event_list_month:0")
        });

        return new InlineKeyboardMarkup(buttons);
    }
}

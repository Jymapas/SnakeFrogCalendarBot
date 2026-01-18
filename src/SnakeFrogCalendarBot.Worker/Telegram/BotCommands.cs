using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot.Types;

namespace SnakeFrogCalendarBot.Worker.Telegram;

public static class BotCommands
{
    public const string BirthdayAdd = "/birthday_add";
    public const string BirthdayList = "/birthday_list";
    public const string BirthdayEdit = "/birthday_edit";
    public const string BirthdayDelete = "/birthday_delete";
    public const string EventAdd = "/event_add";
    public const string EventList = "/event_list";
    public const string EventEdit = "/event_edit";
    public const string EventDelete = "/event_delete";
    public const string Cancel = "/cancel";
    public const string DigestTest = "/digest_test";

    public static readonly string[] All =
    [
        BirthdayAdd,
        BirthdayList,
        BirthdayEdit,
        BirthdayDelete,
        EventAdd,
        EventList,
        EventEdit,
        EventDelete,
        Cancel,
        DigestTest,
    ];

    private static readonly Dictionary<string, string> CustomDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        [BirthdayAdd] = "Добавить день рождения",
        [BirthdayList] = "Список дней рождения",
        [BirthdayEdit] = "Редактировать день рождения",
        [BirthdayDelete] = "Удалить день рождения",
        [EventAdd] = "Добавить событие",
        [EventList] = "Список событий",
        [EventEdit] = "Редактировать событие",
        [EventDelete] = "Удалить событие",
        [Cancel] = "Отменить текущее действие",
        [DigestTest] = "Тест дайджеста",
    };

    public static IReadOnlyList<BotCommand> AsBotCommands() =>
        All
            .Select(c =>
            {
                var cmd = c.TrimStart('/');
                if (CustomDescriptions.TryGetValue(c, out var desc) && !string.IsNullOrWhiteSpace(desc))
                    return new BotCommand { Command = cmd, Description = desc };
                return null;
            })
            .Where(static c => c is not null)
            .Select(static c => c!)
            .ToArray();
}

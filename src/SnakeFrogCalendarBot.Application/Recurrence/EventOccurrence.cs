using NodaTime;

namespace SnakeFrogCalendarBot.Application.Recurrence;

/// <summary>
/// Одно конкретное вхождение события в календарь: локальная дата и, если событие не на весь
/// день, локальное время.
/// </summary>
public readonly record struct EventOccurrence(LocalDate Date, LocalTime? Time);

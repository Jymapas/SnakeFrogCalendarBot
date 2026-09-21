using NodaTime;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Application.Formatting;

/// <summary>
/// Событие вместе с конкретным вхождением в просматриваемый период. Одно повторяющееся
/// событие даёт несколько таких элементов.
/// </summary>
public sealed record EventOccurrenceListItem(Event Event, LocalDate Date, LocalTime? Time);

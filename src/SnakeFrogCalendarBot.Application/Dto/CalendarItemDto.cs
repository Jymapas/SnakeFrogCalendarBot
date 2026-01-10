using NodaTime;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Application.Dto;

public sealed record CalendarItemDto
{
    public required LocalDate Date { get; init; }
    public LocalTime? Time { get; init; }
    public required string Title { get; init; }
    public CalendarItemType Type { get; init; }
    public bool IsAllDay { get; init; }
    public bool HasAttachment { get; init; }
    public int? BirthYear { get; init; }
}

public enum CalendarItemType
{
    Event,
    Birthday
}
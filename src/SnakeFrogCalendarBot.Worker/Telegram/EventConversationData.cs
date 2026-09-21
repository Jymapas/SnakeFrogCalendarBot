namespace SnakeFrogCalendarBot.Worker.Telegram;

public sealed class EventConversationData
{
    public string? Title { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
    public int? Hour { get; set; }
    public int? Minute { get; set; }
    public bool? IsAllDay { get; set; }
    public bool? HasYear { get; set; }
    public string? Kind { get; set; }
    public int? DaysOfWeekMask { get; set; }
    public int? IntervalCount { get; set; }
    public string? IntervalUnit { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? RepeatUntil { get; set; }
    public string? Description { get; set; }
    public string? Place { get; set; }
    public string? Link { get; set; }
}

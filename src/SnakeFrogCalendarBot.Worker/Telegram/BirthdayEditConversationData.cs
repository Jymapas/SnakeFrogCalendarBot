namespace SnakeFrogCalendarBot.Worker.Telegram;

public sealed class BirthdayEditConversationData
{
    public int? BirthdayId { get; set; }
    public string? Field { get; set; }
    public string? PersonName { get; set; }
    public int? Day { get; set; }
    public int? Month { get; set; }
    public int? BirthYear { get; set; }
    public string? Contact { get; set; }
}
using System.Globalization;
using System.Text;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Application.Formatting;

public sealed class BirthdayListFormatter
{
    private readonly CultureInfo _culture;

    public BirthdayListFormatter()
    {
        _culture = CultureInfo.GetCultureInfo("ru-RU");
    }

    public string Format(IReadOnlyList<Birthday> birthdays)
    {
        if (birthdays.Count == 0)
        {
            return "Дней рождения пока нет";
        }

        var builder = new StringBuilder();

        for (var index = 0; index < birthdays.Count; index++)
        {
            var birthday = birthdays[index];
            var date = new DateTime(2000, birthday.Month, birthday.Day);

            builder.AppendLine(date.ToString("d MMMM", _culture));
            builder.AppendLine(birthday.PersonName);

            if (!string.IsNullOrWhiteSpace(birthday.Contact))
            {
                builder.AppendLine(birthday.Contact);
            }

            if (index < birthdays.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}

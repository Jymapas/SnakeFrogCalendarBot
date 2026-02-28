using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Application.Dto;

public sealed record LatestDigestPostInfo(
    DigestType DigestType,
    int TelegramMessageId,
    DateTime PeriodStartLocal,
    DateTime PeriodEndLocal,
    string TimeZoneId);

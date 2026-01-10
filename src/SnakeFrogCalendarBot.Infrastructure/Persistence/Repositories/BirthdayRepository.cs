using Microsoft.EntityFrameworkCore;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Infrastructure.Persistence.Repositories;

public sealed class BirthdayRepository : IBirthdayRepository
{
    private readonly CalendarDbContext _dbContext;

    public BirthdayRepository(CalendarDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Birthday birthday, CancellationToken cancellationToken)
    {
        _dbContext.Birthdays.Add(birthday);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Birthday>> ListAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Birthdays
            .AsNoTracking()
            .OrderBy(birthday => birthday.Month)
            .ThenBy(birthday => birthday.Day)
            .ToListAsync(cancellationToken);
    }
}

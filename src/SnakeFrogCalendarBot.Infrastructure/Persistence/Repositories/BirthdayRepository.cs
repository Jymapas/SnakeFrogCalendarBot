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

    public async Task<Birthday?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _dbContext.Birthdays
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Birthday birthday, CancellationToken cancellationToken)
    {
        _dbContext.Birthdays.Update(birthday);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var birthday = await _dbContext.Birthdays
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (birthday is not null)
        {
            _dbContext.Birthdays.Remove(birthday);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

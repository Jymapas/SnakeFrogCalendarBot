using Microsoft.EntityFrameworkCore;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Infrastructure.Persistence;

public sealed class CalendarDbContext : DbContext
{
    public CalendarDbContext(DbContextOptions<CalendarDbContext> options)
        : base(options)
    {
    }

    public DbSet<Birthday> Birthdays => Set<Birthday>();
    public DbSet<ConversationState> ConversationStates => Set<ConversationState>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<NotificationRun> NotificationRuns => Set<NotificationRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CalendarDbContext).Assembly);
    }
}

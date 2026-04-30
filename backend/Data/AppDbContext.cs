// ================================================================
// Data/AppDbContext.cs
// ================================================================
using DiamondBooking.Models;
using Microsoft.EntityFrameworkCore;

namespace DiamondBooking.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Tenant>          Tenants          => Set<Tenant>();
    public DbSet<User>            Users            => Set<User>();
    public DbSet<Service>         Services         => Set<Service>();
    public DbSet<TimeSlot>        TimeSlots        => Set<TimeSlot>();
    public DbSet<Booking>         Bookings         => Set<Booking>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        // Tenant
        m.Entity<Tenant>().HasIndex(t => t.Slug).IsUnique();

        // User: composite unique on (TenantId, LineUserId)
        m.Entity<User>()
            .HasIndex(u => new { u.TenantId, u.LineUserId }).IsUnique();

        // Booking: status as string enum
        m.Entity<Booking>()
            .Property(b => b.Status)
            .HasConversion<string>();

        // Booking: DateOnly / TimeOnly mapping
        m.Entity<Booking>().Property(b => b.BookingDate).HasColumnType("date");
        m.Entity<Booking>().Property(b => b.StartTime).HasColumnType("time");
        m.Entity<Booking>().Property(b => b.EndTime).HasColumnType("time");

        m.Entity<TimeSlot>().Property(s => s.StartTime).HasColumnType("time");
        m.Entity<TimeSlot>().Property(s => s.EndTime).HasColumnType("time");
        m.Entity<TimeSlot>().Property(s => s.SpecificDate).HasColumnType("date");
    }
}

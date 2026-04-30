// ================================================================
// Helpers/TestDbFactory.cs  —  In-memory DB + seed helpers
// ================================================================
using DiamondBooking.Data;
using DiamondBooking.Models;
using Microsoft.EntityFrameworkCore;

namespace DiamondBooking.Tests.Helpers;

public static class TestDbFactory
{
    public static AppDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    // ── Seed helpers ─────────────────────────────────────────────

    public static Tenant SeedTenant(AppDbContext db, string slug = "diamond-massage")
    {
        var tenant = new Tenant
        {
            Id                 = Guid.NewGuid(),
            Slug               = slug,
            Name               = "Diamond Massage",
            LineChannelId      = "test-channel-id",
            LineChannelSecret  = "test-secret",
            LineAccessToken    = "test-token",
            LiffId             = "test-liff-id",
            IsActive           = true
        };
        db.Tenants.Add(tenant);
        db.SaveChanges();
        return tenant;
    }

    public static User SeedUser(AppDbContext db, Guid tenantId, string lineUserId = "U123456")
    {
        var user = new User
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            LineUserId  = lineUserId,
            DisplayName = "Test User",
            Phone       = "0812345678"
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static Service SeedService(AppDbContext db, Guid tenantId)
    {
        var service = new Service
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            Name            = "นวดแผนไทย 60 นาที",
            DurationMinutes = 60,
            Price           = 350,
            IsActive        = true
        };
        db.Services.Add(service);
        db.SaveChanges();
        return service;
    }

    public static TimeSlot SeedTimeSlot(
        AppDbContext db,
        Guid tenantId,
        int? dayOfWeek = 1,          // Monday
        string start   = "10:00",
        string end     = "11:30",
        int maxBookings = 2)
    {
        var slot = new TimeSlot
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            DayOfWeek   = dayOfWeek,
            StartTime   = TimeOnly.Parse(start),
            EndTime     = TimeOnly.Parse(end),
            MaxBookings = maxBookings,
            IsActive    = true
        };
        db.TimeSlots.Add(slot);
        db.SaveChanges();
        return slot;
    }

    public static Booking SeedBooking(
        AppDbContext db,
        Guid tenantId,
        Guid userId,
        Guid? serviceId   = null,
        string date       = "2025-06-02",   // Monday
        string start      = "10:00",
        string end        = "11:30",
        BookingStatus status = BookingStatus.Confirmed)
    {
        var booking = new Booking
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            UserId      = userId,
            ServiceId   = serviceId,
            BookingDate = DateOnly.Parse(date),
            StartTime   = TimeOnly.Parse(start),
            EndTime     = TimeOnly.Parse(end),
            Status      = status
        };
        db.Bookings.Add(booking);
        db.SaveChanges();
        return booking;
    }
}

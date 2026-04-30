// ================================================================
// ReminderJobTests.cs  —  Unit tests for the Quartz reminder job
// ================================================================
using DiamondBooking.Jobs;
using DiamondBooking.Models;
using DiamondBooking.Services;
using DiamondBooking.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Quartz;

namespace DiamondBooking.Tests;

public class ReminderJobTests
{
    private static (ReminderJob job, Mock<ILineService> lineMock, DiamondBooking.Data.AppDbContext db, Guid tenantId)
        Setup(string dbName)
    {
        var db     = TestDbFactory.Create(dbName);
        var tenant = TestDbFactory.SeedTenant(db);

        var lineMock = new Mock<ILineService>();
        lineMock.Setup(l => l.SendReminderAsync(It.IsAny<Booking>()))
                .Returns(Task.CompletedTask);

        var job = new ReminderJob(db, lineMock.Object, NullLogger<ReminderJob>.Instance);
        return (job, lineMock, db, tenant.Id);
    }

    private static IJobExecutionContext MockContext() =>
        new Mock<IJobExecutionContext>().Object;

    // ── Happy path ────────────────────────────────────────────────

    [Fact]
    public async Task Execute_BookingTomorrow_SendsReminder()
    {
        var (job, lineMock, db, tenantId) = Setup(nameof(Execute_BookingTomorrow_SendsReminder));
        var user    = TestDbFactory.SeedUser(db, tenantId);
        var service = TestDbFactory.SeedService(db, tenantId);

        // Seed a confirmed booking for tomorrow (Bangkok = UTC+7)
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).AddDays(1));
        TestDbFactory.SeedBooking(db, tenantId, user.Id, service.Id,
            date:   tomorrow.ToString("yyyy-MM-dd"),
            status: BookingStatus.Confirmed);

        await job.Execute(MockContext());

        lineMock.Verify(l => l.SendReminderAsync(It.IsAny<Booking>()), Times.Once);
    }

    [Fact]
    public async Task Execute_AfterReminder_SetsReminderSentFlag()
    {
        var (job, lineMock, db, tenantId) = Setup(nameof(Execute_AfterReminder_SetsReminderSentFlag));
        var user    = TestDbFactory.SeedUser(db, tenantId);
        var service = TestDbFactory.SeedService(db, tenantId);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).AddDays(1));
        var booking  = TestDbFactory.SeedBooking(db, tenantId, user.Id, service.Id,
            date: tomorrow.ToString("yyyy-MM-dd"));

        // Simulate SendReminderAsync sets the flag (as the real impl does)
        lineMock.Setup(l => l.SendReminderAsync(It.IsAny<Booking>()))
                .Callback<Booking>(b => { b.ReminderSent = true; db.SaveChanges(); })
                .Returns(Task.CompletedTask);

        await job.Execute(MockContext());

        db.Bookings.Find(booking.Id)!.ReminderSent.Should().BeTrue();
    }

    // ── Should NOT send ───────────────────────────────────────────

    [Fact]
    public async Task Execute_BookingToday_DoesNotSend()
    {
        var (job, lineMock, db, tenantId) = Setup(nameof(Execute_BookingToday_DoesNotSend));
        var user    = TestDbFactory.SeedUser(db, tenantId);
        var service = TestDbFactory.SeedService(db, tenantId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        TestDbFactory.SeedBooking(db, tenantId, user.Id, service.Id,
            date: today.ToString("yyyy-MM-dd"));

        await job.Execute(MockContext());

        lineMock.Verify(l => l.SendReminderAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task Execute_AlreadySentReminder_DoesNotSendAgain()
    {
        var (job, lineMock, db, tenantId) = Setup(nameof(Execute_AlreadySentReminder_DoesNotSendAgain));
        var user    = TestDbFactory.SeedUser(db, tenantId);
        var service = TestDbFactory.SeedService(db, tenantId);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).AddDays(1));
        var booking  = TestDbFactory.SeedBooking(db, tenantId, user.Id, service.Id,
            date: tomorrow.ToString("yyyy-MM-dd"));

        // Mark already sent
        booking.ReminderSent = true;
        db.SaveChanges();

        await job.Execute(MockContext());

        lineMock.Verify(l => l.SendReminderAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task Execute_CancelledBooking_DoesNotSend()
    {
        var (job, lineMock, db, tenantId) = Setup(nameof(Execute_CancelledBooking_DoesNotSend));
        var user    = TestDbFactory.SeedUser(db, tenantId);
        var service = TestDbFactory.SeedService(db, tenantId);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).AddDays(1));
        TestDbFactory.SeedBooking(db, tenantId, user.Id, service.Id,
            date:   tomorrow.ToString("yyyy-MM-dd"),
            status: BookingStatus.Cancelled);

        await job.Execute(MockContext());

        lineMock.Verify(l => l.SendReminderAsync(It.IsAny<Booking>()), Times.Never);
    }

    // ── Resilience ────────────────────────────────────────────────

    [Fact]
    public async Task Execute_LineThrows_DoesNotCrashJob()
    {
        var (job, lineMock, db, tenantId) = Setup(nameof(Execute_LineThrows_DoesNotCrashJob));
        var user    = TestDbFactory.SeedUser(db, tenantId);
        var service = TestDbFactory.SeedService(db, tenantId);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).AddDays(1));
        TestDbFactory.SeedBooking(db, tenantId, user.Id, service.Id,
            date: tomorrow.ToString("yyyy-MM-dd"));

        lineMock.Setup(l => l.SendReminderAsync(It.IsAny<Booking>()))
                .ThrowsAsync(new HttpRequestException("LINE API down"));

        // Job should swallow the exception (catch per-booking)
        Func<Task> act = () => job.Execute(MockContext());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Execute_MultipleBookings_SendsAll()
    {
        var (job, lineMock, db, tenantId) = Setup(nameof(Execute_MultipleBookings_SendsAll));
        var service  = TestDbFactory.SeedService(db, tenantId);
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).AddDays(1));

        // 3 users, 3 bookings tomorrow
        for (var i = 0; i < 3; i++)
        {
            var u = TestDbFactory.SeedUser(db, tenantId, $"U_MULTI_{i}");
            TestDbFactory.SeedBooking(db, tenantId, u.Id, service.Id,
                date: tomorrow.ToString("yyyy-MM-dd"));
        }

        await job.Execute(MockContext());

        lineMock.Verify(l => l.SendReminderAsync(It.IsAny<Booking>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Execute_OneLineFails_OthersStillSent()
    {
        var (job, lineMock, db, tenantId) = Setup(nameof(Execute_OneLineFails_OthersStillSent));
        var service  = TestDbFactory.SeedService(db, tenantId);
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).AddDays(1));

        var u1 = TestDbFactory.SeedUser(db, tenantId, "U_FAIL");
        var u2 = TestDbFactory.SeedUser(db, tenantId, "U_OK");
        TestDbFactory.SeedBooking(db, tenantId, u1.Id, service.Id, date: tomorrow.ToString("yyyy-MM-dd"));
        TestDbFactory.SeedBooking(db, tenantId, u2.Id, service.Id, date: tomorrow.ToString("yyyy-MM-dd"));

        var callCount = 0;
        lineMock.Setup(l => l.SendReminderAsync(It.IsAny<Booking>()))
                .Returns<Booking>(_ =>
                {
                    callCount++;
                    if (callCount == 1) throw new HttpRequestException("fail first");
                    return Task.CompletedTask;
                });

        await job.Execute(MockContext());

        // Both attempted — second one succeeded despite first failing
        lineMock.Verify(l => l.SendReminderAsync(It.IsAny<Booking>()), Times.Exactly(2));
    }
}

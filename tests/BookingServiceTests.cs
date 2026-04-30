// ================================================================
// BookingServiceTests.cs  —  Unit tests for core booking logic
// ================================================================
using DiamondBooking.Models;
using DiamondBooking.Services;
using DiamondBooking.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DiamondBooking.Tests;

public class BookingServiceTests
{
    // ── Fixtures ──────────────────────────────────────────────────

    private static (BookingService svc, Guid tenantId, Guid serviceId) Setup(
        string dbName,
        int slotMaxBookings = 2)
    {
        var db      = TestDbFactory.Create(dbName);
        var tenant  = TestDbFactory.SeedTenant(db);
        var service = TestDbFactory.SeedService(db, tenant.Id);
        TestDbFactory.SeedTimeSlot(db, tenant.Id, maxBookings: slotMaxBookings);

        var lineMock = new Mock<ILineService>();
        lineMock.Setup(l => l.SendBookingConfirmationAsync(It.IsAny<Booking>()))
                .Returns(Task.CompletedTask);

        var svc = new BookingService(db, lineMock.Object, NullLogger<BookingService>.Instance);
        return (svc, tenant.Id, service.Id);
    }

    private static CreateBookingRequest MakeRequest(
        Guid serviceId,
        string lineUserId = "U999",
        string date       = "2025-06-02",   // Monday
        string start      = "10:00",
        string end        = "11:30") =>
        new(lineUserId, serviceId, DateOnly.Parse(date),
            TimeOnly.Parse(start), TimeOnly.Parse(end), null);

    // ── Happy path ────────────────────────────────────────────────

    [Fact]
    public async Task Create_NewUser_ReturnsSuccess()
    {
        var (svc, tenantId, serviceId) = Setup(nameof(Create_NewUser_ReturnsSuccess));

        var result = await svc.CreateAsync(tenantId, MakeRequest(serviceId));

        result.Success.Should().BeTrue();
        result.Message.Should().Be("จองสำเร็จ!");
        result.Booking.Should().NotBeNull();
        result.Booking!.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task Create_ExistingUser_ReusesSameUserRecord()
    {
        var db     = TestDbFactory.Create(nameof(Create_ExistingUser_ReusesSameUserRecord));
        var tenant = TestDbFactory.SeedTenant(db);
        var svc_   = TestDbFactory.SeedService(db, tenant.Id);
        TestDbFactory.SeedTimeSlot(db, tenant.Id);
        TestDbFactory.SeedUser(db, tenant.Id, "U_EXISTING");

        var lineMock = new Mock<ILineService>();
        lineMock.Setup(l => l.SendBookingConfirmationAsync(It.IsAny<Booking>()))
                .Returns(Task.CompletedTask);
        var svc = new BookingService(db, lineMock.Object, NullLogger<BookingService>.Instance);

        var req    = MakeRequest(svc_.Id, lineUserId: "U_EXISTING");
        var result = await svc.CreateAsync(tenant.Id, req);

        result.Success.Should().BeTrue();
        db.Users.Count(u => u.LineUserId == "U_EXISTING").Should().Be(1);  // no duplicate user
    }

    // ── Slot capacity (double-booking) ────────────────────────────

    [Fact]
    public async Task Create_WhenSlotFull_ReturnsFull()
    {
        // Slot max = 1, book twice with different users
        var (svc, tenantId, serviceId) = Setup(nameof(Create_WhenSlotFull_ReturnsFull), slotMaxBookings: 1);

        await svc.CreateAsync(tenantId, MakeRequest(serviceId, "U_A"));
        var result2 = await svc.CreateAsync(tenantId, MakeRequest(serviceId, "U_B"));

        result2.Success.Should().BeFalse();
        result2.Message.Should().Contain("เต็ม");
    }

    [Fact]
    public async Task Create_WhenSlotHasCapacity_AllowsMultipleBookings()
    {
        // Default max = 2
        var (svc, tenantId, serviceId) = Setup(nameof(Create_WhenSlotHasCapacity_AllowsMultipleBookings));

        var r1 = await svc.CreateAsync(tenantId, MakeRequest(serviceId, "U_A"));
        var r2 = await svc.CreateAsync(tenantId, MakeRequest(serviceId, "U_B"));

        r1.Success.Should().BeTrue();
        r2.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Create_SameUser_SameTime_ReturnsConflict()
    {
        var (svc, tenantId, serviceId) = Setup(nameof(Create_SameUser_SameTime_ReturnsConflict));

        await svc.CreateAsync(tenantId, MakeRequest(serviceId, "U_SAME"));
        var result2 = await svc.CreateAsync(tenantId, MakeRequest(serviceId, "U_SAME"));

        result2.Success.Should().BeFalse();
        result2.Message.Should().Contain("มีการจองในช่วงเวลานี้อยู่แล้ว");
    }

    [Fact]
    public async Task Create_CancelledBookingDoesNotBlockSlot()
    {
        var db     = TestDbFactory.Create(nameof(Create_CancelledBookingDoesNotBlockSlot));
        var tenant = TestDbFactory.SeedTenant(db);
        var svc_   = TestDbFactory.SeedService(db, tenant.Id);
        TestDbFactory.SeedTimeSlot(db, tenant.Id, maxBookings: 1);
        var user   = TestDbFactory.SeedUser(db, tenant.Id, "U_CANCEL");

        // Seed a cancelled booking for this slot
        TestDbFactory.SeedBooking(db, tenant.Id, user.Id, svc_.Id,
            status: BookingStatus.Cancelled);

        var lineMock = new Mock<ILineService>();
        lineMock.Setup(l => l.SendBookingConfirmationAsync(It.IsAny<Booking>()))
                .Returns(Task.CompletedTask);
        var svc    = new BookingService(db, lineMock.Object, NullLogger<BookingService>.Instance);

        // Same slot should be bookable again
        var result = await svc.CreateAsync(tenant.Id, MakeRequest(svc_.Id, "U_NEW"));

        result.Success.Should().BeTrue();
    }

    // ── Service validation ─────────────────────────────────────────

    [Fact]
    public async Task Create_UnknownServiceId_ReturnsFail()
    {
        var (svc, tenantId, _) = Setup(nameof(Create_UnknownServiceId_ReturnsFail));

        var result = await svc.CreateAsync(tenantId, MakeRequest(Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("ไม่พบบริการ");
    }

    [Fact]
    public async Task Create_ServiceFromOtherTenant_ReturnsFail()
    {
        var db      = TestDbFactory.Create(nameof(Create_ServiceFromOtherTenant_ReturnsFail));
        var tenant1 = TestDbFactory.SeedTenant(db, "tenant-1");
        var tenant2 = TestDbFactory.SeedTenant(db, "tenant-2");

        // Service belongs to tenant2
        var svc2 = TestDbFactory.SeedService(db, tenant2.Id);
        TestDbFactory.SeedTimeSlot(db, tenant1.Id);

        var lineMock = new Mock<ILineService>();
        lineMock.Setup(l => l.SendBookingConfirmationAsync(It.IsAny<Booking>()))
                .Returns(Task.CompletedTask);
        var svc = new BookingService(db, lineMock.Object, NullLogger<BookingService>.Instance);

        // Try booking tenant1 with tenant2's service
        var result = await svc.CreateAsync(tenant1.Id, MakeRequest(svc2.Id));

        result.Success.Should().BeFalse();
    }

    // ── Cancel ────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_ExistingBooking_SetsStatusCancelled()
    {
        var db     = TestDbFactory.Create(nameof(Cancel_ExistingBooking_SetsStatusCancelled));
        var tenant = TestDbFactory.SeedTenant(db);
        var user   = TestDbFactory.SeedUser(db, tenant.Id);
        var svc_   = TestDbFactory.SeedService(db, tenant.Id);
        TestDbFactory.SeedTimeSlot(db, tenant.Id);
        var booking = TestDbFactory.SeedBooking(db, tenant.Id, user.Id, svc_.Id);

        var lineMock = new Mock<ILineService>();
        lineMock.Setup(l => l.SendTextAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
        var svc = new BookingService(db, lineMock.Object, NullLogger<BookingService>.Instance);

        var ok = await svc.CancelAsync(tenant.Id, booking.Id, "ทดสอบ");

        ok.Should().BeTrue();
        db.Bookings.Find(booking.Id)!.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_ReturnsFalse()
    {
        var db     = TestDbFactory.Create(nameof(Cancel_AlreadyCancelled_ReturnsFalse));
        var tenant = TestDbFactory.SeedTenant(db);
        var user   = TestDbFactory.SeedUser(db, tenant.Id);
        var booking = TestDbFactory.SeedBooking(db, tenant.Id, user.Id,
            status: BookingStatus.Cancelled);

        var lineMock = new Mock<ILineService>();
        var svc = new BookingService(db, lineMock.Object, NullLogger<BookingService>.Instance);

        var ok = await svc.CancelAsync(tenant.Id, booking.Id);

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task Cancel_NotFound_ReturnsFalse()
    {
        var (svc, tenantId, _) = Setup(nameof(Cancel_NotFound_ReturnsFalse));

        var ok = await svc.CancelAsync(tenantId, Guid.NewGuid());

        ok.Should().BeFalse();
    }

    // ── Available slots ───────────────────────────────────────────

    [Fact]
    public async Task GetAvailableSlots_NoBookings_AllAvailable()
    {
        var db     = TestDbFactory.Create(nameof(GetAvailableSlots_NoBookings_AllAvailable));
        var tenant = TestDbFactory.SeedTenant(db);
        TestDbFactory.SeedTimeSlot(db, tenant.Id, dayOfWeek: 1, maxBookings: 2);   // Monday
        TestDbFactory.SeedTimeSlot(db, tenant.Id, dayOfWeek: 1, "14:00", "15:30", maxBookings: 2);

        var lineMock = new Mock<ILineService>();
        var svc = new BookingService(db, lineMock.Object, NullLogger<BookingService>.Instance);

        var monday = new DateOnly(2025, 6, 2);   // known Monday
        var slots  = await svc.GetAvailableSlotsAsync(tenant.Id, monday);

        slots.Should().HaveCount(2);
        slots.Should().AllSatisfy(s => s.Available.Should().BeTrue());
        slots.Should().AllSatisfy(s => s.Remaining.Should().Be(2));
    }

    [Fact]
    public async Task GetAvailableSlots_FullyBooked_ShowsUnavailable()
    {
        var db      = TestDbFactory.Create(nameof(GetAvailableSlots_FullyBooked_ShowsUnavailable));
        var tenant  = TestDbFactory.SeedTenant(db);
        var user    = TestDbFactory.SeedUser(db, tenant.Id);
        TestDbFactory.SeedTimeSlot(db, tenant.Id, dayOfWeek: 1, maxBookings: 1);
        TestDbFactory.SeedBooking(db, tenant.Id, user.Id);   // fills the slot

        var lineMock = new Mock<ILineService>();
        var svc = new BookingService(db, lineMock.Object, NullLogger<BookingService>.Instance);

        var monday = new DateOnly(2025, 6, 2);
        var slots  = await svc.GetAvailableSlotsAsync(tenant.Id, monday);

        slots.Should().HaveCount(1);
        slots[0].Available.Should().BeFalse();
        slots[0].Remaining.Should().Be(0);
    }
}

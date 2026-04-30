// ================================================================
// Services/BookingService.cs  —  Core booking logic
// ================================================================
using DiamondBooking.Data;
using DiamondBooking.Models;
using Microsoft.EntityFrameworkCore;

namespace DiamondBooking.Services;

public record CreateBookingRequest(
    string    LineUserId,
    Guid      ServiceId,
    DateOnly  BookingDate,
    TimeOnly  StartTime,
    TimeOnly  EndTime,
    string?   Note
);

public record BookingResult(bool Success, string Message, Booking? Booking);

public class BookingService(AppDbContext db, ILineService line, ILogger<BookingService> log)
{
    // ------------------------------------------------------------
    // Create booking with double-booking check
    // ------------------------------------------------------------
    public async Task<BookingResult> CreateAsync(Guid tenantId, CreateBookingRequest req)
    {
        // 1. Find or create user
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.LineUserId == req.LineUserId);

        if (user == null)
        {
            user = new User { TenantId = tenantId, LineUserId = req.LineUserId };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        // 2. Validate service exists
        var service = await db.Services
            .FirstOrDefaultAsync(s => s.Id == req.ServiceId && s.TenantId == tenantId && s.IsActive);

        if (service == null)
            return new(false, "ไม่พบบริการที่เลือก", null);

        // 3. Check slot capacity (count active bookings overlapping this time)
        var overlaps = await db.Bookings
            .Where(b =>
                b.TenantId    == tenantId &&
                b.BookingDate == req.BookingDate &&
                b.Status      != BookingStatus.Cancelled &&
                b.StartTime   < req.EndTime &&
                b.EndTime     > req.StartTime)
            .CountAsync();

        // Get max_bookings for this slot
        var dayOfWeek = (int)req.BookingDate.DayOfWeek;
        var slot = await db.TimeSlots
            .Where(s =>
                s.TenantId  == tenantId &&
                s.IsActive &&
                (s.DayOfWeek == dayOfWeek || s.SpecificDate == req.BookingDate) &&
                s.StartTime  == req.StartTime)
            .FirstOrDefaultAsync();

        var maxBookings = slot?.MaxBookings ?? 1;

        if (overlaps >= maxBookings)
            return new(false, "ช่วงเวลานี้เต็มแล้ว กรุณาเลือกเวลาอื่น", null);

        // 4. Check user doesn't already have a booking at same time
        var userConflict = await db.Bookings
            .AnyAsync(b =>
                b.TenantId    == tenantId &&
                b.UserId      == user.Id &&
                b.BookingDate == req.BookingDate &&
                b.Status      != BookingStatus.Cancelled &&
                b.StartTime   < req.EndTime &&
                b.EndTime     > req.StartTime);

        if (userConflict)
            return new(false, "คุณมีการจองในช่วงเวลานี้อยู่แล้ว", null);

        // 5. Create booking
        var booking = new Booking
        {
            TenantId    = tenantId,
            UserId      = user.Id,
            ServiceId   = service.Id,
            BookingDate = req.BookingDate,
            StartTime   = req.StartTime,
            EndTime     = req.EndTime,
            Status      = BookingStatus.Confirmed,
            Note        = req.Note
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        // 6. Load relations for notification
        booking.User    = user;
        booking.Service = service;

        // 7. Send LINE confirmation (fire & forget — don't fail the booking if LINE is slow)
        _ = Task.Run(async () =>
        {
            try { await line.SendBookingConfirmationAsync(booking); }
            catch (Exception ex) { log.LogWarning(ex, "LINE notification failed for booking {Id}", booking.Id); }
        });

        log.LogInformation("Booking {Id} created for tenant {Tenant}", booking.Id, tenantId);
        return new(true, "จองสำเร็จ!", booking);
    }

    // ------------------------------------------------------------
    // Cancel booking
    // ------------------------------------------------------------
    public async Task<bool> CancelAsync(Guid tenantId, Guid bookingId, string? reason = null)
    {
        var booking = await db.Bookings
            .Include(b => b.User)
            .Include(b => b.Service)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.TenantId == tenantId);

        if (booking == null || booking.Status == BookingStatus.Cancelled) return false;

        booking.Status    = BookingStatus.Cancelled;
        booking.AdminNote = reason;
        booking.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Notify user via LINE
        await line.SendTextAsync(
            tenantId,
            booking.User.LineUserId,
            $"❌ ยกเลิกการจองแล้ว\n📅 {booking.BookingDate:dd MMM} ⏰ {booking.StartTime:hh\\:mm}\n" +
            (reason != null ? $"เหตุผล: {reason}" : "สอบถามเพิ่มเติมได้เลยค่ะ"));

        return true;
    }

    // ------------------------------------------------------------
    // Get available time slots for a given date
    // ------------------------------------------------------------
    public async Task<List<SlotInfo>> GetAvailableSlotsAsync(Guid tenantId, DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;

        var slots = await db.TimeSlots
            .Where(s =>
                s.TenantId == tenantId &&
                s.IsActive &&
                (s.DayOfWeek == dayOfWeek || s.SpecificDate == date))
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        var results = new List<SlotInfo>();
        foreach (var slot in slots)
        {
            var booked = await db.Bookings
                .CountAsync(b =>
                    b.TenantId    == tenantId &&
                    b.BookingDate == date &&
                    b.Status      != BookingStatus.Cancelled &&
                    b.StartTime   == slot.StartTime);

            results.Add(new SlotInfo(
                slot.StartTime,
                slot.EndTime,
                slot.MaxBookings - booked,
                booked < slot.MaxBookings
            ));
        }
        return results;
    }
}

public record SlotInfo(TimeOnly Start, TimeOnly End, int Remaining, bool Available);

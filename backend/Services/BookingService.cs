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
    int       DurationMinutes,
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

        // 3. Get max concurrent capacity for this day
        var dayOfWeek = (int)req.BookingDate.DayOfWeek;
        var opSlot = await db.TimeSlots
            .Where(s =>
                s.TenantId == tenantId &&
                s.IsActive &&
                (s.DayOfWeek == dayOfWeek || s.SpecificDate == req.BookingDate))
            .FirstOrDefaultAsync();

        var maxBookings = opSlot?.MaxBookings ?? 1;

        // Count bookings that overlap with requested time range
        var overlaps = await db.Bookings
            .CountAsync(b =>
                b.TenantId    == tenantId &&
                b.BookingDate == req.BookingDate &&
                b.Status      != BookingStatus.Cancelled &&
                b.StartTime   < req.EndTime &&
                b.EndTime     > req.StartTime);

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
            TenantId        = tenantId,
            UserId          = user.Id,
            ServiceId       = service.Id,
            BookingDate     = req.BookingDate,
            StartTime       = req.StartTime,
            EndTime         = req.EndTime,
            DurationMinutes = req.DurationMinutes,
            Status          = BookingStatus.Confirmed,
            Note            = req.Note
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
            $"❌ ยกเลิกการจองแล้ว\n📅 {booking.BookingDate:dd MMM} ⏰ {booking.StartTime:HH\\:mm}\n" +
            (reason != null ? $"เหตุผล: {reason}" : "สอบถามเพิ่มเติมได้เลยค่ะ"));

        return true;
    }

    // ------------------------------------------------------------
    // Get available start times for a given date + duration
    // ------------------------------------------------------------
    public async Task<List<SlotInfo>> GetAvailableSlotsAsync(Guid tenantId, DateOnly date, int durationMinutes)
    {
        var dayOfWeek = (int)date.DayOfWeek;

        // Get operating hours for this day (1 row per day)
        var opSlot = await db.TimeSlots
            .Where(s =>
                s.TenantId == tenantId &&
                s.IsActive &&
                (s.DayOfWeek == dayOfWeek || s.SpecificDate == date))
            .FirstOrDefaultAsync();

        if (opSlot == null) return [];

        var maxConcurrent = opSlot.MaxBookings;
        var results       = new List<SlotInfo>();

        // For today: only show slots starting from now (Bangkok = UTC+7)
        var todayBangkok = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var nowBangkok   = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var minStart     = date == todayBangkok ? nowBangkok : TimeOnly.MinValue;

        var current     = opSlot.StartTime;
        var latestStart = opSlot.EndTime.AddMinutes(-durationMinutes);

        while (current <= latestStart)
        {
            if (current >= minStart)
            {
                var end = current.AddMinutes(durationMinutes);

                var overlaps = await db.Bookings
                    .CountAsync(b =>
                        b.TenantId    == tenantId &&
                        b.BookingDate == date &&
                        b.Status      != BookingStatus.Cancelled &&
                        b.StartTime   < end &&
                        b.EndTime     > current);

                results.Add(new SlotInfo(
                    current,
                    end,
                    maxConcurrent - overlaps,
                    overlaps < maxConcurrent
                ));
            }

            current = current.AddMinutes(durationMinutes);
        }

        return results;
    }
}

public record SlotInfo(TimeOnly Start, TimeOnly End, int Remaining, bool Available);

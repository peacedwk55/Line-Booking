// ================================================================
// Controllers/BookingController.cs  —  LIFF Booking API
// ================================================================
using DiamondBooking.Data;
using DiamondBooking.Models;
using DiamondBooking.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiamondBooking.Controllers;

[ApiController]
[Route("api/{tenantSlug}/bookings")]
public class BookingController(AppDbContext db, BookingService svc) : ControllerBase
{
    // GET /api/diamond-massage/bookings/slots?date=2025-02-01&duration=60
    [HttpGet("slots")]
    public async Task<IActionResult> GetSlots(
        [FromRoute] string tenantSlug,
        [FromQuery] string date,
        [FromQuery] int    duration = 60)
    {
        var tenant = await ResolveTenant(tenantSlug);
        if (tenant == null) return NotFound("Tenant not found");

        if (!DateOnly.TryParse(date, out var parsedDate))
            return BadRequest("Invalid date format. Use YYYY-MM-DD");

        if (duration is not (60 or 90 or 120))
            return BadRequest("duration must be 60, 90, or 120");

        var slots = await svc.GetAvailableSlotsAsync(tenant.Id, parsedDate, duration);
        return Ok(slots.Select(s => new
        {
            start     = s.Start.ToString("HH:mm"),
            end       = s.End.ToString("HH:mm"),
            available = s.Available,
            remaining = s.Remaining
        }));
    }

    // POST /api/diamond-massage/bookings
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromRoute] string tenantSlug,
        [FromBody]  CreateBookingDto dto)
    {
        var tenant = await ResolveTenant(tenantSlug);
        if (tenant == null) return NotFound("Tenant not found");

        // Update display name / picture if provided
        var user = await db.Users.FirstOrDefaultAsync(u =>
            u.TenantId == tenant.Id && u.LineUserId == dto.LineUserId);
        if (user != null)
        {
            if (dto.DisplayName != null) user.DisplayName = dto.DisplayName;
            if (dto.PictureUrl  != null) user.PictureUrl  = dto.PictureUrl;
            await db.SaveChangesAsync();
        }

        var req = new CreateBookingRequest(
            dto.LineUserId,
            dto.ServiceId,
            DateOnly.Parse(dto.Date),
            TimeOnly.Parse(dto.StartTime),
            TimeOnly.Parse(dto.EndTime),
            dto.DurationMinutes,
            dto.Note,
            dto.ContactName,
            dto.ContactPhone
        );

        var result = await svc.CreateAsync(tenant.Id, req);
        if (!result.Success) return BadRequest(new { message = result.Message });

        return Ok(new
        {
            message   = result.Message,
            bookingId = result.Booking!.Id,
            date      = result.Booking.BookingDate.ToString("yyyy-MM-dd"),
            startTime = result.Booking.StartTime.ToString("HH:mm"),
            endTime   = result.Booking.EndTime.ToString("HH:mm"),
            status    = result.Booking.Status.ToString()
        });
    }

    // GET /api/diamond-massage/bookings?lineUserId=Uxxxx
    [HttpGet]
    public async Task<IActionResult> GetUserBookings(
        [FromRoute] string tenantSlug,
        [FromQuery] string lineUserId)
    {
        var tenant = await ResolveTenant(tenantSlug);
        if (tenant == null) return NotFound();

        var bookings = await db.Bookings
            .Include(b => b.Service)
            .Include(b => b.User)
            .Where(b => b.TenantId == tenant.Id && b.User.LineUserId == lineUserId)
            .OrderByDescending(b => b.BookingDate).ThenBy(b => b.StartTime)
            .Select(b => new
            {
                id          = b.Id,
                date        = b.BookingDate.ToString("yyyy-MM-dd"),
                startTime   = b.StartTime.ToString("HH:mm"),
                endTime     = b.EndTime.ToString("HH:mm"),
                service     = b.Service != null ? b.Service.Name : null,
                status      = b.Status.ToString(),
                createdAt   = b.CreatedAt
            })
            .ToListAsync();

        return Ok(bookings);
    }

    // DELETE /api/diamond-massage/bookings/{id}?lineUserId=Uxxxx
    [HttpDelete("{id}")]
    public async Task<IActionResult> Cancel(
        [FromRoute] string tenantSlug,
        [FromRoute] Guid   id,
        [FromQuery] string lineUserId)
    {
        var tenant = await ResolveTenant(tenantSlug);
        if (tenant == null) return NotFound();

        // Only owner can cancel their own booking
        var booking = await db.Bookings
            .Include(b => b.User)
            .FirstOrDefaultAsync(b =>
                b.Id == id &&
                b.TenantId == tenant.Id &&
                b.User.LineUserId == lineUserId);

        if (booking == null) return NotFound("Booking not found");
        if (booking.Status == BookingStatus.Cancelled)
            return BadRequest(new { message = "Booking already cancelled" });

        // Prevent cancellation within 2 hours
        var bookingDateTime = booking.BookingDate.ToDateTime(booking.StartTime);
        if (bookingDateTime - DateTime.UtcNow.AddHours(7) < TimeSpan.FromHours(2))
            return BadRequest(new { message = "ไม่สามารถยกเลิกได้ภายใน 2 ชั่วโมงก่อนนัด" });

        await svc.CancelAsync(tenant.Id, id, "ยกเลิกโดยผู้ใช้");
        return Ok(new { message = "ยกเลิกสำเร็จ" });
    }

    private async Task<Tenant?> ResolveTenant(string slug) =>
        await db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive);
}

public record CreateBookingDto(
    string  LineUserId,
    string? DisplayName,
    string? PictureUrl,
    Guid    ServiceId,
    string  Date,
    string  StartTime,
    string  EndTime,
    int     DurationMinutes,
    string? Note,
    string? ContactName,
    string? ContactPhone
);

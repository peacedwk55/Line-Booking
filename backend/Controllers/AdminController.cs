// ================================================================
// Controllers/AdminController.cs  —  Simple admin panel API
// ================================================================
using DiamondBooking.Data;
using DiamondBooking.Models;
using DiamondBooking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DiamondBooking.Controllers;

[Authorize]
[ApiController]
[Route("api/{tenantSlug}/admin")]
public class AdminController(AppDbContext db, BookingService svc, IConfiguration config) : ControllerBase
{
    // POST /api/diamond-massage/admin/login
    [AllowAnonymous]
    [HttpPost("login")]
    public IActionResult Login([FromRoute] string tenantSlug, [FromBody] LoginDto dto)
    {
        var adminPassword = config["ADMIN_PASSWORD"] ?? "diamond2026";
        if (dto.Password != adminPassword)
            return Unauthorized(new { message = "รหัสผ่านไม่ถูกต้อง" });

        var secret = config["JWT_SECRET"] ?? "diamond-massage-jwt-secret-key-2026-demo";
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var token  = new JwtSecurityToken(
            claims:            [new Claim("slug", tenantSlug)],
            expires:           DateTime.UtcNow.AddDays(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
    }


    // GET /api/diamond-massage/admin/bookings?date=2025-02-01&status=confirmed
    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookings(
        [FromRoute] string  tenantSlug,
        [FromQuery] string? date,
        [FromQuery] string? status)
    {
        var tenant = await ResolveTenant(tenantSlug);
        if (tenant == null) return NotFound();

        var query = db.Bookings
            .Include(b => b.User)
            .Include(b => b.Service)
            .Where(b => b.TenantId == tenant.Id);

        if (DateOnly.TryParse(date, out var d))
            query = query.Where(b => b.BookingDate == d);

        if (Enum.TryParse<BookingStatus>(status, true, out var s))
            query = query.Where(b => b.Status == s);

        var bookings = await query
            .OrderBy(b => b.BookingDate).ThenBy(b => b.StartTime)
            .Select(b => new
            {
                id          = b.Id,
                date        = b.BookingDate.ToString("yyyy-MM-dd"),
                startTime   = b.StartTime.ToString("HH:mm"),
                endTime     = b.EndTime.ToString("HH:mm"),
                status      = b.Status.ToString(),
                service     = b.Service != null ? b.Service.Name : "-",
                userName    = b.User.DisplayName ?? b.User.LineUserId,
                userPhone   = b.User.Phone,
                note        = b.Note,
                adminNote   = b.AdminNote,
                contactName = b.ContactName,
                contactPhone= b.ContactPhone,
                createdAt   = b.CreatedAt
            })
            .ToListAsync();

        return Ok(bookings);
    }

    // PATCH or POST /api/diamond-massage/admin/bookings/{id}
    [HttpPatch("bookings/{id}")]
    [HttpPost("bookings/{id}")]
    public async Task<IActionResult> Update(
        [FromRoute] string          tenantSlug,
        [FromRoute] Guid            id,
        [FromBody]  UpdateBookingDto dto)
    {
        var tenant = await ResolveTenant(tenantSlug);
        if (tenant == null) return NotFound();

        var booking = await db.Bookings
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenant.Id);

        if (booking == null) return NotFound();

        if (dto.Status != null && Enum.TryParse<BookingStatus>(dto.Status, true, out var newStatus))
            booking.Status = newStatus;

        if (dto.AdminNote != null)
            booking.AdminNote = dto.AdminNote;

        booking.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "Updated" });
    }

    // DELETE /api/diamond-massage/admin/bookings/{id}
    [HttpDelete("bookings/{id}")]
    public async Task<IActionResult> Cancel(
        [FromRoute] string  tenantSlug,
        [FromRoute] Guid    id,
        [FromQuery] string? reason)
    {
        var tenant = await ResolveTenant(tenantSlug);
        if (tenant == null) return NotFound();

        var ok = await svc.CancelAsync(tenant.Id, id, reason ?? "ยกเลิกโดย Admin");
        return ok ? Ok(new { message = "Cancelled" }) : NotFound();
    }

    // GET /api/diamond-massage/admin/users
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromRoute] string tenantSlug)
    {
        var tenant = await ResolveTenant(tenantSlug);
        if (tenant == null) return NotFound();

        var users = await db.Users
            .Where(u => u.TenantId == tenant.Id)
            .Select(u => new
            {
                u.Id,
                u.LineUserId,
                u.DisplayName,
                u.Phone,
                u.CreatedAt,
                BookingCount = db.Bookings.Count(b => b.UserId == u.Id)
            })
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        return Ok(users);
    }

    // GET /api/diamond-massage/admin/slots
    [HttpGet("slots")]
    public async Task<IActionResult> GetSlots([FromRoute] string tenantSlug)
    {
        var tenant = await ResolveTenant(tenantSlug);
        if (tenant == null) return NotFound();

        var slots = await db.TimeSlots
            .Where(s => s.TenantId == tenant.Id && s.IsActive)
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
            .Select(s => new {
                id          = s.Id,
                dayOfWeek   = s.DayOfWeek,
                startTime   = s.StartTime.ToString("HH:mm"),
                endTime     = s.EndTime.ToString("HH:mm"),
                maxBookings = s.MaxBookings
            })
            .ToListAsync();

        return Ok(slots);
    }

    // POST /api/diamond-massage/admin/slots/bulk
    [HttpPost("slots/bulk")]
    public async Task<IActionResult> BulkUpdateSlots(
        [FromRoute] string     tenantSlug,
        [FromBody]  UpdateSlotDto dto)
    {
        var tenant = await ResolveTenant(tenantSlug);
        if (tenant == null) return NotFound();

        if (dto.MaxBookings is null or <= 0)
            return BadRequest(new { message = "MaxBookings must be > 0" });

        await db.TimeSlots
            .Where(s => s.TenantId == tenant.Id && s.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.MaxBookings, dto.MaxBookings.Value));

        return Ok(new { message = "Updated all slots" });
    }

    // POST /api/diamond-massage/admin/slots/{id}
    [HttpPost("slots/{id}")]
    public async Task<IActionResult> UpdateSlot(
        [FromRoute] string     tenantSlug,
        [FromRoute] Guid       id,
        [FromBody]  UpdateSlotDto dto)
    {
        var tenant = await ResolveTenant(tenantSlug);
        if (tenant == null) return NotFound();

        var slot = await db.TimeSlots
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenant.Id);
        if (slot == null) return NotFound();

        if (dto.MaxBookings is > 0)
            slot.MaxBookings = dto.MaxBookings.Value;

        await db.SaveChangesAsync();
        return Ok(new { message = "Updated" });
    }

    // GET /api/diamond-massage/admin/dashboard
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromRoute] string tenantSlug)
    {
        var tenant = await ResolveTenant(tenantSlug);
        if (tenant == null) return NotFound();

        var today    = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var thisWeek = today.AddDays(-(int)today.DayOfWeek);

        var stats = new
        {
            todayBookings = await db.Bookings.CountAsync(b =>
                b.TenantId == tenant.Id && b.BookingDate == today && b.Status != BookingStatus.Cancelled),
            weekBookings = await db.Bookings.CountAsync(b =>
                b.TenantId == tenant.Id && b.BookingDate >= thisWeek && b.Status != BookingStatus.Cancelled),
            totalUsers = await db.Users.CountAsync(u => u.TenantId == tenant.Id),
            pendingToday = await db.Bookings.CountAsync(b =>
                b.TenantId == tenant.Id && b.BookingDate == today && b.Status == BookingStatus.Confirmed)
        };

        return Ok(stats);
    }

    private async Task<Tenant?> ResolveTenant(string slug) =>
        await db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive);
}

public record LoginDto(string Password);
public record UpdateBookingDto(string? Status, string? AdminNote);
public record UpdateSlotDto(int? MaxBookings);

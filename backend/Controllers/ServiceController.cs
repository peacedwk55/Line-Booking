// ================================================================
// Controllers/ServiceController.cs  —  Services list for LIFF
// ================================================================
using DiamondBooking.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiamondBooking.Controllers;

[ApiController]
[Route("api/{tenantSlug}/services")]
public class ServiceController(AppDbContext db) : ControllerBase
{
    // GET /api/diamond-massage/services
    [HttpGet]
    public async Task<IActionResult> GetAll([FromRoute] string tenantSlug)
    {
        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.Slug == tenantSlug && t.IsActive);
        if (tenant == null) return NotFound();

        var services = await db.Services
            .Where(s => s.TenantId == tenant.Id && s.IsActive)
            .OrderBy(s => s.SortOrder)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Description,
                s.DurationMinutes,
                s.Price
            })
            .ToListAsync();

        return Ok(services);
    }
}

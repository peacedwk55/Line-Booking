// ================================================================
// Controllers/WebhookController.cs  —  LINE webhook endpoint
// ================================================================
using System.Text.Json;
using DiamondBooking.Data;
using DiamondBooking.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiamondBooking.Controllers;

[ApiController]
[Route("webhook/{tenantSlug}")]
public class WebhookController(AppDbContext db, ILineService line, ILogger<WebhookController> log) : ControllerBase
{
    // POST /webhook/diamond-massage
    [HttpPost]
    public async Task<IActionResult> Handle(
        [FromRoute] string tenantSlug,
        [FromHeader(Name = "X-Line-Signature")] string? signature)
    {
        // 1. Read raw body (needed for signature check)
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        // 2. Resolve tenant
        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.Slug == tenantSlug && t.IsActive);

        if (tenant == null) return NotFound();

        // 3. Verify signature
        if (string.IsNullOrEmpty(signature) ||
            !line.VerifySignature(body, signature, tenant.LineChannelSecret))
        {
            log.LogWarning("Invalid LINE signature for tenant {Slug}", tenantSlug);
            return Unauthorized();
        }

        // 4. Parse and handle events
        try
        {
            var root = JsonSerializer.Deserialize<JsonElement>(body);
            await line.HandleWebhookAsync(tenant.Id, root);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Webhook handling error for {Slug}", tenantSlug);
        }

        // LINE requires 200 OK even if processing fails
        return Ok();
    }
}

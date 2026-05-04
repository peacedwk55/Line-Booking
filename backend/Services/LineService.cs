// ================================================================
// Services/LineService.cs  —  LINE Messaging API integration
// ================================================================
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DiamondBooking.Data;
using DiamondBooking.Models;
using Microsoft.EntityFrameworkCore;

namespace DiamondBooking.Services;

public class LineService(AppDbContext db, IHttpClientFactory http, ILogger<LineService> log) : ILineService
{
    // ------------------------------------------------------------
    // Webhook signature verification
    // ------------------------------------------------------------
    public bool VerifySignature(string body, string signature, string channelSecret)
    {
        var key  = Encoding.UTF8.GetBytes(channelSecret);
        var data = Encoding.UTF8.GetBytes(body);
        var hash = Convert.ToBase64String(new HMACSHA256(key).ComputeHash(data));
        return hash == signature;
    }

    // ------------------------------------------------------------
    // Handle incoming LINE events (follow, message)
    // ------------------------------------------------------------
    public async Task HandleWebhookAsync(Guid tenantId, JsonElement root)
    {
        var events = root.GetProperty("events");
        foreach (var ev in events.EnumerateArray())
        {
            var type   = ev.GetProperty("type").GetString();
            var source = ev.GetProperty("source");
            var lineId = source.GetProperty("userId").GetString()!;

            // Upsert user on first contact
            var user = await db.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.LineUserId == lineId);

            if (user == null)
            {
                user = new User { TenantId = tenantId, LineUserId = lineId };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }

            if (type == "follow")
            {
                await SendTextAsync(tenantId, lineId,
                    "🌸 ยินดีต้อนรับสู่ Diamond Massage!\nกด 'จองนัด' จาก Rich Menu เพื่อเริ่มจองได้เลยค่ะ ✨");
            }
            else if (type == "message")
            {
                var msgType = ev.GetProperty("message").GetProperty("type").GetString();
                if (msgType != "text") continue;

                var text = ev.GetProperty("message").GetProperty("text").GetString()?.Trim().ToLower() ?? "";
                await HandleTextMessageAsync(tenantId, lineId, text);
            }
        }
    }

    // ------------------------------------------------------------
    // Route incoming text message to appropriate reply
    // ------------------------------------------------------------
    private async Task HandleTextMessageAsync(Guid tenantId, string lineUserId, string text)
    {
        // Booking intent: จอง / นัด / เวลา / ว่าง / book
        var bookingKeywords = new[] { "จองนัด", "จอง", "นัด", "เวลา", "ว่าง", "book", "booking", "นัดหมาย", "คิว" };
        if (bookingKeywords.Any(k => text.Contains(k)))
        {
            await SendLiffLinkAsync(tenantId, lineUserId);
            return;
        }

        // Greeting: สวัสดี / หวัดดี / hello / hi / ดีค่ะ / ดีครับ
        var greetKeywords = new[] { "สวัสดี", "หวัดดี", "hello", "hi", "ดีครับ", "ดีค่ะ", "ดีนะ", "ไง" };
        if (greetKeywords.Any(k => text.Contains(k)))
        {
            await SendTextAsync(tenantId, lineUserId,
                "🌸 สวัสดีค่ะ ยินดีต้อนรับสู่ Diamond Massage!\n\n" +
                "เรามีบริการนวดหลายแบบค่ะ สนใจนัดหมายได้เลยนะคะ 😊\n\n" +
                "💬 พิมพ์ 'บริการ' เพื่อดูรายการและราคา\n" +
                "📅 พิมพ์ 'จอง' เพื่อจองนัดหมายได้เลยค่ะ");
            return;
        }

        // Service / price inquiry: บริการ / ราคา / นวด / มีอะไร
        var serviceKeywords = new[] { "บริการ", "ราคา", "นวด", "มีอะไร", "มีอะไรบ้าง", "แนะนำ", "price" };
        if (serviceKeywords.Any(k => text.Contains(k)))
        {
            var services = await db.Services
                .Where(s => s.TenantId == tenantId)
                .OrderBy(s => s.SortOrder)
                .ToListAsync();

            var lines = services.Select(s =>
                $"• {s.Name} — {s.PricePerHour} บ./ชม.\n  {s.Description}");

            await SendTextAsync(tenantId, lineUserId,
                "💆 บริการของเราค่ะ\n\n" +
                string.Join("\n\n", lines) +
                "\n\n📅 สนใจนัดได้เลยนะคะ พิมพ์ 'จอง' ได้เลยค่ะ 🌸");
            return;
        }

        // Fallback
        await SendTextAsync(tenantId, lineUserId,
            "🌸 ขอบคุณที่ทักมาค่ะ\n\n" +
            "📅 พิมพ์ 'จอง' — เพื่อจองนัดหมาย\n" +
            "💆 พิมพ์ 'บริการ' — เพื่อดูรายการและราคา");
    }

    // ------------------------------------------------------------
    // Send a simple text message
    // ------------------------------------------------------------
    public async Task SendTextAsync(Guid tenantId, string lineUserId, string text)
    {
        var tenant = await db.Tenants.FindAsync(tenantId);
        if (tenant == null) return;

        var payload = new
        {
            to = lineUserId,
            messages = new[] { new { type = "text", text } }
        };
        await PostToLineAsync(tenant.LineAccessToken, "/v2/bot/message/push", payload);
        log.LogInformation("LINE text sent to {UserId}", lineUserId);
    }

    // ------------------------------------------------------------
    // Send booking confirmation (flex message)
    // ------------------------------------------------------------
    public async Task SendBookingConfirmationAsync(Booking booking)
    {
        var tenant = await db.Tenants.FindAsync(booking.TenantId);
        var user   = await db.Users.FindAsync(booking.UserId);
        if (tenant == null || user == null) return;

        var dateStr  = booking.BookingDate.ToString("dd MMM yyyy");
        var timeStr  = $"{booking.StartTime:HH\\:mm} – {booking.EndTime:HH\\:mm}";
        var service  = booking.Service?.Name ?? "บริการ";

        var flex = BuildConfirmationFlex(dateStr, timeStr, service, booking.Id.ToString());
        var payload = new { to = user.LineUserId, messages = new[] { flex } };
        await PostToLineAsync(tenant.LineAccessToken, "/v2/bot/message/push", payload);

        db.NotificationLogs.Add(new NotificationLog
        {
            TenantId  = booking.TenantId,
            BookingId = booking.Id,
            UserId    = booking.UserId,
            Type      = "booking_confirmed"
        });
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------
    // Send reminder (called by Quartz job)
    // ------------------------------------------------------------
    public async Task SendReminderAsync(Booking booking)
    {
        var tenant = await db.Tenants.FindAsync(booking.TenantId);
        var user   = await db.Users.FindAsync(booking.UserId);
        if (tenant == null || user == null) return;

        var text = $"💆 แจ้งเตือนการนัดหมาย!\n" +
                   $"พรุ่งนี้: {booking.BookingDate:dd MMM} เวลา {booking.StartTime:HH\\:mm}\n" +
                   $"บริการ: {booking.Service?.Name ?? "-"}\n" +
                   $"Diamond Massage รอต้อนรับคุณค่ะ 🌸";

        await SendTextAsync(booking.TenantId, user.LineUserId, text);

        booking.ReminderSent = true;
        await db.SaveChangesAsync();

        db.NotificationLogs.Add(new NotificationLog
        {
            TenantId  = booking.TenantId,
            BookingId = booking.Id,
            UserId    = booking.UserId,
            Type      = "reminder"
        });
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------
    // Send LIFF link so user can book
    // ------------------------------------------------------------
    private async Task SendLiffLinkAsync(Guid tenantId, string lineUserId)
    {
        var tenant = await db.Tenants.FindAsync(tenantId);
        if (tenant?.LiffId == null) return;

        var text = $"🌸 คลิกเพื่อจองนัดหมาย:\nhttps://liff.line.me/{tenant.LiffId}";
        await SendTextAsync(tenantId, lineUserId, text);
    }

    // ------------------------------------------------------------
    // POST helper
    // ------------------------------------------------------------
    private async Task PostToLineAsync(string token, string path, object payload)
    {
        var client = http.CreateClient();
        client.BaseAddress = new Uri("https://api.line.me");
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var json = JsonSerializer.Serialize(payload);
        var res  = await client.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));
        if (!res.IsSuccessStatusCode)
            log.LogWarning("LINE API error {Status} on {Path}", res.StatusCode, path);
    }

    // ------------------------------------------------------------
    // Flex message builder (booking confirmation)
    // ------------------------------------------------------------
    private static object BuildConfirmationFlex(string date, string time, string service, string bookingId) => new
    {
        type = "flex",
        altText = $"✅ จองแล้ว! {date} เวลา {time}",
        contents = new
        {
            type   = "bubble",
            header = new
            {
                type     = "box",
                layout   = "vertical",
                contents = new[] { new { type = "text", text = "✅ จองสำเร็จ!", weight = "bold", size = "xl", color = "#ffffff" } },
                backgroundColor = "#D4A853"
            },
            body = new
            {
                type   = "box",
                layout = "vertical",
                contents = new object[]
                {
                    new { type = "text", text = "Diamond Massage", weight = "bold", size = "lg" },
                    new { type = "separator", margin = "md" },
                    Row("📅 วันที่", date),
                    Row("⏰ เวลา",  time),
                    Row("💆 บริการ", service),
                    new { type = "text", text = $"ID: {bookingId[..8]}", size = "xs", color = "#aaaaaa", margin = "md" }
                }
            },
            footer = new
            {
                type   = "box",
                layout = "vertical",
                contents = new[] { new { type = "text", text = "ขอบคุณที่ใช้บริการค่ะ 🌸", align = "center", color = "#888888" } }
            }
        }
    };

    private static object Row(string label, string value) => new
    {
        type   = "box",
        layout = "horizontal",
        margin = "sm",
        contents = new object[]
        {
            new { type = "text", text = label, size = "sm", color = "#555555", flex = 2 },
            new { type = "text", text = value, size = "sm", weight = "bold", flex = 3 }
        }
    };
}

// ================================================================
// LineServiceTests.cs  —  Unit tests for LINE integration
// ================================================================
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DiamondBooking.Services;
using DiamondBooking.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DiamondBooking.Tests;

public class LineServiceTests
{
    // ── Signature verification ────────────────────────────────────

    [Fact]
    public void VerifySignature_ValidSignature_ReturnsTrue()
    {
        var svc    = MakeService();
        var secret = "test-channel-secret";
        var body   = """{"events":[]}""";
        var sig    = ComputeSignature(body, secret);

        svc.VerifySignature(body, sig, secret).Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_WrongSecret_ReturnsFalse()
    {
        var svc = MakeService();
        var sig = ComputeSignature("body", "correct-secret");

        svc.VerifySignature("body", sig, "wrong-secret").Should().BeFalse();
    }

    [Fact]
    public void VerifySignature_TamperedBody_ReturnsFalse()
    {
        var svc    = MakeService();
        var secret = "my-secret";
        var sig    = ComputeSignature("""{"original":"body"}""", secret);

        svc.VerifySignature("""{"tampered":"body"}""", sig, secret).Should().BeFalse();
    }

    [Fact]
    public void VerifySignature_EmptySignature_ReturnsFalse()
    {
        var svc = MakeService();
        svc.VerifySignature("body", "", "secret").Should().BeFalse();
    }

    // ── Webhook handling ──────────────────────────────────────────

    [Fact]
    public async Task HandleWebhook_FollowEvent_CreatesUser()
    {
        var db     = TestDbFactory.Create(nameof(HandleWebhook_FollowEvent_CreatesUser));
        var tenant = TestDbFactory.SeedTenant(db);
        var svc    = MakeService(db);

        var payload = BuildWebhookPayload("follow", "U_NEW_FOLLOW");
        await svc.HandleWebhookAsync(tenant.Id, payload);

        db.Users.Should().ContainSingle(u => u.LineUserId == "U_NEW_FOLLOW");
    }

    [Fact]
    public async Task HandleWebhook_FollowEvent_DoesNotDuplicateExistingUser()
    {
        var db     = TestDbFactory.Create(nameof(HandleWebhook_FollowEvent_DoesNotDuplicateExistingUser));
        var tenant = TestDbFactory.SeedTenant(db);
        TestDbFactory.SeedUser(db, tenant.Id, "U_ALREADY_EXISTS");
        var svc = MakeService(db);

        var payload = BuildWebhookPayload("follow", "U_ALREADY_EXISTS");
        await svc.HandleWebhookAsync(tenant.Id, payload);

        db.Users.Count(u => u.LineUserId == "U_ALREADY_EXISTS").Should().Be(1);
    }

    [Fact]
    public async Task HandleWebhook_MessageEvent_CreatesUser()
    {
        var db     = TestDbFactory.Create(nameof(HandleWebhook_MessageEvent_CreatesUser));
        var tenant = TestDbFactory.SeedTenant(db);
        var svc    = MakeService(db);

        var payload = BuildMessagePayload("U_MSG_USER", "hello");
        await svc.HandleWebhookAsync(tenant.Id, payload);

        db.Users.Should().ContainSingle(u => u.LineUserId == "U_MSG_USER");
    }

    [Fact]
    public async Task HandleWebhook_UnknownEventType_DoesNotThrow()
    {
        var db     = TestDbFactory.Create(nameof(HandleWebhook_UnknownEventType_DoesNotThrow));
        var tenant = TestDbFactory.SeedTenant(db);
        var svc    = MakeService(db);

        var payload = BuildWebhookPayload("unsend", "U_UNSEND");

        Func<Task> act = () => svc.HandleWebhookAsync(tenant.Id, payload);
        await act.Should().NotThrowAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static LineService MakeService(DiamondBooking.Data.AppDbContext? db = null)
    {
        db ??= TestDbFactory.Create();
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
                   .Returns(new HttpClient());
        return new LineService(db, httpFactory.Object, NullLogger<LineService>.Instance);
    }

    private static string ComputeSignature(string body, string secret)
    {
        var key  = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(body);
        return Convert.ToBase64String(new HMACSHA256(key).ComputeHash(data));
    }

    private static JsonElement BuildWebhookPayload(string type, string userId)
    {
        var json = $$"""
        {
          "events": [{
            "type": "{{type}}",
            "source": { "type": "user", "userId": "{{userId}}" },
            "timestamp": 1700000000000,
            "replyToken": "test-token"
          }]
        }
        """;
        return JsonDocument.Parse(json).RootElement;
    }

    private static JsonElement BuildMessagePayload(string userId, string text)
    {
        var json = $$"""
        {
          "events": [{
            "type": "message",
            "source": { "type": "user", "userId": "{{userId}}" },
            "message": { "type": "text", "id": "msg-1", "text": "{{text}}" },
            "timestamp": 1700000000000,
            "replyToken": "test-token"
          }]
        }
        """;
        return JsonDocument.Parse(json).RootElement;
    }
}
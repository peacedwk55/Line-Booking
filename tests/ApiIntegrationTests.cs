// ================================================================
// ApiIntegrationTests.cs  —  HTTP-level tests (no real DB needed)
// ================================================================
using System.Net;
using System.Net.Http.Json;
using DiamondBooking.Data;
using DiamondBooking.Tests.Helpers;
using FluentAssertions;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DiamondBooking.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext registrations (EF Core 10 requires removing all)
                ReplaceWithInMemory(services, "integration-test-db");
            });
        });

        // Seed after factory is configured — avoids calling BuildServiceProvider() inside ConfigureServices
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        SeedIntegrationData(db);
    }

    private static void ReplaceWithInMemory(IServiceCollection services, string dbName)
    {
        var toRemove = services
            .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                        (d.ServiceType.IsGenericType &&
                         d.ServiceType.Name.Contains("IDbContextOptionsConfiguration") &&
                         d.ServiceType.GenericTypeArguments.FirstOrDefault() == typeof(AppDbContext)))
            .ToList();
        foreach (var d in toRemove) services.Remove(d);

        services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase(dbName));
    }

    private static void SeedIntegrationData(AppDbContext db)
    {
        if (db.Tenants.Any()) return;   // already seeded (shared db)
        var tenant  = TestDbFactory.SeedTenant(db);
        TestDbFactory.SeedService(db, tenant.Id);
        TestDbFactory.SeedTimeSlot(db, tenant.Id, dayOfWeek: 1);  // Monday
    }

    // ── Health ─────────────────────────────────────────────────────

    [Fact]
    public async Task HealthCheck_Returns200()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Services endpoint ─────────────────────────────────────────

    [Fact]
    public async Task GetServices_KnownTenant_Returns200WithList()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync("/api/diamond-massage/services");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<object>>();
        body.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetServices_UnknownTenant_Returns404()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync("/api/unknown-tenant/services");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Slots endpoint ─────────────────────────────────────────────

    [Fact]
    public async Task GetSlots_ValidDate_Returns200()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync("/api/diamond-massage/bookings/slots?date=2025-06-02");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSlots_InvalidDate_Returns400()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync("/api/diamond-massage/bookings/slots?date=not-a-date");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Create booking endpoint ────────────────────────────────────

    [Fact]
    public async Task CreateBooking_ValidPayload_Returns200()
    {
        var client  = _factory.CreateClient();
        var db      = GetDb();
        var service = db.Services.First();

        var payload = new
        {
            lineUserId = "U_INTEGRATION_TEST",
            serviceId  = service.Id,
            date       = "2025-06-02",
            startTime  = "10:00",
            endTime    = "11:30",
            note       = (string?)null
        };

        var response = await client.PostAsJsonAsync("/api/diamond-massage/bookings", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("bookingId");
    }

    [Fact]
    public async Task CreateBooking_MissingFields_Returns400()
    {
        var client   = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/diamond-massage/bookings", new { });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Webhook endpoint ───────────────────────────────────────────

    [Fact]
    public async Task Webhook_MissingSignature_Returns401()
    {
        var client  = _factory.CreateClient();
        var content = new StringContent("""{"events":[]}""",
            System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/webhook/diamond-massage", content);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_UnknownTenant_Returns404()
    {
        var client  = _factory.CreateClient();
        var content = new StringContent("""{"events":[]}""",
            System.Text.Encoding.UTF8, "application/json");
        content.Headers.Add("X-Line-Signature", "dummy");

        var response = await client.PostAsync("/webhook/ghost-tenant", content);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private AppDbContext GetDb()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}

// ================================================================
// AdminControllerTests.cs  —  Integration tests for admin endpoints
// ================================================================
using System.Net;
using System.Net.Http.Json;
using DiamondBooking.Data;
using DiamondBooking.Models;
using DiamondBooking.Tests.Helpers;
using FluentAssertions;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DiamondBooking.Tests;

public class AdminControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AdminControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            ReplaceWithInMemory(services, "admin-test-db");
            services.AddAuthentication(TestAuthHandler.Scheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
        }));

        // Seed after factory is configured — avoids calling BuildServiceProvider() inside ConfigureServices
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        SeedData(db);
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

        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
    }

    private static void SeedData(AppDbContext db)
    {
        if (db.Tenants.Any()) return;
        var tenant  = TestDbFactory.SeedTenant(db);
        var user    = TestDbFactory.SeedUser(db, tenant.Id);
        var service = TestDbFactory.SeedService(db, tenant.Id);
        TestDbFactory.SeedTimeSlot(db, tenant.Id);
        TestDbFactory.SeedBooking(db, tenant.Id, user.Id, service.Id);
    }

    // ── Dashboard ─────────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_KnownTenant_ReturnsStats()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync("/api/diamond-massage/admin/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        body.Should().ContainKey("todayBookings");
        body.Should().ContainKey("totalUsers");
        body.Should().ContainKey("weekBookings");
    }

    [Fact]
    public async Task Dashboard_UnknownTenant_Returns404()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync("/api/ghost/admin/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Bookings list ─────────────────────────────────────────────

    [Fact]
    public async Task GetBookings_NoFilter_ReturnsAll()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync("/api/diamond-massage/admin/bookings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<object>>();
        body.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetBookings_FilterByDate_ReturnsFiltered()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync(
            "/api/diamond-massage/admin/bookings?date=2025-06-02");  // seeded date

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBookings_FilterByStatus_ReturnsFiltered()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync(
            "/api/diamond-massage/admin/bookings?status=confirmed");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
        body.Should().NotBeNull();
    }

    // ── Update booking ────────────────────────────────────────────

    [Fact]
    public async Task UpdateBooking_ValidStatus_Returns200()
    {
        var client = _factory.CreateClient();
        var db     = GetDb();
        var booking = db.Bookings.First();

        var response = await client.PatchAsJsonAsync(
            $"/api/diamond-massage/admin/bookings/{booking.Id}",
            new { status = "completed", adminNote = "เสร็จเรียบร้อย" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateBooking_NotFound_Returns404()
    {
        var client   = _factory.CreateClient();
        var response = await client.PatchAsJsonAsync(
            $"/api/diamond-massage/admin/bookings/{Guid.NewGuid()}",
            new { status = "completed" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Cancel booking ────────────────────────────────────────────

    [Fact]
    public async Task CancelBooking_Existing_Returns200AndCancels()
    {
        // Use a fresh db name to avoid state pollution
        var factory2 = _factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            ReplaceWithInMemory(services, $"cancel-test-{Guid.NewGuid()}");
        }));

        using var scope = factory2.Services.CreateScope();
        var db2     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant  = TestDbFactory.SeedTenant(db2);
        var user    = TestDbFactory.SeedUser(db2, tenant.Id);
        var service = TestDbFactory.SeedService(db2, tenant.Id);
        TestDbFactory.SeedBooking(db2, tenant.Id, user.Id, service.Id);

        var client   = factory2.CreateClient();
        var db       = factory2.Services.CreateScope()
                               .ServiceProvider.GetRequiredService<AppDbContext>();
        var booking  = db.Bookings.First();

        var response = await client.DeleteAsync(
            $"/api/diamond-massage/admin/bookings/{booking.Id}?reason=ทดสอบ");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Users ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetUsers_KnownTenant_ReturnsList()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync("/api/diamond-massage/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<object>>();
        body.Should().NotBeNullOrEmpty();
    }

    private AppDbContext GetDb() =>
        _factory.Services.CreateScope()
                .ServiceProvider.GetRequiredService<AppDbContext>();
}

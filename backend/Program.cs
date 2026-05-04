// ================================================================
// Program.cs
// ================================================================
using DiamondBooking.Data;
using DiamondBooking.Jobs;
using DiamondBooking.Models;
using DiamondBooking.Services;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ──────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// ── Database ─────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ── Services ─────────────────────────────────────────────────────
builder.Services.AddHttpClient();
builder.Services.AddScoped<ILineService, LineService>();
builder.Services.AddScoped<BookingService>();

// ── CORS (allow LIFF origin) ─────────────────────────────────────
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin()
     .AllowAnyHeader()
     .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
     .SetPreflightMaxAge(TimeSpan.Zero)
));

// ── Quartz (reminder job at 18:00 Bangkok = 11:00 UTC) ───────────
builder.Services.AddQuartz(q =>
{
    var key = new JobKey("reminder-job");
    q.AddJob<ReminderJob>(opts => opts.WithIdentity(key));
    q.AddTrigger(opts => opts
        .ForJob(key)
        .WithIdentity("reminder-trigger")
        .WithCronSchedule("0 0 11 * * ?"));  // 11:00 UTC = 18:00 Bangkok
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// ── Controllers + Swagger ─────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Diamond Massage Booking API", Version = "v1" });
});

var app = builder.Build();

// ── Auto-migrate on startup (relational DB only — skipped for InMemory in tests) ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();  // InMemory: just create schema

    // Seed default tenant + data
    var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    if (!db.Tenants.Any(t => t.Id == tenantId))
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId, Slug = "diamond-massage", Name = "Diamond Massage",
            LineChannelId = "placeholder", LineChannelSecret = "placeholder",
            LineAccessToken = "placeholder", Timezone = "Asia/Bangkok"
        });
        await db.SaveChangesAsync();
        Log.Information("Seeded tenant");
    }

    // Re-seed services if missing or still using old format (duration in name)
    if (!db.Services.Any(s => s.TenantId == tenantId) ||
         db.Services.Any(s => s.TenantId == tenantId && s.Name.Contains("นาที")))
    {
        db.Services.RemoveRange(db.Services.Where(s => s.TenantId == tenantId));
        db.Services.AddRange(
            new Service { TenantId = tenantId, Name = "นวดแผนไทย",  Description = "ผ่อนคลายกล้ามเนื้อแบบดั้งเดิม", PricePerHour = 350, SortOrder = 1 },
            new Service { TenantId = tenantId, Name = "นวดน้ำมัน",   Description = "อโรมาเธอราพีช่วยผ่อนคลาย",      PricePerHour = 450, SortOrder = 2 },
            new Service { TenantId = tenantId, Name = "นวดฝ่าเท้า",  Description = "กระตุ้นจุดสะท้อนสุขภาพ",        PricePerHour = 300, SortOrder = 3 }
        );
        await db.SaveChangesAsync();
        Log.Information("Reseeded services to new format (pricePerHour)");
    }

    // Re-seed time slots: 1 row/day (operating hours). Delete old format (>6 rows) if present.
    var slotCount = db.TimeSlots.Count(s => s.TenantId == tenantId);
    if (slotCount == 0 || slotCount > 6)
    {
        db.TimeSlots.RemoveRange(db.TimeSlots.Where(s => s.TenantId == tenantId));
        foreach (var dow in new[] { 1,2,3,4,5,6 })
            db.TimeSlots.Add(new TimeSlot
            {
                TenantId    = tenantId,
                DayOfWeek   = dow,
                StartTime   = TimeOnly.Parse("10:00"),
                EndTime     = TimeOnly.Parse("20:30"),
                MaxBookings = 2
            });
        await db.SaveChangesAsync();
        Log.Information("Reseeded time slots to new format (1 row/day)");
    }
}

// ── Middleware ────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

// Health check endpoint for Railway
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program {}

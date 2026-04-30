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
     .AllowAnyMethod()
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

    // Seed default tenant + data if not exists
    var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    if (!db.Tenants.Any(t => t.Id == tenantId))
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId, Slug = "diamond-massage", Name = "Diamond Massage",
            LineChannelId = "placeholder", LineChannelSecret = "placeholder",
            LineAccessToken = "placeholder", Timezone = "Asia/Bangkok"
        });
        db.Services.AddRange(
            new Service { TenantId = tenantId, Name = "นวดแผนไทย 60 นาที",  Description = "ผ่อนคลายกล้ามเนื้อแบบดั้งเดิม", DurationMinutes = 60,  Price = 350, SortOrder = 1 },
            new Service { TenantId = tenantId, Name = "นวดแผนไทย 90 นาที",  Description = "ผ่อนคลายแบบเต็มรูปแบบ",         DurationMinutes = 90,  Price = 500, SortOrder = 2 },
            new Service { TenantId = tenantId, Name = "นวดน้ำมัน 60 นาที",   Description = "อโรมาเธอราพีช่วยผ่อนคลาย",      DurationMinutes = 60,  Price = 450, SortOrder = 3 },
            new Service { TenantId = tenantId, Name = "นวดฝ่าเท้า 45 นาที",  Description = "กระตุ้นจุดสะท้อนสุขภาพ",        DurationMinutes = 45,  Price = 280, SortOrder = 4 }
        );
        var times = new[] { ("10:00","11:30"),("11:30","13:00"),("13:00","14:30"),("14:30","16:00"),("16:00","17:30"),("17:30","19:00"),("19:00","20:30") };
        foreach (var dow in new[] { 1,2,3,4,5,6 })
            foreach (var (s, e) in times)
                db.TimeSlots.Add(new TimeSlot { TenantId = tenantId, DayOfWeek = dow, StartTime = TimeOnly.Parse(s), EndTime = TimeOnly.Parse(e), MaxBookings = 2 });
        await db.SaveChangesAsync();
        Log.Information("Seeded default tenant and data");
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

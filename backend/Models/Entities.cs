// ================================================================
// Models/Entities.cs  —  EF Core entity classes
// ================================================================
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiamondBooking.Models;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(100)] public string Slug { get; set; } = "";
    [MaxLength(255)] public string Name { get; set; } = "";
    public string LineChannelId { get; set; } = "";
    public string LineChannelSecret { get; set; } = "";
    public string LineAccessToken { get; set; } = "";
    public string? LiffId { get; set; }
    public string Timezone { get; set; } = "Asia/Bangkok";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = [];
    public ICollection<Service> Services { get; set; } = [];
    public ICollection<Booking> Bookings { get; set; } = [];
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    [MaxLength(255)] public string LineUserId { get; set; } = "";
    [MaxLength(255)] public string? DisplayName { get; set; }
    [MaxLength(20)]  public string? Phone { get; set; }
    public string? PictureUrl { get; set; }
    public bool IsBlocked { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = [];
}

public class Service
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    [MaxLength(255)] public string Name { get; set; } = "";
    public string? Description { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal? PricePerHour { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
}

public class TimeSlot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public int? DayOfWeek { get; set; }        // 0=Sun … 6=Sat, null = specific date
    public DateOnly? SpecificDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int MaxBookings { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
}

public enum BookingStatus { Pending, Confirmed, Cancelled, Completed }

public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? ServiceId { get; set; }
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public string? Note { get; set; }
    public string? AdminNote { get; set; }
    public bool ReminderSent { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public User User { get; set; } = null!;
    public Service? Service { get; set; }
}

public class NotificationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? BookingId { get; set; }
    public Guid? UserId { get; set; }
    [MaxLength(50)] public string Type { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "sent";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

// ================================================================
// Jobs/PreAppointmentReminderJob.cs  —  15-min pre-appointment reminder
// ================================================================
using DiamondBooking.Data;
using DiamondBooking.Models;
using DiamondBooking.Services;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace DiamondBooking.Jobs;

[DisallowConcurrentExecution]
public class PreAppointmentReminderJob(AppDbContext db, ILineService line, ILogger<PreAppointmentReminderJob> log) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var nowBangkok  = DateTime.UtcNow.AddHours(7);
        var todayBangkok = DateOnly.FromDateTime(nowBangkok);
        var nowTime     = TimeOnly.FromDateTime(nowBangkok);

        // Match bookings starting in the 10–20 minute window ahead
        var minTime = nowTime.AddMinutes(10);
        var maxTime = nowTime.AddMinutes(20);

        var bookings = await db.Bookings
            .Include(b => b.User)
            .Include(b => b.Service)
            .Where(b =>
                b.BookingDate      == todayBangkok &&
                b.Status           == BookingStatus.Confirmed &&
                b.PreReminderSent  == false &&
                b.StartTime        >= minTime &&
                b.StartTime        <= maxTime)
            .ToListAsync();

        if (bookings.Count == 0) return;

        log.LogInformation("PreAppointmentReminderJob: {Count} reminders at {Time}", bookings.Count, nowBangkok.ToString("HH:mm"));

        foreach (var booking in bookings)
        {
            try
            {
                await line.SendPreReminderAsync(booking);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Pre-reminder failed for booking {Id}", booking.Id);
            }
        }
    }
}

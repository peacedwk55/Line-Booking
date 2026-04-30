// ================================================================
// Jobs/ReminderJob.cs  —  Quartz background job for reminders
// ================================================================
using DiamondBooking.Data;
using DiamondBooking.Models;
using DiamondBooking.Services;
using Microsoft.EntityFrameworkCore;
using Quartz;
 
namespace DiamondBooking.Jobs;
 
[DisallowConcurrentExecution]
public class ReminderJob(AppDbContext db, ILineService line, ILogger<ReminderJob> log) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).AddDays(1));
 
        var bookings = await db.Bookings
            .Include(b => b.User)
            .Include(b => b.Service)
            .Where(b =>
                b.BookingDate  == tomorrow &&
                b.Status       == BookingStatus.Confirmed &&
                b.ReminderSent == false)
            .ToListAsync();
 
        log.LogInformation("ReminderJob: sending {Count} reminders for {Date}",
            bookings.Count, tomorrow.ToString("yyyy-MM-dd"));
 
        foreach (var booking in bookings)
        {
            try
            {
                await line.SendReminderAsync(booking);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Reminder failed for booking {Id}", booking.Id);
            }
        }
    }
}
 
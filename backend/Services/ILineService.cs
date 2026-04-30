using System.Text.Json;
using DiamondBooking.Models;

namespace DiamondBooking.Services;

public interface ILineService
{
    bool VerifySignature(string body, string signature, string channelSecret);
    Task HandleWebhookAsync(Guid tenantId, JsonElement root);
    Task SendTextAsync(Guid tenantId, string lineUserId, string text);
    Task SendBookingConfirmationAsync(Booking booking);
    Task SendReminderAsync(Booking booking);
}

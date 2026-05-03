using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TravelManagement.API.Hubs;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Interface;
using AppNotification = TravelManagement.Core.Models.Notification;

namespace TravelManagement.API.Infrastructure
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hub;

        public NotificationService(AppDbContext context, IHubContext<NotificationHub> hub)
        {
            _context = context;
            _hub = hub;
        }

        public async Task CreateAsync(string message, int? userId = null)
        {
            var notif = new AppNotification
            {
                Message = message,
                UserId = userId,
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            await _context.Notifications.AddAsync(notif);
            await _context.SaveChangesAsync();

            await _hub.Clients.All.SendAsync("ReceiveNotification", new
            {
                notificationId = notif.NotificationId,
                message = notif.Message,
                createdAt = notif.CreatedAt,
                isRead = notif.IsRead
            });
        }

        public async Task SendPushNotificationAsync(string title, string body)
        {
            var tokens = await _context.Users
                .Where(u => !string.IsNullOrEmpty(u.FcmToken))
                .Select(u => u.FcmToken)
                .ToListAsync();

            if (!tokens.Any()) return;

            var message = new MulticastMessage
            {
                Tokens = tokens!,
                Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body }
            };

            var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
            Console.WriteLine($"Sent {response.SuccessCount} notifications, failed: {response.FailureCount}");
        }
    }
}
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.RegularExpressions;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.API.Infrastructure
{
    public class EmailBookingBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly EmailSettings _settings;

        public EmailBookingBackgroundService(IServiceProvider serviceProvider, IOptions<EmailSettings> options)
        {
            _serviceProvider = serviceProvider;
            _settings = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
                {
                    Console.WriteLine("[EmailBookingBackgroundService] Email credentials not configured. Skipping poll. Retrying in 5 minutes.");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }

                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                    var bookings = await ReadBookingsFromEmailAsync();
                    foreach (var bookingDto in bookings)
                    {
                        if (bookingDto == null) continue;

                        var inquiry = new EmailInquiry
                        {
                            CustomerName = bookingDto.CustomerName ?? "",
                            CustomerNumber = bookingDto.CustomerNumber,
                            From = bookingDto.From,
                            To = bookingDto.To,
                            TravelDate = !string.IsNullOrWhiteSpace(bookingDto.TravelDate)
                                ? DateOnly.ParseExact(bookingDto.TravelDate, "MM-dd-yyyy", CultureInfo.InvariantCulture)
                                : null,
                            Pax = bookingDto.Pax ?? 1,
                            VehicleName = bookingDto.VehicleName,
                            CreatedAt = DateOnly.FromDateTime(DateTime.Now)
                        };

                        await dbContext.EmailInquiries.AddAsync(inquiry);
                        await notificationService.CreateAsync(
                            $"📩Inquiry From: {inquiry.From} → {inquiry.To} | Pax: {inquiry.Pax} | Vehicle: {inquiry.VehicleName}");
                        await notificationService.SendPushNotificationAsync(
                            "📩 New Inquiry Received!",
                            $"{inquiry.From} → {inquiry.To} | {inquiry.Pax} Pax | {inquiry.VehicleName} | {inquiry.TravelDate}");
                    }
                    await dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EmailBookingBackgroundService] Error: {ex}");
                }

                await Task.Delay(_settings.PollIntervalSeconds * 1000, stoppingToken);
            }
        }

        private async Task<List<BookingEmailDto>> ReadBookingsFromEmailAsync()
        {
            if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
                return new List<BookingEmailDto>();

            var bookings = new List<BookingEmailDto>();
            using var client = new ImapClient();

            await client.ConnectAsync(_settings.Host, _settings.Port,
                _settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.Username, _settings.Password);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite);

            var uids = await inbox.SearchAsync(SearchQuery.NotSeen);
            var summaries = await inbox.FetchAsync(uids, MessageSummaryItems.Envelope);

            var latestUids = summaries
                .Where(s => s.Envelope?.From?.Mailboxes.Any(mb =>
                    string.Equals(mb.Address, "website@ezygoataxiservices.in", StringComparison.OrdinalIgnoreCase)) == true)
                .OrderByDescending(s => s.Envelope.Date?.DateTime ?? DateTime.MinValue)
                .Take(10)
                .Select(s => s.UniqueId)
                .ToList();

            foreach (var uid in latestUids)
            {
                var message = await inbox.GetMessageAsync(uid);
                var body = message.TextBody ?? message.HtmlBody;
                if (string.IsNullOrWhiteSpace(body)) continue;

                var booking = ParseBookingEmail(body);
                if (booking == null) continue;

                if (string.IsNullOrWhiteSpace(booking.CustomerNumber) || !IsValidPhone(booking.CustomerNumber))
                    continue;

                bookings.Add(booking);
                await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true);
            }

            await client.DisconnectAsync(true);
            return bookings;
        }

        private bool IsValidPhone(string phone)
        {
            var cleaned = phone.Replace(" ", "").Replace("-", "");
            return Regex.IsMatch(cleaned, @"^(\+91|91|0)?0?[6-9][0-9]{9}$");
        }

        private BookingEmailDto? ParseBookingEmail(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            return new BookingEmailDto
            {
                CustomerName = GetValue(body, "Name"),
                CustomerNumber = GetValue(body, "Phone"),
                TravelDate = GetValue(body, "Pickup Date"),
                Pax = int.TryParse(GetValue(body, "No. of Passenger"), out var pax) ? pax : null,
                VehicleName = GetValue(body, "Select Vehicle"),
                From = GetValue(body, "Select Pick Up Spot"),
                To = GetValue(body, "Select Drop Off Spot")
            };
        }

        private string? GetValue(string body, string key)
        {
            body = body.Replace("\r", "").Replace("\n", " ");
            var pattern = $@"<b>\s*{Regex.Escape(key)}\s*</b>\s*<br\s*/?>\s*(.*?)\s*(</li>|<br|</p|$)";
            var match = Regex.Match(body, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success) return null;
            var raw = Regex.Replace(match.Groups[1].Value, "<.*?>", string.Empty);
            return System.Net.WebUtility.HtmlDecode(raw.Trim());
        }
    }
}
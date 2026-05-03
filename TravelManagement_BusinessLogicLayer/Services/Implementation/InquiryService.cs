using TravelManagement.BusinessLogicLayer.Services.Interface;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.BusinessLogicLayer.Services.Implementation
{
    public class InquiryService : IInquiryService
    {
        private readonly IInquiryRepository _inquiryRepo;
        private readonly IBookingRepository _bookingRepo;

        public InquiryService(IInquiryRepository inquiryRepo, IBookingRepository bookingRepo)
        {
            _inquiryRepo = inquiryRepo;
            _bookingRepo = bookingRepo;
        }

        public Task<List<EmailInquiry>> GetAllInquiriesAsync() => _inquiryRepo.GetPendingInquiriesAsync();

        public async Task<object> ConfirmInquiryAsync(int id)
        {
            var inquiry = await _inquiryRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Inquiry not found");

            if (inquiry.IsConfirmed || inquiry.IsRejected)
                throw new InvalidOperationException("Already processed");

            inquiry.VehicleName = inquiry.Pax switch
            {
                >= 1 and <= 4 => "Sedan",
                > 4 and <= 6 => "Suv",
                > 6 and <= 17 => "TT17Seater",
                > 17 and <= 20 => "TT20Seater",
                > 20 and <= 30 => "Bus30Seater",
                > 30 and <= 40 => "Bus40Seater",
                > 40 and <= 60 => "Bus60Seater",
                _ => "Notspecified"
            };

            string from = inquiry.From?.ToLower() ?? string.Empty;
            string to = inquiry.To?.ToLower() ?? string.Empty;

            string bookingType = from switch
            {
                _ when from.Contains("airport") && !to.Contains("airport") => "AirportPickup",
                _ when to.Contains("airport") && !from.Contains("airport") => "AirportDrop",
                _ when from.Contains("station") || to.Contains("station") => "RailwayStation",
                _ => "Notspecified"
            };

            if (!Enum.TryParse<VechileType>(inquiry.VehicleName, true, out var vehicleType))
                vehicleType = VechileType.Notspecified;

            int vehicleId = await _inquiryRepo.GetVehicleIdByTypeAsync(vehicleType, inquiry.VehicleName);

            var dto = new NewBookiingDTO
            {
                CustomerName = inquiry.CustomerName,
                CustomerNumber = inquiry.CustomerNumber ?? "0000000000",
                From = inquiry.From,
                To = inquiry.To,
                Pax = inquiry.Pax,
                BookingDate = inquiry.TravelDate ?? DateOnly.FromDateTime(DateTime.Today),
                BookingTime = TimeOnly.FromDateTime(DateTime.Now),
                VehicleId = vehicleId,
                BookingType = bookingType,
                BookingStatus = "Pending",
                Amount = 0,
                Payment = "Admin",
                UserId = 16
            };

            var booking = await _bookingRepo.CreateBooking(dto);
            inquiry.IsConfirmed = true;
            await _inquiryRepo.UpdateInquiryAsync(inquiry);
            return booking;
        }

        public async Task RejectInquiryAsync(int id)
        {
            var inquiry = await _inquiryRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Inquiry not found");
            inquiry.IsRejected = true;
            await _inquiryRepo.UpdateInquiryAsync(inquiry);
        }

        public Task<List<Notification>> GetNotificationsAsync() => _inquiryRepo.GetUnreadNotificationsAsync();

        public async Task<Notification?> MarkAsReadAsync(int id)
        {
            var notification = await _inquiryRepo.GetNotificationByIdAsync(id);
            if (notification == null) return null;
            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _inquiryRepo.SaveChangesAsync();
            }
            return notification;
        }

        public async Task<int> MarkAllAsReadAsync()
        {
            var unread = await _inquiryRepo.GetUnreadNotificationsAsync();
            await _inquiryRepo.MarkAllNotificationsAsReadAsync();
            return unread.Count;
        }
    }
}
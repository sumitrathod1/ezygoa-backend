using TravelManagement.BusinessLogicLayer.Services.Interface;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.BusinessLogicLayer.Services.Implementation
{
    public class BookingService : IBookingService
    {
        private readonly IBookingRepository _bookingRepo;

        public BookingService(IBookingRepository bookingRepo)
        {
            _bookingRepo = bookingRepo;
        }

        public Task<Booking?> GetBookingByIdAsync(int id) => _bookingRepo.GetByIdAsync(id);

        public Task<List<ExternalEmployee>> GetExternalEmployeesAsync() => _bookingRepo.GetExternalEmployeesAsync();

        public Task<object> GetAllBookingsWithStatsAsync() => _bookingRepo.GetAllBookingsWithStatsAsync();

        public Task<Booking> CreateBookingAsync(NewBookiingDTO dto) => _bookingRepo.CreateBooking(dto);

        public Task<bool> CancelBookingAsync(int bookingId) => _bookingRepo.CancelBookingAsync(bookingId);

        public Task<object> FilterBookingsAsync(BookingFilterDTO filterDTO) => _bookingRepo.FilterBookingsAsync(filterDTO);

        public Task<List<ExternalVendorSettlementDTO>> GetVendorBookingsAsync(int? vendorId) =>
            _bookingRepo.GetVendorBookingsAsync(vendorId);

        public Task ReassignToExternalAsync(AssignExternalVendorDTO dto) =>
            _bookingRepo.AssignBookingToExternalVendor(dto);

        public Task SettleSettlementAsync(SettleSettlementDTO dto) => _bookingRepo.SettleSettlementAsync(dto);
    }
}
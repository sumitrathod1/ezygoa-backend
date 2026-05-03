using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;

namespace TravelManagement.DataAccessLayer.Repository.Interface
{
    public interface IBookingRepository
    {
        Task<Booking?> GetByIdAsync(int id);
        Task<List<ExternalEmployee>> GetExternalEmployeesAsync();
        Task<object> GetAllBookingsWithStatsAsync();
        Task<Booking> CreateBooking(NewBookiingDTO newBookingDTO);
        Task<bool> CancelBookingAsync(int bookingId);
        Task<object> FilterBookingsAsync(BookingFilterDTO filterDTO);
        Task<List<ExternalVendorSettlementDTO>> GetVendorBookingsAsync(int? externalEmployeeId = null);
        Task AssignBookingToExternalVendor(AssignExternalVendorDTO dto);
        Task SettleSettlementAsync(SettleSettlementDTO dto);
    }
}
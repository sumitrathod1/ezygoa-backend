using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;

namespace TravelManagement.BusinessLogicLayer.Services.Interface
{
    public interface IBookingService
    {
        Task<Booking?> GetBookingByIdAsync(int id);
        Task<List<ExternalEmployee>> GetExternalEmployeesAsync();
        Task<object> GetAllBookingsWithStatsAsync();
        Task<Booking> CreateBookingAsync(NewBookiingDTO dto);
        Task<bool> CancelBookingAsync(int bookingId);
        Task<object> FilterBookingsAsync(BookingFilterDTO filterDTO);
        Task<List<ExternalVendorSettlementDTO>> GetVendorBookingsAsync(int? vendorId);
        Task ReassignToExternalAsync(AssignExternalVendorDTO dto);
        Task SettleSettlementAsync(SettleSettlementDTO dto);
    }
}
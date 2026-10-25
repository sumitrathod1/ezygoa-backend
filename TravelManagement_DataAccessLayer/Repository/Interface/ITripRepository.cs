using TravelManagement.Core.DTOs;

namespace TravelManagement.DataAccessLayer.Repository.Interface
{
    public interface ITripRepository
    {
        /// <summary>Returning-customer lookup: the customer and their current / recent trips.</summary>
        Task<object> FindByCustomerPhoneAsync(string phone);
        Task<object> GetAsync(int tripId);
        Task<object> UpdateStayAsync(int tripId, TripStayDTO dto);
        /// <summary>Same driver and / or vehicle for every upcoming day of the trip.</summary>
        Task<object> AssignAllAsync(int tripId, AssignBookingDTO dto);
    }
}

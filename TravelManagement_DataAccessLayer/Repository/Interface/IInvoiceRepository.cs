using TravelManagement.Core.DTOs;

namespace TravelManagement.DataAccessLayer.Repository.Interface
{
    public interface IInvoiceRepository
    {
        /// <summary>scope: "booking" (default), "trip" (the booking's trip) or "customer" (all their bookings).</summary>
        Task<CustomerInvoiceData> GetAsync(int bookingId, string? scope, bool includeTerms = false);
    }
}

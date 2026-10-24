using TravelManagement.Core.DTOs;

namespace TravelManagement.DataAccessLayer.Repository.Interface
{
    /// <summary>Driver / vehicle assignment and booking hand-over between linked organisations.</summary>
    public interface IDispatchRepository
    {
        Task<object> GetBoardAsync(DateOnly from, DateOnly to);
        Task<object> GetPendingAssignmentCountAsync();
        Task<AssignBookingResult> AssignAsync(int bookingId, AssignBookingDTO dto);
        Task<List<object>> GetTransferTargetsAsync();
        Task<TransferBookingResult> TransferAsync(int bookingId, TransferBookingDTO dto);
    }
}

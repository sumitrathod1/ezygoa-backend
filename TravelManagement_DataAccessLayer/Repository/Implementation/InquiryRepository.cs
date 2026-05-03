using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.DataAccessLayer.Repository.Implementation
{
    public class InquiryRepository : IInquiryRepository
    {
        private readonly AppDbContext _context;

        public InquiryRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<EmailInquiry>> GetPendingInquiriesAsync()
        {
            return await _context.EmailInquiries
                .Where(e => !e.IsRejected && !e.IsConfirmed)
                .OrderByDescending(e => e.Id)
                .ToListAsync();
        }

        public async Task<EmailInquiry?> GetByIdAsync(int id)
        {
            return await _context.EmailInquiries.FindAsync(id);
        }

        public async Task UpdateInquiryAsync(EmailInquiry inquiry)
        {
            _context.EmailInquiries.Update(inquiry);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Notification>> GetUnreadNotificationsAsync()
        {
            return await _context.Notifications
                .Where(n => !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<Notification?> GetNotificationByIdAsync(int id)
        {
            return await _context.Notifications.FindAsync(id);
        }

        public async Task MarkAllNotificationsAsReadAsync()
        {
            var unread = await _context.Notifications.Where(n => !n.IsRead).ToListAsync();
            foreach (var n in unread) n.IsRead = true;
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetVehicleIdByTypeAsync(VechileType vehicleType, string? vehicleName)
        {
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleType == vehicleType);
            if (vehicle == null && vehicleName != null)
                vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.VehicleName!.ToLower() == vehicleName.ToLower());
            return vehicle?.VehicleId ?? 1;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.DataAccessLayer.Repository.Implementation
{
    public class InquiryRepository : IInquiryRepository
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenant;

        public InquiryRepository(AppDbContext context, TenantContext tenant)
        {
            _context = context;
            _tenant  = tenant;
        }

        // Enquiries and notifications used to be shared by every organisation
        private IQueryable<EmailInquiry> OrgInquiries =>
            _tenant.ShouldFilter ? _context.EmailInquiries.Where(e => e.OrgId == _tenant.OrgId) : _context.EmailInquiries;

        private IQueryable<Notification> OrgNotifications =>
            _tenant.ShouldFilter ? _context.Notifications.Where(n => n.OrgId == _tenant.OrgId) : _context.Notifications;

        public async Task<List<EmailInquiry>> GetPendingInquiriesAsync()
        {
            return await OrgInquiries
                .Where(e => !e.IsRejected && !e.IsConfirmed)
                .OrderByDescending(e => e.Id)
                .ToListAsync();
        }

        public async Task<EmailInquiry?> GetByIdAsync(int id)
        {
            var inquiry = await _context.EmailInquiries.FindAsync(id);
            return inquiry != null && _tenant.CanAccess(inquiry.OrgId) ? inquiry : null;
        }

        public async Task UpdateInquiryAsync(EmailInquiry inquiry)
        {
            _context.EmailInquiries.Update(inquiry);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Notification>> GetUnreadNotificationsAsync()
        {
            return await OrgNotifications
                .Where(n => !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<Notification?> GetNotificationByIdAsync(int id)
        {
            var n = await _context.Notifications.FindAsync(id);
            return n != null && _tenant.CanAccess(n.OrgId) ? n : null;
        }

        public async Task MarkAllNotificationsAsReadAsync()
        {
            var unread = await OrgNotifications.Where(n => !n.IsRead).ToListAsync();
            foreach (var n in unread) n.IsRead = true;
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// A vehicle of the enquiry's org that matches the type or name — or null.
        /// (Used to search every org's fleet and fall back to vehicle #1.)
        /// </summary>
        public async Task<int?> GetVehicleIdByTypeAsync(VechileType vehicleType, string? vehicleName, int orgId)
        {
            var fleet = _context.Vehicles.Where(v => v.OrgId == orgId);
            var vehicle = await fleet.FirstOrDefaultAsync(v => v.VehicleType == vehicleType);
            if (vehicle == null && vehicleName != null)
                vehicle = await fleet.FirstOrDefaultAsync(v => v.VehicleName!.ToLower() == vehicleName.ToLower());
            return vehicle?.VehicleId;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}

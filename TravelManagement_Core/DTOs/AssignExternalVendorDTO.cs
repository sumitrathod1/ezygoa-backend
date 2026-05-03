namespace TravelManagement.Core.DTOs
{
    public class AssignExternalVendorDTO
    {
        public int BookingId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string VendorNumber { get; set; } = string.Empty;
        public decimal CommissionAmount { get; set; }
        public string CashCollectedBy { get; set; } = string.Empty;
        public decimal AdvancePay { get; set; }
    }
}
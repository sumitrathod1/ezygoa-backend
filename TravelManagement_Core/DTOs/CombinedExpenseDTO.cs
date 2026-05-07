namespace TravelManagement.Core.DTOs
{
    public class CombinedExpenseVehicle
    {
        public string? VehicleName   { get; set; }
        public string? VehicleNumber { get; set; }
    }

    public class CombinedExpenseDTO
    {
        public int?                   VehicleExpenceId { get; set; }
        public int?                   SalaryId         { get; set; }
        public DateTime               ExpenseDate      { get; set; }
        public decimal                Amount           { get; set; }
        public string                 CategoryType     { get; set; } = "Other";
        public int?                   VehicleID        { get; set; }
        public CombinedExpenseVehicle? Vehicle         { get; set; }
        public string?                Notes            { get; set; }
        public string?                DriverName       { get; set; }
        public bool                   IsSalaryRecord   { get; set; }
    }
}

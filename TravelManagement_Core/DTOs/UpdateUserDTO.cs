namespace TravelManagement.Core.DTOs
{
    public class UpdateUserDTO
    {
        public string? EmployeeName   { get; set; }
        public string? UserName       { get; set; }
        public string? Address        { get; set; }
        public string? Email          { get; set; }
        public string? Number         { get; set; }
        public string? Password       { get; set; }  // optional — only updated when non-empty
        public DateOnly? EmployeeDOB  { get; set; }
        public int     Role           { get; set; }
        public int?    Licence        { get; set; }
        public decimal Salary         { get; set; }
        public int     SalaryDay      { get; set; } = 1;
        public bool    IsSalaryActive { get; set; }
        public string? BankAccount    { get; set; }
    }
}

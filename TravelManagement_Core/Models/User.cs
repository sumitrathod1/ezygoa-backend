using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public enum Licecnce
    {
        LMVC,
        Badge,
        HeavyBadge
    }

    public enum Role
    {
        Employee   = 0,
        Admin      = 1,
        SuperAdmin = 2,
    }

    public class User
    {
        [Key]
        public int userId { get; set; }
        public required string EmployeeName { get; set; }
        public string UserName { get; set; } = string.Empty;
        public DateOnly? EmployeeDOB { get; set; }
        public string? Address { get; set; }
        public Role Role { get; set; }
        public Licecnce? Licence { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        public required string Password { get; set; }

        [Required]
        public string? Number { get; set; }
        public decimal Salary { get; set; }
        public string? ResetPasswordtoken { get; set; }
        public DateTime RestPasswordExpiry { get; set; }
        public DateTime? RenewalMailSentDate { get; set; }
        public bool Status { get; set; }
        public int EmployeAge { get; set; }
        public string? FcmToken { get; set; }
        public int SalaryDay { get; set; } = 1;
        public bool IsSalaryActive { get; set; } = true;
        public string? BankAccount { get; set; }

        // Soft delete
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        // Auto salary tracking
        public DateTime? LastAutoSalaryDate { get; set; }

        // Multi-tenancy: 0 = SuperAdmin system account, 1+ = tenant org
        public int OrgId { get; set; } = 1;
    }
}
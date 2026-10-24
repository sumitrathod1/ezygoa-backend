using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;

namespace TravelManagement.API.Controllers
{
    /// <summary>
    /// Organisation-level spend: advertising (Google Ads, Meta Ads…), software, office,
    /// operational and custom categories. Vehicle costs stay under /Vehicle expenses.
    /// </summary>
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("api")]
    public class BusinessExpensesController : ApiControllerBase
    {
        public static readonly string[] DefaultCategories =
            { "Advertising", "Marketing", "Software", "Operational", "Driver", "Office", "Other" };
        public static readonly string[] Platforms =
            { "Google Ads", "Meta Ads", "Instagram", "Facebook", "YouTube", "Justdial", "Other" };
        public static readonly string[] PaymentMethods = { "UPI", "Card", "Bank transfer", "Cash", "Other" };

        private readonly AppDbContext _db;
        private readonly TenantContext _tenant;

        public BusinessExpensesController(AppDbContext db, TenantContext tenant)
        {
            _db = db;
            _tenant = tenant;
        }

        private IQueryable<BusinessExpense> Org =>
            _db.BusinessExpenses.Where(e => !e.IsDeleted && (!_tenant.ShouldFilter || e.OrgId == _tenant.OrgId));

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
            [FromQuery] string? category, [FromQuery] string? platform)
        {
            var q = Org.AsNoTracking();
            if (from.HasValue) q = q.Where(e => e.ExpenseDate >= from.Value);
            if (to.HasValue) q = q.Where(e => e.ExpenseDate <= to.Value);
            if (!string.IsNullOrWhiteSpace(category)) q = q.Where(e => e.Category == category);
            if (!string.IsNullOrWhiteSpace(platform)) q = q.Where(e => e.Platform == platform);
            return ApiOk(await q.OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.BusinessExpenseId).ToListAsync());
        }

        /// <summary>Default categories + any custom ones this org has used, and the platform list.</summary>
        [HttpGet("options")]
        public async Task<IActionResult> GetOptions()
        {
            var used = await Org.Select(e => e.Category).Distinct().ToListAsync();
            return ApiOk(new
            {
                categories = DefaultCategories.Concat(used.Where(u => !DefaultCategories.Contains(u, StringComparer.OrdinalIgnoreCase)))
                                              .OrderBy(c => Array.IndexOf(DefaultCategories, c) is var i && i >= 0 ? i : 100)
                                              .ThenBy(c => c),
                platforms = Platforms,
                paymentMethods = PaymentMethods,
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BusinessExpenseUpsertDTO dto)
        {
            var error = Validate(dto);
            if (error != null) return ApiBadRequest(error);

            var e = new BusinessExpense
            {
                OrgId = _tenant.WriteOrgId,
                CreatedByUserId = _tenant.UserId > 0 ? _tenant.UserId : null,
            };
            Apply(e, dto);
            _db.BusinessExpenses.Add(e);
            await _db.SaveChangesAsync();
            return ApiCreated(e, "Expense added");
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] BusinessExpenseUpsertDTO dto)
        {
            var e = await Org.FirstOrDefaultAsync(x => x.BusinessExpenseId == id);
            if (e == null) return ApiNotFound("Expense not found");
            var error = Validate(dto);
            if (error != null) return ApiBadRequest(error);

            Apply(e, dto);
            await _db.SaveChangesAsync();
            return ApiOk(e, "Expense updated");
        }

        /// <summary>Soft delete: the row stays for reports history and the audit log.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var e = await Org.FirstOrDefaultAsync(x => x.BusinessExpenseId == id);
            if (e == null) return ApiNotFound("Expense not found");
            e.IsDeleted = true;
            e.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return ApiOk("Expense deleted");
        }

        private static string? Validate(BusinessExpenseUpsertDTO? dto)
        {
            if (dto == null) return "Expense data is required";
            if (dto.Amount <= 0) return "Amount must be greater than zero";
            if (dto.Amount > 10_000_000) return "Amount looks too large — please check it";
            if (string.IsNullOrWhiteSpace(dto.Category)) return "Category is required";
            if (dto.Category.Trim().Length > 60) return "Category is too long";
            if (dto.ExpenseDate == default) return "Date is required";
            if (dto.ExpenseDate > DateOnly.FromDateTime(DateTime.Today.AddDays(1))) return "Date cannot be in the future";
            return null;
        }

        private static void Apply(BusinessExpense e, BusinessExpenseUpsertDTO dto)
        {
            static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            e.ExpenseDate   = dto.ExpenseDate;
            e.Amount        = Math.Round(dto.Amount, 2);
            e.Category      = dto.Category.Trim();
            e.Platform      = Clean(dto.Platform);
            e.CampaignName  = Clean(dto.CampaignName);
            e.Description   = Clean(dto.Description);
            e.PaymentMethod = Clean(dto.PaymentMethod);
        }
    }

    public class BusinessExpenseUpsertDTO
    {
        public DateOnly ExpenseDate { get; set; }
        public decimal Amount { get; set; }
        public string Category { get; set; } = "Other";
        public string? Platform { get; set; }
        public string? CampaignName { get; set; }
        public string? Description { get; set; }
        public string? PaymentMethod { get; set; }
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;

namespace TravelManagement.DataAccessLayer.Repository.Implementation
{
    /// <summary>Reads / saves an organisation's invoice terms; first use starts from the agreed defaults.</summary>
    public static class InvoiceTermsStore
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public static InvoiceTermsDTO Defaults() => new()
        {
            NightCharges = new()
            {
                new() { Label = "7 pm – 12 am", From = "19:00", To = "00:00", Amount = 500, AppliesTo = "FullDay" },
                new() { Label = "After 12 am", From = "00:00", To = "06:00", Amount = 1000, AppliesTo = "FullDay" },
                // Amount to be set by the owner — rows with ₹0 are not printed
                new() { Label = "Airport / station pickup or drop, 7 pm – 8 am", From = "19:00", To = "08:00", Amount = 0, AppliesTo = "Transfer" },
            },
            ExtraRates = new()
            {
                new() { Category = "Dzire", PerHour = 200, PerKm = 19 },
                new() { Category = "Ertiga", PerHour = 250, PerKm = 19 },
                new() { Category = "12 / 14 seater", PerHour = 300, PerKm = 35 },
                new() { Category = "20 seater", PerHour = 400, PerKm = 45 },
                new() { Category = "Urbania", PerHour = 500, PerKm = 50 },
            },
        };

        public static async Task<InvoiceTermsDTO> GetAsync(AppDbContext db, int orgId)
        {
            var row = await db.Set<InvoiceSettings>().AsNoTracking().FirstOrDefaultAsync(s => s.OrgId == orgId);
            if (row == null) return Defaults();
            return new InvoiceTermsDTO
            {
                NightCharges = JsonSerializer.Deserialize<List<NightChargeDTO>>(row.NightChargesJson, Json) ?? new(),
                ExtraRates   = JsonSerializer.Deserialize<List<ExtraRateDTO>>(row.ExtraRatesJson, Json) ?? new(),
                OtherTerms   = row.OtherTerms,
            };
        }

        public static async Task<InvoiceTermsDTO> SaveAsync(AppDbContext db, int orgId, InvoiceTermsDTO dto)
        {
            // Tidy: drop empty rows, valid times only, no negative money
            var nights = dto.NightCharges
                .Where(n => !string.IsNullOrWhiteSpace(n.Label) && TimeOnly.TryParse(n.From, out _) && TimeOnly.TryParse(n.To, out _))
                .Select(n => new NightChargeDTO
                {
                    Label = n.Label.Trim(), From = n.From, To = n.To, Amount = Math.Max(0, n.Amount),
                    AppliesTo = n.AppliesTo is "Transfer" or "All" ? n.AppliesTo : "FullDay",
                }).ToList();
            var rates = dto.ExtraRates
                .Where(r => !string.IsNullOrWhiteSpace(r.Category))
                .Select(r => new ExtraRateDTO { Category = r.Category.Trim(), PerHour = Math.Max(0, r.PerHour), PerKm = Math.Max(0, r.PerKm) })
                .ToList();

            var row = await db.Set<InvoiceSettings>().FirstOrDefaultAsync(s => s.OrgId == orgId);
            if (row == null)
            {
                row = new InvoiceSettings { OrgId = orgId };
                db.Add(row);
            }
            row.NightChargesJson = JsonSerializer.Serialize(nights, Json);
            row.ExtraRatesJson   = JsonSerializer.Serialize(rates, Json);
            row.OtherTerms       = string.IsNullOrWhiteSpace(dto.OtherTerms) ? null : dto.OtherTerms.Trim();
            row.UpdatedAt        = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return new InvoiceTermsDTO { NightCharges = nights, ExtraRates = rates, OtherTerms = row.OtherTerms };
        }

        /// <summary>Is the time inside the window? Windows may run past midnight (19:00 → 08:00).</summary>
        public static bool InWindow(TimeOnly t, string from, string to)
        {
            if (!TimeOnly.TryParse(from, out var f) || !TimeOnly.TryParse(to, out var e)) return false;
            if (f == e) return true;
            return f < e ? t >= f && t < e : t >= f || t < e;
        }
    }
}

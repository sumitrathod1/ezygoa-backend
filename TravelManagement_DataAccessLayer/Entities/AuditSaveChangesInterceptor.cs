using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TravelManagement.Core.Common;
using TravelManagement.Core.Models;

namespace TravelManagement.DataAccessLayer.Entities
{
    /// <summary>
    /// Writes an AuditLog row for every create / update / delete of a financial entity.
    /// Hooked into SaveChanges, so it covers every code path (API, bulk upload,
    /// background jobs) without each caller having to remember. The audit rows are
    /// saved in the same transaction as the change they describe. Generated text is
    /// plain ASCII on purpose, so the audit row can never fail on a database whose
    /// encoding lacks symbols like the rupee sign (that would block the payment itself).
    /// </summary>
    public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private static readonly Dictionary<Type, string> Audited = new()
        {
            [typeof(Booking)]                        = "Booking",
            [typeof(ExternalEmployeeCashCollection)] = "PartnerSettlement",
            [typeof(SettlementPayment)]              = "SettlementPayment",
            [typeof(BookingPaymentAllocation)]       = "PaymentAllocation",
            [typeof(Payments)]                       = "AgentPayment",
            [typeof(VehicleExpence)]                 = "VehicleExpense",
            [typeof(BusinessExpense)]                = "BusinessExpense",
            [typeof(TravelAgent)]                    = "TravelAgent",
            [typeof(ExternalEmployee)]               = "Partner",
            [typeof(Salary)]                         = "Salary",
            [typeof(Trip)]                           = "Trip",
        };

        // Bookkeeping columns that add noise without meaning
        private static readonly HashSet<string> Skip = new() { "CreatedAt", "Payments" };

        // Enum values stored by name ("Completed", not 3) so the activity log is readable
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };

        private readonly TenantContext _tenant;
        private readonly List<(EntityEntry Entry, AuditLog Log, string ChangeText)> _pending = new();
        private bool _writingAudit;

        public AuditSaveChangesInterceptor(TenantContext tenant) => _tenant = tenant;

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Capture(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Capture(eventData.Context);
            return ValueTask.FromResult(result);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            if (Flush(eventData.Context)) eventData.Context!.SaveChanges();
            _writingAudit = false;
            return result;
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (Flush(eventData.Context)) await eventData.Context!.SaveChangesAsync(cancellationToken);
            _writingAudit = false;
            return result;
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData) => _pending.Clear();

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            _pending.Clear();
            return Task.CompletedTask;
        }

        // ────────────────────────────────────────────────────────────────────

        private void Capture(DbContext? ctx)
        {
            if (ctx == null || _writingAudit) return;

            foreach (var entry in ctx.ChangeTracker.Entries())
            {
                if (!Audited.TryGetValue(entry.Metadata.ClrType, out var type)) continue;
                if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

                var changes = new Dictionary<string, object?>();
                foreach (var prop in entry.Properties)
                {
                    var name = prop.Metadata.Name;
                    if (Skip.Contains(name) || prop.Metadata.IsShadowProperty()) continue;

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            if (prop.Metadata.IsPrimaryKey() || prop.CurrentValue == null) continue;
                            changes[name] = prop.CurrentValue;
                            break;
                        case EntityState.Deleted:
                            changes[name] = prop.OriginalValue;
                            break;
                        default:
                            if (!prop.IsModified || Equals(prop.OriginalValue, prop.CurrentValue)) continue;
                            changes[name] = new { from = prop.OriginalValue, to = prop.CurrentValue };
                            break;
                    }
                }
                if (entry.State == EntityState.Modified && changes.Count == 0) continue;

                var action = entry.State switch
                {
                    EntityState.Added   => "Created",
                    EntityState.Deleted => "Deleted",
                    _                   => "Updated",
                };

                _pending.Add((entry, new AuditLog
                {
                    OrgId     = OrgOf(entry),
                    UserId    = _tenant.UserId > 0 ? _tenant.UserId : null,
                    UserName  = _tenant.UserId > 0 ? (_tenant.UserName ?? $"#{_tenant.UserId}") : "system",
                    Action    = action,
                    EntityType = type,
                    Changes   = JsonSerializer.Serialize(changes, JsonOpts),
                    CreatedAt = DateTime.UtcNow,
                }, entry.State == EntityState.Modified ? ChangeText(entry) : ""));
            }
        }

        /// <summary>Fills in keys (real only after the save) and queues the audit rows.</summary>
        private bool Flush(DbContext? ctx)
        {
            if (ctx == null || _writingAudit || _pending.Count == 0) return false;

            foreach (var (entry, log, changeText) in _pending)
            {
                var key = entry.Metadata.FindPrimaryKey();
                log.EntityId = key == null ? null
                    : string.Join(",", key.Properties.Select(p => entry.Property(p.Name).CurrentValue));
                log.BookingId = entry.Entity switch
                {
                    Booking b => b.BookingId,
                    ExternalEmployeeCashCollection s => s.BookingId,
                    SettlementPayment p => p.BookingId,
                    BookingPaymentAllocation a => a.BookingId,
                    Payments pay => pay.BookingId,
                    _ => null,
                };
                log.Summary = Describe(entry, log, changeText);
            }

            ctx.Set<AuditLog>().AddRange(_pending.Select(p => p.Log));
            _pending.Clear();
            _writingAudit = true;   // the follow-up save must not audit itself
            return true;
        }

        private int OrgOf(EntityEntry entry)
        {
            var orgProp = entry.Metadata.FindProperty("OrgId");
            if (orgProp != null && entry.Property("OrgId").CurrentValue is int org && org > 0) return org;
            return _tenant.OrgId;
        }

        private static string Money(object? v) =>
            v is decimal d ? "Rs." + d.ToString("#,##0.##", CultureInfo.InvariantCulture) : Convert.ToString(v, CultureInfo.InvariantCulture) ?? "-";

        private static string Describe(EntityEntry entry, AuditLog log, string changeText)
        {
            string subject = entry.Entity switch
            {
                Booking b                          => $"Booking #{b.BookingId}",
                ExternalEmployeeCashCollection s   => $"Partner settlement for booking #{s.BookingId}",
                SettlementPayment p                => p.EntryType == SettlementEntryType.Reversal
                                                        ? $"Reversal of {Money(p.Amount)} on booking #{p.BookingId}"
                                                        : $"{(p.Direction == "VendorToOwner" ? "Received" : "Paid")} {Money(p.Amount)} on booking #{p.BookingId}",
                BookingPaymentAllocation a         => $"Payment allocation ({a.PayerType}) on booking #{a.BookingId}",
                Payments pay                       => $"Agent payment {Money(pay.AmountPaid)} on booking #{pay.BookingId}",
                VehicleExpence e                   => $"Vehicle expense {Money(e.Amount)} ({e.CategoryType})",
                BusinessExpense e                  => $"Business expense {Money(e.Amount)} ({e.Category}{(string.IsNullOrEmpty(e.Platform) ? "" : ", " + e.Platform)})",
                TravelAgent t                      => $"Agent '{t.Name}'",
                ExternalEmployee x                 => $"Partner '{x.externalEmployeeName}'",
                Salary sal                         => $"Salary #{sal.SalaryId} ({sal.Month}/{sal.Year})",
                Trip trip                          => $"Trip #{trip.TripId}",
                _                                  => log.EntityType,
            };

            var verb = log.Action.ToLowerInvariant();
            if (log.Action != "Updated" || changeText.Length == 0) return $"{subject} {verb}";
            return $"{subject} updated: {changeText}";
        }

        /// <summary>Compact "Field: a → b" list. Must run BEFORE the save resets IsModified.</summary>
        private static string ChangeText(EntityEntry entry)
        {
            var parts = new List<string>();
            foreach (var prop in entry.Properties)
            {
                if (!prop.IsModified || Skip.Contains(prop.Metadata.Name)) continue;
                if (Equals(prop.OriginalValue, prop.CurrentValue)) continue;
                parts.Add($"{prop.Metadata.Name} {Money(prop.OriginalValue)} -> {Money(prop.CurrentValue)}");
                if (parts.Count == 4) { parts.Add("..."); break; }
            }
            return string.Join(", ", parts);
        }
    }
}

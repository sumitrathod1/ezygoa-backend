using TravelManagement.Core.Models;

namespace TravelManagement.Core.Common
{
    /// <summary>
    /// Partner settlement rules — the single source of truth used by the vendor ledger,
    /// partner statements and the earnings dashboard.
    ///
    /// Net position on an externally assigned booking, positive = partner owes us:
    ///   Cash collected by us      → we owe the partner (fare − commission)
    ///   Cash collected by partner → partner owes us (commission − advance we already took);
    ///                               if the advance exceeds the commission, we owe the difference.
    /// ExternalEmployeeCashCollection.TotalPaidToVendor holds the amount settled so far
    /// in the direction that is due.
    /// </summary>
    public static class SettlementMath
    {
        public static decimal NetPosition(ExternalEmployeeCashCollection s, decimal advance) =>
            s.CashCollectedBy == CashCollectedBy.Admin
                ? -(s.BookingAmount - s.CommissionAmount)
                : s.CommissionAmount - advance;

        public static decimal SettledSoFar(ExternalEmployeeCashCollection s, decimal due) =>
            s.SettledAt != null ? due : Math.Min(Math.Max(s.TotalPaidToVendor, 0), due);

        /// <summary>Commission still to be collected from the partner (0 when we hold the cash).</summary>
        public static decimal CommissionOutstanding(ExternalEmployeeCashCollection s, decimal advance)
        {
            var net = NetPosition(s, advance);
            if (net <= 0) return 0;
            return net - SettledSoFar(s, net);
        }

        /// <summary>What we still have to pay the partner.</summary>
        public static decimal PayableOutstanding(ExternalEmployeeCashCollection s, decimal advance)
        {
            var net = NetPosition(s, advance);
            if (net >= 0) return 0;
            var due = -net;
            return due - SettledSoFar(s, due);
        }
    }
}

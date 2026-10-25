using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TravelManagement.Core.Common;
using TravelManagement.Core.DTOs;

namespace TravelManagement.API.Reports
{
    /// <summary>
    /// Commission statement shared with an external partner. Contains only that
    /// partner's bookings and payments — never expenses, other partners or internal figures.
    /// Wording is deliberately plain ("You give ZipGoa ₹500") so any driver can read it.
    /// </summary>
    public static class PartnerStatementPdf
    {
        private const string Brand = "#4B2AAD";
        private const string Muted = "#64748B";
        private const string Line  = "#E2E8F0";
        private const string Give  = "#B91C1C";   // money the partner gives
        private const string Get   = "#15803D";   // money the partner gets

        private static string Rs(decimal v) => "₹" + v.ToString("#,##0.##", CultureInfo.GetCultureInfo("en-IN"));
        private static string D(DateOnly d) => d.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

        public static byte[] Generate(PartnerStatementData s) => Build(s).GeneratePdf();

        public static IDocument Build(PartnerStatementData s)
        {
            string period = s.CustomerId.HasValue ? $"All bookings of {s.CustomerName}"
                : s.BookingId.HasValue ? $"Booking #{s.BookingId}"
                : s.From.HasValue && s.To.HasValue ? $"{D(s.From.Value)} – {D(s.To.Value)}"
                : s.From.HasValue ? $"From {D(s.From.Value)}"
                : s.To.HasValue ? $"Up to {D(s.To.Value)}"
                : "All bookings";
            if (s.OnlyOpen) period += " · only bookings not fully paid";

            // One customer (all their bookings) or one booking: say plainly where the money stands
            bool forCustomer = s.CustomerId.HasValue || s.BookingId.HasValue;
            var live = s.Lines.Where(l => !l.Cancelled).ToList();
            decimal custDue = live.Sum(l => l.Due), custPaid = live.Sum(l => l.Settled), custLeft = live.Sum(l => l.Remaining);
            string? custStatus = !forCustomer ? null
                : live.Count == 0 ? "All trips cancelled"
                : custDue == 0 ? "Nothing to pay"
                : custLeft == 0 ? "Fully paid"
                : custPaid > 0 ? $"Part paid: {Rs(custPaid)} of {Rs(custDue)} given"
                : "Not paid yet";
            string? custLine = s.CustomerId.HasValue
                ? $"Customer: {s.CustomerName}  ·  {live.Count} booking{(live.Count == 1 ? "" : "s")}"
                : s.BookingId.HasValue && s.Lines.Count > 0
                    ? $"Customer: {s.Lines[0].Customer}  ·  Booking #{s.Lines[0].BookingId}  ·  {D(s.Lines[0].TravelDate)}"
                    : null;

            decimal balance = s.PartnerStillOwes - s.WeStillOwe;
            string org = string.IsNullOrWhiteSpace(s.OrgName) ? "us" : s.OrgName;
            var logo = PdfLogo.Load(s.OrgLogo);

            return Document.Create(doc => doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor("#0F172A"));

                // ── Header ───────────────────────────────────────────────────
                page.Header().PaddingBottom(10).BorderBottom(2).BorderColor(Brand).PaddingBottom(8).Row(row =>
                {
                    if (logo != null)
                    {
                        PdfLogo.Tile(row.ConstantItem(54).AlignMiddle(), logo, 54);
                        row.ConstantItem(10);
                    }
                    row.RelativeItem().AlignMiddle().Column(c =>
                    {
                        c.Item().Text(s.OrgName).FontSize(20).Bold().FontColor(Brand);
                        var contact = string.Join("  ·  ", new[] { s.OrgPhone, s.OrgEmail }.Where(x => !string.IsNullOrWhiteSpace(x)));
                        if (contact.Length > 0) c.Item().Text(contact).FontColor(Muted);
                        if (!string.IsNullOrWhiteSpace(s.OrgAddress)) c.Item().Text(s.OrgAddress).FontColor(Muted);
                    });
                    row.ConstantItem(260).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("PARTNER STATEMENT").FontSize(12).Bold();
                        c.Item().AlignRight().Text(period).FontColor(Muted);
                        c.Item().AlignRight().Text($"Generated {s.GeneratedAt:dd MMM yyyy, hh:mm tt}").FontColor(Muted);
                    });
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Spacing(12);

                    // Partner, and the one line that matters: who gives whom how much
                    col.Item().Background("#F5F3FF").Padding(10).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Statement for").FontColor(Muted);
                            c.Item().Text(s.PartnerName).FontSize(13).Bold();
                            if (!string.IsNullOrWhiteSpace(s.PartnerNumber)) c.Item().Text("Phone: " + s.PartnerNumber);
                            if (forCustomer && custLine != null)
                            {
                                c.Item().PaddingTop(6).Text(custLine);
                                c.Item().Text(custStatus!).Bold()
                                    .FontColor(custLeft == 0 ? Get : custPaid > 0 ? "#B45309" : Give);
                            }
                        });
                        r.ConstantItem(300).AlignRight().AlignMiddle().Column(c =>
                        {
                            c.Item().AlignRight().Text(balance > 0 ? $"You need to give {org}"
                                : balance < 0 ? $"{org} will give you" : "All clear").FontSize(11).Bold();
                            c.Item().AlignRight().Text(balance == 0 ? "Nothing to pay" : Rs(Math.Abs(balance)))
                                .FontSize(18).Bold().FontColor(balance > 0 ? Give : balance < 0 ? Get : "#0F172A");
                        });
                    });

                    // Summary
                    col.Item().Row(r =>
                    {
                        void Box(string label, string value, string color)
                        {
                            r.RelativeItem().Border(1).BorderColor(Line).Padding(8).Column(c =>
                            {
                                c.Item().Text(label).FontColor(Muted);
                                c.Item().Text(value).FontSize(12).Bold().FontColor(color);
                            });
                            r.ConstantItem(8);
                        }
                        Box("Trips", s.Lines.Count(l => !l.Cancelled).ToString(), "#0F172A");
                        if (s.ShowFare) Box("Your earning", Rs(s.TotalPartnerEarning), Get);
                        Box($"{org} commission", Rs(s.TotalCommission), "#1D4ED8");
                        Box($"You gave {org}", Rs(s.ReceivedFromPartner), "#0F172A");
                        Box($"{org} gave you", Rs(s.PaidToPartner), "#0F172A");
                        Box($"You still give {org}", Rs(s.PartnerStillOwes), s.PartnerStillOwes > 0 ? Give : "#0F172A");
                        Box($"{org} still gives you", Rs(s.WeStillOwe), s.WeStillOwe > 0 ? Get : "#0F172A");
                    });
                    col.Item().Text(x =>
                    {
                        x.Span("Red").Bold().FontColor(Give);
                        x.Span($" = money you give {org}.     ").FontColor(Muted);
                        x.Span("Green").Bold().FontColor(Get);
                        x.Span($" = money {org} gives you.").FontColor(Muted);
                    });

                    // Trips: every header is aligned the same way as the values under it
                    col.Item().Text("Trips").FontSize(11).Bold();
                    col.Item().Table(t =>
                    {
                        var cols = new List<(string Title, bool Right)>
                        {
                            ("Date", false), ("Booking", false), ("Trip", false), ("Customer", false),
                        };
                        if (s.ShowFare) cols.Add(("Your earning", true));
                        cols.Add(($"{org} commission", true));
                        cols.Add(("Customer paid", false));
                        cols.Add(("Who pays", false));
                        cols.Add(("Paid", true));
                        cols.Add(("Left to pay", true));

                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(60);      // date
                            c.ConstantColumn(46);      // booking
                            c.RelativeColumn(2.6f);    // trip
                            c.RelativeColumn(1.6f);    // customer
                            if (s.ShowFare) c.ConstantColumn(64);
                            c.ConstantColumn(64);      // commission
                            c.ConstantColumn(58);      // customer paid
                            c.ConstantColumn(96);      // who pays
                            c.ConstantColumn(56);      // paid
                            c.ConstantColumn(62);      // left to pay
                        });

                        t.Header(h =>
                        {
                            foreach (var (title, right) in cols)
                            {
                                var cell = h.Cell().Background(Brand).PaddingVertical(5).PaddingHorizontal(4);
                                (right ? cell.AlignRight() : cell.AlignLeft())
                                    .Text(title).FontColor(Colors.White).Bold().FontSize(8.5f);
                            }
                        });

                        var i = 0;
                        foreach (var l in s.Lines)
                        {
                            var bg = i++ % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                            var partnerGives = l.Direction == "VendorToOwner";
                            var color = partnerGives ? Give : Get;
                            IContainer Cell() => t.Cell().Background(bg).BorderBottom(1).BorderColor(Line).PaddingVertical(4).PaddingHorizontal(4);
                            Cell().Text(D(l.TravelDate));
                            Cell().Text("#" + l.BookingId);
                            Cell().Text(l.Route);
                            Cell().Text(l.Customer);
                            if (s.ShowFare)
                                Cell().AlignRight().Text(l.Cancelled ? "—" : Rs(l.PartnerEarning))
                                    .Bold().FontColor(l.Cancelled ? Muted : Get);
                            Cell().AlignRight().Text(Rs(l.Commission));
                            Cell().Text(l.CashCollectedBy == "Us" ? org : "You");
                            if (l.Cancelled)
                                Cell().Text("Trip cancelled").FontColor(Muted);
                            else if (l.Due == 0)
                                Cell().Text("Nothing to pay").FontColor(Muted);
                            else
                                Cell().Text(x =>
                                {
                                    x.Line(partnerGives ? $"You give {org}" : $"{org} gives you").FontSize(8);
                                    x.Span(Rs(l.Due)).Bold().FontColor(color);
                                });
                            Cell().AlignRight().Text(Rs(l.Settled));
                            if (l.Cancelled)
                                Cell().AlignRight().Text("—").FontColor(Muted);
                            else if (l.Remaining == 0)
                                Cell().AlignRight().Text("Done").Bold().FontColor(Muted);
                            else
                                Cell().AlignRight().Text(Rs(l.Remaining)).Bold().FontColor(color);
                        }

                        if (s.Lines.Count == 0)
                            t.Cell().ColumnSpan((uint)cols.Count).Padding(10).AlignCenter()
                                .Text("No trips in this period.").FontColor(Muted);
                    });

                    // Payments
                    col.Item().Text("Money given so far").FontSize(11).Bold();
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(70);
                            c.ConstantColumn(60);
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(1);
                            c.RelativeColumn(2);
                            c.ConstantColumn(80);
                        });
                        t.Header(h =>
                        {
                            var heads = new[] { ("Date", false), ("Booking", false), ("Who gave", false), ("How", false), ("Reference / note", false), ("Amount", true) };
                            foreach (var (title, right) in heads)
                            {
                                var cell = h.Cell().Background("#EEF2FF").PaddingVertical(4).PaddingHorizontal(4);
                                (right ? cell.AlignRight() : cell.AlignLeft()).Text(title).Bold().FontSize(8.5f);
                            }
                        });
                        foreach (var p in s.Payments)
                        {
                            var reversal = p.EntryType == "Reversal";
                            var type = reversal ? "Correction (entry cancelled)"
                                : p.Direction == "VendorToOwner" ? $"You gave {org}" : $"{org} gave you";
                            IContainer Cell() => t.Cell().BorderBottom(1).BorderColor(Line).PaddingVertical(4).PaddingHorizontal(4);
                            Cell().Text(D(p.PaidOn));
                            Cell().Text("#" + p.BookingId);
                            Cell().Text(type + (p.IsReversed ? " (cancelled)" : "")).FontColor(reversal || p.IsReversed ? Muted : "#0F172A");
                            Cell().Text(p.PaymentMethod ?? "—");
                            Cell().Text(string.Join(" · ", new[] { p.Reference, p.Notes }.Where(x => !string.IsNullOrWhiteSpace(x))));
                            Cell().AlignRight().Text((reversal ? "− " : "") + Rs(p.Amount));
                        }
                        if (s.Payments.Count == 0)
                            t.Cell().ColumnSpan(6).Padding(8).AlignCenter().Text("No money given yet.").FontColor(Muted);
                    });

                    if (s.EarlierUnitemised > 0)
                        col.Item().Text($"Includes {Rs(s.EarlierUnitemised)} settled before itemised payment records were kept.")
                            .FontColor(Muted).Italic();
                });

                page.Footer().PaddingTop(6).BorderTop(1).BorderColor(Line).Row(r =>
                {
                    r.RelativeItem().Text($"Shows trips with travel dates in the period above and all money given for them. If anything looks wrong, please tell {org} within 7 days.")
                        .FontSize(7.5f).FontColor(Muted);
                    r.ConstantItem(80).AlignRight().Text(x =>
                    {
                        x.Span("Page ").FontSize(7.5f).FontColor(Muted);
                        x.CurrentPageNumber().FontSize(7.5f).FontColor(Muted);
                        x.Span(" of ").FontSize(7.5f).FontColor(Muted);
                        x.TotalPages().FontSize(7.5f).FontColor(Muted);
                    });
                });
            }));
        }
    }
}

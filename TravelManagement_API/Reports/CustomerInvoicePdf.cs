using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TravelManagement.Core.Common;
using TravelManagement.Core.DTOs;

namespace TravelManagement.API.Reports
{
    /// <summary>
    /// Customer invoice for one booking, a whole trip, or all of a customer's bookings.
    /// Header shows the booking's own organisation — never a hard-coded company.
    /// </summary>
    public static class CustomerInvoicePdf
    {
        private const string Brand = "#4B2AAD";
        private const string Muted = "#64748B";
        private const string Line  = "#E2E8F0";
        private const string Due   = "#B91C1C";
        private const string Ok    = "#15803D";

        private static string Rs(decimal v) => "₹" + v.ToString("#,##0.##", CultureInfo.GetCultureInfo("en-IN"));
        private static string D(DateOnly d) => d.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        private static string T(TimeOnly? t) => t?.ToString("hh:mm tt", CultureInfo.InvariantCulture) ?? "";

        /// <summary>"AirportPickup" → "Airport Pickup".</summary>
        private static string Pretty(string type) =>
            string.Concat(type.Select((c, i) => i > 0 && char.IsUpper(c) && !char.IsUpper(type[i - 1]) ? " " + c : c.ToString()));

        public static byte[] Generate(CustomerInvoiceData s) => Build(s).GeneratePdf();

        public static IDocument Build(CustomerInvoiceData s)
        {
            var logo = PdfLogo.Load(s.OrgLogo);
            return Document.Create(doc => doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor("#0F172A"));

                // ── Header: the organisation ────────────────────────────────
                page.Header().Background(Brand).Padding(16).Row(row =>
                {
                    if (logo != null)
                    {
                        PdfLogo.Tile(row.ConstantItem(58).AlignMiddle(), logo, 58);
                        row.ConstantItem(12);
                    }
                    row.RelativeItem().AlignMiddle().Column(c =>
                    {
                        c.Item().Text(s.OrgName).FontSize(20).Bold().FontColor(Colors.White);
                        var contact = string.Join("   ·   ", new[] { s.OrgPhone, s.OrgEmail }.Where(x => !string.IsNullOrWhiteSpace(x)));
                        if (contact.Length > 0) c.Item().PaddingTop(2).Text(contact).FontColor("#E0E7FF");
                        if (!string.IsNullOrWhiteSpace(s.OrgAddress)) c.Item().Text(s.OrgAddress).FontColor("#E0E7FF");
                    });
                    row.ConstantItem(170).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("INVOICE").FontSize(16).Bold().FontColor(Colors.White);
                        c.Item().AlignRight().Text(s.InvoiceNo).FontColor("#E0E7FF");
                        c.Item().AlignRight().Text(D(s.InvoiceDate)).FontColor("#E0E7FF");
                    });
                });

                page.Content().PaddingTop(14).Column(col =>
                {
                    col.Spacing(12);

                    // Billed to
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Background("#F5F3FF").Padding(10).Column(c =>
                        {
                            c.Item().Text("BILLED TO").FontSize(8).Bold().FontColor(Brand);
                            c.Item().Text(s.CustomerName).FontSize(13).Bold();
                            if (!string.IsNullOrWhiteSpace(s.CustomerNumber)) c.Item().Text(s.CustomerNumber);
                            if (!string.IsNullOrWhiteSpace(s.StayName)) c.Item().PaddingTop(2).Text("Stay: " + s.StayName).FontColor(Muted);
                        });
                        r.ConstantItem(10);
                        r.ConstantItem(190).Background("#F5F3FF").Padding(10).Column(c =>
                        {
                            c.Item().Text("BALANCE DUE").FontSize(8).Bold().FontColor(Brand);
                            c.Item().Text(s.Balance == 0 ? "Fully paid" : Rs(s.Balance))
                                .FontSize(18).Bold().FontColor(s.Balance == 0 ? Ok : Due);
                            c.Item().Text($"{s.Lines.Count} booking{(s.Lines.Count == 1 ? "" : "s")}").FontColor(Muted);
                        });
                    });

                    // Bookings
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(78);     // date / time
                            c.RelativeColumn();       // trip details
                            c.ConstantColumn(66);     // amount
                            c.ConstantColumn(62);     // paid
                            c.ConstantColumn(66);     // balance
                        });
                        t.Header(h =>
                        {
                            foreach (var (title, right) in new[] { ("Date", false), ("Trip details", false), ("Amount", true), ("Paid", true), ("Balance", true) })
                            {
                                var cell = h.Cell().Background(Brand).PaddingVertical(5).PaddingHorizontal(5);
                                (right ? cell.AlignRight() : cell.AlignLeft()).Text(title).FontColor(Colors.White).Bold().FontSize(9);
                            }
                        });

                        var i = 0;
                        foreach (var l in s.Lines)
                        {
                            var bg = i++ % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                            IContainer Cell() => t.Cell().Background(bg).BorderBottom(1).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(5);
                            Cell().Column(c =>
                            {
                                c.Item().Text(D(l.TravelDate)).Bold();
                                if (l.TravelTime.HasValue) c.Item().Text(T(l.TravelTime)).FontColor(Muted);
                                if (l.ReturnDate.HasValue && l.ReturnDate != l.TravelDate) c.Item().Text("to " + D(l.ReturnDate.Value)).FontColor(Muted);
                            });
                            Cell().Column(c =>
                            {
                                c.Item().Text(x =>
                                {
                                    x.Span(string.IsNullOrEmpty(l.TypeLabel) ? Pretty(l.BookingType) : l.TypeLabel).Bold();
                                    x.Span($"   #{l.BookingId}").FontColor(Muted).FontSize(8);
                                    if (l.Status == "Canceled") x.Span("   CANCELLED").FontColor(Due).Bold().FontSize(8);
                                });
                                c.Item().Text($"{l.From ?? "—"}  →  {l.To ?? "—"}");
                                var extra = string.Join("  ·  ", new[]
                                {
                                    l.Pax > 0 ? $"{l.Pax} passenger{(l.Pax == 1 ? "" : "s")}" : null,
                                    l.Vehicle, l.Driver != null ? "Driver: " + l.Driver : null,
                                }.Where(x => !string.IsNullOrWhiteSpace(x)));
                                if (extra.Length > 0) c.Item().Text(extra).FontColor(Muted).FontSize(8.5f);
                            });
                            Cell().AlignRight().Text(Rs(l.Amount));
                            Cell().AlignRight().Text(Rs(l.Paid)).FontColor(l.Paid > 0 ? Ok : Muted);
                            Cell().AlignRight().Text(l.Balance == 0 ? "Paid" : Rs(l.Balance)).Bold().FontColor(l.Balance == 0 ? Ok : Due);
                        }
                    });

                    // Totals
                    col.Item().AlignRight().Width(240).Column(c =>
                    {
                        void Row(string label, string value, string color, bool big = false)
                        {
                            c.Item().BorderBottom(1).BorderColor(Line).PaddingVertical(5).Row(r =>
                            {
                                r.RelativeItem().Text(label).FontColor(big ? "#0F172A" : Muted).Bold();
                                var v = r.AutoItem().Text(value).Bold().FontColor(color);
                                if (big) v.FontSize(14);
                            });
                        }
                        Row(s.Lines.Count > 1 ? "Total (all bookings)" : "Total", Rs(s.Total), "#0F172A");
                        Row("Advance paid", Rs(s.Paid), Ok);
                        Row("Balance due", s.Balance == 0 ? "Fully paid" : Rs(s.Balance), s.Balance == 0 ? Ok : Due, big: true);
                    });

                    // Terms & conditions (only when asked for, only what applies to these bookings)
                    var tc = s.Terms;
                    if (tc != null && (tc.NightCharges.Count > 0 || tc.ExtraRates.Count > 0 || tc.OtherTerms.Count > 0))
                    {
                        col.Item().PaddingTop(4).Background("#F8FAFC").Border(1).BorderColor(Line).Padding(10).Column(c =>
                        {
                            c.Spacing(4);
                            c.Item().Text("TERMS & CONDITIONS").FontSize(9).Bold().FontColor(Brand);

                            if (tc.NightCharges.Count > 0)
                            {
                                c.Item().Text("Driver night charges").Bold();
                                foreach (var n in tc.NightCharges)
                                    c.Item().Row(r =>
                                    {
                                        // "(Full Day)" only when the row's own name doesn't already say it
                                        var name = n.Label.ToLowerInvariant();
                                        var applies = n.AppliesTo == "FullDay" && !name.Contains("full day") ? "  (Full Day)"
                                            : n.AppliesTo == "Transfer" && !name.Contains("airport") && !name.Contains("station") ? "  (airport / station pickup or drop)"
                                            : "";
                                        r.RelativeItem().Text($"•  {n.Label}{applies}");
                                        r.ConstantItem(70).AlignRight().Text(Rs(n.Amount)).Bold();
                                    });
                            }

                            if (tc.ExtraRates.Count > 0)
                            {
                                var pkg = tc.Packages.Count > 0 ? string.Join(" / ", tc.Packages) : "the Full Day package";
                                c.Item().PaddingTop(4).Text($"Extra time and distance after {pkg}").Bold();
                                c.Item().Table(t =>
                                {
                                    t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.ConstantColumn(90); cd.ConstantColumn(80); });
                                    t.Header(h =>
                                    {
                                        h.Cell().PaddingVertical(3).Text(tc.ExtraRatesForVehicle ? "Your vehicle" : "Vehicle").FontColor(Muted).FontSize(8.5f);
                                        h.Cell().PaddingVertical(3).AlignRight().Text("Per extra hour").FontColor(Muted).FontSize(8.5f);
                                        h.Cell().PaddingVertical(3).AlignRight().Text("Per extra km").FontColor(Muted).FontSize(8.5f);
                                    });
                                    foreach (var x in tc.ExtraRates)
                                    {
                                        t.Cell().BorderTop(1).BorderColor(Line).PaddingVertical(3).Text(x.Category);
                                        t.Cell().BorderTop(1).BorderColor(Line).PaddingVertical(3).AlignRight().Text(Rs(x.PerHour));
                                        t.Cell().BorderTop(1).BorderColor(Line).PaddingVertical(3).AlignRight().Text(Rs(x.PerKm));
                                    }
                                });
                            }

                            foreach (var line in tc.OtherTerms)
                                c.Item().Text("•  " + line).FontColor("#334155");
                        });
                    }

                    col.Item().PaddingTop(6).AlignCenter().Text($"Thank you for travelling with {s.OrgName}!").FontSize(11).FontColor(Brand).Bold();
                    // Who to call — the organisation's own phone / WhatsApp / email
                    var help = string.Join("   ·   ", new[]
                    {
                        string.IsNullOrWhiteSpace(s.OrgPhone) ? null : $"Call / WhatsApp: {s.OrgPhone}",
                        string.IsNullOrWhiteSpace(s.OrgEmail) ? null : $"Email: {s.OrgEmail}",
                    }.Where(x => x != null));
                    if (help.Length > 0)
                        col.Item().AlignCenter().Text(x =>
                        {
                            x.Span("Need help? ").FontColor(Muted);
                            x.Span(help).Bold();
                        });
                });

                page.Footer().PaddingTop(6).BorderTop(1).BorderColor(Line).Row(r =>
                {
                    var contact = string.Join("  ·  ", new[] { s.OrgName, s.OrgPhone, s.OrgEmail }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    r.RelativeItem().Text($"For any questions about this invoice contact {contact}.").FontSize(7.5f).FontColor(Muted);
                    r.ConstantItem(70).AlignRight().Text(x =>
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

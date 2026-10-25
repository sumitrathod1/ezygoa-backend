using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace TravelManagement.Core.Common
{
    /// <summary>The organisation's own logo (stored as a data URL) for PDF headers.</summary>
    public static class PdfLogo
    {
        /// <summary>
        /// "data:image/png;base64,…" → a QuestPDF image, or null when there is no logo or it
        /// can't be read (a bad logo must never stop an invoice from being made).
        /// </summary>
        public static Image? Load(string? dataUrl)
        {
            if (string.IsNullOrWhiteSpace(dataUrl)) return null;
            var comma = dataUrl.IndexOf(',');
            if (!dataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) || comma < 0) return null;
            try
            {
                return Image.FromBinaryData(Convert.FromBase64String(dataUrl[(comma + 1)..]));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Logo on a white rounded tile, so it reads on any header colour.</summary>
        public static void Tile(IContainer container, Image logo, float size) =>
            container.Width(size).Height(size).Background("#FFFFFF").CornerRadius(8).Padding(4)
                .AlignCenter().AlignMiddle().Image(logo).FitArea();
    }
}

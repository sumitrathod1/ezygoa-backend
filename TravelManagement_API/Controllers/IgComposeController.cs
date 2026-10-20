/*
 * Instagram post composer — builds a branded 1080x1350 post from a real vehicle photo
 * ====================================================================================
 * POST https://<domain>/api/ig-images/compose        (multipart/form-data)
 *   photo     file  required  jpg / png / webp, up to 20 MB, any orientation
 *   headline  text  required  ~40 chars; auto-shrinks, max 2 lines, then truncates
 *   subline   text  optional  ~90 chars; max 2 lines
 *   label     text  optional  eyebrow above the headline, e.g. "Urbania · 13 Seater"
 *   name      text  optional  source id (n8n sends the Drive file id) — goes into the file name
 *
 * Auth:    X-Upload-Key header must match IG_UPLOAD_KEY (same key as the InstaBank upload)
 * Returns: { url, name }. The JPEG is written to IG_IMAGES_PATH, so it appears in InstaBank
 *          and is served publicly at /ig/<name> for the Instagram Graph API.
 *
 * Fonts:   Assets/Fonts/Poppins-*.ttf (SIL Open Font License, see Assets/Fonts/OFL.txt)
 * Phones:  hard-coded below on purpose — only these two numbers may appear on posts.
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.RegularExpressions;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TravelManagement.API.Controllers
{
    [Route("api/ig-images/compose")]
    [EnableRateLimiting("api")]
    public class IgComposeController : ApiControllerBase
    {
        private const long MaxPhotoBytes = 20L * 1024 * 1024;

        private readonly IConfiguration _config;
        private readonly ILogger<IgComposeController> _logger;

        public IgComposeController(IConfiguration config, ILogger<IgComposeController> logger)
        {
            _config = config;
            _logger = logger;
        }

        // ── POST /api/ig-images/compose ─────────────────────────────────────
        [HttpPost]
        [RequestSizeLimit(25 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 25 * 1024 * 1024)]
        public async Task<IActionResult> Compose(
            IFormFile? photo,
            [FromForm] string? headline,
            [FromForm] string? subline,
            [FromForm] string? label,
            [FromForm] string? name)
        {
            if (!IsAuthorized()) return ApiUnauthorized("Invalid or missing X-Upload-Key");

            if (photo is null || photo.Length == 0)
                return ApiBadRequest("No photo provided (multipart field 'photo')");

            if (photo.Length > MaxPhotoBytes)
                return ApiBadRequest("Photo exceeds 20 MB");

            if (string.IsNullOrWhiteSpace(headline))
                return ApiBadRequest("headline is required");

            var folder = _config["IG_IMAGES_PATH"] ?? "/data/ig";
            Directory.CreateDirectory(folder);

            var slug     = SanitizeName(string.IsNullOrWhiteSpace(name) ? "post" : name);
            // random suffix: n8n sends both weekly posts in parallel, often in the same second
            var suffix   = Guid.NewGuid().ToString("N")[..6];
            var fileName = $"post-{slug}-{DateTime.UtcNow:yyyyMMddHHmmss}-{suffix}.jpg";
            var destPath = System.IO.Path.Combine(folder, fileName);

            try
            {
                await using var input = photo.OpenReadStream();
                using var post = await IgPostRenderer.RenderAsync(
                    input,
                    label?.Trim() ?? string.Empty,
                    headline.Trim(),
                    subline?.Trim() ?? string.Empty);

                await post.SaveAsJpegAsync(destPath, new JpegEncoder { Quality = 90 });
            }
            catch (ImageFormatException ex)
            {
                _logger.LogWarning(ex, "IG compose: unreadable photo {File}", photo.FileName);
                return ApiBadRequest("Photo could not be read — use JPG, PNG or WEBP (iPhone HEIC is not supported)");
            }

            return ApiCreated(new { url = BuildUrl(fileName), name = fileName }, "Post image created");
        }

        // ── Helpers (same rules as IgImagesController) ──────────────────────

        private bool IsAuthorized()
        {
            var key = _config["IG_UPLOAD_KEY"];
            if (string.IsNullOrEmpty(key)) return false;
            Request.Headers.TryGetValue("X-Upload-Key", out var provided);
            return provided == key;
        }

        private string BuildUrl(string fileName)
        {
            var baseUrl = _config["PUBLIC_API_BASE"]?.TrimEnd('/')
                       ?? $"{Request.Scheme}://{Request.Host}";
            return $"{baseUrl}/ig/{fileName}";
        }

        private static string SanitizeName(string raw)
        {
            var slug = Regex.Replace(raw.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            if (slug.Length > 60) slug = slug[..60].Trim('-');
            return string.IsNullOrEmpty(slug) ? "post" : slug;
        }
    }

    /// <summary>
    /// Draws the ZipGoa post template. Layout (1080 x 1350, Instagram 4:5):
    ///   0–820     photo, cover-cropped, fades into navy at the bottom
    ///   820–1240  navy panel: teal label, white headline, muted subline (block centred)
    ///   1240–1350 orange bar: "Book on WhatsApp" + the two booking numbers
    /// </summary>
    internal static class IgPostRenderer
    {
        private const int W = 1080, H = 1350, PhotoH = 820, BarH = 110, Mx = 64, FadeH = 160;
        private const string Phones = "70268 89254  ·  79720 09209";

        private static readonly Color Navy   = Color.ParseHex("1E3A5F");
        private static readonly Color Teal   = Color.ParseHex("2BB5C0");
        private static readonly Color Orange = Color.ParseHex("FF6B35");
        private static readonly Color Muted  = Color.ParseHex("C9D6E3");

        private sealed record Face(FontFamily Family, FontStyle Style)
        {
            public Font At(float size) => Family.CreateFont(size, Style);
        }

        // One collection per file so Medium / SemiBold / Bold never collide on family name.
        private static readonly Lazy<(Face Bold, Face Semi, Face Medium)> Faces = new(() =>
        {
            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts");
            Face Load(string file)
            {
                var family = new FontCollection().Add(System.IO.Path.Combine(dir, file), out var description);
                return new Face(family, description.Style);
            }
            return (Load("Poppins-Bold.ttf"), Load("Poppins-SemiBold.ttf"), Load("Poppins-Medium.ttf"));
        });

        public static async Task<Image<Rgba32>> RenderAsync(Stream photoStream, string label, string headline, string subline)
        {
            var canvas = new Image<Rgba32>(W, H, Navy.ToPixel<Rgba32>());
            try
            {
                await DrawAsync(canvas, photoStream, label, headline, subline);
                return canvas;
            }
            catch
            {
                canvas.Dispose();
                throw;
            }
        }

        private static async Task DrawAsync(Image<Rgba32> canvas, Stream photoStream, string label, string headline, string subline)
        {
            var faces = Faces.Value;

            using (var photo = await Image.LoadAsync<Rgba32>(photoStream))
            {
                photo.Mutate(p => p
                    .AutoOrient()
                    .Resize(new ResizeOptions
                    {
                        Size     = new Size(W, PhotoH),
                        Mode     = ResizeMode.Crop,
                        Position = AnchorPositionMode.Center
                    }));
                canvas.Mutate(c => c.DrawImage(photo, new Point(0, 0), 1f));
            }

            float width = W - 2 * Mx;
            var (hFont, hSize, hLines) = Fit(headline, faces.Bold, 68, 52, width, 2);
            var (sFont, sSize, sLines) = subline.Length > 0
                ? Fit(subline, faces.Medium, 34, 28, width, 2)
                : (faces.Medium.At(34), 34f, new List<string>());

            const float labelSize = 28f;
            float block = labelSize * 1.5f + 12
                        + hLines.Count * hSize * 1.12f
                        + (sLines.Count > 0 ? 22 + sLines.Count * sSize * 1.35f : 0);
            float top = PhotoH + 16, bottom = H - BarH - 16;
            float y = Math.Max(top, top + (bottom - top - block) / 2);

            canvas.Mutate(c =>
            {
                // fade the bottom of the photo into the panel
                c.Fill(new LinearGradientBrush(
                        new PointF(0, PhotoH - FadeH), new PointF(0, PhotoH),
                        GradientRepetitionMode.None,
                        new ColorStop(0f, Navy.WithAlpha(0f)),
                        new ColorStop(0.5f, Navy.WithAlpha(0.33f)),
                        new ColorStop(1f, Navy)),
                    new RectangleF(0, PhotoH - FadeH, W, FadeH));

                // wordmark pill
                var wordFont = faces.Bold.At(34);
                float wordW  = Advance("ZipGoa", wordFont);
                Pill(c, 48, 48, wordW + 56, 64, Navy);
                c.DrawText(new RichTextOptions(wordFont)
                {
                    Origin            = new PointF(48 + 28, 48 + 32),
                    VerticalAlignment = VerticalAlignment.Center
                }, "ZipGoa", Color.White);

                // label → headline → subline
                if (label.Length > 0)
                    c.DrawText(new RichTextOptions(faces.Semi.At(labelSize)) { Origin = new PointF(Mx, y) },
                               label.ToUpperInvariant(), Teal);
                y += labelSize * 1.5f + 12;

                foreach (var line in hLines)
                {
                    c.DrawText(new RichTextOptions(hFont) { Origin = new PointF(Mx, y) }, line, Color.White);
                    y += hSize * 1.12f;
                }

                if (sLines.Count > 0)
                {
                    y += 22;
                    foreach (var line in sLines)
                    {
                        c.DrawText(new RichTextOptions(sFont) { Origin = new PointF(Mx, y) }, line, Muted);
                        y += sSize * 1.35f;
                    }
                }

                // CTA bar
                c.Fill(Orange, new RectangleF(0, H - BarH, W, BarH));
                var ctaFont = faces.Bold.At(34);
                float cy = H - BarH / 2f;
                c.DrawText(new RichTextOptions(ctaFont)
                {
                    Origin            = new PointF(Mx, cy),
                    VerticalAlignment = VerticalAlignment.Center
                }, "Book on WhatsApp", Navy);
                c.DrawText(new RichTextOptions(ctaFont)
                {
                    Origin              = new PointF(W - Mx, cy),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment   = VerticalAlignment.Center
                }, Phones, Navy);
            });
        }

        private static float Advance(string text, Font font) =>
            TextMeasurer.MeasureAdvance(text, new TextOptions(font)).Width;

        private static List<string> Wrap(string text, Font font, float width)
        {
            var lines = new List<string>();
            var cur   = string.Empty;
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = cur.Length == 0 ? word : $"{cur} {word}";
                if (Advance(candidate, font) <= width) { cur = candidate; continue; }
                if (cur.Length > 0) lines.Add(cur);
                cur = word;
            }
            if (cur.Length > 0) lines.Add(cur);
            return lines;
        }

        /// Shrinks the font in 4px steps until the text fits in maxLines; truncates with "…" at the floor size.
        private static (Font Font, float Size, List<string> Lines) Fit(
            string text, Face face, float start, float floor, float width, int maxLines)
        {
            for (var size = start; ; size -= 4)
            {
                var font  = face.At(size);
                var lines = Wrap(text, font, width);
                if (lines.Count <= maxLines) return (font, size, lines);
                if (size - 4 >= floor) continue;

                lines = lines.Take(maxLines).ToList();
                var last = lines[^1];
                while (Advance(last + "…", font) > width && last.Contains(' '))
                    last = last[..last.LastIndexOf(' ')];
                lines[^1] = last + "…";
                return (font, size, lines);
            }
        }

        private static void Pill(IImageProcessingContext c, float x, float y, float w, float h, Color color)
        {
            var r = h / 2;
            c.Fill(color, new RectangleF(x + r, y, w - h, h));
            c.Fill(color, new EllipsePolygon(x + r, y + r, r));
            c.Fill(color, new EllipsePolygon(x + w - r, y + r, r));
        }
    }
}

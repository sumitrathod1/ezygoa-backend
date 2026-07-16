/*
 * Instagram Image Bank — static hosting + upload
 * ================================================
 * Public URLs:  GET  https://<domain>/ig/<filename>
 * Upload:       POST https://<domain>/api/ig-images   (multipart "file" field)
 * List:         GET  https://<domain>/api/ig-images
 * Auth:         X-Upload-Key header must match IG_UPLOAD_KEY env var
 *
 * ── Easypanel deployment ────────────────────────────────────────────────────
 *  In your API service → Storage → Add Volume:
 *    Source (host path) : /data/ig
 *    Target (container) : /data/ig
 *  Without this mount, images are wiped on every container rebuild.
 *
 * ── Required env vars ───────────────────────────────────────────────────────
 *  IG_IMAGES_PATH   /data/ig                        (matches the volume target)
 *  IG_UPLOAD_KEY    <strong random string>           (e.g. openssl rand -hex 32)
 *  PUBLIC_API_BASE  https://api.yourdomain.com       (no trailing slash)
 * ────────────────────────────────────────────────────────────────────────────
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.RegularExpressions;

namespace TravelManagement.API.Controllers
{
    [Route("api/ig-images")]
    [EnableRateLimiting("api")]
    public class IgImagesController : ApiControllerBase
    {
        private readonly IConfiguration _config;

        public IgImagesController(IConfiguration config) => _config = config;

        // ── POST /api/ig-images ─────────────────────────────────────────────
        [HttpPost]
        [RequestSizeLimit(10 * 1024 * 1024)]   // 10 MB outer limit
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (!IsAuthorized()) return ApiUnauthorized("Invalid or missing X-Upload-Key");

            if (file is null || file.Length == 0)
                return ApiBadRequest("No file provided");

            if (file.Length > 8 * 1024 * 1024)
                return ApiBadRequest("File exceeds 8 MB limit");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png"))
                return ApiBadRequest("Only .jpg, .jpeg, and .png files are accepted");

            var folder = _config["IG_IMAGES_PATH"] ?? "/data/ig";
            var baseName = SanitizeName(Path.GetFileNameWithoutExtension(file.FileName));
            var finalName = Deconflict(folder, baseName, ext);
            var destPath  = Path.Combine(folder, finalName);

            await using var stream = System.IO.File.Create(destPath);
            await file.CopyToAsync(stream);

            return ApiCreated(new { url = BuildUrl(finalName) }, "Image uploaded");
        }

        // ── GET /api/ig-images ──────────────────────────────────────────────
        [HttpGet]
        public IActionResult List()
        {
            if (!IsAuthorized()) return ApiUnauthorized("Invalid or missing X-Upload-Key");

            var folder = _config["IG_IMAGES_PATH"] ?? "/data/ig";
            if (!Directory.Exists(folder))
                return ApiOk(Array.Empty<object>());

            var files = Directory.EnumerateFiles(folder)
                .Where(f =>
                {
                    var e = Path.GetExtension(f).ToLowerInvariant();
                    return e is ".jpg" or ".jpeg" or ".png";
                })
                .OrderByDescending(f => System.IO.File.GetCreationTimeUtc(f))
                .Select(f =>
                {
                    var name = Path.GetFileName(f);
                    var info = new FileInfo(f);
                    return new
                    {
                        name,
                        url       = BuildUrl(name),
                        sizeKb    = (int)Math.Ceiling(info.Length / 1024.0),
                        createdAt = info.CreationTimeUtc
                    };
                });

            return ApiOk(files);
        }

        // ── Helpers ─────────────────────────────────────────────────────────

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
            // Lowercase, replace anything not a-z / 0-9 / dash with a dash
            var slug = Regex.Replace(raw.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            return string.IsNullOrEmpty(slug) ? "image" : slug;
        }

        private static string Deconflict(string folder, string baseName, string ext)
        {
            var candidate = $"{baseName}{ext}";
            if (!System.IO.File.Exists(Path.Combine(folder, candidate)))
                return candidate;

            // Append compact timestamp to avoid overwriting existing files
            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            return $"{baseName}_{stamp}{ext}";
        }
    }
}

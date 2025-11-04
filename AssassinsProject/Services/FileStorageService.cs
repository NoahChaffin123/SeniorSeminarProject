using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace AssassinsProject.Services;

public class FileStorageService(IConfiguration cfg, IWebHostEnvironment env)
{
    // Physical base: <contentRoot>/wwwroot/uploads
    private readonly string _basePath =
        Path.Combine(env.ContentRootPath, cfg["Upload:BasePath"] ?? "wwwroot/uploads");

    private static readonly string[] AllowedExt = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    public async Task<(string url, string? contentType, byte[] sha256)>
        SavePlayerPhotoAsync(int gameId, string emailNormalized, IFormFile file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0) throw new InvalidOperationException("No file uploaded.");

        Directory.CreateDirectory(Path.Combine(_basePath, "players", gameId.ToString()));

        var ext = NormalizeExt(Path.GetExtension(file.FileName), file.ContentType);
        if (!AllowedExt.Contains(ext, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unsupported file type. Use jpg, jpeg, png, gif, or webp.");

        var safeEmail = Sanitize(emailNormalized);
        var fileName = $"{safeEmail}{ext}";
        var phys = Path.Combine(_basePath, "players", gameId.ToString(), fileName);

        // Stream once: write to disk and compute SHA-256
        await using var fs = new FileStream(
            phys, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);

        using var sha = SHA256.Create();
        await using (var cs = new CryptoStream(fs, sha, CryptoStreamMode.Write))
        {
            await file.CopyToAsync(cs, ct);
            cs.FlushFinalBlock(); // finalize hash
        }

        var hash = sha.Hash ?? Array.Empty<byte>();
        // Web-relative URL (served by UseStaticFiles from wwwroot)
        var url = $"/uploads/players/{gameId}/{fileName}";
        return (url, file.ContentType, hash);
    }

    // Optional evidence helper (same tuple shape) — adjust caller if you use it.
    public async Task<(string url, string? contentType, byte[] sha256)>
        SaveEvidenceAsync(int gameId, IFormFile file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0) throw new InvalidOperationException("No file uploaded.");

        Directory.CreateDirectory(Path.Combine(_basePath, "evidence", gameId.ToString()));

        var ext = NormalizeExt(Path.GetExtension(file.FileName), file.ContentType);
        if (!AllowedExt.Contains(ext, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unsupported file type. Use jpg, jpeg, png, gif, or webp.");

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var phys = Path.Combine(_basePath, "evidence", gameId.ToString(), fileName);

        await using var fs = new FileStream(
            phys, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);

        using var sha = SHA256.Create();
        await using (var cs = new CryptoStream(fs, sha, CryptoStreamMode.Write))
        {
            await file.CopyToAsync(cs, ct);
            cs.FlushFinalBlock();
        }

        var hash = sha.Hash ?? Array.Empty<byte>();
        var url = $"/uploads/evidence/{gameId}/{fileName}";
        return (url, file.ContentType, hash);
    }

    private static string NormalizeExt(string ext, string? contentType)
    {
        ext = (ext ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(ext) && AllowedExt.Contains(ext)) return ext;

        // Fallback from content-type
        return (contentType ?? "").ToLowerInvariant() switch
        {
            var s when s.StartsWith("image/jpeg") => ".jpg",
            var s when s.StartsWith("image/jpg")  => ".jpg",
            var s when s.StartsWith("image/png")  => ".png",
            var s when s.StartsWith("image/gif")  => ".gif",
            var s when s.StartsWith("image/webp") => ".webp",
            _ => ".jpg"
        };
    }

    private static string Sanitize(string value)
    {
        value = (value ?? "unknown").Trim().ToLowerInvariant();
        return Regex.Replace(value, @"[^a-z0-9._-]+", "-");
    }
}

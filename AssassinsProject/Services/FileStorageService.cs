using System.Security.Cryptography;

namespace AssassinsProject.Services;

public class FileStorageService(IConfiguration cfg, IWebHostEnvironment env)
{
    private readonly string _basePath = Path.Combine(env.ContentRootPath, cfg["Upload:BasePath"] ?? "wwwroot/uploads");

    public async Task<(string url, string? contentType, byte[] sha256)>
        SavePlayerPhotoAsync(int gameId, string emailNormalized, IFormFile file, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.Combine(_basePath, gameId.ToString()));

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
        var fileName = $"{emailNormalized}{ext}";
        var phys = Path.Combine(_basePath, gameId.ToString(), fileName);

        // Write file to disk and compute SHA256 in one pass.
        await using var fs = new FileStream(
            phys, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);

        using var sha = SHA256.Create();

        // IMPORTANT: finalize the hash before reading sha.Hash
        using (var cs = new CryptoStream(fs, sha, CryptoStreamMode.Write))
        {
            await file.CopyToAsync(cs, ct);
            cs.FlushFinalBlock(); // <-- this finalizes the hash
        }

        var hash = sha.Hash ?? Array.Empty<byte>();

        var url = $"/uploads/{gameId}/{fileName}";
        return (url, file.ContentType, hash);
    }
}

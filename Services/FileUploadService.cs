using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace HouseOfAlchemists.API.Services;

public class FileUploadService(IConfiguration config, ILogger<FileUploadService> logger)
{
    private BlobContainerClient? GetContainer()
    {
        var conn = config["Azure:BlobConnectionString"];
        if (string.IsNullOrEmpty(conn)) return null;
        var container = config["Azure:BlobContainerName"] ?? "hoa-uploads";
        var client = new BlobContainerClient(conn, container);
        client.CreateIfNotExists(PublicAccessType.Blob);
        return client;
    }

    public async Task<string?> UploadAsync(IFormFile file, string folder)
    {
        var container = GetContainer();
        if (container == null)
        {
            logger.LogWarning("Azure Blob Storage not configured — file upload skipped");
            return null;
        }
        var ext = Path.GetExtension(file.FileName);
        var blobName = $"{folder}/{Guid.NewGuid()}{ext}";
        var blob = container.GetBlobClient(blobName);
        using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });
        return blob.Uri.ToString();
    }

    public async Task DeleteAsync(string url)
    {
        var container = GetContainer();
        if (container == null) return;
        try
        {
            var uri = new Uri(url);
            var blobName = uri.AbsolutePath.TrimStart('/').Substring(container.Name.Length + 1);
            await container.GetBlobClient(blobName).DeleteIfExistsAsync();
        }
        catch (Exception ex) { logger.LogWarning("Blob delete failed for {Url}: {Ex}", url, ex.Message); }
    }
}

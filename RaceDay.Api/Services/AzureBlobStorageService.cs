
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

public sealed class AzureBlobStorageService
{
    private readonly BlobContainerClient _container;

    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    private static readonly Dictionary<string, string> AllowedTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

    public AzureBlobStorageService(IConfiguration configuration)
    {
        var connectionString =
            configuration["AzureStorage:ConnectionString"];

        var containerName =
            configuration["AzureStorage:ContainerName"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Azure Storage connection string is missing.");
        }

        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new InvalidOperationException(
                "Azure Storage container name is missing.");
        }

        var serviceClient = new BlobServiceClient(connectionString);

        _container = serviceClient.GetBlobContainerClient(containerName);
    }

    public async Task<string> UploadImageAsync(
        IFormFile file,
        string folder)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Please select an image.");
        }

        if (file.Length > MaxFileSize)
        {
            throw new ArgumentException(
                "The image must not exceed 5 MB.");
        }

        var extension = Path.GetExtension(file.FileName);

        if (!AllowedTypes.TryGetValue(extension, out var contentType))
        {
            throw new ArgumentException(
                "Only JPG, JPEG, PNG and WEBP images are allowed.");
        }

        if (!string.Equals(
            file.ContentType,
            contentType,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The image file type is invalid.");
        }

        var safeFolder = folder switch
        {
            "events" => "events",
            "profiles" => "profiles",
            _ => throw new ArgumentException("Invalid image folder.")
        };

        await _container.CreateIfNotExistsAsync(
            PublicAccessType.None);

        var blobName =
            $"{safeFolder}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

        var blobClient = _container.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();

        await blobClient.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                }
            });

        // Store this blob name in the database.
        return blobName;
    }

    public async Task<(Stream Content, string ContentType)?>
        DownloadImageAsync(string blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return null;
        }

        if (!blobName.StartsWith("events/", StringComparison.Ordinal) &&
            !blobName.StartsWith("profiles/", StringComparison.Ordinal))
        {
            return null;
        }

        var blobClient = _container.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync())
        {
            return null;
        }

        var response = await blobClient.DownloadStreamingAsync();

        return (
            response.Value.Content,
            response.Value.Details.ContentType ?? "application/octet-stream"
        );
    }

    public async Task DeleteImageAsync(string? blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return;
        }

        if (!blobName.StartsWith("events/", StringComparison.Ordinal) &&
            !blobName.StartsWith("profiles/", StringComparison.Ordinal))
        {
            return;
        }

        var blobClient = _container.GetBlobClient(blobName);

        await blobClient.DeleteIfExistsAsync();
    }
}

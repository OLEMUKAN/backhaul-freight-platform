using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace TruckService.API.Services
{
    public interface IBlobStorageService
    {
        Task<string> UploadFileAsync(IFormFile file, string containerName, string filePrefix);
        Task<bool> DeleteFileAsync(string blobUrl, string containerName);
        string GetBlobName(string blobUrl);
    }

    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<BlobStorageService> _logger;
        private readonly string _storageBaseUrl;

        public BlobStorageService(
            BlobServiceClient blobServiceClient,
            IConfiguration configuration,
            ILogger<BlobStorageService> logger)
        {
            _blobServiceClient = blobServiceClient;
            _logger = logger;
            _storageBaseUrl = configuration["Azure:Storage:BaseUrl"] ?? "https://yourstorageaccount.blob.core.windows.net";
        }

        public async Task<string> UploadFileAsync(IFormFile file, string containerName, string filePrefix)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    throw new ArgumentException("File is empty or null", nameof(file));
                }

                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                
                // Create the container if it doesn't exist
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

                // Generate a unique file name
                var fileName = $"{filePrefix}-{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var blobClient = containerClient.GetBlobClient(fileName);

                // Set metadata and content type
                var blobUploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = file.ContentType
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        { "OriginalFileName", file.FileName },
                        { "UploadedOn", DateTime.UtcNow.ToString("o") }
                    }
                };

                // Upload the file
                await using var stream = file.OpenReadStream();
                await blobClient.UploadAsync(stream, blobUploadOptions);
                
                _logger.LogInformation("File {FileName} uploaded to blob storage", fileName);
                
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to blob storage");
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string blobUrl, string containerName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(blobUrl))
                {
                    _logger.LogWarning("Attempted to delete a null or empty blob URL");
                    return false;
                }

                var blobName = GetBlobName(blobUrl);
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                var response = await blobClient.DeleteIfExistsAsync();
                
                _logger.LogInformation("File {BlobName} deleted from blob storage. Success: {Success}", 
                    blobName, response.Value);
                
                return response.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file from blob storage");
                throw;
            }
        }

        public string GetBlobName(string blobUrl)
        {
            if (string.IsNullOrWhiteSpace(blobUrl))
            {
                return string.Empty;
            }

            try
            {
                var uri = new Uri(blobUrl);
                var segments = uri.Segments;
                
                // The last segment is the blob name
                return segments[^1];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting blob name from URL {BlobUrl}", blobUrl);
                throw new ArgumentException("Invalid blob URL format", nameof(blobUrl));
            }
        }
    }
}

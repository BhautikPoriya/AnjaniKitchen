using Microsoft.AspNetCore.Http;

namespace AnjaniKitchen.Services;

public interface ICategoryImageStorage
{
    Task<string> UploadAsync(IFormFile image, CancellationToken cancellationToken = default, string? folder = null);
    Task DeleteAsync(string publicId, CancellationToken cancellationToken = default);
    string? GetImageUrl(string? publicId);
}

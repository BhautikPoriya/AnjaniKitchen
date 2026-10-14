using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System.Text.RegularExpressions;

namespace AnjaniKitchen.Services;

public sealed class CloudinaryCategoryImageStorage : ICategoryImageStorage
{
    private readonly string _cloudName;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly string _folder;
    private readonly Cloudinary? _cloudinary;

    public CloudinaryCategoryImageStorage(IConfiguration configuration)
    {
        _cloudName = configuration["Cloudinary:CloudName"] ?? string.Empty;
        _apiKey = configuration["Cloudinary:ApiKey"] ?? string.Empty;
        _apiSecret = configuration["Cloudinary:ApiSecret"] ?? string.Empty;
        _folder = configuration["Cloudinary:Folder"] ?? "category";

        if (!string.IsNullOrWhiteSpace(_cloudName))
        {
            _cloudinary = new Cloudinary(new Account(_cloudName, _apiKey, _apiSecret))
            {
                Api = { Secure = true }
            };
        }
    }

    public async Task<string> UploadAsync(
        IFormFile image,
        CancellationToken cancellationToken = default,
        string? folder = null)
    {
        var cloudinary = GetConfiguredCloudinary();
        await using var stream = image.OpenReadStream();
        var result = await cloudinary.UploadAsync(
            new ImageUploadParams
            {
                File = new FileDescription(image.FileName, stream),
                Folder = string.IsNullOrWhiteSpace(folder) ? _folder : folder,
                PublicId = Guid.NewGuid().ToString("N"),
                Overwrite = false
            },
            cancellationToken);

        if (result.Error is not null)
        {
            throw new CategoryImageStorageException(result.Error.Message);
        }

        if (string.IsNullOrWhiteSpace(result.PublicId))
        {
            throw new InvalidOperationException("Cloudinary completed the upload without returning an image ID.");
        }

        return result.PublicId;
    }

    public async Task DeleteAsync(string publicId, CancellationToken cancellationToken = default)
    {
        var cloudinary = GetConfiguredCloudinary();
        cancellationToken.ThrowIfCancellationRequested();
        var result = await cloudinary.DestroyAsync(new DeletionParams(GetPublicId(publicId)) { ResourceType = ResourceType.Image });
        if (result.Error is not null)
        {
            throw new CategoryImageStorageException(result.Error.Message);
        }
    }

    private string GetPublicId(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var imageUri))
        {
            return value;
        }

        var segments = imageUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (!string.Equals(imageUri.Host, "res.cloudinary.com", StringComparison.OrdinalIgnoreCase)
            || segments.Length < 5
            || !string.Equals(segments[0], _cloudName, StringComparison.Ordinal)
            || !string.Equals(segments[1], "image", StringComparison.Ordinal)
            || !string.Equals(segments[2], "upload", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The stored category image URL is not a Cloudinary image URL for this account.");
        }

        var publicIdStart = 3;
        for (var index = 3; index < segments.Length; index++)
        {
            if (Regex.IsMatch(segments[index], "^v[0-9]+$", RegexOptions.CultureInvariant))
            {
                publicIdStart = index + 1;
                break;
            }
        }

        if (publicIdStart >= segments.Length)
        {
            throw new InvalidOperationException("The stored Cloudinary image URL does not contain an image ID.");
        }

        var publicId = string.Join('/', segments.Skip(publicIdStart).Select(Uri.UnescapeDataString));
        var extensionIndex = publicId.LastIndexOf('.');
        return extensionIndex > publicId.LastIndexOf('/') ? publicId[..extensionIndex] : publicId;
    }

    public string? GetImageUrl(string? publicId)
    {
        if (string.IsNullOrWhiteSpace(publicId))
        {
            return null;
        }

        if (Uri.TryCreate(publicId, UriKind.Absolute, out var existingUrl)
            && (existingUrl.Scheme == Uri.UriSchemeHttp || existingUrl.Scheme == Uri.UriSchemeHttps))
        {
            return existingUrl.ToString();
        }

        if (_cloudinary is null)
        {
            return null;
        }

        return _cloudinary.Api.UrlImgUp.BuildUrl(publicId);
    }

    private Cloudinary GetConfiguredCloudinary()
    {
        if (_cloudinary is null || string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_apiSecret))
        {
            throw new InvalidOperationException(
                "Cloudinary is not configured. Set Cloudinary:CloudName, Cloudinary:ApiKey, and Cloudinary:ApiSecret.");
        }

        return _cloudinary;
    }
}

public sealed class CategoryImageStorageException(string message) : Exception(message)
{
}

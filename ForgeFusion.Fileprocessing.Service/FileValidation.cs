using System.ComponentModel.DataAnnotations;

namespace ForgeFusion.Fileprocessing.Service;

public static class FileValidation
{
    public static void Validate(string fileName, string? contentType, long length, BlobStorageOptions options)
    {
        if (options.AllowedExtensions is { Length: > 0 })
        {
            var ext = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(ext) || !options.AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                throw new ValidationException($"File extension '{ext}' is not allowed.");
        }
        if (options.AllowedContentTypes is { Length: > 0 })
        {
            if (string.IsNullOrWhiteSpace(contentType) || !options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
                throw new ValidationException($"Content type '{contentType}' is not allowed.");
        }
        if (length <= 0)
        {
            throw new ValidationException("Empty files are not allowed.");
        }
        if (options.MaxFileSize.HasValue && length > options.MaxFileSize.Value)
        {
            throw new ValidationException($"File size ({length:N0} bytes) exceeds the maximum allowed size ({options.MaxFileSize.Value:N0} bytes).");
        }
    }
}

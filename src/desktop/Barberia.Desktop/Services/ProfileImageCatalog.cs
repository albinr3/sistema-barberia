namespace Barberia.Desktop.Services;

internal static class ProfileImageCatalog
{
    public static readonly string[] FilePickerExtensions =
    [
        ".bmp",
        ".jpeg",
        ".jpg",
        ".png",
        ".webp"
    ];

    private static readonly HashSet<string> SupportedExtensions = new(FilePickerExtensions, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ProfileImageOption> ListProfileImages()
    {
        var options = new List<ProfileImageOption>();
        var assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
        if (Directory.Exists(assetsDirectory))
        {
            options.AddRange(Directory
                .EnumerateFiles(assetsDirectory, "*.*", SearchOption.AllDirectories)
                .Where(filePath => SupportedExtensions.Contains(Path.GetExtension(filePath)))
                .Where(filePath => IsProfileImage(assetsDirectory, filePath))
                .Select(filePath => new ProfileImageOption(
                    $"Asset - {Path.GetFileNameWithoutExtension(filePath)}",
                    Path.GetRelativePath(AppContext.BaseDirectory, filePath).Replace('\\', '/'))));
        }

        var profileImagesDirectory = LocalAppPaths.ProfileImagesDirectory;
        options.AddRange(Directory
            .EnumerateFiles(profileImagesDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(filePath => SupportedExtensions.Contains(Path.GetExtension(filePath)))
            .Select(filePath => new ProfileImageOption(
                $"Uploaded - {Path.GetFileNameWithoutExtension(filePath)}",
                $"ProfileImages/{Path.GetFileName(filePath)}")));

        return options
            .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string ImportProfileImage(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new InvalidOperationException("Select an image file from Windows Explorer.");
        }

        var extension = Path.GetExtension(sourcePath);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Profile images must be PNG, JPG, JPEG, BMP or WEBP files.");
        }

        var targetFileName = $"{SanitizeFileName(Path.GetFileNameWithoutExtension(sourcePath))}-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var targetPath = Path.Combine(LocalAppPaths.ProfileImagesDirectory, targetFileName);
        File.Copy(sourcePath, targetPath);

        return $"ProfileImages/{targetFileName}";
    }

    public static Uri? ResolveImageUri(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = normalizedPath.StartsWith($"ProfileImages{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath(Path.Combine(LocalAppPaths.ProfileImagesDirectory, normalizedPath["ProfileImages".Length..].TrimStart(Path.DirectorySeparatorChar)))
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, normalizedPath));

        if (!IsAllowedImagePath(fullPath) || !File.Exists(fullPath))
        {
            return null;
        }

        return new Uri(fullPath);
    }

    private static bool IsAllowedImagePath(string fullPath)
    {
        var assetsDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Assets")) + Path.DirectorySeparatorChar;
        var profileImagesDirectory = Path.GetFullPath(LocalAppPaths.ProfileImagesDirectory) + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(assetsDirectory, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(profileImagesDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(fileName
            .Select(character => invalidCharacters.Contains(character) ? '-' : character)
            .ToArray())
            .Trim('-', ' ');

        return string.IsNullOrWhiteSpace(sanitized) ? "profile-image" : sanitized;
    }

    private static bool IsProfileImage(string assetsDirectory, string filePath)
    {
        var relativePath = Path.GetRelativePath(assetsDirectory, filePath).Replace('\\', '/');
        if (relativePath.StartsWith("Branding/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !Path.GetFileNameWithoutExtension(filePath).Contains("logo", StringComparison.OrdinalIgnoreCase);
    }
}

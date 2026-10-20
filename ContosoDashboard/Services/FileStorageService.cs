namespace ContosoDashboard.Services;

public interface IFileStorageService
{
    Task<string> UploadAsync(Stream fileStream, string filePath, string contentType);
    Task DeleteAsync(string filePath);
    Task<Stream> DownloadAsync(string filePath);
    Task<string> GetUrlAsync(string filePath, TimeSpan expiration);
}

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var configuredPath = configuration["DocumentStorage:RootPath"] ?? "AppData/uploads";
        _rootPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> UploadAsync(Stream fileStream, string filePath, string contentType)
    {
        var fullPath = ResolvePath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        try
        {
            await using var destination = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await fileStream.CopyToAsync(destination);
            return filePath;
        }
        catch
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            throw;
        }
    }

    public Task DeleteAsync(string filePath)
    {
        var fullPath = ResolvePath(filePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task<Stream> DownloadAsync(string filePath)
    {
        Stream stream = new FileStream(ResolvePath(filePath), FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task<string> GetUrlAsync(string filePath, TimeSpan expiration)
    {
        throw new NotSupportedException("Local files are not addressable by URL.");
    }

    private string ResolvePath(string filePath)
    {
        if (Path.IsPathRooted(filePath))
        {
            throw new InvalidOperationException("The storage path must be relative.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, filePath));
        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The storage path is outside the configured root.");
        }

        return fullPath;
    }
}

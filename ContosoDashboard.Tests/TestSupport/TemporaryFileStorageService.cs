using ContosoDashboard.Services;

namespace ContosoDashboard.Tests.TestSupport;

public sealed class TemporaryFileStorageService : IFileStorageService, IAsyncDisposable
{
    public TemporaryFileStorageService()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "contoso-doc-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }
    public bool FailUploads { get; set; }

    public async Task<string> UploadAsync(Stream fileStream, string filePath, string contentType)
    {
        if (FailUploads)
        {
            throw new IOException("Simulated storage failure.");
        }

        var fullPath = Path.Combine(RootPath, filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var destination = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await fileStream.CopyToAsync(destination);
        return filePath;
    }

    public Task DeleteAsync(string filePath)
    {
        var fullPath = Path.Combine(RootPath, filePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task<Stream> DownloadAsync(string filePath) =>
        Task.FromResult<Stream>(File.OpenRead(Path.Combine(RootPath, filePath)));

    public Task<string> GetUrlAsync(string filePath, TimeSpan expiration) =>
        throw new NotSupportedException();

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}

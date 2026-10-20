using System.Text;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestSupport;
using Xunit;

namespace ContosoDashboard.Tests;

public class DocumentUploadTests
{
    [Fact]
    public async Task UploadStoresFileAndMetadataWithUniquePath()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var storage = new TemporaryFileStorageService();
        var service = CreateService(database.Context, storage);
        var request = CreateRequest();

        var result = await service.UploadAsync(request, requestingUserId: 13);

        Assert.True(result.Success, result.Error);
        var document = Assert.Single(database.Context.Documents);
        Assert.Equal(13, document.UploadedByUserId);
        Assert.Equal("application/pdf", document.FileType);
        Assert.Equal(request.File!.Content.LongLength, document.FileSize);
        Assert.StartsWith("13/personal/", document.FilePath);
        Assert.True(File.Exists(Path.Combine(storage.RootPath, document.FilePath)));
        Assert.Single(database.Context.DocumentActivities.Where(activity => activity.Action == DocumentActions.Upload));
    }

    [Theory]
    [InlineData("", DocumentCategories.Reports)]
    [InlineData("Report", "")]
    [InlineData("Report", "Unknown")]
    public async Task RejectsMissingOrUnknownMetadata(string title, string category)
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var storage = new TemporaryFileStorageService();
        var service = CreateService(database.Context, storage);
        var request = CreateRequest(title, category);

        var result = await service.UploadAsync(request, requestingUserId: 13);

        Assert.False(result.Success);
        Assert.Empty(database.Context.Documents);
    }

    [Fact]
    public async Task RejectsProjectUploadByNonMember()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var storage = new TemporaryFileStorageService();
        var service = CreateService(database.Context, storage);
        var request = CreateRequest() with { ProjectId = 10 };

        var result = await service.UploadAsync(request, requestingUserId: 14);

        Assert.False(result.Success);
        Assert.Empty(database.Context.Documents);
    }

    [Fact]
    public async Task AllowsAdministratorToUploadToProject()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var storage = new TemporaryFileStorageService();
        var service = CreateService(database.Context, storage);
        var request = CreateRequest() with { ProjectId = 10 };

        var result = await service.UploadAsync(request, requestingUserId: 10);

        Assert.True(result.Success, result.Error);
        Assert.Equal(10, Assert.Single(database.Context.Documents).ProjectId);
    }

    [Fact]
    public async Task StorageFailureDoesNotCreateDatabaseRow()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var storage = new TemporaryFileStorageService { FailUploads = true };
        var service = CreateService(database.Context, storage);

        var result = await service.UploadAsync(CreateRequest(), requestingUserId: 13);

        Assert.False(result.Success);
        Assert.Empty(database.Context.Documents);
    }

    [Fact]
    public async Task DatabaseFailureRemovesTheAlreadyStoredFile()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var storage = new TemporaryFileStorageService();
        await using var failingContext = database.CreateFailingContext();
        var service = CreateService(failingContext, storage);

        var result = await service.UploadAsync(CreateRequest(), requestingUserId: 13);

        Assert.False(result.Success);
        Assert.Empty(database.Context.Documents);
        Assert.Empty(Directory.EnumerateFiles(storage.RootPath, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task MyDocumentsReturnsOnlyTheRequestingUsersUploads()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var storage = new TemporaryFileStorageService();
        var service = CreateService(database.Context, storage);

        Assert.True((await service.UploadAsync(CreateRequest(), requestingUserId: 13)).Success);
        Assert.True((await service.UploadAsync(CreateRequest("Other report"), requestingUserId: 14)).Success);

        var documents = await service.GetMyDocumentsAsync(13);

        Assert.Single(documents);
        Assert.Equal(13, documents[0].UploadedByUserId);
    }

    private static IDocumentService CreateService(ContosoDashboard.Data.ApplicationDbContext context, IFileStorageService storage) =>
        new DocumentService(context, storage, new SimulatedMalwareScanService(), new DocumentValidator());

    private static DocumentUploadRequest CreateRequest(string title = "Quarterly report", string category = DocumentCategories.Reports) => new()
    {
        Title = title,
        Category = category,
        Tags = [" Finance ", "finance"],
        File = new DocumentFilePayload
        {
            FileName = "report.pdf",
            Size = 13,
            Content = Encoding.ASCII.GetBytes("%PDF-1.7 test")
        }
    };
}

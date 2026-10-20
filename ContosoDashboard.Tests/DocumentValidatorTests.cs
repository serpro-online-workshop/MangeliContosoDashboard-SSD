using System.Text;
using ContosoDashboard.Services;
using ContosoDashboard.Tests.TestSupport;
using Xunit;

namespace ContosoDashboard.Tests;

public class DocumentValidatorTests
{
    private readonly DocumentValidator _validator = new();

    [Fact]
    public async Task AcceptsPdfWithMatchingSignatureAndUsesServerMimeType()
    {
        var content = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7 test"));

        var result = await _validator.ValidateAsync("report.pdf", content.Length, content);

        Assert.True(result.IsValid);
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public async Task RejectsEmptyFiles()
    {
        var result = await _validator.ValidateAsync("empty.pdf", 0, Stream.Null);

        Assert.False(result.IsValid);
        Assert.Contains("Empty", result.Error);
    }

    [Fact]
    public async Task RejectsFilesOverTwentyFiveMegabytes()
    {
        var result = await _validator.ValidateAsync("large.pdf", DocumentValidator.MaximumFileSize + 1, new MemoryStream([0x25, 0x50, 0x44, 0x46]));

        Assert.False(result.IsValid);
        Assert.Contains("25 MB", result.Error);
    }

    [Fact]
    public async Task RejectsUnsupportedExtension()
    {
        var result = await _validator.ValidateAsync("archive.zip", 4, new MemoryStream([0x50, 0x4B, 0x03, 0x04]));

        Assert.False(result.IsValid);
        Assert.Contains("not supported", result.Error);
    }

    [Fact]
    public async Task RejectsFileWithMismatchedSignature()
    {
        var result = await _validator.ValidateAsync("renamed.pdf", 4, new MemoryStream([0x4D, 0x5A, 0x90, 0x00]));

        Assert.False(result.IsValid);
        Assert.Contains("contents", result.Error);
    }

    [Fact]
    public void SanitizesSuppliedFileName()
    {
        var sanitized = DocumentValidator.SanitizeFileName("../../folder\\bad\"name.pdf");

        Assert.Equal("badname.pdf", sanitized);
    }

    [Fact]
    public async Task RejectsEicarTestSignature()
    {
        var scanner = new SimulatedMalwareScanService();
        await using var content = new MemoryStream(Encoding.ASCII.GetBytes(TestFiles.EicarSignature()));

        var result = await scanner.ScanAsync(content, "test.txt");

        Assert.False(result.IsClean);
    }
}

using AzrngTools.Models.Pdf;
using AzrngTools.Services.Pdf;
using PdfSharp.Pdf;

namespace AzrngTools.Tests.Services.Pdf;

public class AsposePdfProcessingServiceTests
{
    [Fact]
    public async Task LoadMetadataSplitAndExportWord_ShouldCompleteForGeneratedPdf()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "AzrngToolsPdfTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var sourcePdf = Path.Combine(tempDirectory, "source.pdf");
            CreateSamplePdf(sourcePdf);

            var service = new AsposePdfProcessingService();
            var metadata = await service.LoadMetadataAsync(sourcePdf);

            Assert.True(metadata.IsSuccess, metadata.Message);
            Assert.Equal(2, metadata.Data?.PageCount);

            var splitPath = Path.Combine(tempDirectory, "split.pdf");
            var split = await service.SplitAsync(new PdfSplitRequest(
                sourcePdf,
                [1],
                PdfSplitMode.SingleFile,
                splitPath));

            Assert.True(split.IsSuccess, split.Message);
            Assert.True(File.Exists(splitPath));

            var wordPath = Path.Combine(tempDirectory, "source.docx");
            var word = await service.ExportWordAsync(new PdfWordExportRequest(sourcePdf, wordPath));

            Assert.True(word.IsSuccess, word.Message);
            Assert.True(File.Exists(wordPath));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }

    [Fact]
    public void Service_ShouldNotReportEvaluationMode()
    {
        var service = new AsposePdfProcessingService();

        Assert.False(service.IsEvaluationMode);
        Assert.NotEmpty(service.LicenseStatusText);
    }

    private static void CreateSamplePdf(string path)
    {
        using var document = new PdfDocument();
        document.AddPage();
        document.AddPage();
        document.Save(path);
    }
}

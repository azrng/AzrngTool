using Azrng.Core.Results;
using AzrngTools.Models.Pdf;
using AzrngTools.Services.Pdf;
using AzrngTools.Tests.TestDoubles;
using AzrngTools.ViewModels.Pdf;

namespace AzrngTools.Tests.ViewModels.Pdf;

public class PdfManagementPageViewModelTests
{
    [Fact]
    public void Constructor_ShouldDisableOperationsBeforeFileLoaded()
    {
        var viewModel = new PdfManagementPageViewModel(new FakePdfProcessingService(), new TestMessageService());

        Assert.False(viewModel.HasFile);
        Assert.False(viewModel.CanOperate);
        Assert.Equal("请选择 PDF 文件", viewModel.StatusText);
    }

    [Fact]
    public async Task LoadPdfAsync_ShouldEnableOperationsAfterFileLoaded()
    {
        var messageService = new TestMessageService();
        var viewModel = new PdfManagementPageViewModel(new FakePdfProcessingService(), messageService);

        await viewModel.LoadPdfAsync("C:\\tmp\\demo.pdf");

        Assert.True(viewModel.HasFile);
        Assert.True(viewModel.CanOperate);
        Assert.Equal("demo.pdf", viewModel.FileName);
        Assert.Equal("1-3", viewModel.PageRangeText);
        Assert.Equal("C:\\tmp\\demo_split.pdf", viewModel.SplitOutputPath);
        Assert.Equal("C:\\tmp\\demo.docx", viewModel.WordOutputPath);
        Assert.Contains("已加载 PDF", viewModel.StatusText);
        Assert.Contains(messageService.Messages, message => message.Message.Contains("已加载 PDF"));
    }

    [Fact]
    public async Task LoadPdfAsync_ShouldKeepOperationsDisabledWhenSuccessResultHasNoData()
    {
        var messageService = new TestMessageService();
        var service = new FakePdfProcessingService
        {
            ReturnSuccessfulNullMetadata = true
        };
        var viewModel = new PdfManagementPageViewModel(service, messageService);

        await viewModel.LoadPdfAsync("C:\\tmp\\demo.pdf");

        Assert.False(viewModel.HasFile);
        Assert.False(viewModel.CanOperate);
        Assert.Equal("PDF 加载失败：未读取到文件信息", viewModel.StatusText);
        Assert.Equal("PDF 加载失败：未读取到文件信息", viewModel.LastResultText);
        Assert.Contains(messageService.Messages, message => message.Message == "PDF 加载失败：未读取到文件信息");
    }

    [Fact]
    public async Task SplitPdfCommand_ShouldNotifyWhenNoOutputPathSelected()
    {
        var messageService = new TestMessageService();
        var viewModel = new PdfManagementPageViewModel(new FakePdfProcessingService(), messageService)
        {
            CurrentFile = new PdfLoadedFileSummary("C:\\tmp\\demo.pdf", "demo.pdf", 1024, 3, true, "评估模式"),
            PageRangeText = "1-2",
            SplitOutputPath = string.Empty
        };

        await viewModel.SplitPdfCommand.ExecuteAsync(null);

        Assert.Single(messageService.Messages);
        Assert.Equal("请选择分割输出文件", messageService.Messages[0].Message);
    }

    [Fact]
    public async Task ExportWordCommand_ShouldSetBusyStateDuringExecution()
    {
        var service = new FakePdfProcessingService
        {
            Delay = TimeSpan.FromMilliseconds(100)
        };
        var viewModel = new PdfManagementPageViewModel(service, new TestMessageService())
        {
            CurrentFile = new PdfLoadedFileSummary("C:\\tmp\\demo.pdf", "demo.pdf", 1024, 3, false, "授权已加载"),
            WordOutputPath = "C:\\tmp\\demo.docx"
        };

        var task = viewModel.ExportWordCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.CanOperate);

        await task;

        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.CanOperate);
        Assert.Contains("Word 导出完成", viewModel.LastResultText);
    }

    private sealed class FakePdfProcessingService : IPdfProcessingService
    {
        public TimeSpan Delay { get; set; }

        public bool ReturnSuccessfulNullMetadata { get; set; }

        public string LicenseStatusText => "评估模式";

        public bool IsEvaluationMode => true;

        public async Task<IResultModel<PdfLoadedFileSummary>> LoadMetadataAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            await DelayIfNeeded(cancellationToken);
            if (ReturnSuccessfulNullMetadata)
            {
                return ResultModel<PdfLoadedFileSummary>.Success(null!);
            }

            return PdfResultModel.Success(new PdfLoadedFileSummary(
                filePath,
                Path.GetFileName(filePath),
                1024,
                3,
                IsEvaluationMode,
                LicenseStatusText),
                "已加载 PDF，共 3 页");
        }

        public async Task<IResultModel<PdfSplitResult>> SplitAsync(
            PdfSplitRequest request,
            CancellationToken cancellationToken = default)
        {
            await DelayIfNeeded(cancellationToken);
            return ResultModel<PdfSplitResult>.Success(new PdfSplitResult([request.OutputPath], IsEvaluationMode));
        }

        public async Task<IResultModel<PdfWordExportResult>> ExportWordAsync(
            PdfWordExportRequest request,
            CancellationToken cancellationToken = default)
        {
            await DelayIfNeeded(cancellationToken);
            return ResultModel<PdfWordExportResult>.Success(new PdfWordExportResult(request.OutputFilePath, IsEvaluationMode));
        }

        private async Task DelayIfNeeded(CancellationToken cancellationToken)
        {
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }
        }
    }
}

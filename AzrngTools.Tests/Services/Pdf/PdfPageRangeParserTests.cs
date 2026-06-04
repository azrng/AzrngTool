using AzrngTools.Services.Pdf;

namespace AzrngTools.Tests.Services.Pdf;

public class PdfPageRangeParserTests
{
    [Fact]
    public void Parse_ShouldSupportSinglePagesAndRanges()
    {
        var result = PdfPageRangeParser.Parse("1,3-5,8", 10);

        Assert.True(result.IsSuccess);
        Assert.Equal([1, 3, 4, 5, 8], result.Pages);
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void Parse_ShouldRemoveDuplicatePages()
    {
        var result = PdfPageRangeParser.Parse("1,1,2-3,3", 5);

        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2, 3], result.Pages);
    }

    [Theory]
    [InlineData("", "请输入页码范围")]
    [InlineData("abc", "页码格式不正确")]
    [InlineData("3-1", "页码范围起始页不能大于结束页")]
    [InlineData("0", "页码必须从 1 开始")]
    [InlineData("1-6", "页码超出当前 PDF 页数")]
    public void Parse_ShouldReturnFailureForInvalidExpressions(string expression, string expectedMessage)
    {
        var result = PdfPageRangeParser.Parse(expression, 5);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedMessage, result.ErrorMessage);
        Assert.Empty(result.Pages);
    }
}

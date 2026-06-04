using AzrngTools.Models.Pdf;

namespace AzrngTools.Services.Pdf;

public static class PdfPageRangeParser
{
    public static PdfPageSelectionResult Parse(string? expression, int pageCount)
    {
        if (pageCount <= 0)
        {
            return PdfPageSelectionResult.Failure("当前 PDF 页数无效");
        }

        if (string.IsNullOrWhiteSpace(expression))
        {
            return PdfPageSelectionResult.Failure("请输入页码范围");
        }

        var pages = new SortedSet<int>();
        var parts = expression.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return PdfPageSelectionResult.Failure("请输入页码范围");
        }

        foreach (var part in parts)
        {
            var rangeParts = part.Split('-', StringSplitOptions.TrimEntries);
            if (rangeParts.Length == 1)
            {
                if (!TryParsePage(rangeParts[0], pageCount, out var page, out var error))
                {
                    return PdfPageSelectionResult.Failure(error ?? "页码格式不正确");
                }

                pages.Add(page);
                continue;
            }

            if (rangeParts.Length != 2)
            {
                return PdfPageSelectionResult.Failure("页码格式不正确");
            }

            if (!TryParsePage(rangeParts[0], pageCount, out var start, out var startError))
            {
                return PdfPageSelectionResult.Failure(startError ?? "页码格式不正确");
            }

            if (!TryParsePage(rangeParts[1], pageCount, out var end, out var endError))
            {
                return PdfPageSelectionResult.Failure(endError ?? "页码格式不正确");
            }

            if (start > end)
            {
                return PdfPageSelectionResult.Failure("页码范围起始页不能大于结束页");
            }

            for (var page = start; page <= end; page++)
            {
                pages.Add(page);
            }
        }

        return pages.Count == 0
            ? PdfPageSelectionResult.Failure("请输入页码范围")
            : PdfPageSelectionResult.Success(pages.ToArray());
    }

    private static bool TryParsePage(string value, int pageCount, out int page, out string? errorMessage)
    {
        errorMessage = null;

        if (!int.TryParse(value, out page))
        {
            errorMessage = "页码格式不正确";
            return false;
        }

        if (page < 1)
        {
            errorMessage = "页码必须从 1 开始";
            return false;
        }

        if (page > pageCount)
        {
            errorMessage = "页码超出当前 PDF 页数";
            return false;
        }

        return true;
    }
}

namespace AzrngTools.Services;

/// <summary>
/// 文章标题翻译
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// 中英文翻译
    /// </summary>
    /// <param name="chineseText"></param>
    /// <returns></returns>
    public Task<string> YandexChineseToEnglishAsync(string chineseText);

    /// <summary>
    /// 英中文翻译
    /// </summary>
    /// <param name="englishText"></param>
    /// <returns></returns>
    public Task<string> YandexEnglishToChineseAsync(string englishText);
}
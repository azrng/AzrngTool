namespace AzrngTools.Services;

/// <summary>
/// 页面输入记忆存档服务：把工具页声明的输入键值（标量已字符串编码）按页持久化到应用数据目录，
/// 页面构造时读取恢复，应用重启后输入与选项回到上次离开时的状态（DES-SETTINGS-002）。
/// 敏感字段（密钥、令牌、凭证）由页面侧排除——组装键值集合时不包含该键，本服务不做内容过滤。
/// </summary>
public interface IPageInputStoreService
{
    /// <summary>
    /// 提交页面输入快照：立即返回不做磁盘 IO，服务侧防抖合并连续变更后原子落盘；同页重复提交以最后一次为准。
    /// </summary>
    void Save(string pageId, IReadOnlyDictionary<string, string?> inputs);

    /// <summary>
    /// 读取页面存档；文件不存在或损坏返回空集合（损坏时记录错误日志），页面以自身默认值继续。
    /// </summary>
    IReadOnlyDictionary<string, string?> TryLoad(string pageId);
}

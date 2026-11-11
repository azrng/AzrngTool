using AzrngTools.Utils;
using System.IO;
using System.Runtime.CompilerServices;

namespace AzrngTools.Tests;

/// <summary>
/// 测试进程统一把 AppLog 日志目录重定向到临时区：未显式 ConfigureForTests 的用例
/// （页面输入/主题服务错误路径、性能探针等）不再向真实 %LOCALAPPDATA%\AzrngTools\logs 写日志。
/// AppLogTests 自己的用例会再次调用 ConfigureForTests 指向各自目录，该钩子幂等可覆盖。
/// </summary>
internal static class TestModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        AppLog.ConfigureForTests(Path.Combine(Path.GetTempPath(), "AzrngTools.Tests", "logs"));
    }
}

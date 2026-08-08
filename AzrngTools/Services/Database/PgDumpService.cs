using System.Diagnostics;
using System.Text;
using AzrngTools.Models.Database;
using AzrngTools.Models.Database.DTOs;

namespace AzrngTools.Services.Database;

/// <summary>
/// pg_dump 进程封装服务。通过 PGPASSWORD 环境变量传密码、--file 直接写文件、
/// 捕获 stderr 与 ExitCode 判定成败。
/// </summary>
public class PgDumpService : IPgDumpService, ISingletonDependency
{
    public async Task<PgDumpResult> ExecuteDumpAsync(
        PgDumpRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PgDumpExePath))
        {
            return Fail(request, "未指定 pg_dump.exe 路径。");
        }

        if (!File.Exists(request.PgDumpExePath))
        {
            return Fail(request, $"找不到 pg_dump 可执行文件：{request.PgDumpExePath}");
        }

        if (request.Schemas.Count == 0)
        {
            return Fail(request, "未选择要导出的 schema。");
        }

        var directory = Path.GetDirectoryName(request.OutputFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = request.Connection;
        var psi = new ProcessStartInfo
        {
            FileName = request.PgDumpExePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = string.IsNullOrWhiteSpace(directory) ? Environment.CurrentDirectory : directory
        };

        // --no-password：缺少密码时直接失败，避免交互式提示导致进程挂起
        psi.ArgumentList.Add("--no-password");
        psi.ArgumentList.Add("--host");
        psi.ArgumentList.Add(connection.Host);
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add(connection.Port.ToString());
        psi.ArgumentList.Add("--username");
        psi.ArgumentList.Add(connection.Username);
        psi.ArgumentList.Add("--dbname");
        psi.ArgumentList.Add(request.Database);

        foreach (var schema in request.Schemas)
        {
            psi.ArgumentList.Add("--schema");
            psi.ArgumentList.Add(schema);
        }

        switch (request.Mode)
        {
            case PgDumpContentMode.SchemaOnly:
                psi.ArgumentList.Add("--schema-only");
                break;
            case PgDumpContentMode.DataOnly:
                psi.ArgumentList.Add("--data-only");
                break;
        }

        if (request.OutputFormat == PgDumpOutputFormat.Custom)
        {
            psi.ArgumentList.Add("--format=custom");
        }

        psi.ArgumentList.Add("--file");
        psi.ArgumentList.Add(request.OutputFilePath);

        // 密码走环境变量，不进命令行参数
        psi.Environment["PGPASSWORD"] = connection.Password ?? string.Empty;

        progress?.Report($"正在执行 pg_dump（{request.Database}，{request.Schemas.Count} 个 schema）...");

        var stderr = new StringBuilder();
        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
            // 消费 stdout/stderr 防止缓冲区满导致死锁；--file 模式下 stdout 通常为空
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            await process.WaitForExitAsync(cancellationToken);
            // WaitForExitAsync 返回时异步读取回调可能尚未完成，补一次同步 WaitForExit 刷新，
            // 避免 stderr StringBuilder 丢失尾部行（影响 server version mismatch 等错误检测）
            process.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return new PgDumpResult
            {
                Success = false,
                ExitCode = -1,
                StdError = "导出已取消。",
                OutputPath = request.OutputFilePath,
                OutputFormat = request.OutputFormat
            };
        }
        catch (Exception ex)
        {
            TryKill(process);
            LoggingService.LogError("pg_dump 进程启动失败。", ex);
            return Fail(request, $"pg_dump 启动失败：{ex.Message}");
        }

        var success = process.ExitCode == 0;
        if (!success)
        {
            LoggingService.LogError($"pg_dump failed. exit={process.ExitCode}, stderr={stderr}");
        }

        return new PgDumpResult
        {
            Success = success,
            ExitCode = process.ExitCode,
            StdError = stderr.ToString(),
            OutputPath = request.OutputFilePath,
            OutputFormat = request.OutputFormat
        };
    }

    private static PgDumpResult Fail(PgDumpRequest request, string message) => new()
    {
        Success = false,
        ExitCode = -1,
        StdError = message,
        OutputPath = request.OutputFilePath,
        OutputFormat = request.OutputFormat
    };

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 忽略终止失败
        }
    }
}

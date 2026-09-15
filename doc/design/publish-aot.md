# 发布链路 AOT 恢复指南（DES-ARCH-002）

> 记录 Native AOT 的暂缓决定、原因与恢复步骤。当前发布方式为 JIT 自包含单文件，见 `architecture.md` 的「发布链路」。

## 需求追溯

- 服务于「发布链路」跨模块架构：产物形态、自动更新兼容性与反射依赖治理。

## 暂缓决定（2026-09-15）

发布链路从 Native AOT 回退为 JIT 自包含单文件，原因：

1. **产物形态不满足单 exe 预期**：AOT 机制下 Avalonia/Skia 原生库（`av_libglesv2.dll`、`libSkiaSharp.dll`、`libHarfBuzzSharp.dll` 等）必须留在 exe 旁，做不到真正的单文件；JIT 自包含可通过 `IncludeNativeLibrariesForSelfExtract` 把原生库嵌入 exe、运行时自解压。
2. **反射兼容维护成本高**：项目存在多处反射依赖（见下文审计清单），AOT 裁剪下每项都可能触发运行时 `MissingMethodException`，需要长期投入逐项治理。
3. **收益非刚需**：实测 JIT 自包含关闭压缩后启动约 1.2s、内存约 200MB，桌面工具可接受；AOT 的启动优势不构成切换理由。
4. 自动更新所需的固定命名产物与严格递增版本号由 release 工作流保证，与 AOT/JIT 无关，回退不影响更新链路。

## 恢复步骤

满足以下条件时，按序恢复：

1. **发布命令追加 AOT 参数**：`Publish.bat` 与 `.github/workflows/release.yml` 的 `dotnet publish` 加回 `-p:PublishAot=true`。csproj 已预留联动开关：`PublishAot=true` 时自动启用 `PublishTrimmed`、关闭 `PublishReadyToRun`、开启 `StripSymbols`，无需改 csproj。
2. **反射面兜底**：`AzrngTools/Roots.xml`（`TrimmerRootDescriptor`）保留本程序集全部元数据，标记接口扫描注册依赖运行期反射；新增反射依赖时同步补充根描述。
3. **JSON 序列化治理**：各持久化服务已使用 `JsonSerializerContext` 源生成（AppUpdate、HardwareInfo、ThemePreference、ToolUsageStats、ConnectionConfiguration、AnalysisSqlCacheConfig 等）；恢复前需审计 `Utils/JsonHelper.cs`——其通用序列化仍走反射（`JsonSerializer.Serialize(obj, options)`），AOT 下必须改为源生成上下文或确认调用面可裁剪。
4. **确认功能降级项**：XSLT 转 HTML 依赖运行时代码生成，AOT 版本不可用，页面保留明确提示。

## 反射依赖审计清单（恢复前逐项验证）

| 依赖 | 反射用途 | AOT 风险与处理 |
| --- | --- | --- |
| 标记接口扫描注册 | 运行期反射扫描程序集 | `Roots.xml` 兜底，新增服务无需额外处理 |
| `Utils/JsonHelper.cs` | 反射序列化 | 改造为源生成上下文，或收敛调用面 |
| NJsonSchema | 运行期类型/Schema 生成 | 验证 JSON Schema 页面，必要时裁剪或降级 |
| NPOI / DocumentFormat.OpenXml | 运行期反射构造 | 验证导出链路，补 TrimmerRoot |
| Markdown.Avalonia | 主题/扩展加载 | 验证预览功能（当前入口已隐藏） |
| CommunityToolkit.Mvvm | 源生成器实现，无运行期反射 | 无风险 |

## 验证清单

恢复 AOT 后至少完成：

1. `Publish.bat` 发布成功，`dist` 产物可启动。
2. 全部 35 个工具页逐一 smoke：打开、执行一次核心操作。
3. 数据库工作台连接、查询、导出链路回归。
4. JSON / XML / Schema 相关工具页重点回归（反射裁剪重灾区）。
5. 自动更新全链路：GitHub Release 下载、覆盖安装、重启。
6. 启动时长与内存对比 JIT 基线（启动约 1.2s、内存约 200MB），确认收益仍值得维护成本。

## 相关文档

- `architecture.md`：跨模块架构与发布链路现状。
- `README.md`「本地发布」「GitHub Actions 自动发布」：命令与版本号规则。

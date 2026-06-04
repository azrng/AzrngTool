## [ERR-20260424-001] dotnet-build-output-lock

**Logged**: 2026-04-24T13:35:52+08:00
**Priority**: medium
**Status**: resolved
**Area**: tests

### Summary
本地 `dotnet build` 因正在运行的 `AzrngTools.exe` 占用调试输出文件而失败

### Error
```text
MSB3027: 无法将“...apphost.exe”复制到“bin\Debug\net10.0-windows\AzrngTools.exe”。超出了重试计数 10。失败。文件被“AzrngTools (17776)”锁定。
MSB3021: 无法将文件“...apphost.exe”复制到“bin\Debug\net10.0-windows\AzrngTools.exe”。
```

### Context
- Command/operation attempted: `dotnet build AzrngTools.sln -v minimal`
- Running process: `AzrngTools.exe` from `AzrngTools\bin\Debug\net10.0-windows\AzrngTools.exe`
- Environment details: Windows / PowerShell

### Suggested Fix
- 重新编译前先确认本地调试进程是否仍在运行
- 若不能中断当前进程，优先改用 `Release` 配置或单独输出目录做等价验证

### Metadata
- Reproducible: yes
- Related Files: AzrngTools.sln

### Resolution
- **Resolved**: 2026-04-24T13:42:40+08:00
- **Commit/PR**: pending
- **Notes**: 本次改用 `dotnet build AzrngTools.sln -c Release -v minimal` 完成等价验证，避开正在运行的 Debug 产物锁定。

---

## [ERR-20260604-001] dotnet-test-baseintermediateoutputpath-gotcha

**Logged**: 2026-06-04T13:34:45+08:00
**Priority**: medium
**Status**: resolved
**Area**: tests

### Summary
为绕开运行中桌面应用锁定 `bin\Debug`，将 `BaseIntermediateOutputPath` 指到项目外后，SDK 默认 `Compile` 排除规则失效，项目内旧 `obj` 被当作源码编译并产生重复 AssemblyInfo 错误。

### Error
```text
CS0579: “TargetFrameworkAttribute”特性重复
CS0579: “AssemblyCompanyAttribute”特性重复
```

### Context
- Command/operation attempted: `dotnet test ... -p:BaseOutputPath=... -p:BaseIntermediateOutputPath=...`
- Environment details: Windows / PowerShell / .NET 10
- Trigger: `AzrngTools.exe` 正在运行并锁定默认 Debug 输出目录

### Suggested Fix
- 遇到桌面应用锁定默认输出目录时，优先只设置 `OutputPath` 到系统临时目录。
- 不要随意把 `BaseIntermediateOutputPath` 改到项目外；这会让 SDK 不再默认排除项目内 `obj/**`。

### Metadata
- Reproducible: yes
- Related Files: AzrngTools/AzrngTools.csproj
- See Also: ERR-20260424-001

### Resolution
- **Resolved**: 2026-06-04T13:34:45+08:00
- **Notes**: 改用 `dotnet test ... -p:OutputPath="%TEMP%\\AzrngToolTests-T124-Output\\bin\\"` 后，数据库相关 49 个测试通过。

---

## [ERR-20260602-001] parallel-release-build-test-pdb-lock

**Logged**: 2026-06-02T17:02:00+08:00
**Priority**: low
**Status**: resolved
**Area**: tests

### Summary
并行执行 `dotnet test -c Release` 和 `dotnet build -c Release` 时，主项目 Release PDB 被 `.NET Host` 占用，导致构建临时失败。

### Error
```text
CSC : error CS2012: 无法打开“AzrngTools.pdb”以进行写入 - The process cannot access the file because it is being used by another process.;文件可能被 '.NET Host' 锁定
```

### Context
- Command/operation attempted: 同时执行 `dotnet test AzrngTools.Tests\AzrngTools.Tests.csproj -c Release -v minimal` 和 `dotnet build AzrngTools.sln -c Release -v minimal`
- Environment details: Windows / PowerShell / .NET 10 / Release 配置

### Suggested Fix
- 对同一解决方案的 Release 测试和 Release 构建优先顺序执行，避免同时写入 `obj/Release` 下的 PDB / 编译中间产物。
- 如果已经遇到锁文件失败，等待测试进程退出后重跑构建确认结果。

### Metadata
- Reproducible: yes
- Related Files: AzrngTools.sln

### Resolution
- **Resolved**: 2026-06-02T17:02:30+08:00
- **Commit/PR**: pending
- **Notes**: 顺序重跑 `dotnet build AzrngTools.sln -c Release -v minimal` 后构建通过，0 warning / 0 error。

---

## [ERR-20260602-001] wrong-xaml-path-read

**Logged**: 2026-06-02T15:55:00+08:00
**Priority**: low
**Status**: resolved
**Area**: frontend

### Summary
读取数据库工作台 XAML 时把文件路径误写到 `Views\Database\Workbench` 子目录，导致 `Get-Content` 返回路径不存在

### Error
```text
Cannot find path 'AzrngTools\Views\Database\Workbench\DatabaseWorkbenchPageView.axaml' because it does not exist.
```

### Context
- Command/operation attempted: 读取 `DatabaseWorkbenchPageView.axaml` 前 140 行
- Environment details: Windows / PowerShell / AzrngTool

### Suggested Fix
- 读取非 C# 视图文件前优先以 `rg --files` 的真实结果为准
- 相邻文件路径只作为线索，不把目录层级当成事实

### Metadata
- Reproducible: yes
- Related Files: AzrngTools/Views/Database/DatabaseWorkbenchPageView.axaml

### Resolution
- **Resolved**: 2026-06-02T15:55:00+08:00
- **Commit/PR**: pending
- **Notes**: 已使用 `rg --files` 输出中的真实路径重新读取目标文件。

---

## [ERR-20260602-002] databasetype-namespace-test

**Logged**: 2026-06-02T16:16:00+08:00
**Priority**: low
**Status**: resolved
**Area**: tests

### Summary
新增 `DatabaseServiceTests` 时只引用 `AzrngTools.Models.Database`，误以为 `DatabaseType` 定义在项目模型命名空间，导致测试编译失败

### Error
```text
error CS0103: 当前上下文中不存在名称“DatabaseType”
```

### Context
- Command/operation attempted: `dotnet test AzrngTools.Tests\AzrngTools.Tests.csproj -c Release -v minimal`
- Environment details: Windows / PowerShell / .NET 10

### Suggested Fix
- 使用项目中真实引用来源，`DatabaseType` 当前来自 `Azrng.Core.Model`
- 遇到同名旧文件或注释文件时，以实际编译引用和现有源文件 using 为准

### Metadata
- Reproducible: yes
- Related Files: AzrngTools.Tests/Services/Database/DatabaseServiceTests.cs

### Resolution
- **Resolved**: 2026-06-02T16:16:00+08:00
- **Commit/PR**: pending
- **Notes**: 已为测试文件补充 `using Azrng.Core.Model;`。

---

## [ERR-20260602-003] static-helper-instance-call

**Logged**: 2026-06-02T16:27:00+08:00
**Priority**: low
**Status**: resolved
**Area**: tests

### Summary
将 `CreateCatalogConnectionConfig` 改成 `internal static` 供测试覆盖后，内部仍调用实例方法 `GetCatalogDatabaseName`，导致编译失败

### Error
```text
error CS0120: 对象引用对于非静态的字段、方法或属性“DatabaseService.GetCatalogDatabaseName(DatabaseType, string)”是必需的
```

### Context
- Command/operation attempted: `dotnet test AzrngTools.Tests\AzrngTools.Tests.csproj -c Release -v minimal`
- Environment details: Windows / PowerShell / .NET 10

### Suggested Fix
- 静态化 helper 时同步检查它直接调用的私有 helper 是否也应静态化

### Metadata
- Reproducible: yes
- Related Files: AzrngTools/Services/Database/DatabaseService.cs

### Resolution
- **Resolved**: 2026-06-02T16:27:00+08:00
- **Commit/PR**: pending
- **Notes**: 已将 `GetCatalogDatabaseName` 同步调整为 `static`。

---

## [ERR-20260427-001] ambiguous-jsonexception

**Logged**: 2026-04-27T11:06:01+08:00
**Priority**: low
**Status**: resolved
**Area**: backend

### Summary
在同时引用 `Newtonsoft.Json` 与 `System.Text.Json` 的工具类中直接捕获 `JsonException`，导致编译期类型名冲突

### Error
```text
C:\Work\github\AzrngTool\AzrngTools\Utils\JsonHelper.cs(60,28): error CS0104: “JsonException”是“Newtonsoft.Json.JsonException”和“System.Text.Json.JsonException”之间的不明确的引用
```

### Context
- Command/operation attempted: `dotnet build C:\Work\github\AzrngTool\AzrngTools.sln -c Debug`
- Input or parameters used: `JsonHelper` 新增 `try/catch` 处理 `JToken.Parse` 与 `JsonConvert.DeserializeObject`
- Environment details: Windows / PowerShell / .NET 10

### Suggested Fix
- 在同时使用两个 JSON 库的文件中，异常类型统一写全名，避免依赖 `using` 推断

### Metadata
- Reproducible: yes
- Related Files: AzrngTools/Utils/JsonHelper.cs

### Resolution
- **Resolved**: 2026-04-27T11:06:01+08:00
- **Commit/PR**: pending
- **Notes**: 将相关 `catch (JsonException)` 全部改为 `catch (Newtonsoft.Json.JsonException)`，与当前解析实现保持一致。

---

## [ERR-20260427-002] dotnet-new-windows-framework-option

**Logged**: 2026-04-27T12:21:52+08:00
**Priority**: low
**Status**: resolved
**Area**: tests

### Summary
`dotnet new` 模板在当前环境下不接受 `net10.0-windows` 作为 `--framework` 参数，创建测试工程时会直接报参数无效

### Error
```text
错误: 无效选项:
--framework net10.0-windows
“net10.0-windows”不是“--framework”的有效值。
```

### Context
- Command/operation attempted: `dotnet new xunit --framework net10.0-windows --output C:\Work\github\AzrngTool\AzrngTools.Tests`
- Environment details: Windows / PowerShell / .NET 10 SDK

### Suggested Fix
- 先用模板支持的 `net10.0` 创建项目，再手动把 `.csproj` 改为 `net10.0-windows`

### Metadata
- Reproducible: yes
- Related Files: AzrngTools.Tests/AzrngTools.Tests.csproj

### Resolution
- **Resolved**: 2026-04-27T12:21:52+08:00
- **Commit/PR**: pending
- **Notes**: 本次先执行 `dotnet new xunit --framework net10.0`，再手动把测试工程目标框架改为 `net10.0-windows` 并添加主项目引用。

---

## [ERR-20260427-003] avaloniaedit-test-context

**Logged**: 2026-04-27T13:57:13+08:00
**Priority**: low
**Status**: resolved
**Area**: tests

### Summary
在当前测试栈中直接实例化 `AvaloniaEdit.TextEditor` 会因缺少 UI 上下文而触发类型初始化异常，导致行为层回归测试无法稳定运行

### Error
```text
System.TypeInitializationException : The type initializer for 'AvaloniaEdit.Editing.CaretNavigationCommandHandler' threw an exception.
---- System.NullReferenceException : Object reference not set to an instance of an object.
```

### Context
- Command/operation attempted: `dotnet test C:\Work\github\AzrngTool\AzrngTools.Tests\AzrngTools.Tests.csproj -c Debug`
- Input or parameters used: 在单元测试中直接 `new TextEditor()` 验证 `TextEditorBinding`
- Environment details: Windows / PowerShell / xUnit / 当前测试工程未引入 Avalonia Headless 或专门的 UI 测试上下文

### Suggested Fix
- 不要在当前纯单元测试栈中直接实例化 `TextEditor`
- 若后续需要覆盖编辑器绑定行为，优先引入 Avalonia Headless 或补充专门的 UI 测试上下文后再写行为层测试

### Metadata
- Reproducible: yes
- Related Files: AzrngTools.Tests/Behaviors/TextEditorBindingTests.cs

### Resolution
- **Resolved**: 2026-04-27T13:57:13+08:00
- **Commit/PR**: pending
- **Notes**: 本次移除不稳定的 `TextEditorBinding` 行为测试，保留共享绑定修复，并使用现有单元测试与 Release 构建完成回归验证。

---

## [ERR-20260427-004] debug-withdevelopertools-missing

**Logged**: 2026-04-27T14:07:20+08:00
**Priority**: low
**Status**: pending
**Area**: tests

### Summary
在 `Debug` 配置下编译主项目时，`Program.cs` 中的 `WithDeveloperTools()` 扩展方法当前无法解析，导致 `dotnet test -c Debug` 失败

### Error
```text
C:\Work\github\AzrngTool\AzrngTools\Program.cs(31,14): error CS1061: “AppBuilder”未包含“WithDeveloperTools”的定义，并且找不到可接受第一个“AppBuilder”类型参数的可访问扩展方法“WithDeveloperTools”
```

### Context
- Command/operation attempted: `dotnet test C:\Work\github\AzrngTool\AzrngTools.Tests\AzrngTools.Tests.csproj -c Debug`
- Environment details: Windows / PowerShell / .NET 10 / Avalonia 12

### Suggested Fix
- 单独检查 `WithDeveloperTools()` 相关包引用与 `using` 变化，确认 Avalonia 12 下调试工具扩展方法的来源是否已调整
- 在问题定位前，测试与构建优先使用 `Release` 配置完成等价验证

### Metadata
- Reproducible: yes
- Related Files: AzrngTools/Program.cs

---

## [ERR-20260424-002] rg-pattern-escaping

**Logged**: 2026-04-24T13:35:52+08:00
**Priority**: low
**Status**: resolved
**Area**: docs

### Summary
在 PowerShell 中给 `rg` 传入带转义双引号的正则时写法错误，导致命令未执行

### Error
```text
rg: regex parse error:
    (?:VerticalAlignment=\)
    ^
error: unclosed group
```

### Context
- Command/operation attempted: 用 `rg` 搜索 `VerticalAlignment=\"Top\"` 等 XAML 模式
- Input or parameters used: 双引号与反斜杠混用的正则表达式
- Environment details: Windows PowerShell

### Suggested Fix
- 在 PowerShell 下优先使用单引号包裹 `rg` 模式
- 只查固定文本时优先改用 `rg -F`

### Metadata
- Reproducible: yes
- Related Files: AzrngTools/Views/Encrypts/*.axaml

### Resolution
- **Resolved**: 2026-04-24T13:42:40+08:00
- **Commit/PR**: pending
- **Notes**: 后续改用 `Select-String -SimpleMatch` 做固定文本检索，避免 PowerShell 下 `rg` 正则转义歧义。

---
## [ERR-20260602-001] dotnet_script_unavailable

**Logged**: 2026-06-02T22:10:00+08:00
**Priority**: low
**Status**: pending
**Area**: infra

### Summary
Attempted to inspect Azrng.Core metadata with `dotnet script`, but the global tool is not installed in this environment.

### Error
```text
无法执行，因为找不到指定的命令或文件。
```

### Context
- Command/operation attempted: `dotnet script -`
- Task: inspect NuGet assembly public API for ResultModel and exception types.
- Environment: Windows PowerShell, .NET SDK available but dotnet-script unavailable.

### Suggested Fix
Use PowerShell reflection with dependency resolution, `dotnet exec` against a small compiled helper, or inspect package XML/docs instead of assuming `dotnet-script` exists.

### Metadata
- Reproducible: yes
- Related Files: AzrngTools/AzrngTools.csproj

---
## [ERR-20260602-002] parallel_dotnet_build_test_file_lock

**Logged**: 2026-06-02T22:23:00+08:00
**Priority**: low
**Status**: resolved
**Area**: tests

### Summary
Running `dotnet build` and `dotnet test` in parallel against the same solution caused an Avalonia/MSBuild file lock on the shared obj output DLL.

### Error
```text
MSBUILD : Avalonia error AVLN9999: The process cannot access the file 'AzrngTools\obj\Debug\net10.0-windows\AzrngTools.dll' because it is being used by another process.
```

### Context
- Commands were launched concurrently through parallel tool execution.
- The database-filtered test completed successfully; the build failed only due to shared output file contention.

### Suggested Fix
Do not run `dotnet build` and `dotnet test` concurrently for this solution unless using separate output directories. Run them sequentially for final verification.

### Metadata
- Reproducible: yes
- Related Files: AzrngTools/AzrngTools.csproj

### Resolution
- **Resolved**: 2026-06-02T22:24:00+08:00
- **Notes**: Re-ran `dotnet build AzrngTools.sln -v minimal` sequentially after tests completed; build passed with 0 warnings and 0 errors.

---
## [ERR-20260603-001] powershell_rg_path_glob_mismatch

**Logged**: 2026-06-03T09:25:44+08:00
**Priority**: low
**Status**: pending
**Area**: infra

### Summary
PowerShell 下把类 Unix 的 `rg` 参数直接用于仓库检索，导致不存在的 `src` 路径和通配符参数报错。

### Error
```text
rg: src: 系统找不到指定的文件。 (os error 2)
rg: *.axaml: 文件名、目录名或卷标语法不正确。 (os error 123)
```

### Context
- Command/operation attempted: `rg -n "对象浏览器|存储过程|搜索表|Database" -S src *.axaml *.csproj design-system.yaml`
- Environment details: Windows PowerShell，当前仓库实际工程目录为 `AzrngTools/`，不是 `src/`。

### Suggested Fix
先用 `rg --files` 或 CodeGraph 文件索引确认仓库结构；在 PowerShell 下优先传入真实目录，例如 `rg -n "pattern" AzrngTools -S`，不要假设 Unix shell 风格 glob 会按预期展开。

### Metadata
- Reproducible: yes
- Related Files: AGENTS.md

---
## [ERR-20260603-002] msbuild_intermediate_output_inside_project

**Logged**: 2026-06-03T09:31:00+08:00
**Priority**: low
**Status**: pending
**Area**: infra

### Summary
把 `BaseIntermediateOutputPath` 设置到项目目录内的 `artifacts/verify-build/obj/` 后，SDK 风格项目把生成的 AssemblyInfo 等中间文件纳入编译，触发重复特性错误。

### Error
```text
error CS0579: “System.Reflection.AssemblyCompanyAttribute”特性重复
```

### Context
- Command/operation attempted: `dotnet build AzrngTools.sln -v minimal -p:BaseOutputPath=artifacts\verify-build\bin\ -p:BaseIntermediateOutputPath=artifacts\verify-build\obj\`
- Environment details: Windows PowerShell，SDK-style .NET 项目。

### Suggested Fix
需要绕开被锁定的默认输出时，优先切换配置（如 `-c Release`）或把临时中间目录放到项目目录外；不要把 `BaseIntermediateOutputPath` 放进项目源码树下。

### Metadata
- Reproducible: yes
- Related Files: AzrngTools/AzrngTools.csproj

---

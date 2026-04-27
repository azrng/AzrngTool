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

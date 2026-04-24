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

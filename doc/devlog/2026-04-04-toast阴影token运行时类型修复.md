# 2026-04-04 Toast 阴影 token 运行时类型修复

## 本次目标
- 重新检查导出成功后没有提示的问题。
- 找出 Toast 实际未显示的运行时根因并修复。

## 核心改动
- 检查应用输出目录日志，定位到 Toast 创建时持续抛出的 UI 线程异常：`Unable to cast object of type 'System.String' to type 'Avalonia.Media.BoxShadows'`。
- 确认问题来自 `ToastNotification` 与 `LoadingOverlay` 直接将字符串类型的 `Shadow.*` 动态资源绑定到 `BoxShadow` 属性。
- 移除这两个控件 XAML 中直接绑定 `BoxShadow` 的做法。
- 在控件挂到可视树后，通过 `Application.Current.TryGetResource(...)` 读取当前主题下的字符串阴影 token，并用 `BoxShadows.Parse(...)` 转成真正的 `BoxShadows` 再赋值。
- 保留现有 token 资源结构不动，避免影响当前主题系统其它资源组织方式。

## 修改文件
- `AzrngTools/Controls/Database/ToastNotification.axaml`
- `AzrngTools/Controls/Database/ToastNotification.axaml.cs`
- `AzrngTools/Controls/Database/LoadingOverlay.axaml`
- `AzrngTools/Controls/Database/LoadingOverlay.axaml.cs`
- `TASK.md`

## 校验情况
- 已执行：`dotnet build`
- 结果：通过，0 warning，0 error

## 风险或遗留项
- 当前已修复日志中确认存在的 Toast 创建异常，但仍需重新启动应用后手动验证导出成功提示、导出失败提示、加载浮层显示三条 UI 链路。
- `Shadow.*` token 目前仍保持字符串资源形态；本次通过控件侧解析保证运行稳定，后续若要做更系统的阴影 token 类型治理，需统一评估整个样式体系。

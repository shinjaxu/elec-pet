# 屏幕陪伴吐槽 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 前台窗口停留 30 秒后，桌宠基于本地视觉模型给出陪伴式吐槽。

**Architecture:** 独立的纯逻辑会话时钟管理计时、冷却和版本。Windows 层读取前台窗口并在后台截取主屏可见区域；运行时编排截图与异步 Ollama 请求，控制器负责气泡和菜单接入。

**Tech Stack:** Unity 6000.6.6f1、C#、Win32/GDI、后台 HttpWebRequest、Ollama qwen3.5:4b。

**Spec:** `docs/superpowers/specs/2026-10-23-screen-companion-design.md`

## Global Constraints

- 仅 Windows 独立程序观察，截图只驻留内存且只发送本地模型。
- 连续停留 30 秒、全局冷却 180 秒、最长边 1280 像素、推理超时 60 秒。
- 单个在途任务，交互和窗口变化使旧任务失效；失败不阻塞原有宠物行为。
- 最多 80 个字符、约 12 秒气泡、右键独立开关保存偏好。
- 不修改无关行为，不引入第三方依赖，新增注释使用中文。

## Review Focus

- 切走再切回同一标题不能接受旧任务的结果。
- 服务不可用、模型缺失或不具备视觉能力时不能不断重试。
- 捕获失败、前台改变、锁屏不能泄漏旧像素或弹出旧气泡。
- 暂停、隐藏、喂食、拖拽及开关切换后不能遗留请求或气泡。
- 高 DPI 和部分超出主屏的窗口不能生成越界图像；宠物不能出现在截图中。

### Task 1: 纯逻辑会话时钟

**Files:** `Assets/Scripts/Core/ScreenObservationClock.cs`、`Assets/Tests/EditMode/ScreenCompanionChecks.cs`、测试包装及 `tools/Test-Core.ps1`。

**Interfaces:** `Observe(string identity, double now, bool available)`；`TryBegin(double now, out long generation)`；`IsCurrent(long generation)`。观察对象为 null 或不可用时失效当前会话。TryBegin 原子地标记本会话已处理并更新冷却。

- [x] 写门槛、会话切换、切回、冷却、暂停恢复和单次触发测试，先观察失败。
- [x] 实现时钟，通过独立核心测试及 Unity EditMode 包装。

### Task 2: Windows 窗口读取和截图

**Files:** `Assets/Scripts/Windows/WindowsScreenObserver.cs`、原生独立检查工具。

**Interfaces:** `ObservedWindow`（Handle、Identity、Bounds），`ScreenPixels`（Width、Height、Rgba）；`ReadForeground(IntPtr petWindow)`；`Capture(ObservedWindow expected, IntPtr petWindow)`；`ExcludeFromCapture(IntPtr petWindow)`。

- [x] 先写合成窗口检查裁剪、颜色、尺寸和排除行为，观察功能缺失失败。
- [x] 实现只读前台检测、锁屏保护、GDI 缩放与资源释放；捕获前后确认身份。
- [x] 用独立合成窗口验证，不截图用户正在使用的程序。

### Task 3: 模型编排、气泡与开关

**Files:** `Assets/Scripts/Runtime/PetScreenCompanion.cs`、`DesktopPetController.cs`、`NativeWindow.cs`，必要的测试及编辑器验收入口。

- [x] 检查 /api/show 能力与 /api/chat 的请求/响应契约，用合成图做模型调用验证。
- [x] 异步编排一个捕获/请求，60 秒超时；任务过期或交互不可用时取消并清除气泡。
- [x] 接入 80 字气泡、12 秒显示和持久化菜单开关。
- [x] 验证服务失败、静默、长回复、旧回复及停止流程。

### Task 4: 整体验证与交付

- [x] 运行核心测试、Unity EditMode 与 Windows 构建；修复失败。
- [x] 显式合成窗口验证真实截图与宠物排除，Windows Player 用合成图验证模型回复与取消；气泡观感和多 DPI 实机体验保留人工验收清单。
- [x] 更新 README 与验证记录，打包并校验免安装 EXE。

## 执行记录

- 用户明确要求开始实施，沿用已确认的方案直接执行，不重复请求范围确认。
- 当前目录没有 Git 元数据，保留文档和验证日志，不创建分支或提交。
- 网络实现改为固定回环地址的后台 HttpWebRequest，保留 Unity 原有 HTTP 策略；自动审批拒绝了全局 AlwaysAllowed，未应用该设置。

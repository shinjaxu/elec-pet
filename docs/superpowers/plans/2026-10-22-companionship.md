# 长期陪伴功能实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付可观察的三个关系阶段、固定小窝、小球自主互动和可恢复的本地存档。

**Architecture:** 扩展独立 Core 关系规则，新增生活流程与存档类；Runtime 负责生活流程的坐标、模型、输入及保存调度。Controller 保留更新编排，原生窗口只扩展菜单信息。

**Tech Stack:** Unity 6000.6.6f1、C#、Windows x64、内置渲染管线、现有 NUnit 和 PowerShell 独立测试入口。

**Spec:** [已确认设计](../specs/2026-10-22-companionship-design.md)

**执行状态：** 已完成代码、独立审查修复、测试、预览及 Windows 构建。以下复选框保留原始步骤清单；实际完成证据与调整见 [执行记录](2026-10-22-companionship-progress.md) 和 [验证记录](../../verification.md)。真实桌面验收仍待执行。

## 全局约束

- 本轮交付三个关系阶段、一个固定小窝、一个小球玩具与可靠的本地存档。
- 精力和心情启动时恢复，离线不扣关系、不补算需求。
- 沿用 Unity、Windows 主屏幕、程序化幼龙与现有透明窗口。
- 全部新增或修改的业务注释使用中文；只修改本轮链路，不重构无关代码。
- 暂不引入 SQLite；JSON 存档带版本、临时写入和有效备份；损坏原件须保留。
- 物件与宠物接收命中输入，其他区域继续穿透，不抢焦点。
- 当前目录没有 Git 仓库，交付本地文件，不创建仓库或提交。

## 重点审查

1. 拖动小窝或小球时宠物恰好接近：不能继续追逐旧目标或误触发提起（任务 2、4）。
2. 玩球或靠近途中缩小工作区：宠物与物件都要约束边界，恢复尺寸不跳回旧轨迹（任务 2、4）。
3. 恢复备份后首次保存：不能用损坏主文件覆盖唯一有效备份（任务 3）。
4. 隐藏或暂停时修改过的数据尚未落盘：允许保存，不能推进生活行为（任务 3、4）。
5. 主文件版本高于支持版本：保留原文件，不用默认状态自动覆盖；日志说明无法读取（任务 3）。

## 任务 1：关系阶段与可见反馈

**文件：** 修改 `Assets/Scripts/Core/PetBehavior.cs`、`Assets/Scripts/Runtime/DesktopPetController.cs`；扩展 `Assets/Tests/EditMode/BehaviorChecks.cs` 和 `PetBehaviorTests.cs`。

**接口：** Core 新增 `PetRelationship { New, Familiar, Trusted }` 和只读 `PetBehavior.Relationship`。由 Familiarity 推导：小于 0.1 为 New，小于 0.35 为 Familiar，其余为 Trusted。现有 `HeadPreference` 保留，不再用它单独决定熟悉后的凑头表现。

- [ ] 写失败检查：熟悉度 0、0.099、0.1、0.349、0.35、1 对应正确阶段；连续抚摸每两秒仅增长一次；重建 PetBehavior 后关系不降低。
- [ ] 执行 `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-Core.ps1`，确认新增接口尚不存在或断言失败。
- [ ] 实现阶段推导；Runtime 的凑头反馈在 Familiar 或 Trusted 时启用；保持原有招呼频率和熟悉度增长速度。
- [ ] 重跑同一命令，确认原有运动、互动与新增关系检查通过。

## 任务 2：独立生活流程

**文件：** 新建 `Assets/Scripts/Core/PetLife.cs`、`Assets/Tests/EditMode/LifeChecks.cs`、`PetLifeTests.cs`；修改 `tools/Test-Core.ps1`。

**接口：** 新增 `PetLifeState { Free, GoingHome, Resting, ApproachingOwner, RestingNearOwner, ApproachingBall, WatchingBall, TappingBall, RollingBall }`；`PetLife.State`、`Target`、`BallPosition`、`Progress` 为只读。提供 `Tick(double seconds, PetBehavior behavior, Point2 position, Point2 pointer, bool pointerQuiet, bool mouseBusy)`、`Interrupt()`、`SetHome(Point2 point)`、`SetBall(Point2 point)`、`Resize(DesktopRect bounds)`。构造函数 `PetLife(Random random, DesktopRect bounds, Point2 home, Point2 ball)`；仅 Tick 推进活动时间。

- [ ] 编写失败测试：精力小于 0.35 时回窝优先；距离目标不超过 4 像素才休息；Resting 恢复精力；互动打断后恢复 Free。
- [ ] 编写失败测试：Trusted 且鼠标稳定 4 秒、距离 120–260 像素时才尝试主动靠近，停在距鼠标 100 像素处；靠近最短间隔 90 秒；鼠标移动超过 12 像素或按键中断靠近。
- [ ] 编写失败测试：精力至少 0.5 时可以接近球，观察 1.5 秒、轻碰 0.8 秒、滚动 1.2 秒且最多 45 像素；玩耍间隔至少 45 秒；随机触发通过固定种子复现。
- [ ] 编写边界测试：Tick(0) 保留阶段、位置与冷却；改变窝或球位置取消相关旧流程；缩小再恢复工作区无越界或旧轨迹回跳；极小工作区不会无限尝试不可达目标。
- [ ] 在 Test-Core.ps1 接入 LifeChecks.Run()，先确认失败，再实现 PetLife。生活流程占用期间由 Runtime 阻止随机动作抢占；休息通过 PetBehavior 新增 `Rest(double seconds)` 明确进入恢复精力的卧躺状态，不直接修改精力字段。
- [ ] 重跑独立测试，全部通过；每次新建源码同步创建 Unity .meta，或由实际 Unity 导入生成，不复制 GUID。

## 任务 3：可恢复的本地存档

**文件：** 新建 `Assets/Scripts/Core/PetSaveData.cs`、`PetSaveStore.cs`、`Assets/Tests/EditMode/SaveChecks.cs`、`PetSaveTests.cs`；修改独立测试入口。

**接口：** `PetSaveData` 保存 `Version = 1`、`Familiarity`、`HeadPreference`、`HomeX`、`HomeY`、`BallX`、`BallY`。`PetSaveStore(string directory, Func<string, PetSaveData> decode, Func<PetSaveData, string> encode)` 通过委托隔离 Unity JsonUtility。公开 `PetSaveData Load(Func<PetSaveData> migrate)`、`void Save(PetSaveData data)`、`bool CanSave`，并用 `Action<string, Exception> Report` 向调用方报告失败阶段。读取无存档才调用 migrate；恢复默认或备份不读取旧 PlayerPrefs 冒充当前状态。

- [ ] 写失败测试：有效数据往返和版本、范围、NaN/无穷坐标校验；首次迁移只发生一次；存档不含精力、心情或离线时长。
- [ ] 写故障测试：主文件损坏从备份恢复；恢复后保存保留有效备份；双份损坏先保留损坏原件才允许保存默认状态；较新版本禁写且不改原件。
- [ ] 写故障测试：不可写路径保存失败抛出异常，修复路径后重试成功；临时文件写入失败不能破坏主文件。测试使用系统临时目录，finally 清理自己创建的文件。
- [ ] 运行独立测试确认失败，再实现同目录临时写入、主文件替换、有效备份和损坏原件保留；读取/写入失败日志由 Runtime 接收一次，不重复记录。
- [ ] 重跑独立测试并确认通过。独立检查传入不依赖 Unity 的测试编解码器，Unity 测试另验证实际 JsonUtility 往返。

## 任务 4：物件、输入与流程集成

**文件：** 新建 `Assets/Scripts/Runtime/PetHomeAndBall.cs`、`PetCompanionship.cs`、`Assets/Tests/EditMode/CompanionshipTests.cs`；修改 `DesktopPetController.cs`、`Assets/Scripts/Windows/NativeWindow.cs`。

**接口：** `PetHomeAndBall` 负责两个程序化模型、碰撞体、可见性与销毁；`Transform Home`、`Transform Ball` 暴露命中根节点。`PetCompanionship` 持有 PetLife、PetSaveStore、脏标记和保存调度；Controller 向其传入命中对象、活动时间和屏幕工作区，具体 Unity 接口在此任务内封装，不新增公共通用框架。

- [ ] 添加 Unity 失败测试：球/窝命中只拖动对应物件，不触发宠物 PickUp 或增加 Familiarity；拖动期间保持交互，松手立即保存。
- [ ] 添加 Unity 失败测试：归一化位置在偏移工作区、分辨率和缩放变化后正确映射；旧 PlayerPrefs 屏幕比例转换后迁移；旧键保留。
- [ ] 添加 Unity 失败测试：隐藏/暂停冻结生活与滚动，已脏数据仍按实际时间保存；30 秒自动保存只在脏状态发生；失败保留脏标记，下周期重试。
- [ ] 实现窝垫、小球、分类命中与拖动。物件屏幕位置和宠物共用 WorldPosition 投影，小球轻碰复用伸爪片段；生活流程优先级遵循设计，接近和玩球期间不执行随机闲置或图标访问。
- [ ] 接入 JsonUtility 与 persistentDataPath，迁移旧 PlayerPrefs。替换 OnDestroy 旧保存链路，补充 OnApplicationQuit；确保退出双回调不会重复写入。编辑器退出同样保存。
- [ ] 为右键菜单增加禁用的关系阶段标签，保留 Pause/Hide/Quit 的原有数值和快捷键映射；编辑器面板显示阶段和生活状态。
- [ ] 运行完整 Unity EditMode；测试失败时先定位原因，再针对本轮链路修正。

## 任务 5：预览、构建与交付

**文件：** 新建 `Assets/Editor/CompanionshipPreview.cs`；修改 `README.md`、`docs/verification.md`、`docs/acceptance.md`。

- [ ] 新增编辑器入口 `ElecPet.Editor.CompanionshipPreview.Export`，使用真实运行时模型与相机导出窝边休息、观察球、轻碰球三张预览，保存到 `docs/previews/companionship/`。查看图片，修正遮挡、比例与穿模。
- [ ] 执行独立测试：`powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-Core.ps1`，所有检查通过。
- [ ] 执行 Unity 测试：`& 'C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Unity.exe' -batchmode -projectPath 'D:/shinja/proj/self/elec-pet' -runTests -testPlatform EditMode -testResults 'Logs/companionship-tests.xml' -logFile 'Logs/companionship-tests.log'`。检查进程退出码、XML 中失败数为零及实际测试数量；不使用 -quit 提前中断测试。
- [ ] 导出预览：同一 Unity 路径，使用 `-batchmode -quit -projectPath 'D:/shinja/proj/self/elec-pet' -executeMethod ElecPet.Editor.CompanionshipPreview.Export -logFile 'Logs/companionship-preview.log'`。
- [ ] 构建：同一 Unity 路径，使用 `-batchmode -quit -projectPath 'D:/shinja/proj/self/elec-pet' -executeMethod ElecPet.Editor.PrototypeSetup.BuildWindows -logFile 'Logs/companionship-build.log'`，确认退出码 0 和构建成功记录。
- [ ] Unity 因本地许可证服务被沙箱阻止时，按实际错误申请该测试或构建命令的沙箱外执行，不绕过许可证校验。
- [ ] 对照设计逐项审查关系、生活流程、输入、存档与回归影响；新增验收条目覆盖真实桌面焦点、穿透、不同缩放、退出重启和强制结束后的恢复。
- [ ] 更新 README 的功能、操作及限制；验证记录只写实际执行结果，未进行实机交互则明确标注。交付源代码、预览与 Windows 构建路径。

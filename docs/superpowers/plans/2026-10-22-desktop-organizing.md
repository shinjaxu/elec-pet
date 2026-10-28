# 宠物自动整理桌面图标 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 桌宠在键鼠空闲 30 秒后自主逐个搬运真实桌面图标并分类排齐，输入恢复即暂停，文件路径不变。

**Architecture:** 纯 C# 布局规划与搬运状态机生成可测试的计划和轨迹。单个 STA Shell 会话串行读取和定位图标，Runtime 编排表现并处理输入、快照变化与暂停。沿用现有窗口、程序化幼龙和测试脚本，不引入第三方进程或依赖。

**Tech Stack:** Unity 6000.6.6f1、C#、Windows Shell COM、Win32、PowerShell、Unity EditMode/NUnit。

**Spec:** `docs/superpowers/specs/2026-10-22-desktop-organizing-design.md`

## Global Constraints

- 键鼠空闲 30 秒后开始，重新操作电脑时暂停，再次空闲后继续。
- 只调整图标位置，不改变文件路径，不创建归档目录，不打开、复制、删除或重命名文件。
- 沿用 Unity 6000.6.6f1、现有程序化幼龙、透明窗口与主屏幕范围。
- 用户不需要点击菜单或启动额外工具；功能随现有桌宠运行。
- 自动排列开启时不启动搬运，不擅自关闭系统设置。
- Shell COM 操作在专用 STA 工作线程串行执行，COM 对象和 PIDL 在所属线程持有与释放。
- 暂停或取消使旧请求失效；继续前确认最后实际位置，不让延迟写入覆盖用户调整。
- 新增或修改注释使用中文。只改本功能链路；保留存档格式、关系成长和普通拖拽规则。
- 当前不是 Git 仓库，不执行初始化、分支或提交；每项完成后保留工作区文件并更新勾选状态。

## Review Focus

1. 快捷方式指向目录、同名图标与 Shell 枚举顺序改变：必须分类正确且保持身份稳定，任务 2 覆盖。
2. 满屏、槽位互换、跨屏和 DPI 换算：不能叠放、丢图标或触碰副屏，任务 2、3、6 覆盖。
3. 输入发生在请求排队后、执行前或响应前：旧写入失效、恢复不跳动，任务 3、4 覆盖。
4. 对齐网格、用户修改设置、Explorer 重启：恢复所改设置，旧 COM/PIDL 不复用，任务 1、3、6 覆盖。
5. 图标靠边、四足叼持、暂停跨过长时间：接触自然、移动连续，任务 4、5 覆盖。

## 文件与职责

- 新增 `Assets/Scripts/Core/DesktopOrganization.cs`：项目类型、分类、确定性网格计划和占位顺序。
- 新增 `Assets/Scripts/Core/DesktopCarry.cs`：纯坐标搬运阶段、轨迹、输入等待和进度。
- 修改 `Assets/Scripts/Windows/DesktopIcons.cs`：保留只读入口，新增 Shell 会话和图标定位接口。
- 新增 `Assets/Scripts/Windows/DesktopIconWorker.cs`：专用 STA、合并请求、代次、取消和资源释放。
- 修改 `Assets/Scripts/Windows/NativeWindow.cs`：空闲时间、非启动用途的整理状态展示。
- 新增 `Assets/Scripts/Runtime/PetDesktopOrganizer.cs`：功能编排；新增 `DesktopOrganizationPreview.cs`：合成图标演示。
- 修改 `Assets/Scripts/Runtime/DesktopPetController.cs`、`PrototypePet.cs`：最小必要的优先级、目标注视与姿态连接。
- 新增 `Assets/Tests/EditMode/OrganizationChecks.cs`、`OrganizationTests.cs`、`CarryChecks.cs`、`CarryTests.cs`、`IconWorkerChecks.cs`、`IconWorkerTests.cs`；沿用纯 C# 检查与 NUnit 包装结构。
- 修改 `tools/Test-Core.ps1`；新增 `tools/Test-Organization.ps1`；修改 README 与验收、验证记录。
- 如需批处理导出，新增 `Assets/Editor/OrganizationPreview.cs`，避免改写已有行走预览。
- 新增 Unity 资源的 `.meta` 使用 Unity 导入生成，不复制其他资源 GUID。

## Task 1：先验证 Shell 能否平滑搬动真实图标

**Files:** 修改 DesktopIcons、NativeWindow；新增 Test-Organization.ps1，接口伪造测试归任务 3。

**Interfaces:**
- `NativeWindow.ReadIdleSeconds(): double`，失败抛出原生异常，默认调用端停止整理。
- `DesktopIcons.OpenSession(): DesktopIconSession`，返回仅供当前 STA 使用的 IDisposable 会话。
- `DesktopIconSession.Move(string id, Point2 screenPosition): Point2` 返回读回位置；`RestoreAlignment(): void`。
- 快照托管类型在任务 2 定义，公开 Read 方法在任务 3 接入；本任务先使用最小私有探测表示取得身份、位置和设置，不提前扩展 Runtime。

- [x] 1. 在独立脚本加入只读默认模式及显式 `-Write -ItemId <id>` 模式；写模式要求明确项目身份，只移动指定测试图标。记录原位置和对齐标志，finally 恢复，不遍历修改用户全部桌面。
- [x] 2. 先运行只读模式，输出会话是否可用、空闲秒数、间距和自动排列状态；不得输出文件名、路径。确认当前已有探测仍只读。
- [x] 3. 按微软文档扩展 COM 槽位与签名，GetAutoArrange 保留 HRESULT；位置调用仅使用 SVSI_POSITIONITEM，不选择、激活或模拟输入。主屏/工作区坐标由原生窗口测量转换。
- [ ] 4. 明确测试项目后，写模式用约 30Hz 缓动轨迹验证读回位置、网格吸附及用户输入中断。若吸附阻碍，保存并暂改对应对齐标志，异常和中断恢复；用户改过该位时不覆盖其选择。
- [x] 5. 检查写入恢复结果并记录。若实测证明不能可靠连续定位或恢复设置，停止后续实现并报告真实限制，不用副本动画替代已确认的真实图标方案。未执行真实写入时可继续纯逻辑和模拟实现，保留该验收未通过状态。

**Verify:** `powershell -STA -NoProfile -ExecutionPolicy Bypass -File tools/Test-Windows.ps1` 和新脚本默认模式，预期编译通过且无写入；显式写模式预期中间读回坐标连续变化、最终恢复成功。未获明确测试项目时完成只读与模拟验证，写入验收单独列为未执行，不能宣称通过。

## Task 2：确定性分类布局与无重叠搬运顺序

**Files:** 新增 DesktopOrganization、OrganizationChecks、OrganizationTests；修改 Test-Core.ps1 注册检查。

**Interfaces:**
- `DesktopItem(string id, string name, string extension, bool folder, bool shortcut, Point2 position)`；`DesktopItemCategory` 枚举对应图片、文档、文件夹、快捷方式、其他。
- `DesktopSnapshot` 包含 `Items`、`WorkArea`、`GridOrigin`、`Spacing`、`AutoArrange` 和视图身份 `ViewId`，坐标一律 Win32 屏幕像素。
- `DesktopOrganization.Classify(DesktopItem item): DesktopItemCategory`。
- `DesktopOrganization.Plan(DesktopSnapshot snapshot): OrganizationPlan`，包含只读 `Moves` 与 `BlockedReason`；每个 `DesktopMove` 包含 `Id`、`From`、`To` 和 `Temporary`。

- [x] 1. 写失败检查：目录快捷方式归快捷方式；`.PNG` 归图片；同名不同 id 保留；枚举顺序反转得到相同目标；空集合为 0 个移动；已到位误差不超过 2 像素时为 0 个移动。
- [x] 2. 补检查：100×100 间距、300×300 可用网格容量为 9；10 项时整批阻止；有一空格的交换先临时腾位且每步目标为空；无空格循环阻止；副屏项目不移动；无效间距阻止。
- [x] 3. 执行 Test-Core，确认新检查因缺少类型或规划结果错误而失败，记录原因。
- [x] 4. 实现分类扩展名表、稳定排序与网格布局；从实测原点和完整单元计算容量，空间允许才留组间空格；以 id 跟踪占位，先处理空目标，闭环使用空闲临时格。
- [x] 5. 执行 Test-Core，预期所有旧检查与新增 OrganizationChecks 通过；NUnit 包装调用相同检查，不重复一套实现。

## Task 3：可取消的串行 STA 执行器

**Files:** 新增 DesktopIconWorker、IconWorkerChecks、IconWorkerTests；连接任务 1 的适配器与任务 2 的托管类型。

**Interfaces:**
- `IDesktopIconSession : IDisposable`：`Read(): DesktopSnapshot`、`Move(string id, Point2 screenPosition): Point2`、`RestoreAlignment(): void`；DesktopIconSession 实现该接口。
- `DesktopIconWorker(Func<IDesktopIconSession> factory, Func<double> idleSeconds)`；`RequestRead(int generation): void`；`RequestMove(int generation, long sequence, string id, Point2 position): void`；`Invalidate(int generation): void`；`TryTake(out IconWorkerResult result): bool`；`Dispose(): void`。
- `IconWorkerResult` 包含 Generation、Sequence、快照或读回位置、异常及取消状态；不向主线程传递 COM 或 PIDL。

- [x] 1. 伪造会话用同步闸门验证：100 个排队位置仅保留最新待执行位置；输入空闲 29.99 秒拒绝写，30 秒允许；失效代次拒绝写；结果带原代次，调用方可丢弃迟到响应。
- [x] 2. 补检查：请求执行中发生取消不得被标为新会话完成；恢复前读回真实位置；异常只生成一次故障结果并恢复对齐；Dispose 后无新请求，COM 释放发生在创建线程。
- [x] 3. 新增 `tools/Test-Organization.ps1` 的模拟检查分支，编译 Core、Windows 与纯检查类型；先执行失败，再实现单 STA 工作循环和有界合并槽，最后重跑至通过。
- [x] 4. Move 前核对输入、代次、项目存在与预期源位置；Shell 失败释放当前会话，低频新读取才重连，不在每帧重试。正常恢复还要检查视图身份和用户设置变化。
- [x] 5. 保持已有 ReadPositions 只读兼容；Runtime 将统一使用 worker 快照，后续删除仅因本次替换而多余的旧扫描线程。

**Verify:** 模拟检查无桌面写入且全部通过，原有 Test-Windows 只读模式仍通过。

## Task 4：搬运阶段、空闲门槛和暂停连续性

**Files:** 新增 DesktopCarry、CarryChecks、CarryTests；修改 Test-Core 注册。

**Interfaces:**
- `CarryPhase { Idle, Approach, PickUp, Transport, PutDown, Complete }`。
- `DesktopCarry.Begin(DesktopMove move, Point2 petPosition, DesktopRect petBounds, Point2 carryOffset): void`。
- `DesktopCarry.Tick(double dt, double idleSeconds, bool enabled): void`；`Cancel(): void`；`Rebase(Point2 petPosition, Point2 actualIconPosition): void`。
- 只读输出 `Phase`、`Progress`、`PetPosition`、`IconPosition`、`Grip`、`WantsWrite`；等待原生确认时由编排层禁止时间推进，不让已完成逻辑领先于真实位置。

- [x] 1. 写失败检查：29.99 秒不推进，30 秒推进；暂停 100 秒后进度不变；抓取和放下各 0.6 秒；搬运均速基准 90 像素/秒并首尾缓动；最终位置准确命中目标。
- [x] 2. 补检查：阶段切换前后坐标连续，源图标保持不动直到抓取；边缘图标对应宠物安全范围内的站位；斜向移动不超速；dt 为 0 不推进；Cancel 无后续写入。
- [x] 3. 执行 Test-Core 确认失败，实现确定性轨迹及夹持偏移，再执行至全部通过。
- [x] 4. 暂停后的 Rebase 从实测位置重建剩余轨迹，不重放已经完成的抓取段，不使用暂停期间墙钟时间。

## Task 5：自动编排、四足叼持与编辑器演示

**Files:** 新增 PetDesktopOrganizer、DesktopOrganizationPreview、OrganizationPreview；修改 DesktopPetController、PrototypePet、NativeWindow；新增必要的 EditMode 集成检查。

**Interfaces:**
- `PetDesktopOrganizer.Tick(double dt, double idleSeconds, bool enabled, Point2 petPosition): void`；`Interrupt(): void`；`Resize(DesktopRect workArea): void`；`Dispose(): void`。
- 输出 `Active`、`PetPosition`、`LookTarget`、`Grip`、`Phase`、`Progress`、`Status`；依赖任务 2–4 的公开类型，不增加另一套计时或布局。
- `PrototypePet.Animate` 末尾增加可选的 `float organizingGrip = 0, float organizingBend = 0`，保留所有旧调用；叼持不改变 PetState.Carried 的“宠物被提起”语义。
- `OrganizationPreview.Render(): void` 导出静态关键帧和连续帧至 `docs/previews/organization/`，仅使用合成图标。

- [x] 1. 编排检查覆盖：无变化快照不重复计划；仅变化目标产生任务；30 秒自动启动；动作中输入暂停；外部位置/身份/工作区变化废弃计划；真实放下确认后才推进下一个项目。
- [x] 2. Controller 将整理优先级置于生活自主行为之前、用户互动之后；整理期间暂停关系之外的随机动作计时，保持既有存档与关系追踪。替换旧图标扫描，由 worker 快照同时供只读观察使用。
- [x] 3. BeginPress、抚摸、暂停/隐藏、RecalculateArea、退出连接相应中断或冻结路径。统一原生左上坐标到 Unity 左下和地面投影，避免 DPI 比例重复换算。
- [x] 4. PrototypePet 增加低头、前爪试探与嘴部叼持过渡，四爪搬运步态保持；任务目标替代搬运期间鼠标注视，转身沿用现有平滑速度。
- [x] 5. 编辑器合成桌面自动空闲触发并显示图标分类、当前阶段，输入会重置空闲计时。原生菜单只读显示当前状态及自动排列阻止原因，不新增手动启动要求。
- [x] 6. 执行原有和新增 EditMode 检查，导出并逐张查看走近、抓取、四方向搬运、放下、靠边接触与暂停恢复关键帧，修复图标跳动、脚滑或姿态断层。

## Task 6：构建、真实桌面验收与交付说明

**Files:** 修改 README.md、docs/acceptance.md、docs/verification.md，必要时更新独立测试脚本；只修本功能验证发现的问题。

- [x] 1. 运行 `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-Core.ps1`、Windows 只读探测和组织模拟检查；预期全部通过，无用户桌面写入。
- [x] 2. 使用 `C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Unity.exe` 执行 EditMode 测试，项目路径为 `D:/shinja/proj/self/elec-pet`，结果 XML 与日志写入 `Temp/OrganizationValidation/`。若已有编辑器占用，不启动第二个同项目实例覆盖状态，记录并使用现有可用测试路径。
- [x] 3. 执行 `ElecPet.Editor.PrototypeSetup.BuildWindows`，日志记录构建结果，预期生成 `Builds/Windows/ElecPet.exe`；构建不能代替桌面运行验收。
- [ ] 4. 真实桌面写入仅针对明确指定的测试图标/测试桌面，事先记录位置与系统设置，验证自动开始、真实平滑轨迹、输入暂停和恢复、无改路径、自动排列阻止、网格标志恢复、满屏、DPI/任务栏变化、副屏不动与 Explorer 重启恢复。没有隔离环境或指定项目时，标明未执行，不自行整理当前用户桌面作为测试。
- [x] 5. 更新使用说明为自动整理规则，并记录每条执行命令、实际结果、预览路径、未执行验收和限制；不把模拟或编辑器结果写成真实 Windows 验收。
- [x] 6. 对照规格逐条检查覆盖，审阅全部差异与新增文件；清理本次生成的无用代码和临时资产，保留必要预览；提供可点击构建/文档链接及已验证和未验证范围。

## 执行方式

建议本会话内由主代理顺序实现。Shell 会话、状态机与表现接口紧密相依，顺序推进便于在真实定位能力验证后调整细节，避免多个实现者同时改 Controller。收到用户对本计划及执行方式的确认后进入 executing-plans；如用户选择子代理方式，改用 subagent-driven-development。

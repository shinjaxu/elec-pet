# ElecPet · Windows 3D 桌面宠物

依据原设计聊天的总结方案实现，当前为**桌面技术原型与首版长期陪伴**。正式美术、随机领养、外观成长、旅行和 SQLite 尚未实现，本地进度使用带备份的 JSON 存档。

## 本轮内容

- 鼠标捡球互动：自由活动时鼠标靠近约 175 像素，小龙伏低蓄势、兴奋摆尾并转头追视。快速甩动至少约 90 像素后停稳，终点出现橙色小球；小龙飞奔拾起，叼球持续追随鼠标，接近约 75 像素后固定落点，展翼螺旋飞行两圈再落地放球，小球随后缩小消失。暂停、隐藏、提起、抚摸和喂食会取消互动；整理及其他明确互动期间不抢占。屏幕边缘使用可达落点，不连通屏幕导致停滞时自动退出。
- 精修程序化无牙仔风格模型：连续的宽扁头吻与肩胸腰胯、嵌入式绿色竖瞳、独立上下眼睑、扁平耳鳍、弧面蝠翼与独立红黑尾鳍。翼骨与嘴线贴合实际曲面，皮肤带克制的细鳞和柔和高光；保留水平四足支撑、注视、错峰爬行与触碰反馈。当前为程序化风格模型，尚未制作电影级资产与正式骨骼动画。
- 身体根据实际移动方向转身，可展示正面、侧面与背面；停步后保留朝向，头部在自然范围内独立注视鼠标。步频按移动距离推进，支撑脚后移抵消身体前进；两段肢体通过膝肘弯曲完成跨步与卧躺，转弯时尾巴延迟跟随。相机与移动统一使用地面投影，避免上下移动与前后步态错位。
- 鼠标靠近时追逐，普通跟随到约 90 像素时停稳，鼠标离开跟随范围后恢复归边；轻点触碰，缓慢移动鼠标抚摸，按住并移动可提起，松手后平滑归边。
- 沿主屏幕工作区四边巡游，避开任务栏。
- 宠物可自主整理桌面图标：键盘空闲满 30 秒后，按名称升序将前 12 个图标排成桌面中央的空心爱心，多余图标从左上角向下逐列排齐，避开爱心留白区。6–11 个图标使用对称的稀疏轮廓；少于 6 个或工作区放不下爱心时从左上角规整排列。宠物逐个走近、低头叼起、平滑搬运并放下。鼠标移动、点击和滚轮不重置计时，也不会因普通鼠标操作暂停；键盘输入时暂停，再次键盘空闲后核对真实位置继续。只调整图标位置，文件路径保持原样；提起或抚摸宠物仍会取消当前任务并在之后重新规划。
- 整理完毕不重复搬动已到位图标，新增或移动项目后只处理目标变化的项目。沿用 Windows 图标间距，避开任务栏和副屏；需要互换位置时先搬到空闲槽位，空间不足则保留原布局。开启 Windows“自动排列图标”时等待关闭；搬运时临时释放网格吸附，暂停和结束后恢复。右键菜单只显示状态，无需手动启动。
- 新增部位互动：缓慢划过头部或身体会眯眼迎合，触碰翅膀或尾巴会回头护住；短时间连续三次轻点会躲闪，随后自行恢复。
- 提起时四爪悬空轻晃，放下后先站稳、整理姿势；每隔约 10–22 秒选择一次歪头伸爪、伸懒腰、整理翅膀、伏卧睡觉、卧躺休息或抬头警戒，同一片段不连续重复，用户互动优先。卧躺时头身下沉、四爪贴地、耳翼收拢；警戒时抬头、竖耳和展开翅膀。
- 自由活动时，鼠标距离约 95–175 像素且精力足够，会触发约 2.4 秒的玩耍式扑食：伏低蓄势、腾身扑出、落地恢复；目标在起扑时锁定，最多前进 65 像素，受原有桌面安全边距约束。冷却为 18 秒活动时间，不点击或操作鼠标附近的桌面内容；抚摸和提起可打断动作。
- 文件喂食：从资源管理器拖住一个或多个普通文件，停在宠物身上满 3 秒后，宠物张嘴等待并提示“松手投喂”；松手才开始进食。图片、视频和其他文件分别显示不同的小卡片，配合真实下颌开合、咀嚼、吞咽、眯眼和翅尾反馈。文件只有在系统确认移入回收站后才显示吃完；容量不足等失败保留或恢复原文件。提前松手、拖离宠物、暂停、隐藏或提起会取消未提交操作。喂食期间暂缓自主整理与追逐，批量文件逐个处理。
- 精力、心情和熟悉度影响随机动作的概率；温柔互动逐渐形成摸头偏好，熟悉后会凑头、低频挥爪招呼。心情自行恢复，没有强制喂养或每日签到要求。
- 熟悉度形成初识、熟悉、信任三个阶段；熟悉后增加凑头反馈，信任后鼠标在附近稳定停留四秒会低频主动靠近，保持约 100 像素距离趴下休息。移动鼠标或点击可取消亲近，主动亲近至少间隔 90 秒活动时间。
- 关系、摸头偏好通过本地 JSON 保存：变化后每 30 秒自动保存，退出时立即保存。首次读取可迁移旧 PlayerPrefs，主文件损坏时尝试备份并保留损坏原件。精力与心情在启动时恢复，离线不扣关系；暂停和隐藏冻结生活行为，但仍允许已有进度落盘。
- Windows 构建包含透明置顶窗口、按模型碰撞体切换鼠标穿透、禁止点击激活、右键菜单和全局快捷键。
- 屏幕陪伴：前台窗口及标题连续停留 30 秒后，截取其在主屏上的可见区域，交给本机 Ollama 的 `qwen3.5:4b`，宠物用一两句中文轻松吐槽。每次连续停留最多观察一次，全局冷却 3 分钟；切换窗口或标题、暂停、隐藏、喂食、提起和锁屏会取消当前观察及旧气泡。截图只在内存处理，不保存，不上传云端，宠物本身排除在截图外。右键“关闭屏幕陪伴 / 开启屏幕陪伴”可独立控制并保存偏好，默认开启。
- 左键点击小龙，松开后立即看当前窗口并回复，保留原有触碰动作并使用奶油白气泡。手动点击跳过自动等待和冷却，至少间隔 2 秒；已有截图或聊天任务时不叠加，模型能力检查期间可接住一次点击。拖拽松手、喂食、暂停、隐藏和陪伴关闭时不触发。

原型角色与边界之间保留安全距离，以避免模型被裁切；正式模型的脚底/爪部精确接触、侧边攀爬和顶部休息动画尚未制作。图标观察以读取的位置为近似目标，未对图标轮廓做精确贴合。当前仅支持主屏幕。

## 用户免安装版

本次鼠标捡球版为 `Builds/Portable/ElecPet-Portable-MouseFetch.exe`，同时保留点击看图、奶油白气泡与表情显示。退出旧版后再打开；本次单独保存新版本，保留旧包。

直接运行 `Builds/Portable/ElecPet-Portable.exe`，只需分发这一个文件。适用于 Windows 10/11 64 位，无需安装 Unity、.NET、VC++、Python 或 7-Zip，也不需要管理员权限或联网下载组件。

启动时自动将内置运行库和资源释放到当前用户临时目录，正常退出后清理。进度仍保存在下文的用户存档目录，退出或替换 EXE 不会删除。退出方式：右键宠物选择“退出”，或按 **Ctrl+Alt+Q**。

开发者更新程序后，先在 Unity 中执行 **桌宠 → 构建 Windows x64**，再生成单文件：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Build-Portable.ps1
```

打包使用随项目提供的 [7-Zip 官方 LZMA SDK](https://www.7-zip.org/sdk.html) 原生模块，来源与校验值见 `tools/packaging/README.txt`。原始 `Builds/Windows/ElecPet.exe` 仍需同目录下的 DLL 和数据文件；分发单文件时请选择 `Builds/Portable/ElecPet-Portable.exe`。

## 开发环境打开与运行

1. 安装 Unity Hub，以及 **Unity 6000.6.6f1** 和 Windows Build Support。工程使用内置渲染管线，已按本机安装版本调整。[官方版本页](https://unity.com/releases/editor/whats-new/6000.6.6f1)
2. 在 Hub 中将本目录作为项目添加并打开，等待依赖导入。
3. 选择菜单 **桌宠 → 生成原型场景**，再点击 Play，查看无牙仔原型与鼠标互动。
4. 选择 **桌宠 → 构建 Windows x64**。
5. 运行 `Builds/Windows/ElecPet.exe`，检验真实桌面行为。

编辑器预览使用普通 Game 视图，不修改 Unity Editor 的窗口样式，也不读取真实桌面图标。内置五个合成图标，键盘空闲30秒后自动演示同一套整理流程，键盘输入会暂停，鼠标活动不重置计时。右键菜单和全局快捷键仅在 Windows 独立程序启用。

搬运效果见 [八秒叼持动画](docs/previews/organization/carry.gif)。Unity 菜单 **桌宠 → 导出整理搬运动画** 导出240帧，再运行 `tools/Export-OrganizationPreview.py` 合成GIF。该预览验证模型动作，不代替真实 Shell 图标平滑定位验收。

爱心布局见 [完整轮廓、左上溢出与稀疏轮廓示意](docs/previews/organization/heart-layout.png)。执行 `tools/Export-HeartLayoutPreview.ps1` 可从实际规划结果重新生成，示意图使用合成文件，不操作真实桌面。

动态转身与步态预览见 [八秒行走动画](docs/previews/locomotion.gif)。可通过 **桌宠 → 导出转身步态动态预览** 重新导出 240 帧，再使用安装了 Pillow 的 Python 执行 `tools/Export-MotionPreview.py` 合成为 GIF。

模型精修见 [侧前方](docs/previews/quality/angle.png)、[面部](docs/previews/quality/face-open.png) 与 [真实桌面相机原尺寸](docs/previews/quality/runtime-light-315.png)。**桌宠 → 导出模型完整验收** 可一次导出细节大图、八朝向浅深底原尺寸图、互动姿势、转身与叼持动画帧；原尺寸图使用运行时的 45 度俯视、每世界单位 100 像素及关闭 MSAA。`tools/Generate-DragonTextures.py` 可用 Pillow 重新生成无缝细鳞和虹膜纹理。

文件喂食见 [进食动画](docs/previews/feeding/feeding.gif) 和 [回收失败反馈](docs/previews/feeding/failed.gif)。使用 **桌宠 → 导出文件喂食预览**，再执行 `tools/Export-FeedingPreview.py` 可重新生成；离线预览不操作真实文件。首版支持可创建保全硬链接的本机固定磁盘普通文件，拒绝文件夹、只读文件、网络路径和符号链接；不读取图片或视频内容，不提供永久删除选项。

| 操作 | 方式 |
| --- | --- |
| 暂停 / 继续 | 右键宠物菜单或 Ctrl+Alt+P |
| 隐藏 / 恢复 | 右键宠物菜单或 Ctrl+Alt+H |
| 退出 | 右键宠物菜单或 Ctrl+Alt+Q |
| 查看关系阶段 | 右键宠物菜单；编辑器预览面板 |
| 鼠标捡球 | 靠近小龙，快速甩动鼠标后停稳，无需点击 |
| 文件喂食 | 拖文件到宠物身上停留 3 秒，看到提示后松手 |
| 屏幕陪伴 | 点击小龙立即看图回复，或前台窗口连续停留 30 秒自动触发；右键宠物可独立开关 |

如果快捷键被其他程序占用，初始化会记录错误并退出，避免隐藏后无法恢复。程序默认限制到 30 帧；资源占用需要在真实构建中测量。

屏幕陪伴需要本机 Ollama 服务运行在 `http://127.0.0.1:11434`，且已安装支持视觉的 `qwen3.5:4b`。启动和恢复观察时自动检查能力，模型未启动、缺失或不支持视觉时，右键菜单显示状态并在 3 分钟后重试，不影响宠物其他活动。截图最长边 1280 像素，模型请求最长等待 60 秒，回复气泡最多 80 字符、显示 12 秒；看不清或没有值得评论的内容时可能保持安静。浏览器标签页仅根据窗口标题近似识别，标题相同的页面变化无法区分。仅支持 Windows 10 2004（构建 19041）及以上的截图排除能力；旧系统自动停止屏幕观察，保留宠物其他功能。编辑器不读取真实屏幕。

陪伴文字使用奶油白圆角气泡、鼠尾草绿引号和深色左对齐正文，短句自动收窄，长句换行；轻微上浮淡入，并在原有 12 秒结束前淡出。顶部空间不足时移到宠物下方，按窗口 DPI 放大字体。Windows 运行版补充系统 Segoe UI Emoji 字体回退和彩色渲染，保留模型回复中的表情；部分国旗和连接符序列的组合效果受系统字体支持限制。实际渲染见 [短句预览](docs/previews/speech-bubble/short.png)、[长句预览](docs/previews/speech-bubble/long.png) 与 [Windows 表情预览](docs/previews/speech-bubble/emoji-player.png)；Unity 菜单“桌宠 → 导出陪伴气泡预览”可重新生成十一种合成案例，不读取桌面或调用模型。

进度位于 `%USERPROFILE%/AppData/LocalLow/ElecPet/ElecPet Prototype/pet.json`，上一份有效进度为 `pet.backup.json`。异常强制结束最多可能丢失最近一个自动保存周期的关系变化。发现较新版本存档时本次禁用写入，保留原文件。已移除常驻小球和小窝，捡球互动仅生成临时小球；保留自主休息、亲近与关系成长；文件投喂为自愿互动，没有饥饿、签到或离线惩罚。旧存档中的物件位置会被忽略，不影响已有关系。


## 验证

无需 Unity 即可执行独立逻辑测试：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-Core.ps1
```

屏幕捕获的合成检查（不读取用户屏幕）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-ScreenCapture.ps1
```

加 `-VisibleFixture` 可显式验证实际捕获：短暂前置一个新建的合成色块窗口和宠物替身，确认窗口身份后只截该区域，在内存检查方向、颜色及宠物排除，随后关闭并恢复原焦点，不保存图像。

本地模型的真实调用检查位于 `Assets/Tests/Player/ScreenModelIntegrationTests.cs`，是需要手动选择的测试，只发送合成红蓝图像。可在 Unity Test Runner 的 PlayMode 选择该测试运行，也可使用命令行 `-runTests -testPlatform StandaloneWindows64 -testFilter ElecPet.Tests.ScreenModelIntegrationTests.LocalModelReturnsACompanionReplyAndSuspensionClearsIt` 验证实际 Windows Player；普通测试和正式构建不自动调用模型。

只读探测当前 Windows 桌面：

```powershell
powershell -STA -NoProfile -ExecutionPolicy Bypass -File tools/Test-Windows.ps1
```

多屏窗口和负坐标点击回归：`powershell -STA -NoProfile -ExecutionPolicy Bypass -File tools/Test-MultiMonitor.ps1`。仅调整测试自建的隐藏窗口，核对虚拟桌面、逐屏工作区及左上方副屏点击坐标，并扫描拖拽目标，验证宠物安全包围盒完全位于真实工作区。活动范围避让错位屏幕间的空隙与任务栏，在屏幕外沿保留安全距离，相接屏幕的共享范围允许跨越。

自动整理的线程取消与合成桌面集成检查（不移动真实图标）：

```powershell
powershell -STA -NoProfile -ExecutionPolicy Bypass -File tools/Test-Organization.ps1 -Simulated
```

不传参数执行 `tools/Test-Organization.ps1` 可只读查看图标间距、排列状态与键盘空闲时间。显式 `-Write -ItemId <测试项目身份>` 才启用单个测试图标的往返定位探测，正常完成回到原位置，键盘输入或位置冲突时停止写入；请仅在明确的测试项目上运行。

Unity Mono 运行时检查：`tools/Test-MonoWindows.ps1` 只读验证键盘检测和真实桌面接口；加 `-WriteFixture` 会在桌面创建独占的临时测试图标，仅对它做8像素往返和61次位置读回，然后清理并核对既有图标位置不变。已通过此项真实写入验证，完整桌宠观感仍按实机清单检查。

文件喂食的原生检查：`tools/Test-FileDrop.ps1` 验证 OLE 接口、三秒门槛和取消规则；随后执行 `tools/Invoke-MonoX64.ps1 -Assembly Logs/FileDropValidation/WindowsDropChecks.exe`，在 Windows 构建实际使用的 64 位 Mono 库下复测。`tools/Test-OleDropRoute.ps1 -MonoX64 -ExtendedStyle 524320 -ToggleTransparent` 在独立后台桌面运行两个进程，验证窗口退出穿透后接收真实拖放、连续三秒和松手提交，不切换用户桌面或移动指针。`-Mono` 使用编辑器附带的 32 位程序，不能代替 64 位回归。

`tools/Test-FeedingWindows.ps1 -Mono -RecycleFixture` 只回收测试工具在本项目日志目录创建并校验的独占文件，验证中文路径、HTML 关联目录保留及回收失败恢复；不用于测试任何已有用户文件。编译后可通过 `tools/Invoke-MonoX64.ps1 -Assembly Logs/FeedingWindowsValidation/WindowsFeedingChecks.exe -Arguments @('Logs/FeedingWindowsValidation', '--recycle-fixture')` 复测 64 位回收服务。

也可在 Unity 的 **Window → General → Test Runner → EditMode** 中执行测试。完整验收步骤见 [桌面验收清单](docs/acceptance.md)，本次可执行检查的真实结果见 [验证记录](docs/verification.md)。

## 项目结构

| 目录 | 职责 |
| --- | --- |
| `Assets/Scripts/Core` | 不依赖 Unity 的坐标、运动、互动与随机行为规则 |
| `Assets/Scripts/Windows` | 原生窗口、鼠标、快捷键、Shell 图标位置 |
| `Assets/Scripts/Runtime` | 临时模型、行为编排、交互与表现 |
| `Assets/Editor` | 生成启动场景、设置构建参数、构建 Windows 程序 |
| `Assets/Tests/EditMode` | 可在 Unity 和系统编译器中复用的行为检查 |

构建设置采用 D3D11 并禁用 Flip Model：Unity 明确说明 DWM 透明窗口需要使用 BitBlt 交换链。[Unity 说明](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/PlayerSettings-useFlipModelSwapchain.html)

图标位置读取使用 Shell 的 `IFolderView`，参考微软桌面图标访问方式。[微软示例](https://devblogs.microsoft.com/oldnewthing/20130318-00/?p=4933/)

鼠标捡球合成动画见 [完整互动预览](docs/previews/mouse-fetch/mouse-fetch.gif)。Unity 菜单“桌宠 → 导出鼠标捡球预览”复用真实状态机和运行层导出 225 帧，再执行 `tools/Export-MouseFetchPreview.py` 合成为动画。单独检查手势、追随、固定落点及退出：`powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-MouseFetch.ps1`。

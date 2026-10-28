# SDD ledger — plan: docs/superpowers/plans/2026-10-22-desktop-organizing.md

## 执行记录

- 执行方式：主代理顺序实现，使用 executing-plans、test-driven-development、using-git-worktrees 技能；最终独立评审。
- 基线：Test-Core 139 项通过，Test-Windows 读取 12 个位置且无写入；沙箱阻止临时存档与 Shell 激活，获自动审批后在沙箱外只读/测试执行成功。
- Ruling: 当前目录没有 Git 元数据，按已批准规格原地修改；以本文件和验证日志代替依赖 Git 的任务脚本，不初始化仓库 — 避免改变现有项目管理方式 — 代价：没有提交级回滚，保留进度文档。
- Pre-flight 1→2→3：任务 1 的会话先暴露定位与恢复；任务 2 定义托管快照；任务 3 才接入 Read 与统一接口，无类型前置依赖。
- Pre-flight 2→4→5：所有 Core 和 Shell 坐标使用 Win32 屏幕像素，Runtime 边界统一转换；PetMotion 原安全边距不变。
- Pre-flight 3→5：工作线程只输出托管结果，代次取消与源位置核对优先于写入；Unity 主线程不持有 COM。
- Pre-flight 4→5→6：实际写入读回确认后再推进完成，编辑器演示不读写 Shell；真实 Windows 写入验收独立记录。
- Ruling: 未指定可移动的测试图标，真实写入探测先不执行，继续只读、模拟与编辑器验证 — 计划允许延期此项 — 代价：连续定位和网格恢复需单独真实验收，不可宣称完成该项。

## 任务

- Task 1: in progress
- Task 2: pending
- Task 3: pending
- Task 4: pending
- Task 5: pending
- Task 6: pending
- Task 1: 只读探测 RED（OpenSession 缺失）→GREEN，12 项、间距76×84、自动排列关闭；写入验收未执行。
- Task 2: complete — Test-Core: 20 organization checks，旧139项通过，RED为缺少分类规划入口。
- Task 3: complete — Test-Organization -Simulated: 8 worker checks，验证有界队列、29.99秒阻止、代次取消、源位置冲突、STA释放和失败清理；RED为缺少执行器。
- Ruling: 快照只含数据的类型提前到任务1，与会话Read一并实现，任务2再实现业务规划 — 让只读原生探测立即可编译 — 代价：仅改变文件创建顺序，不改变接口。
- Ruling: 新增Position接口用于执行前核对单个图标，不每帧全桌面扫描 — 兼顾用户位置变更保护与Shell负载 — 代价：需要在模拟适配器实现同一方法。
- Task 4: complete — Test-Core: 409 carry checks，通过29.99/30秒、阶段衔接、90像素速度上限、暂停、实测位置恢复和边缘站位；RED为缺少DesktopCarry。
- Task 5: in progress — 新增真实worker+合成桌面的7项编排检查全部通过；正在接入Controller与叼持表现。
- Unity首轮EditMode: 32/32通过，结果Temp/OrganizationValidation/editmode.xml。首个Unity子进程仍运行时误启动第二实例返回1；随后确认首轮结果，后续统一使用带Wait的隐藏进程并跟踪session，避免并发打开项目。
- Task 5: Controller已接入自动整理优先级、输入中断、暂停隐藏、窗口变更与退出清理；旧图标观察共用同一worker快照。编译和旧32项EditMode通过。
- 网格原点回归：只读坐标显示当前桌面原点22,2，间距76×84；新增多数偏移检测，21项布局检查通过。
- Task 5: complete — 32/32 Unity EditMode通过（editmode-final.xml）；合成搬运动画240帧导出成功，查看105帧发现固定偏移导致图标位于背部，先新增嘴部偏移回归RED，再改为真实模型嘴部屏幕投影；410项搬运检查通过，重导出确认嘴部接触。
- Task 6: in progress — Windows构建中，真实桌面写入未执行。
- Ruling: 新增SetGripOffset连接实际模型嘴部投影，并从Shell读取图标实际尺寸 — 固定偏移不能满足全向叼持接触 — 代价：表现与搬运之间多一个轻量参数，不改变文件操作范围。
- Ruling: Preview使用与原生相同的STA执行器和合成会话，窗口变更重建整理组件 — 避免两套自动规则产生差异 — 代价：尺寸变化取消旧计划并重新等待快照。
- 独立只读评审由organization_review完成，发现3项Important，无Critical/Minor。已统一进入一次修复回归：
  1. 稀疏桌面暂停恢复原点漂移：新增LiveDesktop回归，RED明确失败；同ViewId/工作区/间距/图标尺寸时保留既有网格，不随搬运重新标定。
  2. 写入探测finally仍写回：新增PowerShell行为检查，RED busy场景发现writes=1；清理仅恢复设置，正常正弦往返末帧回原位，输入与源位置变化立即停止。
  3. 后台新图标占目标：新增实worker集成回归，RED明确出现叠放；抓取与放下阶段前各刷新一次快照，变化时重规划，不每帧全量扫描。
- Final: Ruling: 评审未判断真实COM平滑与网格恢复、混合DPI/多屏/Explorer真实行为 — 无指定测试桌面，保留实机验收清单，不用模拟结果替代 — 代价：真实系统效果尚未确认。
- Final: Ruling: 评审未运行Unity检查接触观感 — 主代理已导出并查看真实Unity嘴部接触帧，编辑器自动检查已通过，长时间暂停后的真实桌面体验仍需实机验收 — 代价：视觉结论限于合成演示。
- Final: fixed 原点漂移 — LiveDesktop暂停恢复 RED→GREEN，原目标保持；副屏、几何变化仍重新规划。
- Final: fixed 清理回位覆盖用户 — PowerShell busy场景RED写入1次→GREEN写入0次，输入中断和位置冲突场景通过。
- Final: fixed 新占位叠放 — 后台新增目标占位RED→GREEN，抓取与放下前各一次快照核对。
- 补充在途调用取消与新代次读回检查，worker现10项通过；四向叼持接触图全部导出并逐张查看，左右前后均贴近嘴部。
- 完成批次汇总日志：只在所有位置读回确认后生成批次计数，Controller以参数化日志记录搬运次数，不输出桌面文件名/路径；新增检查先RED（缺少批次输出）再实现。
- Task 6: complete（实现、模拟验证、Unity测试与Windows构建）；真实桌面写入验收未执行，计划Task1第4步/Task6第4步保持未勾选，不宣称实机通过。
- Final checks: Test-Core 570项通过；worker10、probe3、organizer14项通过；Unity最终32/32通过；Windows最终构建成功退出0；GIF240帧/8000ms及四向图已检查。
- Deferred minors: 无。没有Git仓库，不执行分支集成菜单或提交；保留全部实现、构建与验证记录。

## 实机故障与键盘空闲修订

- 用户反馈：实机没有搬运动作；触发条件改为键盘空闲30秒，鼠标不计入。
- 根因：Player.log 明确显示 RuntimeType.InvokeMember 在 Unity Mono 抛出 NotImplementedException，Shell会话创建失败。新增 Test-MonoWindows 使用Unity自带mono.exe，成功复现同一异常（RED）。
- 修复：以IShellWindows原生虚表替换COM反射调用；Unity Mono只读实机读取12项通过（GREEN）。
- 键盘空闲：新增只记录活动时间的键盘事件监听，独立消息线程保证后台生效，不读取字符或键码；鼠标完全不参与计时，抚摸/提起仍保留直接互动优先级。5项纯逻辑检查先RED后GREEN，原570项通过。
- 真实写入：Test-MonoWindows -WriteFixture 创建一个独占临时桌面文件，仅移动该测试图标8像素往返，61次读回通过，回到原位后清理；既有12项位置核对不变。未批量移动用户桌面图标。
- 待完成：Unity完整测试、构建和本轮独立审查。
- 本轮独立审查1项Important：普通鼠标靠近会Greeting抢占搬运。控制器层回归明确RED: Expected Wander, But was Greeting；加整理活跃保护后GREEN，保留明确抚摸/提起。
- 最终验证：Core575项、worker10/probe3/organizer14通过；Unity34/34通过；Windows构建成功；Mono实机自建图标61次连续定位通过、原12项不变；系统.NET只读探测保持通过。
- 审查限度：没有注入真实按键进行全系统输入测试，长时钩子静默移除与DPI/多屏观感未实测；不把安装成功表述为完整按键实测。
- 验证日志移入Logs/KeyboardOrganizationValidation，避免Unity后续启动清理Temp导致证据丢失。
- 已恢复并保留修复后的源代码及新构建；为更新正常关闭旧版后恢复运行新版。

# 陪伴气泡视觉优化计划

**目标：** 按用户已选择的奶油白风格改善陪伴弹窗外观，保留业务触发链路。

**设计：** `docs/superpowers/specs/2026-10-23-speech-bubble-design.md`

1. 新增独立 PetSpeechBubble 绘制与布局，接入 Controller；以几何边界检查和实际渲染确认位置、排版、尾巴与阴影。
2. 新增只绘制合成内容的 Editor 预览导出，检查短长句、四角与 100/125/150% 缩放；不调用模型和桌面行为。
3. 回归现有 Unity 测试，构建 Windows 并更新免安装包。

本次为可逆的视觉调整，不为颜色与像素写镜像测试；重点使用实际 IMGUI 渲染验收。用户已选择视觉方向，在当前会话直接执行。

## 完成记录

- 九张实际 IMGUI 预览及布局断言全部通过，已查看短长句、顶部回退及150%缩放。
- 既有 Unity EditMode 65/65 通过，正式 Windows 构建成功。
- 当前旧 EXE 被运行进程占用，新免安装包另存为 `Builds/Portable/ElecPet-Portable-Cream.exe` 并通过完整性校验，未中断用户进程。

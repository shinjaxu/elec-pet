打包工具来源

7zr.exe（x64）和 7zSD.sfx 来自 7-Zip 官方 LZMA SDK 26.05。
官方页面：https://www.7-zip.org/sdk.html
下载地址：https://github.com/ip7z/7zip/releases/download/26.05/lzma2605.7z
原始压缩包 SHA-256：668FDA469B7F584F246008CF28359C66EF98D65FAFB3B22A52672EEBE6970C28

LZMA SDK 为公有领域软件，原始说明保留在 lzma.txt。
这些文件仅供开发者打包；最终用户只需 ElecPet-Portable.exe。
7zSD.sfx 为原生 Windows 自解压启动模块，不依赖系统 .NET、PowerShell 或外部 7-Zip。
打包时为副本嵌入 app.manifest，显式使用 asInvoker 普通用户权限，保留原始模块。

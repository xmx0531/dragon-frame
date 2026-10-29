# 小龙娘相框

Windows 11 上的轻量悬浮相框，基于 [OverlayPlayer](https://github.com/buraaks/OverlayPlayer) 改造，使用 C#、WPF 和 .NET 8。

支持 JPG、JPEG、PNG、动态 GIF 和 MP4；可选只播图片、只播视频或混播。图片按比例完整显示，支持随机轮播、自定义切换间隔、透明置顶、窗口拖动与缩放、图片靠齐、托盘操作及锁定后鼠标穿透。视频播完后切换，声音默认关闭。

## 构建

在 Windows 上安装 .NET 8 SDK，然后运行 `构建.ps1`。脚本会优先使用项目内的 `开发工具/dotnet/dotnet.exe`（如有），否则使用系统 `dotnet`。输出位于 `构建输出/程序/小龙娘相框.exe`，需要保留输出目录中的其他文件一起运行。也可直接执行：

```powershell
dotnet publish .\源码\DragonFrame\DragonFrame.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o .\构建输出\程序
```

命令行直接构建时，生成的入口名为 `DragonFrame.exe`。

使用方法见 [使用说明](使用说明.md)。本仓库只包含相框源码、构建脚本和许可证；个人图库、视频、运行设置、本机工具、构建产物和独立视频下载器均未收录。

## 已知限制

WPF 视频播放的内存占用明显高于图片/GIF。连续切换大量短视频时，专用内存可能继续上升；长期视频混播稳定性仍需更多实测。相框优先用于图片和 GIF，视频功能按需启用。GIF 会保留动画播放，不以首帧替代。

## 来源与许可

- 原项目 [buraaks/OverlayPlayer](https://github.com/buraaks/OverlayPlayer)，基于提交 `ae28a9d7de1351f8dc53669246ff93f6c7ee6338` 改造，MIT 许可证见 [LICENSE](LICENSE)。
- GIF 动画实现使用 [XamlAnimatedGif](https://github.com/XamlAnimatedGif/XamlAnimatedGif)，Apache-2.0 许可证见 [licenses/XamlAnimatedGif-Apache-2.0.txt](licenses/XamlAnimatedGif-Apache-2.0.txt)。


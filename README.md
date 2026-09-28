# 北海栈

北海栈是一款 Windows 本地创作资料管理工具，包含写作资料、剧本、AIGC 资产、导言生成器，以及内嵌的番茄小说下载器。

## 下载

请前往 [Releases](https://github.com/lm131420m-eng/beihai-zhan/releases/latest) 下载 `beihaiZ-v*-Windows-x64.zip`，完整解压后运行 `北海栈.exe`。

## 更新

北海栈启动后会在后台检查 GitHub Releases。也可以打开“设置 → 关于 → 检查北海栈更新”手动检查。发现新版本后，软件会询问是否下载更新包。

发布新版本时：

1. 修改 `Beihai.cs` 中的 `AppVersion`。
2. 重新构建并打包为 `beihaiZ-v版本号-Windows-x64.zip`。
3. 在 GitHub 创建相同版本号的 Release，并上传压缩包。

## 系统要求

- Windows 10/11 x64
- Microsoft Edge WebView2 Runtime

## 第三方组件

番茄小说下载功能来自 [POf-L/Fanqie-novel-Downloader](https://github.com/POf-L/Fanqie-novel-Downloader)。完整发行包同时保留对应来源与说明文件。



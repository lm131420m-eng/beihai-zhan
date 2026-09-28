$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot
$workspace = Split-Path $project -Parent
$release = Join-Path $workspace 'output\beihaiZ'
$sdk = Join-Path $project 'vendor\webview2'
New-Item -ItemType Directory -Path $release -Force | Out-Null
$fanqieDir = Join-Path $release '第三方工具\番茄小说下载器'
New-Item -ItemType Directory -Path $fanqieDir -Force | Out-Null
Copy-Item -LiteralPath "$project\fanqie-download\FanqieNovelDownloader.exe","$project\fanqie-download\SHA256SUMS-unsigned.txt","$project\fanqie-download\来源与说明.txt" -Destination $fanqieDir -Force
if ((Get-FileHash -LiteralPath "$fanqieDir\FanqieNovelDownloader.exe" -Algorithm SHA256).Hash.ToLowerInvariant() -ne '0783255134e177d586e4b60803946180ceb4320f228d4e5dffde54c9063fddff') { throw 'Fanqie downloader SHA-256 verification failed' }
Copy-Item -LiteralPath "$sdk\lib\net462\Microsoft.Web.WebView2.Core.dll","$sdk\lib\net462\Microsoft.Web.WebView2.WinForms.dll","$sdk\runtimes\win-x64\native\WebView2Loader.dll" -Destination $release -Force
Copy-Item -LiteralPath "$sdk\LICENSE.txt" -Destination "$release\WebView2-LICENSE.txt" -Force
& "$project\make-brand-assets.ps1" -Source "$project\assets\logo-source.png" -PngOutput "$release\北海栈-logo.png" -IcoOutput "$release\北海.ico"
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$release\北海栈.exe" "/win32icon:$release\北海.ico" "/resource:$project\fanqie-adapter.js,fanqie-adapter.js" /reference:System.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll /reference:System.Xml.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/reference:$release\Microsoft.Web.WebView2.Core.dll" "/reference:$release\Microsoft.Web.WebView2.WinForms.dll" "$project\Beihai.cs" "$project\ImportStore.cs" "$project\DarkMenuColors.cs" "$project\FanqieBridge.cs"
if ($LASTEXITCODE -ne 0) { throw 'C# compilation failed' }
Copy-Item -LiteralPath "$project\北海栈.exe.config","$project\使用说明.txt" -Destination $release -Force
Write-Output "Built: $release\北海栈.exe"

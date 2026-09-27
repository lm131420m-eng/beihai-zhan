using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Net;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

internal static class Program
{
    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [STAThread]
    private static void Main(string[] args)
    {
        // Match Tauri's PerMonitorV2 context before creating any HWND. Cross-process
        // SetParent otherwise resets its DPI context and offsets WebView2 hit testing.
        try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch(EntryPointNotFoundException) { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        bool test = args.Length > 0 && args[0] == "--self-test";
        bool embedTest = args.Length > 0 && args[0] == "--embed-test";
        bool created;
        using (var mutex = new Mutex(true, test||embedTest ? "Beihai.Library.Test" : "Beihai.Library.Desktop", out created))
        {
            if (!created) { MessageBox.Show("北海栈已在运行，请从任务栏打开。", "北海栈"); return; }
            try { Application.Run(new LibraryForm(test,embedTest)); }
            catch (Exception ex) { MessageBox.Show("程序启动失败：\n" + ex.Message, "北海栈"); Environment.ExitCode = 1; }
        }
    }
}

internal sealed class LibraryForm : Form
{
    private readonly string root = AppDomain.CurrentDomain.BaseDirectory;
    private readonly string profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BeihaiLibrary", "WebView2");
    private readonly WebView2 web = new WebView2();
    private readonly Label loading = new Label();
    private readonly bool test;
    private readonly bool embedTest;
    private bool testing;
    private string errors = "";
    private ImportStore imports;
    private string catalogScript;
    private string pendingPlatform;
    private string pendingCollection="writing";
    private bool importing;
    private bool dark;
    private string themeScript;
    private ToolStripMenuItem themeMenu;
    private readonly Panel fanqieHost = new Panel();
    private Process fanqieProcess;
    private IntPtr fanqieWindow=IntPtr.Zero;
    private Task fanqieEmbedTask;
    private FanqieBridge fanqieBridge;
    private readonly System.Windows.Forms.Timer fanqieLayoutTimer=new System.Windows.Forms.Timer { Interval=1000 };
    private bool embedTestStarted;
    private bool fanqieRequested;
    private bool closing;
    private bool appUpdateCheckStarted;
    private uint suppressedFanqieProcessId;
    private IntPtr fanqieShowHook=IntPtr.Zero;
    private WinEventDelegate fanqieShowCallback;
    private double headerFraction;
    private readonly string themeFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BeihaiLibrary","theme.txt");
    private const string FanqieSha256="9ed29ace9e977f5b306973119b369844d4be4d3d3f931230327aae3818270891";
    private const string AppVersion="1.0.0";
    private const string AppReleaseApi="https://api.github.com/repos/lm131420m-eng/beihai-zhan/releases/latest";
    private const string AppReleasePage="https://github.com/lm131420m-eng/beihai-zhan/releases/latest";
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
    [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr SetParent(IntPtr child,IntPtr parent);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW",SetLastError=true)] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd,int command);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
    private delegate void WinEventDelegate(IntPtr hook,uint eventType,IntPtr hwnd,int idObject,int idChild,uint eventThread,uint eventTime);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint eventMin,uint eventMax,IntPtr module,WinEventDelegate callback,uint processId,uint threadId,uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
    private delegate bool EnumWindowsDelegate(IntPtr hwnd,IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsDelegate callback,IntPtr parameter);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hwnd);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd,out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd,ref Point point);
    private readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };

    public LibraryForm(bool selfTest,bool embeddingTest)
    {
        test = selfTest;
        embedTest = embeddingTest;
        try { dark=!test && File.Exists(themeFile) && File.ReadAllText(themeFile).Trim()=="dark"; } catch { dark=false; }
        Text = "北海栈";
        ClientSize = new Size(1380, 900);
        MinimumSize = new Size(900, 620);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 10);
        BackColor = Color.FromArgb(247,248,252);
        string iconPath = Path.Combine(root, "北海.ico");
        if (File.Exists(iconPath)) Icon = new Icon(iconPath);
        var menu = new MenuStrip();
        menu.BackColor = Color.White;
        var importMenu = new ToolStripMenuItem("导入文件");
        importMenu.Click += async delegate { await ImportFiles(); };
        themeMenu = new ToolStripMenuItem("深色模式") { Checked=dark };
        themeMenu.Click += async delegate { await ChangeTheme(!dark,true); };
        menu.Items.AddRange(new ToolStripItem[] {importMenu, themeMenu});
        var settings=new ToolStripMenuItem("设置") { Alignment=ToolStripItemAlignment.Right,DropDownDirection=ToolStripDropDownDirection.BelowLeft };
        var zoomMenu=new ToolStripMenuItem("界面缩放 · 100%");
        var zoom=new TrackBar { Minimum=6,Maximum=20,Value=10,SmallChange=1,LargeChange=2,TickFrequency=2,Width=220,Height=45,AutoSize=false,AccessibleName="界面缩放，60% 到 200%" };
        zoom.ValueChanged+=(sender,e)=>{web.ZoomFactor=zoom.Value/10.0;zoomMenu.Text="界面缩放 · "+(zoom.Value*10)+"%";};
        zoomMenu.DropDownItems.Add(new ToolStripControlHost(zoom));
        zoomMenu.DropDownItems.Add("恢复 100%",null,delegate { zoom.Value=10; });
        zoomMenu.DropDownOpened+=(sender,e)=>zoom.Focus();
        settings.DropDownItems.Add(zoomMenu);
        settings.DropDownItems.Add("关于",null,async delegate { await ShowAbout(); });
        menu.Items.Add(settings);
        web.Dock = DockStyle.Fill;
        loading.Text = "正在打开北海栈…";
        loading.Dock = DockStyle.Fill;
        loading.TextAlign = ContentAlignment.MiddleCenter;
        loading.ForeColor = Color.FromArgb(108,84,216);
        fanqieHost.Visible=false;fanqieHost.BackColor=Color.FromArgb(23,23,31);
        fanqieHost.Resize+=(s,e)=>ResizeFanqieWindow();
        web.ZoomFactorChanged+=(s,e)=>LayoutFanqieHost();
        fanqieLayoutTimer.Tick+=(s,e)=>{if(fanqieHost.Visible)ResizeFanqieWindow();};
        fanqieLayoutTimer.Start();
        Controls.Add(web); Controls.Add(loading); Controls.Add(fanqieHost); Controls.Add(menu);
        MainMenuStrip = menu;
        ApplyNativeTheme();
        Shown += async delegate { await Initialize(); };
        Layout+=(s,e)=>LayoutFanqieHost();
        FormClosed += delegate { fanqieLayoutTimer.Dispose();if(fanqieBridge!=null)fanqieBridge.Dispose();web.Dispose(); };
        FormClosing += async (s,e) => {
            if(importing) { e.Cancel=true; MessageBox.Show(this,"正在保存导入内容，请稍候再关闭。","北海栈");return; }
            if(closing)return;
            e.Cancel=true;closing=true;CloseFanqie();
            // Let the child consume WM_CLOSE before destroying its parent HWND.
            for(int i=0;i<60&&fanqieProcess!=null&&!fanqieProcess.HasExited;i++)await Task.Delay(50);
            Close();
        };
        if(!test&&!embedTest) PreloadFanqie();
    }

    private async Task Initialize()
    {
        try
        {
            if (!File.Exists(Path.Combine(root,"北海.html")) || !Directory.Exists(Path.Combine(root,"资料（勿删）")))
                throw new IOException("资料文件不完整。请先完整解压软件包，再打开北海栈.exe。");
            string webProfile=test||embedTest ? Path.Combine(Path.GetTempPath(),"BeihaiWebViewTest-"+Guid.NewGuid().ToString("N")) : profile;
            var environment = await CoreWebView2Environment.CreateAsync(null,webProfile);
            await web.EnsureCoreWebView2Async(environment);
            await ChangeTheme(dark,false);
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("beihai.local", root, CoreWebView2HostResourceAccessKind.DenyCors);
            imports = new ImportStore(test||embedTest ? Path.Combine(Path.GetTempPath(),"BeihaiImportTest-"+Guid.NewGuid().ToString("N")) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BeihaiLibrary","Imports"));
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("imports.beihai.local", imports.Root, CoreWebView2HostResourceAccessKind.Allow);
            if(test) PrepareImportTest();
            await UpdateImportCatalog();
            web.CoreWebView2.WebMessageReceived += async (s,e) => {
                if(Uri.UnescapeDataString(e.Source)!="https://beihai.local/北海.html") return;
                string message=e.TryGetWebMessageAsString();
                if(message.StartsWith("header-size:")) {
                    double fraction;
                    if(double.TryParse(message.Substring(12),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out fraction)&&fraction>0&&fraction<1){headerFraction=fraction;LayoutFanqieHost();}
                }
                else if(message=="import-files") await ImportFiles();
                else if((message=="open-fanqie"||message=="embed-fanqie"||message=="prepare-fanqie")&&!test&&!embedTest) await EnsureFanqieEmbedded();
                else if(message=="hide-fanqie") HideFanqie();
                else if(message=="open-fanqie-folder") OpenLocal(FanqieDirectory());
                else if(message=="open-fanqie-source") Process.Start(new ProcessStartInfo("https://github.com/POf-L/Fanqie-novel-Downloader") { UseShellExecute=true });
            };
            web.CoreWebView2.Settings.IsStatusBarEnabled = false;
            await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("addEventListener('DOMContentLoaded',()=>{const h=document.querySelector('header');if(!h)return;const report=()=>chrome.webview.postMessage('header-size:'+h.getBoundingClientRect().bottom/innerWidth);new ResizeObserver(report).observe(h);addEventListener('resize',report);report();});");
            web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            web.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            web.CoreWebView2.NewWindowRequested += (s,e) => { e.Handled = true; OpenUri(e.Uri); };
            web.CoreWebView2.NavigationStarting += (s,e) => {
                Uri uri;
                if (!Uri.TryCreate(e.Uri,UriKind.Absolute,out uri)) { e.Cancel = true; return; }
                if (uri.Host != "beihai.local" || (uri.AbsolutePath != "/北海.html" && Uri.UnescapeDataString(uri.AbsolutePath) != "/北海.html"))
                { e.Cancel = true; OpenUri(e.Uri); }
                else HideFanqie();
            };
            web.CoreWebView2.DownloadStarting += (s,e) => {
                using (var dialog = new SaveFileDialog())
                {
                    dialog.FileName = Path.GetFileName(e.ResultFilePath);
                    dialog.Title = "保存资料文件";
                    dialog.Filter = "所有文件 (*.*)|*.*";
                    dialog.OverwritePrompt = true;
                    if (dialog.ShowDialog(this) == DialogResult.OK) { e.ResultFilePath = dialog.FileName; e.Handled = true; }
                    else e.Cancel = true;
                }
            };
            if (test)
            {
                web.CoreWebView2.WebMessageReceived += (s,e) => { string value=e.TryGetWebMessageAsString();if(value.StartsWith("__beihai_error__:"))errors += value.Substring(17) + "\n"; };
                await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("addEventListener('error',e=>chrome.webview.postMessage('__beihai_error__:'+e.message));addEventListener('unhandledrejection',e=>chrome.webview.postMessage('__beihai_error__:'+String(e.reason)));");
            }
            web.CoreWebView2.NavigationCompleted += async (s,e) => {
                loading.Visible = !e.IsSuccess;
                if (!e.IsSuccess) { loading.Text = "页面加载失败：" + e.WebErrorStatus; if(test) FinishTest(false, loading.Text); }
                else {
                    if(!test&&!embedTest&&!appUpdateCheckStarted){appUpdateCheckStarted=true;CheckForAppUpdates(true);}
                    if(pendingPlatform!=null) {
                        string platform=pendingPlatform; pendingPlatform=null;
                        await web.ExecuteScriptAsync("window.switchCollection("+json.Serialize(pendingCollection)+");Object.assign(state,{category:'all',platform:window.collectionPlatformFor("+json.Serialize(platform)+"),genre:'全部分类',query:'',page:1});$('query').value='';resetPage();");
                    }
                    if (test && !testing) { testing = true; await SelfTest(); }
                    else if(embedTest&&!embedTestStarted){embedTestStarted=true;await RunEmbedTest();}
                }
            };
            web.CoreWebView2.Navigate("https://beihai.local/北海.html");
        }
        catch (WebView2RuntimeNotFoundException)
        {
            loading.Text = "缺少 Microsoft Edge WebView2 Runtime。安装运行环境后重新打开本软件。";
            if (test) { FinishTest(false,loading.Text); return; }
            if (MessageBox.Show(loading.Text + "\n\n是否打开微软官方下载页面？", "需要安装运行环境", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                Process.Start(new ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/") { UseShellExecute = true });
        }
        catch (Exception ex) { loading.Text = ex.Message; if (test) FinishTest(false,ex.ToString()); else MessageBox.Show(this,ex.Message,"无法启动北海栈",MessageBoxButtons.OK,MessageBoxIcon.Error); }
    }

    private string LocalPath(Uri uri)
    {
        string candidate = Path.GetFullPath(Path.Combine(root,Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/').Replace('/',Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(root,StringComparison.OrdinalIgnoreCase)) throw new IOException("无法打开资料目录以外的路径。");
        return candidate;
    }
    private string FanqieDirectory() { return Path.Combine(root,"第三方工具","番茄小说下载器"); }
    private string FanqieExecutable() { return Path.Combine(FanqieDirectory(),"FanqieNovelDownloader.exe"); }
    private string FileSha256(string file)
    {
        using(var stream=File.OpenRead(file))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    }
    private async Task EnsureFanqieEmbedded()
    {
        fanqieRequested=true;
        try
        {
            if(fanqieEmbedTask!=null&&!fanqieEmbedTask.IsCompleted) await fanqieEmbedTask;
            else if(fanqieProcess==null||fanqieProcess.HasExited||!IsWindow(fanqieWindow)) {
                fanqieEmbedTask=EmbedFanqieCore();await fanqieEmbedTask;
            }
            if(closing||!fanqieRequested)return;
            if(web.CoreWebView2!=null)await web.ExecuteScriptAsync("window.commitFanqieCollection?.()");
            await Task.Delay(80);
            fanqieHost.Visible=true;fanqieHost.BringToFront();
            if(IsIconic(fanqieWindow))ShowWindow(fanqieWindow,9);
            LayoutFanqieHost();
        }
        catch(Exception ex){EndFanqieWindowSuppression();if(closing)return;fanqieHost.Visible=false;if(embedTest)throw;MessageBox.Show(this,"无法内嵌番茄小说下载器：\n"+ex.Message,"北海栈",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
    private async void PreloadFanqie()
    {
        try
        {
            // Start and fully render the downloader while its host panel is hidden.
            // The first click can then reuse the ready child window without exposing
            // the third-party startup surface or its initial white/color paint.
            fanqieEmbedTask=EmbedFanqieCore();
            await fanqieEmbedTask;
        }
        catch
        {
            EndFanqieWindowSuppression();
            CloseFanqie();
            fanqieProcess=null;
            fanqieWindow=IntPtr.Zero;
            fanqieEmbedTask=null;
        }
    }
    private void BeginFanqieWindowSuppression()
    {
        EndFanqieWindowSuppression();
        fanqieShowCallback=(hook,eventType,hwnd,idObject,idChild,eventThread,eventTime)=>{
            if(hwnd==IntPtr.Zero||idObject!=0||suppressedFanqieProcessId==0)return;
            uint processId;GetWindowThreadProcessId(hwnd,out processId);
            if(processId==suppressedFanqieProcessId&&GetParent(hwnd)==IntPtr.Zero)ShowWindow(hwnd,0);
        };
        fanqieShowHook=SetWinEventHook(0x8002,0x8002,IntPtr.Zero,fanqieShowCallback,0,0,0);
    }
    private void EndFanqieWindowSuppression()
    {
        suppressedFanqieProcessId=0;
        if(fanqieShowHook!=IntPtr.Zero){UnhookWinEvent(fanqieShowHook);fanqieShowHook=IntPtr.Zero;}
        fanqieShowCallback=null;
    }
    private IntPtr FindTopLevelProcessWindow(uint processId)
    {
        IntPtr best=IntPtr.Zero;long bestArea=0;
        EnumWindows((hwnd,parameter)=>{
            uint candidate;GetWindowThreadProcessId(hwnd,out candidate);
            if(candidate!=processId||GetParent(hwnd)!=IntPtr.Zero||GetWindowTextLength(hwnd)==0)return true;
            NativeRect rect;if(!GetWindowRect(hwnd,out rect))return true;
            long area=Math.Max(0,rect.Right-rect.Left)*(long)Math.Max(0,rect.Bottom-rect.Top);
            if(area>bestArea){bestArea=area;best=hwnd;}
            return true;
        },IntPtr.Zero);
        return best;
    }
    private async Task EmbedFanqieCore()
    {
        string executable=FanqieExecutable();
        if(!File.Exists(executable))throw new FileNotFoundException("番茄小说下载器文件缺失，请重新解压完整软件包。",executable);
        if(fanqieProcess==null||fanqieProcess.HasExited)
        {
            if(fanqieBridge!=null)fanqieBridge.Dispose();
            fanqieBridge=new FanqieBridge();
            var start=new ProcessStartInfo(executable){UseShellExecute=false,WorkingDirectory=FanqieDirectory(),WindowStyle=ProcessWindowStyle.Hidden,CreateNoWindow=true};
            BeginFanqieWindowSuppression();
            const string browserArguments="WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS";
            string previousArguments=Environment.GetEnvironmentVariable(browserArguments,EnvironmentVariableTarget.Process);
            try
            {
                Environment.SetEnvironmentVariable(browserArguments,"--remote-debugging-address=127.0.0.1 --remote-debugging-port="+fanqieBridge.Port,EnvironmentVariableTarget.Process);
                fanqieProcess=Process.Start(start);
            }
            finally { Environment.SetEnvironmentVariable(browserArguments,previousArguments,EnvironmentVariableTarget.Process); }
            suppressedFanqieProcessId=(uint)fanqieProcess.Id;
            fanqieWindow=IntPtr.Zero;
        }
        for(int i=0;i<200&&fanqieWindow==IntPtr.Zero;i++)
        {
            await Task.Delay(50);fanqieProcess.Refresh();
            if(closing)return;
            if(fanqieProcess.HasExited)throw new IOException("番茄小说下载器启动后意外退出。");
            fanqieWindow=FindTopLevelProcessWindow((uint)fanqieProcess.Id);
        }
        if(fanqieWindow==IntPtr.Zero)throw new IOException("未找到番茄小说下载器窗口。");
        // Keep the third-party top-level window hidden until it is fully embedded.
        // This avoids the small native window flashing on screen during startup.
        ShowWindow(fanqieWindow,0);
        await Task.Delay(80);
        if(closing)return;
        LayoutFanqieHost();
        const int GWL_STYLE=-16;
        const long WS_CHILD=0x40000000L,WS_POPUP=unchecked((long)0x80000000L),WS_CAPTION=0x00C00000L,WS_THICKFRAME=0x00040000L,WS_MINIMIZEBOX=0x00020000L,WS_MAXIMIZEBOX=0x00010000L;
        long style=GetWindowLongPtr(fanqieWindow,GWL_STYLE).ToInt64();
        style=(style&~(WS_POPUP|WS_CAPTION|WS_THICKFRAME|WS_MINIMIZEBOX|WS_MAXIMIZEBOX|0x20000000L|0x01000000L))|WS_CHILD;
        SetWindowLongPtr(fanqieWindow,GWL_STYLE,new IntPtr(style));
        SetParent(fanqieWindow,fanqieHost.Handle);
        if(GetParent(fanqieWindow)!=fanqieHost.Handle)throw new IOException("Windows 拒绝嵌入下载器窗口。");
        EndFanqieWindowSuppression();
        ShowWindow(fanqieWindow,9);ResizeFanqieWindow();
        await fanqieBridge.Connect();
        string adapter;
        using(var reader=new StreamReader(typeof(LibraryForm).Assembly.GetManifestResourceStream("fanqie-adapter.js")))adapter=reader.ReadToEnd();
        await fanqieBridge.Call("Page.addScriptToEvaluateOnNewDocument",new { source=adapter });
        await fanqieBridge.Evaluate(adapter);
        await ApplyFanqieTheme();
        await fanqieBridge.Evaluate("new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))");
        // The embedded WebView can briefly paint its default white surface even
        // after DOM readiness. Keep its parent hidden for a few more frames.
        await Task.Delay(700);
        ResizeFanqieWindow();
    }
    private async Task ApplyFanqieTheme() {
        if(fanqieBridge!=null&&fanqieProcess!=null&&!fanqieProcess.HasExited)
            await fanqieBridge.Evaluate("window.beihaiAdapter?.theme('"+(dark?"dark":"light")+"')");
    }
    private async Task ShowAbout() {
        bool checkApp=false,checkFanqie=false;
        using(var dialog=new Form { Text="关于北海栈",ClientSize=new Size(470,258),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,Font=Font,BackColor=BackColor,ForeColor=ForeColor }) {
            var title=new Label { Text="北海栈",Font=new Font(Font.FontFamily,20,FontStyle.Bold),AutoSize=true,Location=new Point(24,22) };
            var info=new Label { Text="版本 "+AppVersion+"\n写作 · 剧本 · AIGC资产 · 内嵌番茄小说下载器\n启动后自动检查北海栈新版本",AutoSize=true,Location=new Point(26,72) };
            var appUpdate=new Button { Text="检查北海栈更新",Location=new Point(24,154),Size=new Size(198,38),FlatStyle=FlatStyle.Flat,BackColor=dark?Color.FromArgb(120,96,211):Color.FromArgb(108,84,216),ForeColor=Color.White };
            appUpdate.Click+=(sender,e)=>{checkApp=true;dialog.Close();};
            var fanqieUpdate=new Button { Text="检查番茄更新",Location=new Point(244,154),Size=new Size(198,38),FlatStyle=FlatStyle.Flat };
            fanqieUpdate.Click+=(sender,e)=>{checkFanqie=true;dialog.Close();};
            var close=new Button { Text="关闭",Location=new Point(332,207),Size=new Size(110,34),DialogResult=DialogResult.Cancel };
            dialog.Controls.AddRange(new Control[]{title,info,appUpdate,fanqieUpdate,close});dialog.CancelButton=close;
            dialog.ShowDialog(this);
        }
        if(checkApp)await CheckForAppUpdates(false);
        else if(checkFanqie)await OpenFanqieSettings(true);
    }
    private async Task CheckForAppUpdates(bool silent)
    {
        try
        {
            string response;
            using(var client=new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent]="BeihaiZhan/"+AppVersion;
                client.Headers[HttpRequestHeader.Accept]="application/vnd.github+json";
                response=await client.DownloadStringTaskAsync(AppReleaseApi);
            }
            var release=json.Deserialize<Dictionary<string,object>>(response);
            string tag=Convert.ToString(release["tag_name"]),versionText=tag.TrimStart('v','V');
            Version latest,current;
            if(!Version.TryParse(versionText,out latest)||!Version.TryParse(AppVersion,out current))throw new IOException("版本号格式无效。");
            if(latest<=current){if(!silent)MessageBox.Show(this,"当前已经是最新版本（v"+AppVersion+"）。","北海栈更新",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
            if(MessageBox.Show(this,"发现北海栈 "+tag+"，当前版本为 v"+AppVersion+"。\n\n是否立即下载更新包？","发现新版本",MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes)return;
            string downloadUrl=null,fileName=null;
            object assetsValue;
            if(release.TryGetValue("assets",out assetsValue))
            {
                var assets=assetsValue as object[];
                if(assets!=null)foreach(var value in assets)
                {
                    var asset=value as Dictionary<string,object>;if(asset==null)continue;
                    string name=Convert.ToString(asset["name"]);
                    if(name.EndsWith("Windows-x64.zip",StringComparison.OrdinalIgnoreCase)){fileName=name;downloadUrl=Convert.ToString(asset["browser_download_url"]);break;}
                }
            }
            if(String.IsNullOrEmpty(downloadUrl)||!downloadUrl.StartsWith("https://github.com/lm131420m-eng/beihai-zhan/releases/download/",StringComparison.OrdinalIgnoreCase))
            { Process.Start(new ProcessStartInfo(AppReleasePage){UseShellExecute=true});return; }
            string downloads=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");Directory.CreateDirectory(downloads);
            string destination=Path.Combine(downloads,fileName);
            using(var client=new WebClient()){client.Headers[HttpRequestHeader.UserAgent]="BeihaiZhan/"+AppVersion;await client.DownloadFileTaskAsync(downloadUrl,destination);}
            MessageBox.Show(this,"更新包已下载到：\n"+destination+"\n\n关闭北海栈后解压覆盖旧文件即可完成更新。","下载完成",MessageBoxButtons.OK,MessageBoxIcon.Information);
            Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+destination+"\""){UseShellExecute=true});
        }
        catch(Exception ex){if(!silent)MessageBox.Show(this,"暂时无法检查更新：\n"+ex.Message+"\n\n也可以打开 GitHub 发布页手动下载。","北海栈更新",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
    private async Task OpenFanqieSettings(bool checkUpdates) {
        if(web.CoreWebView2==null)return;
        try {
            await web.ExecuteScriptAsync("window.switchCollection('fanqie')");
            await EnsureFanqieEmbedded();
            if(fanqieBridge!=null)await fanqieBridge.Evaluate("window.beihaiAdapter.settings("+(checkUpdates?"true":"false")+")");
        } catch(Exception ex){MessageBox.Show(this,"无法打开下载器设置："+ex.Message,"北海栈");}
    }
    private void LayoutFanqieHost()
    {
        if(MainMenuStrip==null)return;
        int top=web.Top+(int)Math.Ceiling(headerFraction>0?headerFraction*web.ClientSize.Width:77*web.ZoomFactor*DeviceDpi/96.0);
        fanqieHost.Bounds=new Rectangle(0,top,ClientSize.Width,Math.Max(0,ClientSize.Height-top));
        ResizeFanqieWindow();
    }
    private void ResizeFanqieWindow()
    {
        if(fanqieWindow==IntPtr.Zero||fanqieHost.IsDisposed||!IsWindow(fanqieWindow))return;
        // Tauri applies its own decorations during startup. Remove them again once
        // ready, and clip any remaining native non-client insets outside the host.
        long oldStyle=GetWindowLongPtr(fanqieWindow,-16).ToInt64();
        long style=(oldStyle&~(0x80000000L|0x00C00000L|0x00040000L|0x00080000L|0x00030000L|0x20000000L|0x01000000L))|0x40000000L;
        long oldEx=GetWindowLongPtr(fanqieWindow,-20).ToInt64(),ex=oldEx&~0x00020301L;
        bool changed=style!=oldStyle||ex!=oldEx;
        if(changed){SetWindowLongPtr(fanqieWindow,-16,new IntPtr(style));SetWindowLongPtr(fanqieWindow,-20,new IntPtr(ex));SetWindowPos(fanqieWindow,IntPtr.Zero,0,0,fanqieHost.Width,fanqieHost.Height,0x0010|0x0020|0x0004);}
        NativeRect outer,client;Point origin=new Point();
        if(!GetWindowRect(fanqieWindow,out outer)||!GetClientRect(fanqieWindow,out client)||!ClientToScreen(fanqieWindow,ref origin))return;
        int left=Math.Max(0,origin.X-outer.Left),top=Math.Max(0,origin.Y-outer.Top);
        int right=Math.Max(0,outer.Right-origin.X-client.Right),bottom=Math.Max(0,outer.Bottom-origin.Y-client.Bottom);
        int width=fanqieHost.ClientSize.Width+left+right,height=fanqieHost.ClientSize.Height+top+bottom;
        Point hostOrigin=fanqieHost.PointToScreen(Point.Empty);
        if(changed||outer.Left!=hostOrigin.X-left||outer.Top!=hostOrigin.Y-top||outer.Right-outer.Left!=width||outer.Bottom-outer.Top!=height)
            SetWindowPos(fanqieWindow,IntPtr.Zero,-left,-top,width,height,0x0010|0x0020|0x0004);
    }
    private void HideFanqie(){fanqieRequested=false;fanqieHost.Visible=false;}
    private void CloseFanqie()
    {
        try{if(fanqieProcess!=null&&!fanqieProcess.HasExited){if(IsWindow(fanqieWindow))PostMessage(fanqieWindow,0x0010,IntPtr.Zero,IntPtr.Zero);else fanqieProcess.CloseMainWindow();}}catch{}
    }
    private async Task RunEmbedTest()
    {
        try
        {
            await web.ExecuteScriptAsync("switchCollection('fanqie')");
            await EnsureFanqieEmbedded();
            string ui=Convert.ToString(await fanqieBridge.Evaluate("JSON.stringify({hidden:getComputedStyle(document.querySelector('.topbar')).display==='none',full:document.querySelector('.app-shell').getBoundingClientRect().width===innerWidth,settings:!!document.querySelector('.beihai-update-card #topbarUpdateCard'),theme:document.documentElement.dataset.beihaiTheme})"));
            if(!ui.Contains("\"hidden\":true")||!ui.Contains("\"full\":true")||!ui.Contains("\"settings\":true"))throw new Exception("界面适配未通过："+ui);
            await fanqieBridge.Evaluate("window.beihaiAdapter.theme('light')");
            if(Convert.ToString(await fanqieBridge.Evaluate("getComputedStyle(document.querySelector('.accent-button')).backgroundColor"))!="rgb(108, 84, 216)")throw new Exception("浅色紫色主题未生效");
            await fanqieBridge.Evaluate("window.beihaiAdapter.theme('dark')");
            if(Convert.ToString(await fanqieBridge.Evaluate("getComputedStyle(document.body).backgroundColor"))!="rgb(23, 23, 31)")throw new Exception("深色主题未生效");
            await ApplyFanqieTheme();
            if(!Convert.ToBoolean(await fanqieBridge.Evaluate("window.beihaiAdapter.settings(false);document.querySelector('[data-view-panel=\"settings\"]').classList.contains('active')")))throw new Exception("设置入口未打开");
            if(fanqieWindow==IntPtr.Zero||GetParent(fanqieWindow)!=fanqieHost.Handle||!fanqieHost.Visible)throw new Exception("窗口未嵌入北海栈内容区");
            int processId=fanqieProcess.Id;IntPtr embeddedWindow=fanqieWindow;
            await web.ExecuteScriptAsync("switchCollection('writing')");await Task.Delay(200);
            if(fanqieHost.Visible)throw new Exception("切换回写作后，内嵌窗口没有隐藏");
            await web.ExecuteScriptAsync("switchCollection('fanqie')");await EnsureFanqieEmbedded();
            if(!fanqieHost.Visible||fanqieProcess.Id!=processId||fanqieWindow!=embeddedWindow||GetParent(fanqieWindow)!=fanqieHost.Handle)throw new Exception("返回番茄下载后没有复用原内嵌窗口");
            File.WriteAllText(Path.Combine(root,"embed-test.txt"),"PASS\r\n内嵌窗口、切换隐藏、实例复用、标题栏隐藏、全宽布局、版本设置入口、浅色紫色主题及深色主题检查通过。");Environment.ExitCode=0;Close();
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(root,"embed-test.txt"),"FAIL\r\n"+ex);Environment.ExitCode=1;Close();}
    }
    private async Task UpdateImportCatalog()
    {
        string catalog=imports.CatalogJson();
        if(catalogScript!=null) web.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(catalogScript);
        catalogScript=await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.BEIHAI_IMPORTS="+catalog+";");
    }
    private void ApplyNativeTheme()
    {
        BackColor=dark?Color.FromArgb(23,23,31):Color.FromArgb(247,248,252);
        ForeColor=dark?Color.FromArgb(233,231,242):Color.FromArgb(38,37,61);
        loading.BackColor=BackColor;loading.ForeColor=dark?Color.FromArgb(196,179,255):Color.FromArgb(108,84,216);
        fanqieHost.BackColor=BackColor;
        if(MainMenuStrip!=null) {
            MainMenuStrip.BackColor=dark?Color.FromArgb(34,34,46):Color.White;
            MainMenuStrip.ForeColor=ForeColor;
            MainMenuStrip.Renderer=dark?new ToolStripProfessionalRenderer(new DarkMenuColors()):new ToolStripProfessionalRenderer();
            ApplyMenuColors(MainMenuStrip.Items);
        }
        if(themeMenu!=null) themeMenu.Checked=dark;
        if(IsHandleCreated) { try{int value=dark?1:0;if(DwmSetWindowAttribute(Handle,20,ref value,4)!=0)DwmSetWindowAttribute(Handle,19,ref value,4);}catch{} }
    }
    private void ApplyMenuColors(ToolStripItemCollection items)
    {
        foreach(ToolStripItem item in items) {
            item.ForeColor=ForeColor;
            var submenu=item as ToolStripMenuItem;
            if(submenu!=null) { submenu.DropDown.BackColor=dark?Color.FromArgb(34,34,46):Color.White; ApplyMenuColors(submenu.DropDownItems); }
        }
    }
    private async Task ChangeTheme(bool value,bool persist)
    {
        dark=value;ApplyNativeTheme();
        if(fanqieBridge!=null)try{await ApplyFanqieTheme();}catch(Exception){ /* child may still be starting */ }
        if(persist && !test) { try{Directory.CreateDirectory(Path.GetDirectoryName(themeFile));File.WriteAllText(themeFile,dark?"dark":"light");}catch(Exception ex){MessageBox.Show(this,"主题已切换，但无法保存设置："+ex.Message,"主题设置");} }
        if(web.CoreWebView2==null) return;
        string name=dark?"dark":"light";
        if(themeScript!=null)web.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(themeScript);
        themeScript=await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.BEIHAI_THEME='"+name+"';");
        await web.ExecuteScriptAsync("window.setBeihaiTheme?.('"+name+"')");
    }
    private async Task ImportFiles()
    {
        if(importing || imports==null || web.CoreWebView2==null) return;
        string currentCollection=json.Deserialize<string>(await web.ExecuteScriptAsync("window.activeCollection||'writing'"));
        string[] files;
        using(var picker=new OpenFileDialog())
        {
            picker.Title="选择要导入的例文或压缩包（可多选）";
            picker.Filter="支持的文档与资产|*.txt;*.md;*.docx;*.pdf;*.png;*.jpg;*.jpeg;*.webp;*.gif;*.mp4;*.mov;*.webm;*.mp3;*.wav;*.flac;*.glb;*.gltf;*.obj;*.zip|文本与Word|*.txt;*.md;*.docx|图像|*.png;*.jpg;*.jpeg;*.webp;*.gif|音视频|*.mp4;*.mov;*.webm;*.mp3;*.wav;*.flac|ZIP 压缩包|*.zip";
            picker.Multiselect=true;
            if(picker.ShowDialog(this)!=DialogResult.OK) return;
            files=picker.FileNames;
        }
        string platform,genre,collection;
        using(var dialog=new Form())
        {
            dialog.Text="导入到资料库"; dialog.ClientSize=new Size(460,325); dialog.StartPosition=FormStartPosition.CenterParent;
            dialog.FormBorderStyle=FormBorderStyle.FixedDialog; dialog.MaximizeBox=false; dialog.MinimizeBox=false; dialog.Font=Font;
            var intro=new Label{Text="已选择 "+files.Length+" 个文件。ZIP 内的支持格式会自动导入。",Left=22,Top=18,Width=415,Height=45};
            var collectionLabel=new Label{Text="大类目",Left=22,Top=76,Width=110};
            var collectionBox=new ComboBox{Left=140,Top=71,Width=290,DropDownStyle=ComboBoxStyle.DropDownList};
            collectionBox.Items.AddRange(new object[]{"写作","剧本","AIGC资产"});
            collectionBox.SelectedIndex=currentCollection=="scripts"?1:currentCollection=="aigc"?2:0;
            var sourceLabel=new Label{Text="平台 / 来源",Left=22,Top=80,Width=110};
            var sourceBox=new TextBox{Text="我的导入",Left=140,Top=75,Width=290,MaxLength=60};
            var genreLabel=new Label{Text="题材 / 分类",Left=22,Top=123,Width=110};
            var genreBox=new TextBox{Text=files.Length==1 && Path.GetExtension(files[0]).Equals(".zip",StringComparison.OrdinalIgnoreCase)?Path.GetFileNameWithoutExtension(files[0]):"自定义资料",Left=140,Top=118,Width=290,MaxLength=60};
            var tip=new Label{Text="导入后可搜索、收藏和阅读；内容相同的文件自动跳过。\n旧版 .doc 请先另存为 .docx。",Left=22,Top=163,Width=415,Height=50,ForeColor=Color.DimGray};
            var ok=new Button{Text="开始导入",Left=236,Top=223,Width=96,Height=32};
            var cancel=new Button{Text="取消",Left=344,Top=223,Width=86,Height=32,DialogResult=DialogResult.Cancel};
            ok.Click+=(s,e)=>{if(String.IsNullOrWhiteSpace(sourceBox.Text)||String.IsNullOrWhiteSpace(genreBox.Text)) { MessageBox.Show(dialog,"请填写平台 / 来源和题材 / 分类。");return;}dialog.DialogResult=DialogResult.OK;};
            dialog.Controls.AddRange(new Control[]{intro,sourceLabel,sourceBox,genreLabel,genreBox,tip,ok,cancel}); dialog.AcceptButton=ok; dialog.CancelButton=cancel;
            foreach(Control control in new Control[]{sourceLabel,sourceBox,genreLabel,genreBox,tip,ok,cancel})control.Top+=48;
            dialog.Controls.AddRange(new Control[]{collectionLabel,collectionBox});
            tip.Text="文本文档可直接阅读，图像、音视频及其他资产可打开原文件。\n内容相同的文件自动跳过。";
            if(dark) {
                dialog.BackColor=Color.FromArgb(34,34,46);dialog.ForeColor=Color.FromArgb(233,231,242);
                foreach(Control control in dialog.Controls) {
                    control.ForeColor=dialog.ForeColor;
                    if(control is TextBox || control is Button) control.BackColor=Color.FromArgb(48,43,64);
                    var button=control as Button;if(button!=null){button.UseVisualStyleBackColor=false;button.FlatStyle=FlatStyle.Flat;}
                }
                tip.ForeColor=Color.FromArgb(185,178,204);
            }
            if(dialog.ShowDialog(this)!=DialogResult.OK) return;
            platform=sourceBox.Text.Trim();genre=genreBox.Text.Trim();
            collection=new[]{"writing","scripts","aigc"}[collectionBox.SelectedIndex];
        }
        importing=true; loading.Text="正在导入资料，请稍候…"; loading.Visible=true; loading.BringToFront();
        try
        {
            var known=ImportStore.BuiltinHashes(File.ReadAllText(Path.Combine(root,"北海.html")));
            var result=await Task.Run(()=>imports.Import(files,platform,genre,known,message=>{ if(!IsDisposed) BeginInvoke(new Action(()=>loading.Text=message)); },collection));
            await UpdateImportCatalog();
            if(result.Added>0) { pendingPlatform=platform;pendingCollection=collection; web.CoreWebView2.Reload(); }
            string details=result.Details.Count>0 ? "\n\n"+String.Join("\n",result.Details.GetRange(0,Math.Min(5,result.Details.Count)))+"\n\n完整结果见“资料库 → 打开我的导入文件夹”。" : "";
            MessageBox.Show(this,result.Summary+details,"导入完成",MessageBoxButtons.OK,result.Failed>0?MessageBoxIcon.Warning:MessageBoxIcon.Information);
        }
        catch(Exception ex) { MessageBox.Show(this,"导入未完成："+ex.Message,"导入失败",MessageBoxButtons.OK,MessageBoxIcon.Error); }
        finally { importing=false;loading.Visible=false; }
    }
    private void OpenUri(string address)
    {
        try
        {
            var uri = new Uri(address);
            if (uri.Scheme == "https" && uri.Host == "beihai.local") OpenLocal(LocalPath(uri));
            else if (uri.Scheme == "https" && uri.Host == "imports.beihai.local")
            {
                string location=Path.GetFullPath(Path.Combine(imports.Root,Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/').Replace('/',Path.DirectorySeparatorChar)));
                if(!location.StartsWith(imports.Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new IOException("文件路径无效");
                OpenLocal(location);
            }
            else if (uri.Scheme == "https" || uri.Scheme == "http") Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
        }
        catch(Exception ex) { MessageBox.Show(this,ex.Message,"无法打开文件",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
    }
    private void OpenLocal(string path)
    {
        try {
            if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("文件不存在：" + path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        } catch(Exception ex) { MessageBox.Show(this,ex.Message,"无法打开文件",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
    }
    private async Task Check(string script, string label)
    {
        string result = await web.ExecuteScriptAsync(script);
        if (result != "true") throw new Exception(label + ": " + result);
    }
    private void PrepareImportTest()
    {
        string fixture=Path.Combine(imports.Root,"导入测试.txt");
        File.WriteAllText(fixture,"这是导入功能验证用例。\n"+new string('文',160));
        var result=imports.Import(new[]{fixture},"导入功能测试","测试分类",new HashSet<string>(),s=>{});
        if(result.Added!=1) throw new Exception("测试文件导入失败");
        if(new ImportStore(imports.Root).Load().Count!=1) throw new Exception("导入持久化失败");
        if(imports.Import(new[]{fixture},"导入功能测试","测试分类",new HashSet<string>(),s=>{}).Duplicates!=1) throw new Exception("重复检测失败");
    }
    private async Task SelfTest()
    {
        try
        {
            await Check("DATA.length>0 && document.querySelectorAll('#cards .card').length>0", "首页渲染");
            await Check("document.querySelectorAll('#main-collections button').length===4", "顶部四大类目");
            await web.ExecuteScriptAsync("switchCollection('scripts')");
            await Check("filtered.length===0 && $('cards').textContent.includes('剧本') && $('generator-nav').hidden", "剧本类目隔离");
            await web.ExecuteScriptAsync("switchCollection('aigc')");
            await Check("filtered.length===0 && $('cards').textContent.includes('AIGC资产')", "资产类目隔离");
            await web.ExecuteScriptAsync("switchCollection('writing')");
            await Check("$('generator-nav').childNodes[1].textContent==='导言生成' && $('generator-nav').textContent.includes('598')", "导言生成入口名称未被平台脚本覆盖");
            await web.ExecuteScriptAsync("document.querySelector('[data-cat=\"stories\"]').click()");
            await Check("!$('writing-filter-panel').hidden && document.querySelectorAll('#platform-filters [data-content-platform]').length>1 && document.querySelectorAll('#filters [data-genre]').length>1 && !$('sidebar-platform-title') && $('platforms').hidden", "写作平台与小说类型双重筛选布局");
            await web.ExecuteScriptAsync("document.querySelector('#platform-filters [data-content-platform]:not([data-content-platform=\"\"])').click()");
            await Check("state.category==='stories' && !!state.platform && filtered.every(d=>collectionPlatformFor(d.platform)===state.platform)", "内容区小说平台筛选");
            await Check("platforms.includes('其他') && !platforms.includes('兰亭') && DATA.filter(d=>(d.collection||'writing')==='writing'&&d.type==='例文'&&isOtherWritingPlatform(d.platform)).length===Number(document.querySelector('[data-platform=\"其他\"] .count').textContent.replaceAll(',',''))", "写作平台大类和其他归并");
            await web.ExecuteScriptAsync("document.querySelector('[data-platform=\"其他\"]').click()");
            await Check("filtered.length>0 && filtered.every(d=>d.type==='例文'&&isOtherWritingPlatform(d.platform))", "其他平台筛选");
            await web.ExecuteScriptAsync("document.querySelector('[data-cat=\"all\"]').click()");
            await web.ExecuteScriptAsync("switchCollection('fanqie')");
            await Check("!!document.querySelector('[data-fanqie-open]') && $('cards').textContent.includes('直接显示在北海栈当前窗口中')", "番茄下载大类");
            if(!File.Exists(FanqieExecutable()) || FileSha256(FanqieExecutable())!=FanqieSha256)throw new Exception("番茄小说下载器文件或 SHA-256 校验失败");
            using(var fanqiePreview=File.Create(Path.Combine(root,"番茄下载预览.png")))await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,fanqiePreview);
            await web.ExecuteScriptAsync("switchCollection('writing')");
            await Check("!$('import-files') && !document.querySelector('.header-right') && !document.querySelector('.side-note') && !document.querySelector('footer.footer')", "界面精简与导入位置");
            await ChangeTheme(true,false);
            await Check("document.documentElement.dataset.theme==='dark' && getComputedStyle(document.body).backgroundColor==='rgb(23, 23, 31)' && localStorage.getItem('beihai-theme')==='dark'", "深色模式和主题存储");
            await web.ExecuteScriptAsync("openItem(DATA.find(d=>d.platform==='导入功能测试').id)");
            for(int i=0;i<100;i++) { if(await web.ExecuteScriptAsync("bodyText.length>0")=="true") break;await Task.Delay(100); }
            await Check("bodyText.includes('这是导入功能验证用例') && $('original').href.startsWith('https://imports.beihai.local/files/')", "导入正文和原文件路径");
            await web.ExecuteScriptAsync("$('modal').close()");
            await web.ExecuteScriptAsync("DATA.splice(DATA.findIndex(d=>d.platform==='导入功能测试'),1);platforms.splice(platforms.indexOf('导入功能测试'),1);$('stats').querySelectorAll('b')[0].textContent=num(countType('例文'));$('stats').querySelectorAll('b')[1].textContent=platforms.length;nav();render();");
            await web.ExecuteScriptAsync("$('query').value='不存在的测试标题xyz987';$('query').dispatchEvent(new Event('input')); ");
            await Task.Delay(500);
            await Check("document.querySelectorAll('#cards .card').length===0", "搜索过滤");
            await web.ExecuteScriptAsync("$('query').value='';$('query').dispatchEvent(new Event('input')); ");
            await Task.Delay(500);
            await web.ExecuteScriptAsync("openItem(DATA.find(d=>d.chunk!==undefined && d.type==='例文').id)");
            for (int i=0;i<100;i++) { if(await web.ExecuteScriptAsync("bodyText.length>0") == "true") break; await Task.Delay(100); }
            await Check("bodyText.length>100 && $('modal').open && !$('copy').disabled", "正文解压阅读");
            await Check("getComputedStyle($('text')).color==='rgb(222, 217, 233)'", "深色正文阅读");
            await web.ExecuteScriptAsync("$('modal').close();document.querySelector('[data-save]').click();");
            await Check("JSON.parse(localStorage.getItem('liwan-favorites-v1')).length>0", "收藏持久化");
            await web.ExecuteScriptAsync("document.querySelector('[data-save]').click();openItem(DATA.find(d=>d.type==='提示词').id)");
            await Check("bodyText.length>0 && !$('copy').disabled && $('download').href.startsWith('blob:')", "提示词阅读和下载链接");
            await web.ExecuteScriptAsync("$('modal').close();showGenerator(true)");
            await Task.Delay(2000);
            await Check("$('generator-frame').contentDocument.querySelectorAll('#rw-grid .card').length>0", "书名导言生成器");
            await Check("$('generator-frame').contentDocument.documentElement.dataset.theme==='dark'", "生成器主题同步");
            await ChangeTheme(false,false);
            await Check("document.documentElement.dataset.theme==='light' && $('generator-frame').contentDocument.documentElement.dataset.theme==='light'", "恢复浅色模式");
            await ChangeTheme(true,false);
            await web.ExecuteScriptAsync("showGenerator(false)");
            if (await web.ExecuteScriptAsync("DATA.some(d=>d.platform==='时空对话梗专题')") == "true")
            {
                await web.ExecuteScriptAsync("switchCollection('writing');state.platform=collectionPlatformFor('时空对话梗专题');state.genre='时空对话梗';render()");
                await Check("filtered.length===29 && filtered.every(d=>d.genre==='时空对话梗')", "新增专题筛选");
                await web.ExecuteScriptAsync("openItem(DATA.find(d=>d.platform==='时空对话梗专题' && d.ext==='DOCX').id)");
                for (int i=0;i<100;i++) { if(await web.ExecuteScriptAsync("bodyText.length>0") == "true") break; await Task.Delay(100); }
                await Check("bodyText.length>100 && !$('copy').disabled && $('original').href.endsWith('.docx')", "新增 Word 正文阅读");
                await web.ExecuteScriptAsync("$('modal').close()");
            }
            await Task.Delay(400);
            using (var stream = File.Create(Path.Combine(root,"桌面预览.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
            await ChangeTheme(false,false);
            if(errors.Length>0) throw new Exception(errors);
            FinishTest(true,"番茄下载大类、官方文件 SHA-256、界面精简、导入入口、导入持久化、重复检测、导入正文与原文件链接、首页、搜索、正文解压阅读、收藏存储、提示词阅读、书名导言生成器和 JavaScript 错误检查通过。");
        }
        catch(Exception ex) { FinishTest(false,ex.ToString()); }
    }
    private void FinishTest(bool success,string message)
    {
        File.WriteAllText(Path.Combine(root,"self-test.txt"),(success?"PASS":"FAIL") + "\n" + message);
        Environment.ExitCode = success ? 0 : 1;
        Close();
    }
}

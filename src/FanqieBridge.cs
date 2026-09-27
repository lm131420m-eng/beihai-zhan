using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Web.Script.Serialization;

// Per-child loopback connection. No system environment or browser settings change.
internal sealed class FanqieBridge : IDisposable
{
    public readonly int Port;
    private ClientWebSocket socket;
    private int sequence;
    private readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
    private readonly JavaScriptSerializer json=new JavaScriptSerializer { MaxJsonLength=8*1024*1024 };
    public FanqieBridge() {
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        Port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
    }
    public async Task Connect() {
        for(int i=0;i<100;i++) {
            try {
                using(var client=new WebClient()) {
                    client.Proxy=null;client.Encoding=Encoding.UTF8;
                    var targets=json.DeserializeObject(await client.DownloadStringTaskAsync("http://127.0.0.1:"+Port+"/json/list")) as object[];
                    foreach(Dictionary<string,object> target in targets) {
                        string url=Convert.ToString(target["url"]);
                        if(url!="http://tauri.localhost/"&&url!="https://tauri.localhost/")continue;
                        var uri=new Uri(Convert.ToString(target["webSocketDebuggerUrl"]));
                        if(!uri.IsLoopback)throw new IOException("下载器连接地址不是本机地址。");
                        socket=new ClientWebSocket();socket.Options.Proxy=null;
                        using(var timeout=new CancellationTokenSource(3000))await socket.ConnectAsync(uri,timeout.Token);
                        return;
                    }
                }
            }catch(WebException){}catch(WebSocketException){}catch(TaskCanceledException){}
            await Task.Delay(150);
        }
        throw new IOException("下载器界面连接超时，请关闭后重新打开番茄下载。");
    }
    public async Task<object> Call(string method,object parameters) {
        await gate.WaitAsync();
        try {
            if(socket==null||socket.State!=WebSocketState.Open)throw new IOException("下载器界面尚未连接。");
            int id=++sequence;
            var bytes=Encoding.UTF8.GetBytes(json.Serialize(new { id=id,method=method,@params=parameters }));
            using(var timeout=new CancellationTokenSource(5000)) {
                await socket.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,timeout.Token);
                while(true) {
                    using(var message=new MemoryStream()) {
                        var buffer=new byte[16384];WebSocketReceiveResult part;
                        do {
                            part=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),timeout.Token);
                            if(part.MessageType==WebSocketMessageType.Close)throw new IOException("下载器已关闭。");
                            message.Write(buffer,0,part.Count);
                            if(message.Length>8*1024*1024)throw new IOException("下载器响应过大。");
                        }while(!part.EndOfMessage);
                        var reply=json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(message.ToArray()));
                        if(!reply.ContainsKey("id")||Convert.ToInt32(reply["id"])!=id)continue;
                        if(reply.ContainsKey("error"))throw new IOException(json.Serialize(reply["error"]));
                        return reply["result"];
                    }
                }
            }
        }finally{gate.Release();}
    }
    public async Task<object> Evaluate(string script) {
        var reply=(Dictionary<string,object>)await Call("Runtime.evaluate",new { expression=script,returnByValue=true,awaitPromise=true });
        if(reply.ContainsKey("exceptionDetails"))throw new IOException("下载器界面适配失败："+json.Serialize(reply["exceptionDetails"]));
        var result=(Dictionary<string,object>)reply["result"];
        return result.ContainsKey("value")?result["value"]:null;
    }
    public void Dispose(){if(socket!=null)socket.Dispose();}
}

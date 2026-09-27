using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Web.Script.Serialization;

internal sealed class ImportRecord
{
    public int id; public string title, platform, genre, format, type, ext, path, original, source, date, hash, preview, chunk;
    public string[] aliases; public long size; public int chars; public string collection;
}
internal sealed class ImportResult
{
    public int Added, Duplicates, Unsupported, Failed;
    public readonly List<string> Details = new List<string>();
    public string Summary { get { return "新增 " + Added + " 份，跳过重复 " + Duplicates + " 份，不支持 " + Unsupported + " 份，失败 " + Failed + " 份。"; } }
}
internal sealed class ImportStore
{
    public readonly string Root;
    private const long MaxFile = 50L * 1024 * 1024;
    private const long MaxTotal = 256L * 1024 * 1024;
    private readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024, RecursionLimit = 100 };
    public ImportStore(string root) { Root = root; Directory.CreateDirectory(root); }
    public List<ImportRecord> Load()
    {
        string file = Path.Combine(Root,"catalog.json");
        if (!File.Exists(file)) return new List<ImportRecord>();
        var records = json.Deserialize<List<ImportRecord>>(File.ReadAllText(file,Encoding.UTF8));
        if (records == null || records.Any(r=>r.id>=0 || String.IsNullOrEmpty(r.hash))) throw new IOException("导入目录索引损坏，请保留目录并从 catalog.backup.json 恢复。");
        return records;
    }
    public string CatalogJson() { return json.Serialize(Load()); }
    public static HashSet<string> BuiltinHashes(string html)
    {
        return new HashSet<string>(Regex.Matches(html,"\"hash\"\\s*:\\s*\"([a-f0-9]{10,64})\"").Cast<Match>().Select(m=>m.Groups[1].Value),StringComparer.OrdinalIgnoreCase);
    }
    public ImportResult Import(string[] files, string platform, string genre, HashSet<string> builtins, Action<string> progress, string collection = "writing")
    {
        var records = Load();
        var hashes = new HashSet<string>(records.Select(r=>r.hash),StringComparer.OrdinalIgnoreCase);
        var result = new ImportResult();
        int next = records.Count==0 ? -1 : records.Min(r=>r.id)-1;
        long total = 0; int candidates = 0;
        Action<string,byte[]> accept = (name,bytes) => {
            progress("正在导入：" + Path.GetFileName(name));
            try
            {
                string hash;
                using(var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                if(hashes.Contains(hash) || builtins.Contains(hash) || builtins.Contains(hash.Substring(0,10))) { result.Duplicates++; return; }
                string ext=Path.GetExtension(name).ToLowerInvariant();
                bool readable=ext==".docx" || ext==".txt" || ext==".md";
                string body=ext==".docx" ? ReadDocx(bytes) : readable ? Decode(bytes) : "";
                if(readable && String.IsNullOrWhiteSpace(body)) throw new IOException("没有可读取的正文");
                string basename=Path.GetFileName(name.Replace('\\','/'));
                string title=Path.GetFileNameWithoutExtension(basename);
                Match titled=Regex.Match(title,"^《([^》]+)》");
                if(titled.Success) title=titled.Groups[1].Value;
                string relative="files/"+hash+ext;
                string chunk="user_"+hash;
                int id=next--;
                var record = new ImportRecord { id=id,title=title,platform=platform,genre=genre,format="未标注",type="例文",ext=ext.Substring(1).ToUpperInvariant(),path="__imports__/"+relative,original=basename,aliases=new[]{basename},source=name,date=DateTime.Now.ToString("yyyy-MM-dd"),hash=hash,size=bytes.Length,chars=Regex.Replace(body,"\\s","").Length,preview=Regex.Replace(body,"\\s+"," ").Trim(),chunk=chunk };
                record.preview=record.preview.Substring(0,Math.Min(140,record.preview.Length));
                record.collection=collection;
                if(!readable){record.chunk=null;record.preview=record.ext+" 资产文件 · 可打开原文件或下载使用。";}
                Directory.CreateDirectory(Path.Combine(Root,"files"));
                Directory.CreateDirectory(Path.Combine(Root,"chunks"));
                File.WriteAllBytes(Path.Combine(Root,relative.Replace('/',Path.DirectorySeparatorChar)),bytes);
                var textMap=new Dictionary<string,string>{{id.ToString(),body}};
                byte[] content=Encoding.UTF8.GetBytes(json.Serialize(textMap));
                string packed;
                using(var buffer=new MemoryStream()) { using(var gzip=new GZipStream(buffer,CompressionMode.Compress,true)) gzip.Write(content,0,content.Length); packed=Convert.ToBase64String(buffer.ToArray()); }
                File.WriteAllText(Path.Combine(Root,"chunks",chunk+".js"),"window.LIWAN_PACKS["+json.Serialize(chunk)+"]="+json.Serialize(packed)+";",new UTF8Encoding(false));
                records.Add(record); hashes.Add(hash); result.Added++;
            }
            catch(Exception ex) { result.Failed++; result.Details.Add(name+"："+ex.Message); }
        };
        foreach(string file in files)
        {
            try
            {
                if(Path.GetExtension(file).Equals(".zip",StringComparison.OrdinalIgnoreCase))
                {
                    using(var archive = new ZipArchive(File.OpenRead(file),ZipArchiveMode.Read,false,Encoding.GetEncoding(936)))
                    {
                        foreach(var entry in archive.Entries)
                        {
                            if(entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\")) continue;
                            if(++candidates>10000) throw new IOException("单次文件数过多，请分批导入");
                            if(!Supported(entry.FullName)) { result.Unsupported++; continue; }
                            if(entry.Length>MaxFile || total+entry.Length>MaxTotal) { result.Failed++; result.Details.Add(entry.FullName+"：超过单文件 50 MB 或单批次 256 MB 限制"); continue; }
                            using(var stream=entry.Open()) { var bytes=ReadLimited(stream,MaxFile); total+=bytes.Length; accept(Path.GetFileName(file)+"/"+entry.FullName,bytes); }
                        }
                    }
                }
                else
                {
                    if(!Supported(file)) { result.Unsupported++; continue; }
                    var info=new FileInfo(file);
                    if(info.Length>MaxFile || total+info.Length>MaxTotal) throw new IOException("超过单文件 50 MB 或单批次 256 MB 限制");
                    using(var stream=File.OpenRead(file)) { var bytes=ReadLimited(stream,MaxFile); total+=bytes.Length; accept(file,bytes); }
                }
            }
            catch(Exception ex) { result.Failed++; result.Details.Add(Path.GetFileName(file)+"："+ex.Message); }
        }
        if(result.Added>0)
        {
            string catalog=Path.Combine(Root,"catalog.json"), temp=Path.Combine(Root,"catalog.next.json");
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            { var bytes=Encoding.UTF8.GetBytes(json.Serialize(records)); stream.Write(bytes,0,bytes.Length); stream.Flush(true); }
            if(File.Exists(catalog)) File.Replace(temp,catalog,Path.Combine(Root,"catalog.backup.json"));
            else File.Move(temp,catalog);
        }
        File.WriteAllText(Path.Combine(Root,"最近导入结果.txt"),DateTime.Now+"\r\n"+result.Summary+"\r\n"+String.Join("\r\n",result.Details),Encoding.UTF8);
        return result;
    }
    private static bool Supported(string path) { string ext=Path.GetExtension(path).ToLowerInvariant(); return new[]{".txt",".md",".docx",".pdf",".png",".jpg",".jpeg",".webp",".gif",".mp4",".mov",".webm",".mp3",".wav",".flac",".glb",".gltf",".obj"}.Contains(ext); }
    private static byte[] ReadLimited(Stream stream,long limit)
    {
        using(var output=new MemoryStream()) { byte[] buffer=new byte[81920]; int n; while((n=stream.Read(buffer,0,buffer.Length))>0) { if(output.Length+n>limit) throw new IOException("解压内容过大"); output.Write(buffer,0,n); } return output.ToArray(); }
    }
    private static string Decode(byte[] bytes)
    {
        if(bytes.Length>=2 && bytes[0]==255 && bytes[1]==254) return Encoding.Unicode.GetString(bytes,2,bytes.Length-2);
        if(bytes.Length>=2 && bytes[0]==254 && bytes[1]==255) return Encoding.BigEndianUnicode.GetString(bytes,2,bytes.Length-2);
        if(bytes.Length>=3 && bytes[0]==239 && bytes[1]==187 && bytes[2]==191) return new UTF8Encoding(false,true).GetString(bytes,3,bytes.Length-3);
        try { return new UTF8Encoding(false,true).GetString(bytes); }
        catch(DecoderFallbackException) { return Encoding.GetEncoding(54936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback).GetString(bytes); }
    }
    private static string ReadDocx(byte[] bytes)
    {
        using(var archive=new ZipArchive(new MemoryStream(bytes),ZipArchiveMode.Read))
        {
            var entry=archive.GetEntry("word/document.xml");
            if(entry==null || entry.Length>MaxFile) throw new IOException("Word 文档格式不完整或正文过大");
            var text=new StringBuilder();
            using(var stream=entry.Open())
            using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaxFile}))
            {
                bool inText=false;
                while(reader.Read())
                {
                    if(reader.NamespaceURI=="http://schemas.openxmlformats.org/wordprocessingml/2006/main")
                    {
                        if(reader.NodeType==XmlNodeType.Element && reader.LocalName=="t") inText=true;
                        if(reader.NodeType==XmlNodeType.EndElement && reader.LocalName=="t") inText=false;
                        if(reader.NodeType==XmlNodeType.EndElement && reader.LocalName=="p") text.Append('\n');
                        if(reader.NodeType==XmlNodeType.Element && (reader.LocalName=="br" || reader.LocalName=="cr")) text.Append('\n');
                        if(reader.NodeType==XmlNodeType.Element && reader.LocalName=="tab") text.Append('\t');
                    }
                    else if(inText && (reader.NodeType==XmlNodeType.Text || reader.NodeType==XmlNodeType.SignificantWhitespace)) text.Append(reader.Value);
                }
            }
            return text.ToString();
        }
    }
}

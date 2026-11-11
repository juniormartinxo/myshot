using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace MyShot {
 // Versão instalada e repositório de releases. Ao publicar, a tag da release deve ser vX.Y.Z com este mesmo número.
 internal static class AppInfo {
  internal const string Version="0.1.0";
  internal const string Repository="juniormartinxo/myshot";
 }

 [DataContract] internal sealed class ReleaseAsset {
  [DataMember(Name="name")] public string Name {get;set;}
  [DataMember(Name="browser_download_url")] public string Url {get;set;}
 }

 [DataContract] internal sealed class Release {
  [DataMember(Name="tag_name")] public string Tag {get;set;}
  [DataMember(Name="assets")] public ReleaseAsset[] Assets {get;set;}
 }

 // Atualização só acontece por ação explícita do usuário, nunca em segundo plano.
 internal static class Updates {
  const string LatestUrl="https://api.github.com/repos/"+AppInfo.Repository+"/releases/latest";
  const string ExeName="MyShot.exe", ChecksumName="MyShot.exe.sha256";
  static readonly string[] TrustedHosts={"api.github.com","github.com","objects.githubusercontent.com"};

  internal static Version Parse(string tag){
   Version version;
   return Version.TryParse((tag??"").Trim().TrimStart('v','V'),out version)?version:null;
  }

  internal static bool IsNewer(string tag){
   Version latest=Parse(tag),current=Parse(AppInfo.Version);
   return latest!=null&&current!=null&&latest>current;
  }

  internal static bool IsTrusted(string url){
   Uri uri;
   if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!=Uri.UriSchemeHttps)return false;
   foreach(var host in TrustedHosts)if(uri.Host==host)return true;
   return false;
  }

  // Aceita o formato do sha256sum ("<hash>  <nome>") ou só o hash.
  internal static string ExpectedHash(string checksumFile,string fileName){
   foreach(var line in checksumFile.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){
    var parts=line.Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
    if(parts.Length==0||parts[0].Length!=64)continue;
    if(parts.Length==1||Path.GetFileName(parts[parts.Length-1].TrimStart('*'))==fileName)return parts[0].ToLowerInvariant();
   }
   return null;
  }

  internal static bool HashMatches(byte[] data,string expected){
   if(expected==null)return false;
   using(var sha=SHA256.Create()){
    var actual=new StringBuilder(64);
    foreach(var b in sha.ComputeHash(data))actual.Append(b.ToString("x2"));
    return String.Equals(actual.ToString(),expected,StringComparison.OrdinalIgnoreCase);
   }
  }

  internal static ReleaseAsset Find(Release release,string name){
   if(release.Assets!=null)foreach(var asset in release.Assets)if(asset.Name==name&&IsTrusted(asset.Url))return asset;
   return null;
  }

  // Retorna null quando o repositório ainda não tem nenhuma release publicada.
  internal static Release Latest(){
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   byte[] json;
   try{json=Get(LatestUrl,15000,"application/vnd.github+json");}
   catch(WebException e){var response=e.Response as HttpWebResponse;if(response!=null&&response.StatusCode==HttpStatusCode.NotFound)return null;throw;}
   using(var stream=new MemoryStream(json))return (Release)new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Release)).ReadObject(stream);
  }

  internal static byte[] Download(ReleaseAsset asset){return Get(asset.Url,120000,"application/octet-stream");}

  static byte[] Get(string url,int timeoutMs,string accept){
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   var request=(HttpWebRequest)WebRequest.Create(url);
   request.UserAgent="MyShot/"+AppInfo.Version;request.Accept=accept;request.Timeout=timeoutMs;request.ReadWriteTimeout=timeoutMs;
   using(var response=(HttpWebResponse)request.GetResponse())using(var stream=response.GetResponseStream())using(var buffer=new MemoryStream()){
    stream.CopyTo(buffer);return buffer.ToArray();
   }
  }

  // Baixa e confere o SHA-256 antes de tocar no executável atual.
  internal static byte[] DownloadVerified(Release release){
   var exe=Find(release,ExeName);var checksum=Find(release,ChecksumName);
   if(exe==null||checksum==null)throw new InvalidOperationException("A release "+release.Tag+" não tem os arquivos de instalação.");
   var data=Download(exe);
   var expected=ExpectedHash(Encoding.UTF8.GetString(Download(checksum)),ExeName);
   if(!HashMatches(data,expected))throw new InvalidDataException("A verificação de integridade do arquivo baixado falhou.");
   return data;
  }

  // No Windows, o executável em uso pode ser renomeado, mas não sobrescrito.
  internal static void Install(byte[] newExe){
   string exe=Application.ExecutablePath,next=exe+".new",old=exe+".old";
   File.WriteAllBytes(next,newExe);
   try{
    if(File.Exists(old))File.Delete(old);
    File.Move(exe,old);
    try{File.Move(next,exe);}
    catch(Exception){File.Move(old,exe);throw;}
   }finally{if(File.Exists(next))File.Delete(next);}
  }

  internal static void Relaunch(){
   Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--updated"){UseShellExecute=false});
  }

  // Remove o .old deixado pela atualização anterior; se ainda estiver em uso, tenta de novo na próxima abertura.
  internal static void CleanupPrevious(){
   try{var old=Application.ExecutablePath+".old";if(File.Exists(old))File.Delete(old);}
   catch(IOException){}catch(UnauthorizedAccessException){}
  }
 }
}

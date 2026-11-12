using System;
using System.Collections.Generic;
using System.Globalization;
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
  internal const string Version="0.0.4";
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
  const string ExeName="MyShot.exe", ChecksumName="MyShot.exe.sha256", ManifestName="MyShot.manifest", SignatureName="MyShot.manifest.sig";
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
   try{json=Get(LatestUrl,15000,"application/vnd.github+json",1000000);}
   catch(WebException e){var response=e.Response as HttpWebResponse;if(response!=null&&response.StatusCode==HttpStatusCode.NotFound)return null;throw;}
   using(var stream=new MemoryStream(json))return (Release)new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Release)).ReadObject(stream);
  }

  internal static byte[] Download(ReleaseAsset asset){return Get(asset.Url,120000,"application/octet-stream",100000000);}

  // Cada redirecionamento é conferido contra a lista de hosts confiáveis antes de ser seguido; o corpo tem limite de tamanho.
  const int MaxRedirects=5;
  static byte[] Get(string url,int timeoutMs,string accept,int maxBytes){
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   using(var response=OpenChecked(url,timeoutMs,accept))using(var stream=response.GetResponseStream())using(var buffer=new MemoryStream()){
    if(response.ContentLength>maxBytes)throw new InvalidDataException("Resposta maior que o limite permitido.");
    var chunk=new byte[81920];int read;
    while((read=stream.Read(chunk,0,chunk.Length))>0){
     if(buffer.Length+read>maxBytes)throw new InvalidDataException("Resposta maior que o limite permitido.");
     buffer.Write(chunk,0,read);
    }
    return buffer.ToArray();
   }
  }

  static HttpWebResponse OpenChecked(string url,int timeoutMs,string accept){
   for(int hop=0;hop<=MaxRedirects;hop++){
    if(!IsTrusted(url))throw new InvalidDataException("Endereço fora dos hosts confiáveis: "+url);
    var request=(HttpWebRequest)WebRequest.Create(url);
    request.UserAgent="MyShot/"+AppInfo.Version;request.Accept=accept;request.Timeout=timeoutMs;request.ReadWriteTimeout=timeoutMs;
    request.AllowAutoRedirect=false;
    var response=(HttpWebResponse)request.GetResponse();
    int code=(int)response.StatusCode;
    if(code==301||code==302||code==303||code==307||code==308){
     string location=response.Headers["Location"];
     response.Close();
     if(string.IsNullOrEmpty(location))throw new WebException("Redirecionamento sem destino.");
     url=new Uri(new Uri(url),location).AbsoluteUri;
     continue;
    }
    return response;
   }
   throw new WebException("Redirecionamentos demais.");
  }
  // Manifesto assinado: versão, SHA-256 e tamanho do executável, assinados com RSA (SHA-256). A chave pública
  // que valida a assinatura vem embutida no app (assets/update-public-key.xml), não da release.
  internal sealed class SignedManifest {
   internal string Version,Sha256;internal long Size;
  }

  internal static SignedManifest ReadSignedManifest(Release release){
   var manifestAsset=Find(release,ManifestName);var signatureAsset=Find(release,SignatureName);
   if(manifestAsset==null||signatureAsset==null)throw new InvalidOperationException("A release "+release.Tag+" não tem o manifesto assinado.");
   byte[] manifest=Download(manifestAsset);
   byte[] signature=Convert.FromBase64String(Encoding.ASCII.GetString(Download(signatureAsset)).Trim());
   if(!VerifySignature(manifest,signature,EmbeddedPublicKey()))throw new InvalidDataException("A assinatura do manifesto não é válida.");
   var manifestData=ParseManifest(Encoding.UTF8.GetString(manifest));
   if(manifestData.Version!=release.Tag.TrimStart('v','V'))throw new InvalidDataException("A versão do manifesto não corresponde à release.");
   return manifestData;
  }

  internal static SignedManifest ParseManifest(string text){
   var values=new Dictionary<string,string>();
   foreach(var line in text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){
    int eq=line.IndexOf('=');if(eq<=0)continue;
    values[line.Substring(0,eq).Trim()]=line.Substring(eq+1).Trim();
   }
   string version,sha256,size;
   if(!values.TryGetValue("version",out version)||!values.TryGetValue("sha256",out sha256)||!values.TryGetValue("size",out size))throw new InvalidDataException("Manifesto incompleto.");
   long length;
   if(!long.TryParse(size,NumberStyles.None,CultureInfo.InvariantCulture,out length)||length<=0||sha256.Length!=64)throw new InvalidDataException("Manifesto inválido.");
   return new SignedManifest{Version=version,Sha256=sha256.ToLowerInvariant(),Size=length};
  }

  internal static bool VerifySignature(byte[] data,byte[] signature,string publicKeyXml){
   using(var rsa=new RSACryptoServiceProvider()){
    rsa.FromXmlString(publicKeyXml);
    return rsa.VerifyData(data,"SHA256",signature);
   }
  }

  static string EmbeddedPublicKey(){
   using(var stream=typeof(Updates).Assembly.GetManifestResourceStream("MyShot.update-public-key.xml")){
    if(stream==null)throw new InvalidOperationException("Chave pública de atualização ausente no executável.");
    using(var reader=new StreamReader(stream))return reader.ReadToEnd();
   }
  }

  // Baixa o executável e confere tamanho e SHA-256 contra o manifesto já validado pela assinatura.
  internal static byte[] DownloadVerified(Release release,SignedManifest manifest){
   var exe=Find(release,ExeName);
   if(exe==null)throw new InvalidOperationException("A release "+release.Tag+" não tem o executável.");
   var data=Download(exe);
   if(data.LongLength!=manifest.Size||!HashMatches(data,manifest.Sha256))throw new InvalidDataException("O executável baixado não confere com o manifesto assinado.");
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

using System;
using System.IO;
using Microsoft.Win32;

namespace MyShot {
 // HKCU Run applies to the signed-in user. No machine-wide registration or elevation.
 internal sealed class WindowsStartup {
  internal const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
  readonly RegistryKey root;
  readonly string keyPath;
  readonly string executable;
  internal WindowsStartup(string executablePath) : this(Registry.CurrentUser,RunKey,executablePath) {}
  internal WindowsStartup(RegistryKey rootKey,string registryPath,string executablePath){root=rootKey;keyPath=registryPath;executable=executablePath;}
  internal static string Command(string path){
   if(String.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path))throw new ArgumentException("O caminho do MyShot precisa ser absoluto.");
   string command="\""+Path.GetFullPath(path)+"\" --startup";
   if(command.Length>260)throw new InvalidOperationException("Mova o MyShot para uma pasta com caminho mais curto para iniciar com o Windows.");
   return command;
  }
  internal string RegisteredCommand(){using(var key=root.OpenSubKey(keyPath,false))return key==null?null:key.GetValue("MyShot",null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string;}
  internal bool IsEnabled(){return !String.IsNullOrWhiteSpace(RegisteredCommand());}
  internal void SetEnabled(bool enabled){
   if(enabled){string command=Command(executable);using(var key=root.CreateSubKey(keyPath)){if(key==null)throw new IOException("Não foi possível configurar a inicialização do MyShot.");key.SetValue("MyShot",command,RegistryValueKind.String);}}
   else using(var key=root.OpenSubKey(keyPath,true)){if(key!=null)key.DeleteValue("MyShot",false);}
  }
 }
}

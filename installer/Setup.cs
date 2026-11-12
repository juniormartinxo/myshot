using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace MyShotSetup {
 // Instalador do MyShot: copia o executável para %LOCALAPPDATA%\Programs\MyShot (pasta do usuário, sem administrador),
 // cria o atalho no menu Iniciar e abre o app. O executável é embutido como recurso durante o build.
 internal static class Program {
  [STAThread] static void Main(){
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   Application.Run(new SetupForm());
  }
 }

 internal sealed class SetupForm : Form {
  readonly string installDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","MyShot");
  readonly Label status=new Label{AutoSize=false,ForeColor=Color.FromArgb(155,161,172),Font=new Font("Segoe UI",9)};
  readonly Button install=new Button{Text="Instalar",Width=140,Height=36,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(43,111,214),ForeColor=Color.White,Font=new Font("Segoe UI",10,FontStyle.Bold)};

  internal SetupForm(){
   Text="MyShot · Instalação";ClientSize=new Size(460,230);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
   BackColor=Color.FromArgb(36,39,44);ForeColor=Color.FromArgb(232,235,240);Font=new Font("Segoe UI",10);
   var title=new Label{Text="Instalar MyShot",AutoSize=true,Font=new Font("Segoe UI",18,FontStyle.Bold),Location=new Point(28,22)};
   var info=new Label{Text="O MyShot será instalado em:\n"+installDir,AutoSize=false,Location=new Point(28,72),Size=new Size(404,44),ForeColor=Color.FromArgb(232,235,240)};
   status.Text="Não precisa de permissão de administrador.";status.Location=new Point(28,130);status.Size=new Size(404,40);
   install.Location=new Point(28,176);
   install.FlatAppearance.BorderSize=0;
   install.Click+=(s,e)=>Install();
   Controls.Add(title);Controls.Add(info);Controls.Add(status);Controls.Add(install);
   AcceptButton=install;
  }

  void Install(){
   install.Enabled=false;
   try{
    Directory.CreateDirectory(installDir);
    string exe=Path.Combine(installDir,"MyShot.exe");
    CopyResource("MyShot.exe",exe);
    CopyResource("MyShot.exe.config",Path.Combine(installDir,"MyShot.exe.config"));
    CreateShortcut(exe);
    status.Text="Instalado com sucesso. O MyShot foi aberto e fica na bandeja.";
    Process.Start(new ProcessStartInfo(exe){UseShellExecute=true});
    Close();
   }catch(Exception e){
    install.Enabled=true;
    status.Text="Não foi possível instalar: "+e.Message;
   }
  }

  static void CopyResource(string resourceName,string target){
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
   using(var file=File.Create(target)){
    if(stream==null)throw new InvalidOperationException("Arquivo do instalador ausente: "+resourceName);
    stream.CopyTo(file);
   }
  }

  // Atalho do menu Iniciar criado pelo WScript.Shell (COM), sem dependências extras.
  static void CreateShortcut(string exe){
   string programs=Environment.GetFolderPath(Environment.SpecialFolder.Programs);
   string link=Path.Combine(programs,"MyShot.lnk");
   Type shellType=Type.GetTypeFromProgID("WScript.Shell");
   object shell=Activator.CreateInstance(shellType);
   object shortcut=shellType.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{link});
   Type shortcutType=shortcut.GetType();
   shortcutType.InvokeMember("TargetPath",BindingFlags.SetProperty,null,shortcut,new object[]{exe});
   shortcutType.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,shortcut,new object[]{Path.GetDirectoryName(exe)});
   shortcutType.InvokeMember("Description",BindingFlags.SetProperty,null,shortcut,new object[]{"MyShot · Captura de tela"});
   shortcutType.InvokeMember("Save",BindingFlags.InvokeMethod,null,shortcut,null);
  }
 }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MyShot {
 // Janela oculta: guarda os atalhos globais, o ícone da bandeja e o estado da captura.
 // A interface visível é a barra de captura (CaptureBar), o editor e as preferências.
 internal sealed class Home : Form {
  internal Preferences Prefs;
  readonly NotifyIcon tray;
  readonly CaptureBar bar;
  readonly ContextMenuStrip moreMenu=new ContextMenuStrip();
  readonly ToolStripMenuItem updateItem;
  bool capturing;
  static string lastConflict;
  readonly List<Editor> editors=new List<Editor>();

  internal Home(bool startInTray=false) {
   UI.Style(this);Text="MyShot";ShowInTaskbar=false;
   string warning;Prefs=Preferences.Load(Preferences.PathName,out warning);
   CreateHandle();
   bar=new CaptureBar(this);
   updateItem=new ToolStripMenuItem("Atualizar",null,(s,e)=>CheckForUpdates());
   moreMenu.Items.Add(updateItem);moreMenu.Items.Add("Ir para a bandeja",null,(s,e)=>bar.Hide());moreMenu.Items.Add(new ToolStripSeparator());moreMenu.Items.Add("Sair",null,(s,e)=>Exit());
   var menu=new ContextMenuStrip();menu.Items.Add("Capturar região",null,(s,e)=>BeginCapture(false,false));menu.Items.Add("Salvar desktop direto",null,(s,e)=>BeginCapture(true,true));menu.Items.Add("Abrir MyShot",null,(s,e)=>Restore());menu.Items.Add("Preferências",null,(s,e)=>{Restore();OpenSettings();});menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Sair",null,(s,e)=>Exit());
   tray=new NotifyIcon{Text="MyShot · Captura de tela",Icon=Theme.AppIcon,ContextMenuStrip=menu,Visible=true};tray.DoubleClick+=(s,e)=>Restore();
   RegisterKeys();
   if(warning!=null)tray.ShowBalloonTip(4000,"MyShot",warning,ToolTipIcon.Warning);
   if(!startInTray)Restore();
  }

  // Nunca mostra a própria janela: o app abre a barra de captura, não esta janela.
  protected override void SetVisibleCore(bool value){base.SetVisibleCore(false);}

  internal bool BarVisible{get{return bar.Visible;}}
  internal Form Bar{get{return bar;}}
  internal void ShowBar(){bar.Present();}
  internal void ShowMoreMenu(){moreMenu.Show(Cursor.Position);}
  void Restore(){ShowBar();}
  IWin32Window DialogOwner(){return bar.Visible?(IWin32Window)bar:this;}
  void Exit(){Close();}
  internal void Persist(){Prefs.Save(Preferences.PathName);}

  internal void RegisterKeys() {
   Native.UnregisterHotKey(Handle,1);Native.UnregisterHotKey(Handle,2);
   uint mods=0x4000u|2u|(Prefs.ShiftInsteadOfAlt?4u:1u);
   bool a=Native.RegisterHotKey(Handle,1,mods,(uint)Prefs.CaptureKey);
   bool b=Native.RegisterHotKey(Handle,2,mods,(uint)Prefs.FileKey);
   string prefix=Prefs.ShiftInsteadOfAlt?"Ctrl+Shift+":"Ctrl+Alt+";
   string capture=prefix+(Keys)Prefs.CaptureKey,save=prefix+(Keys)Prefs.FileKey;
   bar.SetShortcuts(capture,save,a,b);
   // Balão só quando o conflito muda; a dica da barra continua sempre mostrando o estado.
   string conflict=(!a?capture+" ":"")+(!b?save:"");
   if(conflict.Length==0)lastConflict=null;
   else if(conflict!=lastConflict){lastConflict=conflict;tray.ShowBalloonTip(4000,"MyShot","Atalho em uso: "+conflict.Trim()+". Altere em Preferências.",ToolTipIcon.Warning);}
  }

  protected override void WndProc(ref Message m){if(m.Msg==0x0312){BeginCapture(m.WParam.ToInt32()==2,m.WParam.ToInt32()==2);return;}base.WndProc(ref m);}

  internal void OpenSettings(){
   using(var f=new SettingsDialog(Prefs)){
    IWin32Window owner=DialogOwner();
    if(f.ShowDialog(owner)==DialogResult.OK){
     bool themeChanged=f.Value.Appearance!=Prefs.Appearance;
     try{f.Value.Save(Preferences.PathName);Prefs=f.Value;RegisterKeys();}catch(Exception e){UI.Error(owner,e);return;}
     if(themeChanged)MessageBox.Show(owner,"O novo tema será aplicado na próxima abertura do MyShot.","MyShot",MessageBoxButtons.OK,MessageBoxIcon.Information);
     try{f.ApplyStartup();}catch(Exception e){MessageBox.Show(owner,"As outras preferências foram salvas, mas não foi possível alterar a inicialização com o Windows.\n\n"+e.Message,"MyShot",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
   }
  }

  internal async void BeginCapture(bool direct,bool whole) {
   if(capturing)return;capturing=true;
   bool restore=bar.Visible;var visibleEditors=editors.FindAll(x=>x.Visible);Editor opened=null;
   try {
    bar.Hide();foreach(var editor in visibleEditors)editor.Hide();
    await System.Threading.Tasks.Task.Delay(180);Native.DwmFlush();
    Rectangle bounds=SystemInformation.VirtualScreen;
    using(var desktop=DesktopCapture.Take(bounds)) {
     Bitmap selected=null;
     try {
      if(whole)selected=(Bitmap)desktop.Clone();
      else using(var selection=new Selection(desktop,bounds)){if(selection.ShowDialog()==DialogResult.OK)selected=desktop.Clone(selection.Selected,System.Drawing.Imaging.PixelFormat.Format32bppArgb);}
      if(selected==null)return;
      if(Prefs.Sound)System.Media.SystemSounds.Asterisk.Play();
      if(direct){Directory.CreateDirectory(Prefs.Folder);string path=Path.Combine(Prefs.Folder,"MyShot-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+"."+Prefs.Format);ImageOutput.Save(selected,path);tray.ShowBalloonTip(2500,"Imagem salva",path,ToolTipIcon.Info);}
      else {var editor=new Editor(selected,this);opened=editor;editors.Add(editor);editor.FormClosed+=(s,e)=>editors.Remove(editor);editor.Show();}
     }finally{if(selected!=null)selected.Dispose();}
    }
   }catch(Exception e){Restore();MessageBox.Show(DialogOwner(),"Não foi possível capturar o desktop. Execute na sessão interativa do Windows, fora da tela de bloqueio.\n\n"+e.Message,"MyShot",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   finally{capturing=false;foreach(var editor in visibleEditors)if(!editor.IsDisposed)editor.Show();if(restore)Restore();if(opened!=null&&!opened.IsDisposed){opened.BringToFront();opened.Activate();}}
  }

  // Ação explícita do botão Atualizar: consulta a última release, confirma com o usuário e instala.
  async void CheckForUpdates(){
   SetUpdateBusy(true,"Verificando…");
   Release release=null;string error=null;
   try{release=await System.Threading.Tasks.Task.Run(()=>Updates.Latest());}catch(Exception e){error=e.Message;}
   SetUpdateBusy(false,"Atualizar");
   if(error!=null){MessageBox.Show(DialogOwner(),"Não foi possível verificar atualizações. Confira sua conexão e tente novamente.\n\n"+error,"MyShot",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
   if(release==null||!Updates.IsNewer(release.Tag)){MessageBox.Show(DialogOwner(),"Você já está na versão mais recente ("+AppInfo.Version+").","MyShot",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
   if(Updates.Find(release,"MyShot.exe")==null||Updates.Find(release,"MyShot.exe.sha256")==null){MessageBox.Show(DialogOwner(),"A versão "+release.Tag+" existe, mas ainda não tem os arquivos de instalação publicados.","MyShot",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
   if(MessageBox.Show(DialogOwner(),"Nova versão disponível: "+release.Tag+" (atual: "+AppInfo.Version+").\n\nBaixar e reiniciar o MyShot agora?","MyShot",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
   SetUpdateBusy(true,"Baixando…");
   try{
    var data=await System.Threading.Tasks.Task.Run(()=>Updates.DownloadVerified(release));
    Updates.Install(data);
    Updates.Relaunch();
    Exit();
   }catch(Exception e){
    SetUpdateBusy(false,"Atualizar");
    MessageBox.Show(DialogOwner(),"Não foi possível instalar a atualização. O MyShot atual continua instalado.\n\n"+e.Message,"MyShot",MessageBoxButtons.OK,MessageBoxIcon.Error);
   }
  }
  void SetUpdateBusy(bool busy,string text){updateItem.Enabled=!busy;updateItem.Text=text;}

  protected override void Dispose(bool disposing){
   if(disposing){
    Native.UnregisterHotKey(Handle,1);Native.UnregisterHotKey(Handle,2);
    tray.Visible=false;tray.ContextMenuStrip.Dispose();tray.Dispose();moreMenu.Dispose();bar.Dispose();
    foreach(var e in editors.ToArray())e.Dispose();
   }
   base.Dispose(disposing);
  }
 }
}

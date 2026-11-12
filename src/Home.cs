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
  CaptureBar bar;
  readonly ContextMenuStrip moreMenu=new ContextMenuStrip();
  readonly ToolStripMenuItem updateItem;
  bool capturing;
  static string lastConflict;
  // Testes ligam este modo para não criar notificações do Windows a cada execução.
  internal static bool Quiet{get;set;}
  readonly List<Editor> editors=new List<Editor>();

  internal Home(bool startInTray=false) {
   UI.Style(this);Text="MyShot";ShowInTaskbar=false;
   string warning;Prefs=Preferences.Load(Preferences.PathName,out warning);
   CreateHandle();
   bar=new CaptureBar(this);
   bar.ApplyOpacity(Prefs.BarOpacity);
   updateItem=new ToolStripMenuItem("Atualizar",null,(s,e)=>CheckForUpdates());
   moreMenu.Items.Add(updateItem);moreMenu.Items.Add("Ir para a bandeja",null,(s,e)=>bar.Hide());moreMenu.Items.Add(new ToolStripSeparator());moreMenu.Items.Add("Sair",null,(s,e)=>Exit());
   var menu=new ContextMenuStrip();menu.Items.Add("Capturar região",null,(s,e)=>BeginCapture(false,false));menu.Items.Add("Capturar área livre",null,(s,e)=>BeginCapture(false,false,false,true));menu.Items.Add("Repetir última região",null,(s,e)=>BeginCapture(false,false,true));menu.Items.Add("Salvar desktop direto",null,(s,e)=>BeginCapture(true,true));menu.Items.Add("Abrir MyShot",null,(s,e)=>Restore());menu.Items.Add("Preferências",null,(s,e)=>{Restore();OpenSettings();});menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Sair",null,(s,e)=>Exit());
   tray=new NotifyIcon{Text="MyShot · Captura de tela",Icon=Theme.AppIcon,ContextMenuStrip=menu,Visible=true};tray.DoubleClick+=(s,e)=>Restore();
   RegisterKeys();
   if(warning!=null&&!Quiet)tray.ShowBalloonTip(4000,"MyShot",warning,ToolTipIcon.Warning);
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
   Native.UnregisterHotKey(Handle,1);Native.UnregisterHotKey(Handle,2);Native.UnregisterHotKey(Handle,3);
   uint mods=0x4000u|(uint)Prefs.HotkeyModifiers;
   bool a=Native.RegisterHotKey(Handle,1,mods,(uint)Prefs.CaptureKey);
   bool b=Native.RegisterHotKey(Handle,2,mods,(uint)Prefs.FileKey);
   bool c=Native.RegisterHotKey(Handle,3,mods,(uint)Prefs.FreeKey);
   string prefix=Preferences.ModifierText(Prefs.HotkeyModifiers)+"+";
   string capture=prefix+(Keys)Prefs.CaptureKey,save=prefix+(Keys)Prefs.FileKey,free=prefix+(Keys)Prefs.FreeKey;
   bar.SetShortcuts(capture,save,a,b,free,c);
   // Balão só quando o conflito muda; a dica da barra continua sempre mostrando o estado.
   string conflict=(!a?capture+" ":"")+(!b?save+" ":"")+(!c?free:"");
   if(conflict.Length==0)lastConflict=null;
   else if(conflict!=lastConflict){lastConflict=conflict;if(!Quiet)tray.ShowBalloonTip(4000,"MyShot","Atalho em uso: "+conflict.Trim()+". Altere em Preferências.",ToolTipIcon.Warning);}
  }

  protected override void WndProc(ref Message m){if(m.Msg==0x0312){int id=m.WParam.ToInt32();if(id==3){BeginCapture(false,false,false,true);return;}BeginCapture(id==2,id==2);return;}base.WndProc(ref m);}

  void RecreateBar(){
   var old=bar;bool visible=old.Visible;
   bar=new CaptureBar(this);bar.TakePlaceOf(old);bar.ApplyOpacity(Prefs.BarOpacity);
   old.Dispose();
   if(visible)bar.Present();
  }

  internal void OpenSettings(){
   // Enquanto houver uma janela de edição aberta, as Preferências não abrem (a troca de tema recria a barra).
   if(editors.Count>0){MessageBox.Show(DialogOwner(),"Feche as janelas de edição antes de abrir as Preferências.","MyShot",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
   using(var f=new SettingsDialog(Prefs)){
    IWin32Window owner=DialogOwner();
    if(f.ShowDialog(owner)==DialogResult.OK){
     bool themeChanged=f.Value.Appearance!=Prefs.Appearance;
     f.Value.SetLastRegion(Prefs.LastRegion);
     try{
      f.Value.Save(Preferences.PathName);Prefs=f.Value;
      // O tema é aplicado na hora: as cores da barra ficam fixadas na criação, então ela é recriada.
      if(themeChanged){Theme.Apply(Prefs.Appearance!="light");RecreateBar();}
      RegisterKeys();bar.ApplyOpacity(Prefs.BarOpacity);
     }catch(Exception e){UI.Error(owner,e);return;}
     try{f.ApplyStartup();}catch(Exception e){MessageBox.Show(owner,"As outras preferências foram salvas, mas não foi possível alterar a inicialização com o Windows.\n\n"+e.Message,"MyShot",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
   }
  }

  internal async void BeginCapture(bool direct,bool whole,bool reuse=false,bool freeform=false) {
   if(capturing)return;capturing=true;
   bool restore=bar.Visible;var visibleEditors=editors.FindAll(x=>x.Visible);Editor opened=null;
   try {
    bar.Hide();foreach(var editor in visibleEditors)editor.Hide();
    await System.Threading.Tasks.Task.Delay(180);Native.DwmFlush();
    Rectangle bounds=SystemInformation.VirtualScreen;
    using(var desktop=DesktopCapture.Take(bounds)) {
     Bitmap selected=null;
     try {
      bool transparent=false;
      if(whole)selected=(Bitmap)desktop.Clone();
      else if(freeform){
       // Área livre: o polígono recorta a imagem e o que fica fora dele sai transparente (somente PNG).
       using(var polygon=new PolygonSelection(desktop,bounds)){
        if(polygon.ShowDialog()==DialogResult.OK){selected=PolygonCapture.Crop(desktop,polygon.Corners);transparent=selected!=null;}
       }
      }
      else {
       // "Repetir" usa a última região sem perguntar; se ela não couber mais na tela, abre a seleção.
       Rectangle? fixedRegion=reuse?ToDesktop(Prefs.LastRegion,bounds):null;
       if(fixedRegion.HasValue)selected=desktop.Clone(fixedRegion.Value,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
       else using(var selection=new Selection(desktop,bounds,ToDesktop(Prefs.LastRegion,bounds))){
        if(selection.ShowDialog()==DialogResult.OK){
         selected=desktop.Clone(selection.Selected,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
         RememberRegion(selection.Selected,bounds);
        }
       }
      }
      if(selected==null)return;
      if(Prefs.Sound)CaptureSound.Play();
      if(direct){Directory.CreateDirectory(Prefs.Folder);string path=Path.Combine(Prefs.Folder,"MyShot-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+"."+Prefs.Format);ImageOutput.Save(selected,path);tray.ShowBalloonTip(2500,"Imagem salva",path,ToolTipIcon.Info);}
      else {var editor=new Editor(selected,this){KeepsTransparency=transparent};opened=editor;editors.Add(editor);editor.FormClosed+=(s,e)=>editors.Remove(editor);editor.Show();}
     }finally{if(selected!=null)selected.Dispose();}
    }
   }catch(Exception e){Restore();MessageBox.Show(DialogOwner(),"Não foi possível capturar o desktop. Execute na sessão interativa do Windows, fora da tela de bloqueio.\n\n"+e.Message,"MyShot",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   finally{capturing=false;foreach(var editor in visibleEditors)if(!editor.IsDisposed)editor.Show();if(restore)Restore();if(opened!=null&&!opened.IsDisposed){opened.BringToFront();opened.Activate();}}
  }

  // Converte uma região da tela virtual para coordenadas do desktop capturado, limitada a ele.
  static Rectangle? ToDesktop(Rectangle? screenRegion,Rectangle bounds){
   if(!screenRegion.HasValue)return null;
   var r=screenRegion.Value;r.Offset(-bounds.X,-bounds.Y);
   r=Rectangle.Intersect(new Rectangle(Point.Empty,bounds.Size),r);
   return r.Width>1&&r.Height>1?r:(Rectangle?)null;
  }
  // Guarda a última região em coordenadas da tela virtual. Se não for possível gravar, a captura continua normalmente.
  void RememberRegion(Rectangle desktopRegion,Rectangle bounds){
   var r=desktopRegion;r.Offset(bounds.X,bounds.Y);
   Prefs.SetLastRegion(r);
   try{Persist();}catch(Exception){}
  }

  // Ação explícita do botão Atualizar: consulta a última release, confirma com o usuário e instala.
  async void CheckForUpdates(){
   SetUpdateBusy(true,"Verificando…");
   Release release=null;string error=null;
   try{release=await System.Threading.Tasks.Task.Run(()=>Updates.Latest());}catch(Exception e){error=e.Message;}
   SetUpdateBusy(false,"Atualizar");
   if(error!=null){MessageBox.Show(DialogOwner(),"Não foi possível verificar atualizações. Confira sua conexão e tente novamente.\n\n"+error,"MyShot",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
   if(release==null){MessageBox.Show(DialogOwner(),"Ainda não há nenhuma versão publicada para atualizar.","MyShot",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
   if(!Updates.IsNewer(release.Tag)){MessageBox.Show(DialogOwner(),"Você já está na versão mais recente ("+AppInfo.Version+").","MyShot",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
   // A assinatura do manifesto é validada antes de perguntar: uma release não assinada corretamente nem é oferecida.
   Updates.SignedManifest manifest;
   try{manifest=await System.Threading.Tasks.Task.Run(()=>Updates.ReadSignedManifest(release));}
   catch(Exception e){MessageBox.Show(DialogOwner(),"A versão "+release.Tag+" não passou na verificação de assinatura, então não será oferecida.\n\n"+e.Message,"MyShot",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
   if(MessageBox.Show(DialogOwner(),"Nova versão disponível: "+release.Tag+" (atual: "+AppInfo.Version+").\n\nBaixar e reiniciar o MyShot agora?","MyShot",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
   SetUpdateBusy(true,"Baixando…");
   try{
    var data=await System.Threading.Tasks.Task.Run(()=>Updates.DownloadVerified(release,manifest));
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
    Native.UnregisterHotKey(Handle,1);Native.UnregisterHotKey(Handle,2);Native.UnregisterHotKey(Handle,3);
    tray.Visible=false;tray.ContextMenuStrip.Dispose();tray.Dispose();moreMenu.Dispose();bar.Dispose();
    foreach(var e in editors.ToArray())e.Dispose();
   }
   base.Dispose(disposing);
  }
 }
}

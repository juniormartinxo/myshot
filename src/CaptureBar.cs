using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MyShot {
 // Barra pequena no topo da tela, no estilo da barra de captura do Windows. É a única interface que o app mostra ao abrir.
 internal sealed class CaptureBar : Form {
  readonly Home home;
  readonly ToolStrip strip=new ToolStrip{GripStyle=ToolStripGripStyle.Hidden,Dock=DockStyle.Fill,BackColor=Theme.Surface,Renderer=new ShotStripRenderer(true),ImageScalingSize=new Size(Theme.BarIconSize,Theme.BarIconSize),Padding=new Padding(24,4,24,12)};
  readonly DragGrip grip;
  readonly ToolStripButton region,freeform,desktop,direct;

  internal CaptureBar(Home owner){
   home=owner;UI.Style(this);
   FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;KeyPreview=true;BackColor=Theme.Surface;
   // Sempre na frente das outras aplicações; só some ao fechar ou ir para a bandeja (e durante a captura).
   TopMost=true;
   // Vidro escuro: a janela inteira fica semitransparente, sem cantos arredondados.
   region=Add("Capturar região",()=>home.BeginCapture(false,false));region.Image=Theme.Icon("capture",Theme.Ink);
   freeform=Add("Área livre",()=>home.BeginCapture(false,false,false,true));freeform.Image=Theme.Icon("Polygon",Theme.Ink);freeform.ToolTipText="Capturar área livre: marque os cantos de um polígono";
   desktop=Add("Desktop inteiro",()=>home.BeginCapture(false,true));desktop.Image=Theme.Icon("desktop",Theme.Ink);desktop.ToolTipText="Capturar o desktop inteiro, com todas as telas";
   direct=Add("Salvar desktop direto",()=>home.BeginCapture(true,true));direct.Image=Theme.Icon("save",Theme.Ink);
   strip.Items.Add(new ToolStripSeparator());
   var prefs=Add("Preferências",()=>home.OpenSettings());prefs.Image=Theme.Icon("style",Theme.Ink);prefs.ToolTipText="Preferências: pasta, formato, atalhos e aparência";
   var more=Add("Mais opções",()=>home.ShowMoreMenu());more.Image=Theme.Icon("more",Theme.Ink);more.ToolTipText="Mais opções: atualizar o MyShot e outras ações";
   var close=Add("Fechar barra (o MyShot continua na bandeja)",()=>Hide());close.Image=Theme.Icon("close",Theme.Ink);
   // Alça de arrasto embaixo da barra; o fundo dos botões também arrasta, sem cursor de movimento sobre a barra inteira.
   grip=new DragGrip(StartDrag){Dock=DockStyle.Bottom,Height=Dip(24)};
   Controls.Add(grip);Controls.Add(strip);
   var content=strip.GetPreferredSize(Size.Empty);
   ClientSize=new Size(content.Width,content.Height+grip.Height);
   KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape)Hide();};
   strip.MouseDown+=(s,e)=>{
    if(e.Button!=MouseButtons.Left)return;
    var item=strip.GetItemAt(e.Location);
    if(item==null||item is ToolStripSeparator)StartDrag();
   };
   // Mão só sobre os botões; o resto da barra continua arrastando a janela.
   strip.MouseMove+=(s,e)=>{strip.Cursor=strip.GetItemAt(e.Location) is ToolStripButton?Cursors.Hand:Cursors.Default;};
  }

  [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ReleaseCapture();
  [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam);
  const int WmNcLButtonDown=0xA1,HtCaption=2;
  bool placed;

  // Move a janela como se o usuário tivesse pego a barra de título.
  void StartDrag(){ReleaseCapture();SendMessage(Handle,WmNcLButtonDown,(IntPtr)HtCaption,IntPtr.Zero);}

  // Sombra do Windows (CS_DROPSHADOW) para a janela sem borda; a barra fica flutuando com uma sombra suave.
  protected override CreateParams CreateParams{get{var cp=base.CreateParams;cp.ClassStyle|=0x00020000;return cp;}}
  // Cantos suaves pelo DWM (Windows 11): região recortada não tem antialias e serrilha a borda.
  [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
  protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);int corner=2;try{DwmSetWindowAttribute(Handle,33,ref corner,4);}catch(Exception){}}
  // Opacidade configurável em Preferências (porcentagem).
  internal void ApplyOpacity(int percent){Opacity=Math.Max(0.3,Math.Min(1,percent/100.0));}
  int Dip(int value){return (int)Math.Round(value*DeviceDpi/96f);}

  ToolStripButton Add(string text,Action action){
   // Botão quadrado: lado = ícone + margem interna dos dois lados; o ícone fica centralizado.
   int pad=Dip(8),side=Theme.BarIconSize+2*pad;
   var b=new ToolStripButton{DisplayStyle=ToolStripItemDisplayStyle.Image,AutoSize=false,Size=new Size(side,side),Padding=new Padding(pad),ImageAlign=ContentAlignment.MiddleCenter,Margin=new Padding(2,0,2,0),AccessibleName=text,ToolTipText=text};
   b.Click+=(s,e)=>action();strip.Items.Add(b);return b;
  }

  // Tooltips trazem o atalho; quando um atalho está em uso, a dica informa e aponta as Preferências.
  internal void SetShortcuts(string capture,string save,bool captureOk,bool saveOk,string free,bool freeOk){
   region.ToolTipText=captureOk?"Capturar região ("+capture+")":"Atalho "+capture+" em uso. Altere em Preferências";
   direct.ToolTipText=saveOk?"Salvar desktop direto ("+save+")":"Atalho "+save+" em uso. Altere em Preferências";
   freeform.ToolTipText=freeOk?"Capturar área livre ("+free+"): marque os cantos de um polígono":"Atalho "+free+" em uso. Altere em Preferências";
  }

  // Barra recriada ao trocar o tema: assume a posição da anterior em vez de voltar ao centro.
  internal void TakePlaceOf(CaptureBar previous){placed=true;Location=previous.Location;}

  // Primeira abertura: centralizada no topo. Depois, reabre onde o usuário a deixou, mantida dentro da área de trabalho.
  internal void Present(){
   var area=Screen.PrimaryScreen.WorkingArea;
   if(!placed){Location=new Point(area.Left+(area.Width-Width)/2,area.Top+Dip(24));placed=true;}
   else Location=new Point(Math.Max(area.Left,Math.Min(Location.X,area.Right-Width)),Math.Max(area.Top,Math.Min(Location.Y,area.Bottom-Height)));
   Show();Activate();
  }
 }

 // Alça com seis pontos (duas linhas de três) para arrastar a barra.
 internal sealed class DragGrip : Control {
  readonly Action onDrag;
  internal DragGrip(Action drag){
   onDrag=drag;BackColor=Theme.Surface;Cursor=Cursors.SizeAll;DoubleBuffered=true;
   AccessibleName="Arrastar barra";AccessibleRole=AccessibleRole.Grip;
  }
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   float scale=DeviceDpi/96f,radius=1.4f*scale,gap=5f*scale;
   float centerX=Width/2f,centerY=Height/2f;
   using(var brush=new SolidBrush(Theme.Muted))
    for(int row=-1;row<=1;row+=2)
     for(int col=-1;col<=1;col++)
      e.Graphics.FillEllipse(brush,centerX+col*gap-radius,centerY+row*gap/2-radius,radius*2,radius*2);
  }
  protected override void OnMouseDown(MouseEventArgs e){
   base.OnMouseDown(e);
   if(e.Button==MouseButtons.Left)onDrag();
  }
 }
}

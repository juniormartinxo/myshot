using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MyShot {
 internal static class Theme {
  // Cores são trocadas por Apply antes de qualquer janela ser criada; janelas abertas mantêm o tema anterior.
  internal static Color Background,Surface,Raised,Canvas,Ink,Muted,Border,Accent,AccentHover,Selected,Warning,WarningInk;
  internal static readonly Color OnAccent=Color.White;
  internal static bool IsDark{get;private set;}
  static Icon appIcon;
  internal static Icon AppIcon{get{return appIcon??(appIcon=CreateAppIcon());}}
  static Theme(){Apply(true);}
  internal static void Apply(bool dark){
   IsDark=dark;
   if(dark){
    // Carbono: cinza-azulado escuro, sem preto chapado. Contraste do texto principal acima de 10:1.
    Background=Rgb(36,39,44);Surface=Rgb(46,50,56);Raised=Rgb(58,63,70);Canvas=Rgb(30,33,37);
    Ink=Rgb(232,235,240);Muted=Rgb(160,166,176);Border=Rgb(72,78,88);
    Selected=Rgb(38,64,100);Warning=Rgb(70,56,26);WarningInk=Rgb(255,213,138);
   } else {
    Background=Rgb(243,244,246);Surface=Rgb(255,255,255);Raised=Rgb(233,235,239);Canvas=Rgb(221,224,229);
    Ink=Rgb(28,31,36);Muted=Rgb(92,98,110);Border=Rgb(204,208,215);
    Selected=Rgb(219,234,254);Warning=Rgb(254,243,199);WarningInk=Rgb(120,53,15);
   }
   Accent=Rgb(43,111,214);AccentHover=dark?Rgb(58,127,227):Rgb(33,92,184);
  }
  static Icon CreateAppIcon(){using(var stream=typeof(Theme).Assembly.GetManifestResourceStream("MyShot.myshot.ico")){if(stream==null)throw new InvalidOperationException("Ícone do MyShot ausente.");return new Icon(stream,new Size(32,32));}}
  internal static Color Rgb(int r,int g,int b){return System.Drawing.Color.FromArgb(r,g,b);}
  internal static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
  internal static Label Label(string text,float size,bool bold){return new Label{Text=text,AutoSize=true,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=bold?Ink:Muted,Margin=new Padding(0),UseMnemonic=false};}
  internal static Panel Rule(){return new Panel{Height=1,Dock=DockStyle.Top,BackColor=Border,Margin=new Padding(0,12,0,12)};}
  internal static void DisposeImages(Control control){var strip=control as ToolStrip;if(strip!=null)foreach(ToolStripItem item in strip.Items)if(item.Image!=null){Forget(item.Image);item.Image.Dispose();item.Image=null;}foreach(Control child in control.Controls)DisposeImages(child);}

  // Escala de DPI da tela (96 = 100%). Definida em Main antes de qualquer janela.
  internal static float Scale=1f;
  internal static int IconSize{get{return Math.Max(24,(int)Math.Round(24*Scale));}}
  // Ícones da barra de captura: 16 px em 100% de escala, sem alterar o tamanho usado no editor.
  internal static int BarIconSize{get{return Math.Max(16,(int)Math.Round(16*Scale));}}

  // Ícones são desenhados como vetor diretamente no destino, em qualquer escala de DPI, em vez de um bitmap redimensionado (que fica borrado).
  // Icon() devolve um bitmap só como marcador de tamanho para a ToolStrip; o renderizador troca por DrawIcon na hora de pintar.
  sealed class IconSource{internal string Name;internal Color Color;}
  static readonly Dictionary<Bitmap,IconSource> sources=new Dictionary<Bitmap,IconSource>();
  internal static Bitmap Icon(string name,Color color){
   int size=IconSize;var image=new Bitmap(size,size);
   using(var g=Graphics.FromImage(image))DrawIcon(g,name,color,new Rectangle(0,0,size,size));
   sources[image]=new IconSource{Name=name,Color=color};
   return image;
  }
  internal static bool TryGetIcon(Image image,out string name,out Color color){
   IconSource source;var bitmap=image as Bitmap;
   if(bitmap!=null&&sources.TryGetValue(bitmap,out source)){name=source.Name;color=source.Color;return true;}
   name=null;color=Color.Empty;return false;
  }
  static void Forget(Image image){var bitmap=image as Bitmap;if(bitmap!=null)sources.Remove(bitmap);}
  internal static void DrawIcon(Graphics g,string name,Color color,Rectangle rect){
   SvgIcons.Draw(g,name,color,rect);
  }
 }
 // Keep native button keyboard, focus, accessibility and click semantics; customize paint only.
 internal sealed class ActionButton : Button {
  internal bool Primary;
  internal Color? Swatch;
  internal string Icon;
  bool hover,pressed;
  internal ActionButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;UseVisualStyleBackColor=false;}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
  protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
  protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space){pressed=true;Invalidate();}base.OnKeyDown(e);}
  protected override void OnKeyUp(KeyEventArgs e){pressed=false;Invalidate();base.OnKeyUp(e);}
  protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
  protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
  protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
  protected override void OnPaint(PaintEventArgs e){
   float scale=DeviceDpi/96f;var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent==null?Theme.Background:Parent.BackColor);
   Color bg=Primary?(hover||pressed?Theme.AccentHover:Theme.Accent):(hover||pressed?Theme.Raised:Theme.Surface);
   Color fg=Primary?Theme.OnAccent:Theme.Ink;if(!Enabled){bg=Theme.Background;fg=Theme.Muted;}
   using(var shape=Theme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),5*scale))using(var brush=new SolidBrush(bg))using(var border=new Pen(Primary?bg:Theme.Border)) {g.FillPath(brush,shape);if(!Primary)g.DrawPath(border,shape);}
   var text=ClientRectangle;TextFormatFlags flags=TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis;
   if(Swatch.HasValue&&Text.Length==0){int side=(int)(20*scale),x=(Width-side)/2,y=(Height-side)/2;using(var b=new SolidBrush(Swatch.Value))g.FillEllipse(b,x,y,side,side);using(var p=new Pen(Theme.Border))g.DrawEllipse(p,x,y,side,side);}
   else if(Swatch.HasValue){int side=(int)(24*scale),left=(int)(16*scale);using(var b=new SolidBrush(Swatch.Value))g.FillEllipse(b,left,(Height-side)/2,side,side);using(var p=new Pen(Theme.Border))g.DrawEllipse(p,left,(Height-side)/2,side,side);text.X=(int)(52*scale);text.Width-=text.X+(int)(12*scale);flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis;}
   if(Icon!=null&&Text.Length==0){int side=(int)(18*scale);Theme.DrawIcon(g,Icon,fg,new Rectangle((Width-side)/2,(Height-side)/2,side,side));}
   if(Text.Length>0)TextRenderer.DrawText(g,Text,Font,text,fg,flags);
   if(Focused){var r=Rectangle.Inflate(ClientRectangle,-(int)(5*scale),-(int)(5*scale));ControlPaint.DrawFocusRectangle(g,r,fg,bg);}
  }
 }
 internal sealed class BrandMark : Control {
  internal BrandMark(){Size=new Size(44,44);DoubleBuffered=true;AccessibleRole=AccessibleRole.Graphic;AccessibleName="MyShot";}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Theme.Round(new RectangleF(0,0,Width-1,Height-1),10))using(var b=new SolidBrush(Theme.Accent))e.Graphics.FillPath(b,path);Theme.DrawIcon(e.Graphics,"capture",Theme.OnAccent,new Rectangle(Width/6,Height/6,Width*2/3,Height*2/3));}
 }
 internal sealed class ShotStripRenderer : ToolStripProfessionalRenderer {
  // accentHover: o botão fica azul com ícone branco enquanto o mouse está em cima (barra de captura).
  readonly bool accentHover;
  internal ShotStripRenderer(bool accentHover=false){RoundedEdges=false;this.accentHover=accentHover;}
  // Menus de contexto no estilo do app: cores do tema, destaque azul no hover, sem coluna de ícones.
  internal static void StyleMenu(ContextMenuStrip menu){
   menu.Renderer=new ShotStripRenderer(true);
   menu.BackColor=Theme.Surface;menu.ForeColor=Theme.Ink;menu.ShowImageMargin=true;menu.Font=new Font("Segoe UI",10f);
  }
  bool Highlighted(ToolStripItem item){return accentHover&&(item.Selected||item.Pressed);}
  protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e){
   string name;Color color;
   // Ícones de menu usam a cor do tema atual na hora de desenhar, então acompanham a troca de tema.
   if(Theme.TryGetIcon(e.Image,out name,out color)){Color shown=Highlighted(e.Item)?Theme.OnAccent:e.Item is ToolStripMenuItem?Theme.Ink:color;Theme.DrawIcon(e.Graphics,name,e.Item.Enabled?shown:Theme.Muted,e.ImageRectangle);return;}
   base.OnRenderItemImage(e);
  }
  protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(Theme.Surface);}
  // Coluna de ícones dos menus com o fundo do tema, em vez do cinza padrão do Windows.
  protected override void OnRenderImageMargin(ToolStripRenderEventArgs e){using(var b=new SolidBrush(Theme.Surface))e.Graphics.FillRectangle(b,e.AffectedBounds);}
  // Menus ganham uma borda fina do tema; a barra continua sem borda.
  protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){if(e.ToolStrip is ToolStripDropDown){using(var p=new Pen(Theme.Border))e.Graphics.DrawRectangle(p,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1);}}
  protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e){
   if(e.Vertical){base.OnRenderSeparator(e);return;}
   using(var p=new Pen(Theme.Border))e.Graphics.DrawLine(p,10,e.Item.Height/2,e.Item.Width-10,e.Item.Height/2);
  }
  // Item de lista suspensa: destaque com a cor do tema, sem o azul padrão do Windows.
  protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){using(var b=new SolidBrush(Highlighted(e.Item)?Theme.Accent:e.Item.Selected?Theme.Raised:Theme.Surface))e.Graphics.FillRectangle(b,new Rectangle(Point.Empty,e.Item.Size));}
  protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e){
   var button=e.Item as ToolStripButton;bool selected=button!=null&&button.Checked;bool primary=Equals(e.Item.Tag,"primary");
   var r=new RectangleF(2,2,e.Item.Width-4,e.Item.Height-4);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   if(Highlighted(e.Item)){using(var p=Theme.Round(r,5))using(var b=new SolidBrush(Theme.Accent))e.Graphics.FillPath(b,p);return;}
   if(selected||primary||e.Item.Selected||e.Item.Pressed){using(var p=Theme.Round(r,5))using(var b=new SolidBrush(primary?Theme.Accent:selected||e.Item.Pressed?Theme.Selected:Theme.Raised))e.Graphics.FillPath(b,p);}
   if(selected)using(var p=Theme.Round(r,5))using(var pen=new Pen(Theme.Accent))e.Graphics.DrawPath(pen,p);
  }
  protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=!e.Item.Enabled?Theme.Muted:Equals(e.Item.Tag,"primary")||Highlighted(e.Item)?Theme.OnAccent:Theme.Ink;base.OnRenderItemText(e);}
 }
}

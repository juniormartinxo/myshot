using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MyShot {
 internal static class Theme {
  internal static readonly Color Background=Color(.975,.005,155),Surface=Color(.994,.003,155),Canvas=Color(.925,.008,155);
  internal static readonly Color Ink=Color(.27,.018,155),Muted=Color(.48,.018,155),Border=Color(.86,.008,155);
  internal static readonly Color Accent=Color(.44,.085,160),AccentHover=Color(.38,.08,160),Selected=Color(.935,.025,160);
  internal static readonly Color Warning=Color(.95,.025,85),WarningInk=Color(.40,.065,70);
  internal static readonly Icon AppIcon=CreateAppIcon();
  [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
  static Icon CreateAppIcon(){using(var bitmap=new Bitmap(32,32))using(var g=Graphics.FromImage(bitmap)){g.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Round(new RectangleF(0,0,31,31),7))using(var b=new SolidBrush(Accent))g.FillPath(b,p);using(var glyph=Icon("capture",Surface))g.DrawImage(glyph,new Rectangle(4,4,24,24));IntPtr handle=bitmap.GetHicon();try{using(var borrowed=System.Drawing.Icon.FromHandle(handle))return (Icon)borrowed.Clone();}finally{DestroyIcon(handle);}}}
  // OKLCH -> OKLab -> linear sRGB -> sRGB. Tokens remain perceptually defined.
  internal static Color Color(double l,double c,double h) {
   double a=c*Math.Cos(h*Math.PI/180),b=c*Math.Sin(h*Math.PI/180);
   double x=Math.Pow(l+.3963377774*a+.2158037573*b,3),y=Math.Pow(l-.1055613458*a-.0638541728*b,3),z=Math.Pow(l-.0894841775*a-1.291485548*b,3);
   return System.Drawing.Color.FromArgb(Channel(4.0767416621*x-3.3077115913*y+.2309699292*z),Channel(-1.2684380046*x+2.6097574011*y-.3413193965*z),Channel(-.0041960863*x-.7034186147*y+1.707614701*z));
  }
  static int Channel(double v){v=v<=.0031308?12.92*v:1.055*Math.Pow(v,1/2.4)-.055;return (int)Math.Round(Math.Max(0,Math.Min(1,v))*255);}
  internal static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
  internal static Label Label(string text,float size,bool bold){return new Label{Text=text,AutoSize=true,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=bold?Ink:Muted,Margin=new Padding(0),UseMnemonic=false};}
  internal static Panel Rule(){return new Panel{Height=1,Dock=DockStyle.Top,BackColor=Border,Margin=new Padding(0,12,0,12)};}
  internal static void DisposeImages(Control control){var strip=control as ToolStrip;if(strip!=null)foreach(ToolStripItem item in strip.Items)if(item.Image!=null){item.Image.Dispose();item.Image=null;}foreach(Control child in control.Controls)DisposeImages(child);}
  internal static Bitmap Icon(string name,Color color){
   var image=new Bitmap(24,24);
   using(var g=Graphics.FromImage(image))using(var p=new Pen(color,1.65f)) {
    g.SmoothingMode=SmoothingMode.AntiAlias;p.StartCap=p.EndCap=LineCap.Round;p.LineJoin=LineJoin.Round;
    switch(name){
     case "chevronLeft":g.DrawLines(p,new[]{new Point(15,5),new Point(8,12),new Point(15,19)});break;
     case "chevronRight":g.DrawLines(p,new[]{new Point(9,5),new Point(16,12),new Point(9,19)});break;
     case "color":using(var b=new SolidBrush(color))g.FillEllipse(b,4,4,16,16);using(var border=new Pen(Ink,1))g.DrawEllipse(border,4,4,16,16);break;
     case "width":g.DrawLine(p,4,5,20,5);using(var medium=new Pen(color,3))g.DrawLine(medium,4,12,20,12);using(var thick=new Pen(color,5))g.DrawLine(thick,4,20,20,20);break;
     case "opacity":g.DrawEllipse(p,3,3,18,18);using(var b=new SolidBrush(color))g.FillPie(b,3,3,18,18,90,180);break;
     case "fill":g.DrawRectangle(p,3,4,18,16);using(var b=new SolidBrush(color))g.FillRectangle(b,6,7,12,10);break;
     case "fontSize":g.DrawLines(p,new[]{new Point(3,19),new Point(8,5),new Point(13,19)});g.DrawLine(p,5,14,11,14);g.DrawLine(p,19,5,19,19);g.DrawLines(p,new[]{new Point(16,8),new Point(19,5),new Point(22,8)});g.DrawLines(p,new[]{new Point(16,16),new Point(19,19),new Point(22,16)});break;
     case "font":g.DrawLines(p,new[]{new Point(4,20),new Point(12,4),new Point(20,20)});g.DrawLine(p,7,14,17,14);g.DrawLine(p,2,21,22,21);break;
     case "edit":g.DrawLines(p,new[]{new Point(5,15),new Point(16,4),new Point(20,8),new Point(9,19),new Point(4,20),new Point(5,15)});g.DrawLine(p,13,7,17,11);g.DrawLine(p,4,23,20,23);break;
     case "delete":g.DrawLines(p,new[]{new Point(6,7),new Point(7,21),new Point(17,21),new Point(18,7)});g.DrawLine(p,4,6,20,6);g.DrawLines(p,new[]{new Point(9,6),new Point(9,3),new Point(15,3),new Point(15,6)});g.DrawLine(p,10,10,10,17);g.DrawLine(p,14,10,14,17);break;
     case "move":g.DrawLines(p,new[]{new Point(5,3),new Point(5,21),new Point(10,16),new Point(14,22),new Point(17,20),new Point(13,14),new Point(20,13),new Point(5,3)});break;
     case "capture":g.DrawLines(p,new[]{new Point(9,4),new Point(4,4),new Point(4,9)});g.DrawLines(p,new[]{new Point(15,4),new Point(20,4),new Point(20,9)});g.DrawLines(p,new[]{new Point(4,15),new Point(4,20),new Point(9,20)});g.DrawLines(p,new[]{new Point(15,20),new Point(20,20),new Point(20,15)});g.DrawLine(p,9,12,15,12);g.DrawLine(p,12,9,12,15);break;
     case "desktop":g.DrawRectangle(p,3,4,18,13);g.DrawLine(p,12,17,12,21);g.DrawLine(p,8,21,16,21);break;
     case "save":g.DrawLines(p,new[]{new Point(5,3),new Point(17,3),new Point(21,7),new Point(21,21),new Point(3,21),new Point(3,3),new Point(5,3)});g.DrawRectangle(p,7,3,9,6);g.DrawRectangle(p,7,14,10,7);break;
     case "copy":g.DrawRectangle(p,8,8,12,13);g.DrawLines(p,new[]{new Point(5,16),new Point(3,16),new Point(3,3),new Point(15,3),new Point(15,5)});break;
     case "undo":g.DrawLines(p,new[]{new Point(8,4),new Point(3,9),new Point(8,14)});g.DrawBezier(p,3,9,21,4,23,18,17,20);break;
     case "redo":g.DrawLines(p,new[]{new Point(16,4),new Point(21,9),new Point(16,14)});g.DrawBezier(p,21,9,3,4,1,18,7,20);break;
     case "Line":g.DrawLine(p,4,20,20,4);break;
     case "Rectangle":g.DrawRectangle(p,3,5,18,14);break;
     case "Ellipse":g.DrawEllipse(p,3,4,18,16);break;
     case "Arrow":g.DrawLine(p,4,20,20,4);g.DrawLines(p,new[]{new Point(10,4),new Point(20,4),new Point(20,14)});break;
     case "Pencil":g.DrawLines(p,new[]{new Point(4,17),new Point(17,4),new Point(21,8),new Point(8,21),new Point(3,22),new Point(4,17)});g.DrawLine(p,14,7,18,11);break;
     case "Text":g.DrawLine(p,4,5,20,5);g.DrawLine(p,12,5,12,21);g.DrawLine(p,8,21,16,21);g.DrawLine(p,4,5,4,8);g.DrawLine(p,20,5,20,8);break;
     case "Mosaic":for(int y=4;y<20;y+=6)for(int x=4;x<20;x+=6)using(var b=new SolidBrush(System.Drawing.Color.FromArgb(((x+y)/6)%2==0?220:100,color)))g.FillRectangle(b,x,y,4,4);break;
     case "Step":g.DrawEllipse(p,2,2,20,20);using(var f=new Font("Segoe UI",11,FontStyle.Bold))TextRenderer.DrawText(g,"1",f,new Rectangle(3,2,18,20),color,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);break;
    }
   }return image;
  }
 }
 // Keep native button keyboard, focus, accessibility and click semantics; customize paint only.
 internal sealed class ActionButton : Button {
  internal bool Primary,Quiet;
  internal string Glyph;
  internal Color? Swatch;
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
   Color bg=Primary?(hover||pressed?Theme.AccentHover:Theme.Accent):(hover||pressed?Theme.Selected:Quiet?Theme.Background:Theme.Surface);
   Color fg=Primary?Theme.Surface:Theme.Ink;if(!Enabled){bg=Theme.Background;fg=Theme.Muted;}
   using(var shape=Theme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),7*scale))using(var brush=new SolidBrush(bg))using(var border=new Pen(Primary?bg:Theme.Border)) {g.FillPath(brush,shape);if(!Quiet&&!Primary)g.DrawPath(border,shape);}
   var text=ClientRectangle;TextFormatFlags flags=TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis;
   if(Glyph!=null||Swatch.HasValue){int side=(int)(24*scale),left=(int)(16*scale);if(Swatch.HasValue){using(var b=new SolidBrush(Swatch.Value))g.FillEllipse(b,left,(Height-side)/2,side,side);using(var p=new Pen(Theme.Border))g.DrawEllipse(p,left,(Height-side)/2,side,side);}else using(var icon=Theme.Icon(Glyph,fg))g.DrawImage(icon,new Rectangle(left,(Height-side)/2,side,side));text.X=(int)(52*scale);text.Width-=text.X+(int)(12*scale);flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis;}
   TextRenderer.DrawText(g,Text,Font,text,fg,flags);
   if(Focused){var r=Rectangle.Inflate(ClientRectangle,-(int)(5*scale),-(int)(5*scale));ControlPaint.DrawFocusRectangle(g,r,fg,bg);}
  }
 }
 internal sealed class BrandMark : Control {
  internal BrandMark(){Size=new Size(44,44);DoubleBuffered=true;AccessibleRole=AccessibleRole.Graphic;AccessibleName="MyShot";}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Theme.Round(new RectangleF(0,0,Width-1,Height-1),10))using(var b=new SolidBrush(Theme.Accent))e.Graphics.FillPath(b,path);using(var icon=Theme.Icon("capture",Theme.Surface))e.Graphics.DrawImage(icon,new Rectangle(Width/6,Height/6,Width*2/3,Height*2/3));}
 }
 internal sealed class ShotStripRenderer : ToolStripProfessionalRenderer {
  readonly bool sidebar;
  internal ShotStripRenderer(bool sidebar=false){this.sidebar=sidebar;RoundedEdges=false;}
  protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e){
   if(!sidebar){base.OnRenderItemImage(e);return;}
   float scale=e.ToolStrip.DeviceDpi/96f;int side=(int)Math.Round(20*scale);
   int left=e.Item.DisplayStyle==ToolStripItemDisplayStyle.Image?(e.Item.Width-side)/2:(int)Math.Round(14*scale);
   base.OnRenderItemImage(new ToolStripItemImageRenderEventArgs(e.Graphics,e.Item,e.Image,new Rectangle(left,(e.Item.Height-side)/2,side,side)));
  }
  protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(Theme.Surface);}
  protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){}
  protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e){
   var button=e.Item as ToolStripButton;bool selected=button!=null&&button.Checked;bool primary=Equals(e.Item.Tag,"primary");
   var r=new RectangleF(2,2,e.Item.Width-4,e.Item.Height-4);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   if(selected||primary||e.Item.Selected||e.Item.Pressed){using(var p=Theme.Round(r,6))using(var b=new SolidBrush(primary?Theme.Accent:selected||e.Item.Pressed?Theme.Selected:Theme.Background))e.Graphics.FillPath(b,p);}
   if(selected)using(var p=Theme.Round(r,6))using(var pen=new Pen(Theme.Accent))e.Graphics.DrawPath(pen,p);
  }
  protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=!e.Item.Enabled?Theme.Muted:Equals(e.Item.Tag,"primary")?Theme.Surface:Theme.Ink;if(sidebar){int left=(int)Math.Round(44*e.ToolStrip.DeviceDpi/96f);e.TextRectangle=new Rectangle(left,0,Math.Max(0,e.Item.Width-left-8),e.Item.Height);e.TextFormat=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix;}base.OnRenderItemText(e);}
 }
}

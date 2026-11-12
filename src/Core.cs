using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;

namespace MyShot {
 internal static class Native {
  [DllImport("user32.dll", SetLastError=true)] internal static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint key);
  [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h, int id);
  [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);
  [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("dwmapi.dll")] internal static extern int DwmFlush();
 }
 [DataContract] internal sealed class Preferences {
  [DataMember] public string Folder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
  [DataMember] public string Format = "png";
  [DataMember] public bool Sound;
  // "dark" (padrão) ou "light". Aplicado na próxima abertura do app.
  [DataMember] public string Appearance = "dark";
  [DataMember] public int CaptureKey = (int)Keys.S;
  [DataMember] public int FileKey = (int)Keys.F;
  [DataMember] public int FreeKey = (int)Keys.A;
  [DataMember] public bool ShiftInsteadOfAlt {get;set;}
  // Modificadores do atalho em bits (Alt=1, Ctrl=2, Shift=4, Win=8). Zero = arquivo antigo: usa ShiftInsteadOfAlt.
  [DataMember] public int Modifiers;
  internal const int ModAlt=1,ModCtrl=2,ModShift=4,ModWin=8;
  internal int HotkeyModifiers{get{if(Modifiers!=0)return Modifiers;return ShiftInsteadOfAlt?(ModCtrl|ModShift):(ModCtrl|ModAlt);}}
  internal static string ModifierText(int mask){var parts=new System.Collections.Generic.List<string>();if((mask&ModCtrl)!=0)parts.Add("Ctrl");if((mask&ModAlt)!=0)parts.Add("Alt");if((mask&ModShift)!=0)parts.Add("Shift");if((mask&ModWin)!=0)parts.Add("Win");return String.Join("+",parts);}
  [DataMember] public int Color = System.Drawing.Color.FromArgb(239,68,68).ToArgb();
  [DataMember] public int Thickness = 4;
  [DataMember] public int Opacity = 100;
  [DataMember] public int StepTextColor = -1;
  // Opacidade da barra de captura, em porcentagem (30 a 100).
  [DataMember] public int BarOpacity = 86;
  [DataMember] public int FontSize = 16;
  [DataMember] public string FontFamily = "Segoe UI";
  [DataMember] public bool Filled;
  // Última região capturada, em coordenadas da tela virtual. Largura ou altura menor que 2 = nenhuma.
  [DataMember] public int RegionX, RegionY, RegionWidth, RegionHeight;
  internal Rectangle? LastRegion { get { return RegionWidth>1 && RegionHeight>1 ? new Rectangle(RegionX,RegionY,RegionWidth,RegionHeight) : (Rectangle?)null; } }
  internal void SetLastRegion(Rectangle? region) { var r=region.HasValue?region.Value:Rectangle.Empty; RegionX=r.X; RegionY=r.Y; RegionWidth=r.Width; RegionHeight=r.Height; }
  internal static string PathName { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MyShot","preferences.json"); } }
  internal static Preferences Load(string path, out string warning) {
   warning=null;
   try {
    if(!File.Exists(path))return new Preferences();
    using(var stream=File.OpenRead(path)) {
     var p=(Preferences)new DataContractJsonSerializer(typeof(Preferences)).ReadObject(stream);
     if(p==null || String.IsNullOrWhiteSpace(p.Folder) || !Path.IsPathRooted(p.Folder))throw new InvalidDataException("Pasta inválida");
     if(p.Format!="png" && p.Format!="jpg" && p.Format!="bmp")p.Format="png";
     p.Thickness=Math.Max(1,Math.Min(40,p.Thickness)); p.Opacity=Math.Max(5,Math.Min(100,p.Opacity));
     p.BarOpacity=Math.Max(30,Math.Min(100,p.BarOpacity)); p.FontSize=Math.Max(8,Math.Min(120,p.FontSize));
     if(String.IsNullOrWhiteSpace(p.FontFamily))p.FontFamily="Segoe UI";
     if(p.CaptureKey<65||p.CaptureKey>90)p.CaptureKey=83;
     if(p.FileKey<65||p.FileKey>90)p.FileKey=70;
     if(p.FreeKey<65||p.FreeKey>90)p.FreeKey=65;
     return p;
    }
   }catch(Exception e) { warning="Preferências não carregadas: "+e.Message; return new Preferences(); }
  }
  internal void Save(string path) {
   Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
   var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try { using(var stream=File.Create(temp))new DataContractJsonSerializer(typeof(Preferences)).WriteObject(stream,this);
    if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
   } finally { if(File.Exists(temp))File.Delete(temp); }
  }
 }
 internal enum Tool { Line, Rectangle, Ellipse, Arrow, Pencil, Text, Mosaic, Step, Polygon, Face }
 internal sealed class Mark {
  internal Tool Tool;
  internal Point A,B;
  internal List<Point> Points = new List<Point>();
  internal Color Color;
  internal float Width;
  internal bool Filled;
  internal string Text="";
  internal string FontFamily="Segoe UI";
  internal float FontSize=16;
  internal bool Bold;
  internal Color TextColor=Color.White;
  internal int Number;
  internal Rectangle Rect { get { return Geometry.Between(A,B); } }
  internal Mark Translated(Point delta){var copy=(Mark)MemberwiseClone();copy.Points=new List<Point>(Points);copy.Translate(delta);return copy;}
  internal void Translate(Point delta){A.Offset(delta);B.Offset(delta);for(int i=0;i<Points.Count;i++){var p=Points[i];p.Offset(delta);Points[i]=p;}}
 }
 internal static class Geometry {
  internal static Rectangle Between(Point a,Point b) { return Rectangle.FromLTRB(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X),Math.Max(a.Y,b.Y)); }
  internal static Rectangle Fit(Size image,Rectangle target) {
   double scale=Math.Min((double)target.Width/image.Width,(double)target.Height/image.Height);
   int w=Math.Max(1,(int)(image.Width*scale)),h=Math.Max(1,(int)(image.Height*scale));
   return new Rectangle(target.X+(target.Width-w)/2,target.Y+(target.Height-h)/2,w,h);
  }
  internal static Point ImagePoint(Point p,Rectangle view,Size image) {
   return new Point(Math.Max(0,Math.Min(image.Width-1,(int)((p.X-view.X)*(double)image.Width/view.Width))),Math.Max(0,Math.Min(image.Height-1,(int)((p.Y-view.Y)*(double)image.Height/view.Height))));
  }
 }
 internal sealed class Document : IDisposable {
  internal readonly Bitmap Original;
  internal readonly List<Mark> Marks=new List<Mark>();
  sealed class Edit {internal Action Undo,Redo;}
  readonly Stack<Edit> undo=new Stack<Edit>(),redo=new Stack<Edit>();
  internal bool CanUndo {get{return undo.Count>0;}}
  internal bool CanRedo {get{return redo.Count>0;}}
  internal int NextStep {get {int n=1;foreach(var m in Marks)if(m.Tool==Tool.Step)n=Math.Max(n,m.Number+1);return n;}}
  internal Document(Bitmap source) {Original=(Bitmap)source.Clone();}
  internal void Add(Mark mark) {int index=Marks.Count;Marks.Add(mark);Record(new Edit{Undo=()=>Marks.Remove(mark),Redo=()=>Marks.Insert(index,mark)});}
  void Record(Edit edit){undo.Push(edit);redo.Clear();}
  internal bool Move(Mark mark,Point delta){
   if(!Marks.Contains(mark))return false;delta=AnnotationGeometry.LimitMove(mark,delta,Original.Size);if(delta==Point.Empty)return false;
   mark.Translate(delta);Point inverse=new Point(-delta.X,-delta.Y);Record(new Edit{Undo=()=>mark.Translate(inverse),Redo=()=>mark.Translate(delta)});return true;
  }
  internal bool Delete(Mark mark){
   int index=Marks.IndexOf(mark);if(index<0)return false;
   Marks.RemoveAt(index);Record(new Edit{Undo=()=>Marks.Insert(index,mark),Redo=()=>Marks.Remove(mark)});return true;
  }
  internal bool EditText(Mark mark,string text){return EditTextStyle(mark,text,mark.Color,mark.FontFamily,mark.FontSize,mark.Bold,mark.A);}
  // Texto, estilo (cor, fonte, tamanho, negrito) e posição mudam juntos e voltam com um único Desfazer.
  internal bool EditTextStyle(Mark mark,string text,Color color,string family,float size,bool bold,Point origin){
   if(!Marks.Contains(mark)||mark.Tool!=Tool.Text||String.IsNullOrWhiteSpace(text))return false;
   string previousText=mark.Text,previousFamily=mark.FontFamily;Color previousColor=mark.Color;float previousSize=mark.FontSize;bool previousBold=mark.Bold;Point previousOrigin=mark.A;
   if(previousText==text&&previousColor==color&&previousFamily==family&&previousSize==size&&previousBold==bold&&previousOrigin==origin)return false;
   Action<string,Color,string,float,bool,Point> apply=(t,c,f,s,b,o)=>{
    mark.Text=t;mark.Color=c;mark.FontFamily=f;mark.FontSize=s;mark.Bold=b;
    mark.Translate(new Point(o.X-mark.A.X,o.Y-mark.A.Y));
   };
   apply(text,color,family,size,bold,origin);
   Record(new Edit{
    Undo=()=>apply(previousText,previousColor,previousFamily,previousSize,previousBold,previousOrigin),
    Redo=()=>apply(text,color,family,size,bold,origin)});
   return true;
  }
  internal Mark HitTest(Point point,float tolerance){for(int i=Marks.Count-1;i>=0;i--)if(AnnotationGeometry.Hit(Marks[i],point,tolerance))return Marks[i];return null;}
  internal void Undo(){if(CanUndo){var edit=undo.Pop();edit.Undo();redo.Push(edit);}}
  internal void Redo(){if(CanRedo){var edit=redo.Pop();edit.Redo();undo.Push(edit);}}
  internal Bitmap Render(Mark pending,Mark replacing=null,Mark replacement=null) {
   var b=new Bitmap(Original.Width,Original.Height,PixelFormat.Format32bppArgb);
   using(var g=Graphics.FromImage(b))g.DrawImageUnscaled(Original,0,0);
   foreach(var m in Marks) Draw(b,m==replacing&&replacement!=null?replacement:m);
   if(pending!=null)Draw(b,pending);
   return b;
  }
  static void Draw(Bitmap b,Mark m) {
   if(m.Tool==Tool.Mosaic) {
    // Rect exclui a última linha e coluna (FromLTRB); o +1 garante que o mosaico cubra até a borda da imagem.
    Rectangle r=Rectangle.Intersect(new Rectangle(Point.Empty,b.Size),Rectangle.FromLTRB(m.Rect.Left,m.Rect.Top,m.Rect.Right+1,m.Rect.Bottom+1));
    if(r.Width==0||r.Height==0)return;
    int block=Math.Max(4,(int)m.Width*3);
    // Every block samples existing pixels before painting. No source image is embedded in exports.
    using(var sample=b.Clone(r,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(b))
     for(int y=r.Top;y<r.Bottom;y+=block)for(int x=r.Left;x<r.Right;x+=block) {
      int w=Math.Min(block,r.Right-x),h=Math.Min(block,r.Bottom-y); Color c=sample.GetPixel(x-r.X+w/2,y-r.Y+h/2);
      using(var brush=new SolidBrush(c))g.FillRectangle(brush,x,y,w,h);
     }
    return;
   }
   using(var g=Graphics.FromImage(b))using(var pen=new Pen(m.Color,m.Width))using(var brush=new SolidBrush(m.Color)) {
    g.SmoothingMode=SmoothingMode.AntiAlias; g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
    pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
    Rectangle r=m.Rect;
    switch(m.Tool) {
     case Tool.Line:g.DrawLine(pen,m.A,m.B);break;
     case Tool.Arrow:
      if(m.A!=m.B)using(var cap=new AdjustableArrowCap(4,5,true)){pen.CustomEndCap=cap;g.DrawLine(pen,m.A,m.B);}break;
     case Tool.Rectangle:if(r.Width>0&&r.Height>0){if(m.Filled)g.FillRectangle(brush,r);else g.DrawRectangle(pen,r);}break;
     case Tool.Ellipse:if(r.Width>0&&r.Height>0){if(m.Filled)g.FillEllipse(brush,r);else g.DrawEllipse(pen,r);}break;
     case Tool.Pencil:if(m.Points.Count>1)g.DrawLines(pen,m.Points.ToArray());else g.FillEllipse(brush,m.A.X-m.Width/2,m.A.Y-m.Width/2,m.Width,m.Width);break;
     case Tool.Polygon:case Tool.Face:if(m.Points.Count>1){var pts=m.Points.ToArray();if(m.Filled&&pts.Length>2)g.FillPolygon(brush,pts);g.DrawPolygon(pen,pts);}break;
     case Tool.Text:using(var font=new Font(m.FontFamily,m.FontSize,m.Bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))g.DrawString(m.Text,font,brush,m.A);break;
     case Tool.Step:
      float d=m.FontSize*1.7f;g.FillEllipse(brush,m.A.X-d/2,m.A.Y-d/2,d,d);
      // Número centralizado pelo contorno real dos dígitos (não pela linha de texto, que tem espaço de ascendente/descendente).
      using(var path=new GraphicsPath()){
       using(var font=new Font(m.FontFamily,m.FontSize,FontStyle.Bold,GraphicsUnit.Pixel))path.AddString(m.Number.ToString(),font.FontFamily,(int)FontStyle.Bold,m.FontSize,PointF.Empty,StringFormat.GenericTypographic);
       var ink=path.GetBounds();
       using(var shift=new Matrix()){shift.Translate(m.A.X-(ink.X+ink.Width/2),m.A.Y-(ink.Y+ink.Height/2));path.Transform(shift);}
       using(var white=new SolidBrush(Color.FromArgb(m.Color.A,m.TextColor)))g.FillPath(white,path);
      }break;
    }
   }
  }
  internal void Save(string path) {using(var b=Render(null)) ImageOutput.Save(b,path);}
  public void Dispose(){Original.Dispose();}
 }
 internal static class ImageOutput {
  internal static void Save(Bitmap bitmap,string path) {
   string ext=Path.GetExtension(path).ToLowerInvariant();
   var format=ext==".png"?ImageFormat.Png:ext==".jpg"||ext==".jpeg"?ImageFormat.Jpeg:ext==".bmp"?ImageFormat.Bmp:null;
   if(format==null)throw new ArgumentException("Use PNG, JPG ou BMP.");
   string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{bitmap.Save(temp,format);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
 internal static class DesktopCapture {
  internal static Bitmap Take(Rectangle bounds) {
   var image=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb);
   try{using(var g=Graphics.FromImage(image))g.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size,CopyPixelOperation.SourceCopy);return image;}
   catch{image.Dispose();throw;}
  }
 }
}


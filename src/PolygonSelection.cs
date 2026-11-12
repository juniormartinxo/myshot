using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace MyShot {
 // Seleção de área livre: cada clique marca um canto. Enter, duplo clique ou clique no primeiro canto fecha o polígono.
 // Backspace desfaz o último canto; Esc ou clique direito cancela.
 internal sealed class PolygonSelection : Form {
  readonly Bitmap desktop;
  readonly List<Point> corners=new List<Point>();
  Point mouse;
  internal Point[] Corners;
  internal PolygonSelection(Bitmap image,Rectangle bounds) {
   desktop=image;AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;MaximumSize=bounds.Size;MinimumSize=bounds.Size;Bounds=bounds;TopMost=true;ShowInTaskbar=false;Cursor=Cursors.Cross;DoubleBuffered=true;KeyPreview=true;
   Shown+=(s,e)=>{Bounds=bounds;Activate();Native.SetForegroundWindow(Handle);};
  }
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;
   g.DrawImageUnscaled(desktop,0,0);
   using(var shade=new SolidBrush(Color.FromArgb(128,Color.Black)))g.FillRectangle(shade,ClientRectangle);
   // O interior do polígono em construção mostra a tela original, como a área que será capturada.
   if(corners.Count>=3)using(var path=new GraphicsPath()){path.AddPolygon(corners.ToArray());g.SetClip(path);g.DrawImageUnscaled(desktop,0,0);g.ResetClip();}
   if(corners.Count>0){
    g.SmoothingMode=SmoothingMode.AntiAlias;
    using(var pen=new Pen(Color.White,1)){
     if(corners.Count>1)g.DrawLines(pen,corners.ToArray());
     g.DrawLine(pen,corners[corners.Count-1],mouse);
     if(corners.Count>=3)using(var closing=new Pen(Color.FromArgb(160,Color.White),1){DashStyle=DashStyle.Dash})g.DrawLine(closing,mouse,corners[0]);
    }
    foreach(var c in corners)g.FillEllipse(Brushes.White,c.X-3,c.Y-3,6,6);
   }
   string label=corners.Count==0?"Clique para marcar o primeiro canto   ·   Esc cancela":corners.Count<3?"Marque mais cantos   ·   Backspace desfaz   ·   Esc cancela":"Enter ou clique no primeiro canto fecha   ·   Backspace desfaz   ·   Esc cancela";
   var size=TextRenderer.MeasureText(label,SystemFonts.MessageBoxFont);
   int x=Math.Max(8,Math.Min(ClientSize.Width-size.Width-40,mouse.X+18)),y=Math.Max(8,Math.Min(ClientSize.Height-42,mouse.Y+22));
   g.SmoothingMode=SmoothingMode.AntiAlias;using(var bg=new SolidBrush(Color.FromArgb(235,Theme.Canvas)))using(var shape=Theme.Round(new RectangleF(x,y,size.Width+20,32),6))g.FillPath(bg,shape);
   TextRenderer.DrawText(g,label,SystemFonts.MessageBoxFont,new Point(x+10,y+9),Theme.Ink);
  }
  protected override void OnMouseMove(MouseEventArgs e){mouse=e.Location;Invalidate();}
  protected override void OnMouseDown(MouseEventArgs e){
   if(e.Button==MouseButtons.Right){Cancel();return;}
   if(e.Button!=MouseButtons.Left)return;
   mouse=e.Location;
   if(corners.Count>=3&&Near(e.Location,corners[0],8)){Finish();return;}
   // Um clique repetido no mesmo lugar (parte de um duplo clique) não cria canto novo.
   if(corners.Count>0&&Near(e.Location,corners[corners.Count-1],2))return;
   corners.Add(e.Location);Invalidate();
  }
  protected override void OnMouseDoubleClick(MouseEventArgs e){if(e.Button==MouseButtons.Left)Finish();}
  protected override bool ProcessCmdKey(ref Message msg,Keys key){
   if(key==Keys.Escape){Cancel();return true;}
   if(key==Keys.Enter){Finish();return true;}
   if(key==Keys.Back&&corners.Count>0){corners.RemoveAt(corners.Count-1);Invalidate();return true;}
   return base.ProcessCmdKey(ref msg,key);
  }
  protected override void WndProc(ref Message m){if(m.Msg==0x02E0)return;base.WndProc(ref m);}
  void Finish(){if(corners.Count<3)return;Corners=corners.ToArray();DialogResult=DialogResult.OK;Close();}
  void Cancel(){DialogResult=DialogResult.Cancel;Close();}
  static bool Near(Point a,Point b,int tolerance){return Math.Abs(a.X-b.X)<=tolerance&&Math.Abs(a.Y-b.Y)<=tolerance;}
 }

 internal static class PolygonCapture {
  // Recorta o menor retângulo que envolve o polígono e deixa transparente o que fica fora dele.
  // Retorna null se a área útil ficar pequena demais.
  internal static Bitmap Crop(Bitmap source,Point[] corners){
   int minX=int.MaxValue,minY=int.MaxValue,maxX=int.MinValue,maxY=int.MinValue;
   foreach(var c in corners){minX=Math.Min(minX,c.X);minY=Math.Min(minY,c.Y);maxX=Math.Max(maxX,c.X);maxY=Math.Max(maxY,c.Y);}
   var box=Rectangle.Intersect(new Rectangle(Point.Empty,source.Size),Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1));
   if(box.Width<2||box.Height<2)return null;
   var local=new Point[corners.Length];
   for(int i=0;i<corners.Length;i++)local[i]=new Point(corners[i].X-box.X,corners[i].Y-box.Y);
   var result=new Bitmap(box.Width,box.Height,PixelFormat.Format32bppArgb);
   using(var path=new GraphicsPath()){
    path.FillMode=FillMode.Winding;path.AddPolygon(local);
    using(var g=Graphics.FromImage(result)){g.Clear(Color.Transparent);g.SetClip(path);g.DrawImage(source,new Rectangle(0,0,box.Width,box.Height),box,GraphicsUnit.Pixel);}
   }
   return result;
  }
 }
}

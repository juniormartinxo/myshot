using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MyShot {
 internal static class AnnotationGeometry {
  internal static RectangleF Bounds(Mark mark){
   RectangleF bounds=mark.Rect;
   if(mark.Tool==Tool.Text){
    using(var bitmap=new Bitmap(1,1))using(var graphics=Graphics.FromImage(bitmap))using(var font=new Font(mark.FontFamily,mark.FontSize,mark.Bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel)){
     graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
     bounds=new RectangleF(mark.A,graphics.MeasureString(mark.Text,font));
    }
   }else if(mark.Tool==Tool.Step){float d=mark.FontSize*1.7f;bounds=new RectangleF(mark.A.X-d/2,mark.A.Y-d/2,d,d);}
   else if(mark.Tool==Tool.Pencil&&mark.Points.Count>0){float l=mark.Points[0].X,t=mark.Points[0].Y,r=l,b=t;foreach(var p in mark.Points){l=Math.Min(l,p.X);t=Math.Min(t,p.Y);r=Math.Max(r,p.X);b=Math.Max(b,p.Y);}bounds=RectangleF.FromLTRB(l,t,r,b);}
   if(mark.Tool==Tool.Arrow&&mark.A!=mark.B){foreach(var p in ArrowHead(mark)){bounds=RectangleF.FromLTRB(Math.Min(bounds.Left,p.X),Math.Min(bounds.Top,p.Y),Math.Max(bounds.Right,p.X),Math.Max(bounds.Bottom,p.Y));}}
   if(mark.Tool!=Tool.Text&&mark.Tool!=Tool.Mosaic&&mark.Tool!=Tool.Step)bounds.Inflate(Math.Max(.5f,mark.Width/2),Math.Max(.5f,mark.Width/2));
   return bounds;
  }
  internal static bool Hit(Mark mark,Point point,float tolerance){
   float radius=Math.Max(.5f,mark.Width/2)+tolerance;
   switch(mark.Tool){
    case Tool.Line:return SegmentDistance(point,mark.A,mark.B)<=radius;
    case Tool.Arrow:
     if(SegmentDistance(point,mark.A,mark.B)<=radius)return true;
     if(mark.A==mark.B)return false;
     using(var head=new GraphicsPath()){head.AddPolygon(ArrowHead(mark));using(var pen=new Pen(System.Drawing.Color.Black,Math.Max(1,tolerance*2)))return head.IsVisible(point)||head.IsOutlineVisible(point,pen);}
    case Tool.Pencil:
     if(mark.Points.Count<2)return SegmentDistance(point,mark.A,mark.A)<=radius;
     for(int i=1;i<mark.Points.Count;i++)if(SegmentDistance(point,mark.Points[i-1],mark.Points[i])<=radius)return true;return false;
    case Tool.Ellipse:case Tool.Step:
     RectangleF ellipse=mark.Tool==Tool.Step?Bounds(mark):(RectangleF)mark.Rect;
     if(ellipse.Width<=0||ellipse.Height<=0)return false;
     using(var path=new GraphicsPath()){path.AddEllipse(ellipse);using(var pen=new Pen(System.Drawing.Color.Black,Math.Max(1,radius*2)))return path.IsVisible(point)||path.IsOutlineVisible(point,pen);}
    default:var bounds=Bounds(mark);bounds.Inflate(tolerance,tolerance);return bounds.Contains(point);
   }
  }
  static PointF[] ArrowHead(Mark mark){
   double dx=mark.B.X-mark.A.X,dy=mark.B.Y-mark.A.Y,length=Math.Sqrt(dx*dx+dy*dy);
   if(length==0)return new[]{(PointF)mark.B,(PointF)mark.B,(PointF)mark.B};
   float ux=(float)(dx/length),uy=(float)(dy/length),headLength=5*mark.Width,halfWidth=2*mark.Width;
   var center=new PointF(mark.B.X-ux*headLength,mark.B.Y-uy*headLength);
   return new[]{(PointF)mark.B,new PointF(center.X-uy*halfWidth,center.Y+ux*halfWidth),new PointF(center.X+uy*halfWidth,center.Y-ux*halfWidth)};
  }
  static double SegmentDistance(Point p,Point a,Point b){double dx=b.X-a.X,dy=b.Y-a.Y,d=dx*dx+dy*dy;double t=d==0?0:Math.Max(0,Math.Min(1,((p.X-a.X)*dx+(p.Y-a.Y)*dy)/d));double x=p.X-a.X-t*dx,y=p.Y-a.Y-t*dy;return Math.Sqrt(x*x+y*y);}
  internal static Point LimitMove(Mark mark,Point offset,Size image){
   RectangleF bounds=Bounds(mark);
   // Existing clipped marks may remain clipped, but a drag cannot hide more of them.
   int minX=Math.Min(0,(int)Math.Ceiling(-bounds.Left)),maxX=Math.Max(0,(int)Math.Floor(image.Width-bounds.Right));
   int minY=Math.Min(0,(int)Math.Ceiling(-bounds.Top)),maxY=Math.Max(0,(int)Math.Floor(image.Height-bounds.Bottom));
   return new Point(Math.Max(minX,Math.Min(maxX,offset.X)),Math.Max(minY,Math.Min(maxY,offset.Y)));
  }
 }
}

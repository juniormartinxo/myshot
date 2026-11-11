using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace MyShot {
 // Ícones SVG (Lucide, licença ISC) embutidos como recursos em assets/icons. Convertidos para caminhos do GDI+ e desenhados como vetor,
 // então ficam nítidos em qualquer escala de DPI. Para trocar um ícone, substitua o arquivo .svg correspondente e recompile.
 internal static class SvgIcons {
  sealed class Shape {internal readonly List<GraphicsPath> Paths=new List<GraphicsPath>();internal float Stroke=2f;}
  static readonly Dictionary<string,Shape> cache=new Dictionary<string,Shape>();

  // Desenha o ícone com a cor dada, ocupando o retângulo informado (o viewBox 24×24 é escalado para ele).
  internal static void Draw(Graphics g,string name,Color color,Rectangle rect){
   var shape=Load(name);if(shape==null)return;
   var state=g.Save();
   g.SmoothingMode=SmoothingMode.AntiAlias;g.TranslateTransform(rect.X,rect.Y);g.ScaleTransform(rect.Width/24f,rect.Height/24f);
   using(var pen=new Pen(color,shape.Stroke){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})
    foreach(var path in shape.Paths)g.DrawPath(pen,path);
   g.Restore(state);
  }

  static Shape Load(string name){
   Shape shape;
   if(cache.TryGetValue(name,out shape))return shape;
   shape=null;
   using(var stream=typeof(SvgIcons).Assembly.GetManifestResourceStream("MyShot.icons."+name+".svg")){
    if(stream!=null){
     var xml=new XmlDocument();xml.Load(stream);
     var root=xml.DocumentElement;
     shape=new Shape();
     string width=root.GetAttribute("stroke-width");float stroke;
     if(width.Length>0&&float.TryParse(width,NumberStyles.Float,CultureInfo.InvariantCulture,out stroke))shape.Stroke=stroke;
     // O SVG declara namespace; GetElementsByTagName("*") encontra os elementos independentemente dele.
     foreach(XmlElement node in root.GetElementsByTagName("*"))AddElement(shape,node);
    }
   }
   cache[name]=shape;
   return shape;
  }

  static void AddElement(Shape shape,XmlElement e){
   if(e==null)return;
   var path=new GraphicsPath();
   switch(e.LocalName){
    case "path":ParsePath(path,e.GetAttribute("d"));break;
    case "circle":{float cx=Num(e,"cx"),cy=Num(e,"cy"),r=Num(e,"r");path.AddEllipse(cx-r,cy-r,2*r,2*r);}break;
    case "ellipse":{float cx=Num(e,"cx"),cy=Num(e,"cy"),rx=Num(e,"rx"),ry=Num(e,"ry");path.AddEllipse(cx-rx,cy-ry,2*rx,2*ry);}break;
    case "line":path.AddLine(Num(e,"x1"),Num(e,"y1"),Num(e,"x2"),Num(e,"y2"));break;
    case "rect":{float x=Num(e,"x"),y=Num(e,"y"),w=Num(e,"width"),h=Num(e,"height"),rx=Math.Min(Num(e,"rx"),Math.Min(w,h)/2);if(rx>0)RoundRect(path,x,y,w,h,rx);else path.AddRectangle(new RectangleF(x,y,w,h));}break;
    case "polyline":case "polygon":{var pts=ParsePoints(e.GetAttribute("points"));if(pts.Count>1){path.AddLines(pts.ToArray());if(e.LocalName=="polygon")path.CloseFigure();}}break;
    default:path.Dispose();return;
   }
   if(path.PointCount>0)shape.Paths.Add(path);else path.Dispose();
  }

  static float Num(XmlElement e,string attribute){
   float value;string text=e.GetAttribute(attribute);
   return text.Length>0&&float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)?value:0f;
  }

  static void RoundRect(GraphicsPath p,float x,float y,float w,float h,float r){
   float d=r*2;p.AddArc(x,y,d,d,180,90);p.AddLine(x+r,y,x+w-r,y);p.AddArc(x+w-d,y,d,d,270,90);p.AddLine(x+w,y+r,x+w,y+h-r);
   p.AddArc(x+w-d,y+h-d,d,d,0,90);p.AddLine(x+w-r,y+h,x+r,y+h);p.AddArc(x,y+h-d,d,d,90,90);p.CloseFigure();
  }

  static List<PointF> ParsePoints(string text){
   var points=new List<PointF>();var numbers=new List<float>();
   foreach(Match m in Regex.Matches(text??"",@"[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?"))numbers.Add(float.Parse(m.Value,CultureInfo.InvariantCulture));
   for(int i=0;i+1<numbers.Count;i+=2)points.Add(new PointF(numbers[i],numbers[i+1]));
   return points;
  }

  // Leitor posicional do atributo d. Flags de arco (0/1) são lidos um caractere por vez, porque o Lucide os escreve grudados (ex.: "010").
  sealed class PathCursor {
   static readonly Regex numberAt=new Regex(@"\G[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?");
   readonly string s;int pos;
   internal PathCursor(string text){s=text;}
   void SkipSeparators(){while(pos<s.Length&&(char.IsWhiteSpace(s[pos])||s[pos]==','))pos++;}
   internal bool AtEnd(){SkipSeparators();return pos>=s.Length;}
   internal bool NextIsCommand(){SkipSeparators();return pos<s.Length&&char.IsLetter(s[pos]);}
   internal char ReadCommand(){SkipSeparators();return s[pos++];}
   internal double ReadNumber(){SkipSeparators();var m=numberAt.Match(s,pos);if(!m.Success)throw new FormatException("Número inválido no caminho SVG.");pos+=m.Length;return double.Parse(m.Value,CultureInfo.InvariantCulture);}
   internal bool ReadFlag(){SkipSeparators();return s[pos++]=='1';}
  }

  // Interpreta o atributo d de <path>: M/L/H/V/C/S/Q/T/A/Z, em maiúsculas (absoluto) e minúsculas (relativo).
  static void ParsePath(GraphicsPath path,string d){
   var r=new PathCursor(d??"");
   char cmd='M';double cx=0,cy=0,sx=0,sy=0,ctrlX=0,ctrlY=0;char lastType=' ';
   while(!r.AtEnd()){
    if(r.NextIsCommand()){cmd=r.ReadCommand();if(cmd=='Z'||cmd=='z'){path.CloseFigure();cx=sx;cy=sy;lastType='Z';continue;}}
    else if(cmd=='Z'||cmd=='z')break;
    bool rel=char.IsLower(cmd);char up=char.ToUpperInvariant(cmd);
    double ox=rel?cx:0,oy=rel?cy:0;
    switch(up){
     case 'M':{double x=r.ReadNumber()+ox,y=r.ReadNumber()+oy;path.StartFigure();cx=sx=x;cy=sy=y;cmd=rel?'l':'L';lastType='M';}break;
     case 'L':{double x=r.ReadNumber()+ox,y=r.ReadNumber()+oy;path.AddLine((float)cx,(float)cy,(float)x,(float)y);cx=x;cy=y;lastType='L';}break;
     case 'H':{double x=r.ReadNumber()+(rel?cx:0);path.AddLine((float)cx,(float)cy,(float)x,(float)cy);cx=x;lastType='H';}break;
     case 'V':{double y=r.ReadNumber()+(rel?cy:0);path.AddLine((float)cx,(float)cy,(float)cx,(float)y);cy=y;lastType='V';}break;
     case 'C':{double x1=r.ReadNumber()+ox,y1=r.ReadNumber()+oy,x2=r.ReadNumber()+ox,y2=r.ReadNumber()+oy,x=r.ReadNumber()+ox,y=r.ReadNumber()+oy;Cubic(path,cx,cy,x1,y1,x2,y2,x,y);ctrlX=x2;ctrlY=y2;cx=x;cy=y;lastType='C';}break;
     case 'S':{double x1=lastType=='C'||lastType=='S'?2*cx-ctrlX:cx,y1=lastType=='C'||lastType=='S'?2*cy-ctrlY:cy;double x2=r.ReadNumber()+ox,y2=r.ReadNumber()+oy,x=r.ReadNumber()+ox,y=r.ReadNumber()+oy;Cubic(path,cx,cy,x1,y1,x2,y2,x,y);ctrlX=x2;ctrlY=y2;cx=x;cy=y;lastType='S';}break;
     case 'Q':{double qx=r.ReadNumber()+ox,qy=r.ReadNumber()+oy,x=r.ReadNumber()+ox,y=r.ReadNumber()+oy;Quad(path,cx,cy,qx,qy,x,y);ctrlX=qx;ctrlY=qy;cx=x;cy=y;lastType='Q';}break;
     case 'T':{double qx=lastType=='Q'||lastType=='T'?2*cx-ctrlX:cx,qy=lastType=='Q'||lastType=='T'?2*cy-ctrlY:cy;double x=r.ReadNumber()+ox,y=r.ReadNumber()+oy;Quad(path,cx,cy,qx,qy,x,y);ctrlX=qx;ctrlY=qy;cx=x;cy=y;lastType='T';}break;
     case 'A':{double rx=r.ReadNumber(),ry=r.ReadNumber(),phi=r.ReadNumber();bool fa=r.ReadFlag(),fs=r.ReadFlag();double x=r.ReadNumber()+ox,y=r.ReadNumber()+oy;Arc(path,cx,cy,rx,ry,phi,fa,fs,x,y);cx=x;cy=y;lastType='A';}break;
     default:return;
    }
   }
  }

  static void Cubic(GraphicsPath p,double x0,double y0,double x1,double y1,double x2,double y2,double x3,double y3){
   p.AddBezier((float)x0,(float)y0,(float)x1,(float)y1,(float)x2,(float)y2,(float)x3,(float)y3);
  }

  static void Quad(GraphicsPath p,double x0,double y0,double qx,double qy,double x3,double y3){
   Cubic(p,x0,y0,x0+2.0/3*(qx-x0),y0+2.0/3*(qy-y0),x3+2.0/3*(qx-x3),y3+2.0/3*(qy-y3),x3,y3);
  }

  // Arco SVG (parametrização por extremos) convertido em curvas de Bézier cúbicas.
  static void Arc(GraphicsPath p,double x1,double y1,double rx,double ry,double phiDeg,bool largeArc,bool sweep,double x2,double y2){
   if(x1==x2&&y1==y2)return;
   if(rx==0||ry==0){p.AddLine((float)x1,(float)y1,(float)x2,(float)y2);return;}
   rx=Math.Abs(rx);ry=Math.Abs(ry);
   double phi=phiDeg*Math.PI/180,cos=Math.Cos(phi),sin=Math.Sin(phi);
   double dx=(x1-x2)/2,dy=(y1-y2)/2;
   double x1p=cos*dx+sin*dy,y1p=-sin*dx+cos*dy;
   double lambda=x1p*x1p/(rx*rx)+y1p*y1p/(ry*ry);
   if(lambda>1){rx*=Math.Sqrt(lambda);ry*=Math.Sqrt(lambda);}
   double num=rx*rx*ry*ry-rx*rx*y1p*y1p-ry*ry*x1p*x1p,den=rx*rx*y1p*y1p+ry*ry*x1p*x1p;
   double coef=Math.Sqrt(Math.Max(0,num/den));if(largeArc==sweep)coef=-coef;
   double cxp=coef*rx*y1p/ry,cyp=-coef*ry*x1p/rx;
   double cx=cos*cxp-sin*cyp+(x1+x2)/2,cy=sin*cxp+cos*cyp+(y1+y2)/2;
   double a1=Angle(1,0,(x1p-cxp)/rx,(y1p-cyp)/ry);
   double da=Angle((x1p-cxp)/rx,(y1p-cyp)/ry,(-x1p-cxp)/rx,(-y1p-cyp)/ry);
   if(!sweep&&da>0)da-=2*Math.PI;else if(sweep&&da<0)da+=2*Math.PI;
   int segments=Math.Max(1,(int)Math.Ceiling(Math.Abs(da)/(Math.PI/2)));
   double delta=da/segments,t=4.0/3.0*Math.Tan(delta/4);
   double px=x1,py=y1,a=a1;
   for(int s=0;s<segments;s++){
    double ca=Math.Cos(a),sa=Math.Sin(a),cb=Math.Cos(a+delta),sb=Math.Sin(a+delta);
    double c1x=cx+rx*(cos*(ca-t*sa)-sin*(sa+t*ca)),c1y=cy+rx*(sin*(ca-t*sa)+cos*(sa+t*ca));
    double c2x=cx+rx*(cos*(cb+t*sb)-sin*(sb-t*cb)),c2y=cy+rx*(sin*(cb+t*sb)+cos*(sb-t*cb));
    double ex=cx+rx*cos*cb-ry*sin*sb,ey=cy+rx*sin*cb+ry*cos*sb;
    p.AddBezier((float)px,(float)py,(float)c1x,(float)c1y,(float)c2x,(float)c2y,(float)ex,(float)ey);
    px=ex;py=ey;a+=delta;
   }
  }

  static double Angle(double ux,double uy,double vx,double vy){
   double sign=ux*vy-uy*vx<0?-1:1;
   double dot=ux*vx+uy*vy,len=Math.Sqrt(ux*ux+uy*uy)*Math.Sqrt(vx*vx+vy*vy);
   return sign*Math.Acos(Math.Max(-1,Math.Min(1,dot/len)));
  }
 }
}

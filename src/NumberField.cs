using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace MyShot {
 // Campo numérico no visual do app: caixa arredondada, valor à esquerda e setas discretas à direita.
 // Aceita digitação, setas do teclado, roda do mouse e cliques nas setas.
 internal sealed class NumberField : Control {
  decimal value,minimum=0,maximum=100;
  bool hover;
  int hoverArrow=0;
  string typing;
  const int ArrowWidth=22;
  internal event EventHandler ValueChanged;

  internal NumberField(){
   SetStyle(ControlStyles.Selectable|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);
   BackColor=Theme.Background;ForeColor=Theme.Ink;Font=new Font("Segoe UI",10f);
   TabStop=true;Cursor=Cursors.IBeam;Size=new Size(72,30);
  }

  internal decimal Minimum{get{return minimum;}set{minimum=value;Value=this.value;}}
  internal decimal Maximum{get{return maximum;}set{maximum=value;Value=this.value;}}
  internal decimal Value{
   get{return value;}
   set{
    decimal clamped=Math.Max(minimum,Math.Min(maximum,value));
    if(clamped==this.value&&typing==null){return;}
    this.value=clamped;typing=null;Invalidate();
    if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);
   }
  }

  protected override bool IsInputKey(Keys key){
   return key==Keys.Up||key==Keys.Down||key==Keys.Enter||key==Keys.Escape||base.IsInputKey(key);
  }

  protected override void OnKeyDown(KeyEventArgs e){
   base.OnKeyDown(e);
   switch(e.KeyCode){
    case Keys.Up:Value+=e.Shift?10:1;e.Handled=true;break;
    case Keys.Down:Value-=e.Shift?10:1;e.Handled=true;break;
    case Keys.Enter:Commit();e.Handled=true;break;
    case Keys.Back:
     if(typing!=null){typing=typing.Length>0?typing.Substring(0,typing.Length-1):null;Invalidate();}
     e.Handled=true;break;
   }
  }

  protected override void OnKeyPress(KeyPressEventArgs e){
   base.OnKeyPress(e);
   if(char.IsDigit(e.KeyChar)||(e.KeyChar=='-'&&(typing==null||typing.Length==0))){
    typing=(typing??"")+e.KeyChar;Invalidate();e.Handled=true;
   }
  }

  protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Commit();}

  // Aplica o texto digitado (se houver) e volta a mostrar o valor.
  void Commit(){
   if(typing==null)return;
   decimal parsed;
   if(decimal.TryParse(typing,NumberStyles.Integer,CultureInfo.InvariantCulture,out parsed))Value=parsed;
   else{typing=null;Invalidate();}
   typing=null;Invalidate();
  }

  protected override void OnMouseWheel(MouseEventArgs e){
   base.OnMouseWheel(e);
   Value+=e.Delta>0?1:-1;
  }

  protected override void OnMouseDown(MouseEventArgs e){
   base.OnMouseDown(e);
   Focus();
   if(e.Button!=MouseButtons.Left)return;
   if(e.X>=Width-ArrowWidth){Value+=e.Y<Height/2?1:-1;}
  }

  protected override void OnMouseMove(MouseEventArgs e){
   base.OnMouseMove(e);
   int arrow=e.X>=Width-ArrowWidth?(e.Y<Height/2?1:2):0;
   if(arrow!=hoverArrow||!hover){hoverArrow=arrow;hover=true;Invalidate();}
  }

  protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;hoverArrow=0;Invalidate();}

  protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}

  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent!=null?Parent.BackColor:Theme.Surface);
   var box=new RectangleF(0.5f,0.5f,Width-1,Height-1);
   using(var path=Theme.Round(box,6))
   using(var fill=new SolidBrush(Theme.Background))
   using(var edge=new Pen(Focused?Theme.Accent:Theme.Border,Focused?1.5f:1f)){
    g.FillPath(fill,path);g.DrawPath(edge,path);
   }
   // Setas: cada metade tem seu próprio destaque ao passar o mouse.
   var arrows=new Rectangle(Width-ArrowWidth,1,ArrowWidth-1,Height-2);
   if(hover&&hoverArrow==1)using(var b=new SolidBrush(Theme.Raised))g.FillRectangle(b,arrows.X,arrows.Y,arrows.Width,arrows.Height/2);
   if(hover&&hoverArrow==2)using(var b=new SolidBrush(Theme.Raised))g.FillRectangle(b,arrows.X,arrows.Y+arrows.Height/2,arrows.Width,arrows.Height-arrows.Height/2);
   using(var pen=new Pen(Theme.Muted,1.6f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round}){
    int cx=arrows.X+arrows.Width/2;int upY=Height/2-3,downY=Height/2+3;
    g.DrawLines(pen,new[]{new Point(cx-4,upY+2),new Point(cx,upY-2),new Point(cx+4,upY+2)});
    g.DrawLines(pen,new[]{new Point(cx-4,downY-2),new Point(cx,downY+2),new Point(cx+4,downY-2)});
   }
   string shown=typing??value.ToString(CultureInfo.InvariantCulture);
   var textRect=new Rectangle(10,0,Width-ArrowWidth-14,Height);
   TextRenderer.DrawText(g,shown,Font,textRect,ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
   if(Focused&&typing!=null){
    Size s=TextRenderer.MeasureText(shown,Font);
    using(var caret=new Pen(Theme.Ink,1f))g.DrawLine(caret,10+s.Width+1,Height/2-s.Height/2,10+s.Width+1,Height/2+s.Height/2);
   }
  }
 }
}

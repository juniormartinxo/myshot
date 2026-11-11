using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MyShot {
 // Seletor de opções no visual do app: caixa arredondada com o valor e uma lista suspensa no tema.
 internal sealed class ChoiceBox : Control {
  readonly List<object> items=new List<object>();
  int selected=-1;
  bool hover;
  internal event EventHandler SelectedIndexChanged;

  internal ChoiceBox(){
   SetStyle(ControlStyles.Selectable|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);
   BackColor=Color.Transparent;TabStop=true;Cursor=Cursors.Hand;Font=new Font("Segoe UI",10f);Size=new Size(130,32);
  }

  internal List<object> Items{get{return items;}}
  internal int SelectedIndex{
   get{return selected;}
   set{
    int index=value<items.Count?value:-1;
    if(index==selected)return;
    selected=index;Invalidate();
    if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);
   }
  }
  internal object SelectedItem{
   get{return selected>=0?items[selected]:null;}
   set{SelectedIndex=value==null?-1:items.IndexOf(value);}
  }

  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent!=null?Parent.BackColor:Theme.Surface);
   var box=new RectangleF(0.5f,0.5f,Width-1,Height-1);
   using(var path=Theme.Round(box,6))
   using(var fill=new SolidBrush(hover?Theme.Raised:Theme.Background))
   using(var edge=new Pen(Focused?Theme.Accent:Theme.Border,Focused?1.5f:1f)){g.FillPath(fill,path);g.DrawPath(edge,path);}
   string text=selected>=0?selected<items.Count?items[selected].ToString():"":"";
   TextRenderer.DrawText(g,text,Font,new Rectangle(10,0,Width-30,Height),ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
   using(var pen=new Pen(Theme.Muted,1.6f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round}){
    int cx=Width-14,cy=Height/2;
    g.DrawLines(pen,new[]{new Point(cx-4,cy-2),new Point(cx,cy+2),new Point(cx+4,cy-2)});
   }
  }

  protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}
  protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;Invalidate();}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();if(e.Button==MouseButtons.Left)ShowList();}
  protected override bool IsInputKey(Keys key){return key==Keys.Up||key==Keys.Down||base.IsInputKey(key);}
  protected override void OnKeyDown(KeyEventArgs e){
   base.OnKeyDown(e);
   if(e.KeyCode==Keys.Up&&selected>0){SelectedIndex=selected-1;e.Handled=true;}
   if(e.KeyCode==Keys.Down&&selected<items.Count-1){SelectedIndex=selected+1;e.Handled=true;}
  }

  // Lista suspensa com o mesmo tema do app; a escolha é aplicada ao clicar.
  void ShowList(){
   var menu=new ContextMenuStrip{Renderer=new ShotStripRenderer(),BackColor=Theme.Surface,ForeColor=Theme.Ink,ShowImageMargin=false,Font=Font};
   for(int i=0;i<items.Count;i++){
    int index=i;
    var entry=new ToolStripMenuItem(items[i].ToString(),null,(s,e)=>SelectedIndex=index){Checked=i==selected};
    menu.Items.Add(entry);
   }
   menu.Show(this,new Point(0,Height+2));
  }
 }

 // Caixa de seleção no visual do app: quadrado arredondado com marca de destaque e rótulo à direita.
 internal sealed class ToggleBox : Control {
  bool checkedValue,hover;
  internal event EventHandler CheckedChanged;
  const int Box=18,Gap=8;

  internal ToggleBox(){
   SetStyle(ControlStyles.Selectable|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor|ControlStyles.AllPaintingInWmPaint,true);
   BackColor=Color.Transparent;TabStop=true;Cursor=Cursors.Hand;Font=new Font("Segoe UI",10f);
   AutoSize=true;
  }

  internal bool Checked{
   get{return checkedValue;}
   set{if(value==checkedValue)return;checkedValue=value;Invalidate();if(CheckedChanged!=null)CheckedChanged(this,EventArgs.Empty);}
  }

  protected override Size DefaultSize{get{return new Size(200,24);}}
  public override Size GetPreferredSize(Size proposedSize){
   var text=TextRenderer.MeasureText(Text??"",Font);
   return new Size(Box+Gap+text.Width+2,Math.Max(Box,text.Height)+4);
  }

  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent!=null?Parent.BackColor:Theme.Surface);
   int y=(Height-Box)/2;
   var rect=new RectangleF(0.5f,y+0.5f,Box-1,Box-1);
   using(var path=Theme.Round(rect,4)){
    if(checkedValue){using(var fill=new SolidBrush(hover?Theme.AccentHover:Theme.Accent))g.FillPath(fill,path);}
    else{using(var fill=new SolidBrush(Theme.Background))g.FillPath(fill,path);using(var edge=new Pen(Focused?Theme.Accent:Theme.Border,1.2f))g.DrawPath(edge,path);}
   }
   if(checkedValue){
    using(var pen=new Pen(Theme.OnAccent,2f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})
     g.DrawLines(pen,new[]{new PointF(4,y+9.5f),new PointF(7.5f,y+13f),new PointF(14f,y+5.5f)});
   }
   TextRenderer.DrawText(g,Text??"",Font,new Rectangle(Box+Gap,0,Width-Box-Gap,Height),ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
  }

  protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}
  protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;Invalidate();}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();if(e.Button==MouseButtons.Left)Checked=!Checked;}
  protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Space){Checked=!Checked;e.Handled=true;}}
  protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Invalidate();}
 }

 internal static class ThemedFrame {
  // Caixa de texto dentro de um quadro arredondado no tema do app (a borda nativa do TextBox não é personalizável).
  internal static Panel Wrap(TextBox box){
   box.BorderStyle=BorderStyle.None;box.BackColor=Theme.Background;box.ForeColor=Theme.Ink;
   var frame=new Panel{Dock=DockStyle.Fill,Padding=new Padding(10,8,10,8),BackColor=Theme.Surface,Margin=new Padding(0,0,0,8)};
   frame.Paint+=(s,e)=>{
    var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
    using(var path=Theme.Round(new RectangleF(0.5f,0.5f,frame.Width-1,frame.Height-1),6))
    using(var pen=new Pen(Theme.Border)){g.DrawPath(pen,path);}
   };
   box.Dock=DockStyle.Fill;
   frame.Controls.Add(box);
   return frame;
  }
 }
}

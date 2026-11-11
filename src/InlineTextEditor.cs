using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyShot {
 // Edição de texto dentro da própria anotação: uma barra flutuante acima do texto oferece cor, fonte, tamanho, negrito,
 // confirmar e cancelar. O texto aparece ao vivo na imagem; o cursor fica sobre a posição do texto.
 internal sealed class InlineTextEditor : Panel {
  internal sealed class Style {
   internal string Text;internal Color Color;internal string Family;internal float Size;internal bool Bold;
  }

  // Espaço entre a barra e o início do texto, medido em pixels.
  const int TextTopPadding=15;

  readonly Action<Style> change,confirm;
  readonly Action cancel;
  readonly Panel boxHost=new Panel{Dock=DockStyle.Fill,Padding=new Padding(0,TextTopPadding,0,0),BackColor=Color.Transparent};
  readonly InlineTextInput box=new InlineTextInput{Dock=DockStyle.Fill};
  readonly ToolStrip bar=new ToolStrip{GripStyle=ToolStripGripStyle.Hidden,Dock=DockStyle.Top,Renderer=new ShotStripRenderer(),BackColor=Theme.Surface,ImageScalingSize=new Size(Theme.BarIconSize,Theme.BarIconSize),Padding=new Padding(4)};
  readonly ToolStripButton colorItem,fontItem,boldItem,confirmItem,cancelItem;
  readonly NumericUpDown sizeInput=new NumericUpDown{Minimum=6,Maximum=200,Width=56,BorderStyle=BorderStyle.FixedSingle,AccessibleName="Tamanho do texto"};
  Color color;string family;float size;bool bold;float pixelScale=1f;
  internal Action DragBegin;
  internal Action<Point> DragTo;

  internal InlineTextEditor(Style initial,float scale,Action<Style> onChange,Action<Style> onConfirm,Action onCancel){
   change=onChange;confirm=onConfirm;cancel=onCancel;pixelScale=scale;
   color=Color.FromArgb(initial.Color.R,initial.Color.G,initial.Color.B);family=initial.Family;size=initial.Size;bold=initial.Bold;
   BackColor=Color.Transparent;

   colorItem=Item("Cor",()=>{using(var dialog=new ColorDialog{Color=color,FullOpen=true})if(dialog.ShowDialog(this)==DialogResult.OK){color=dialog.Color;colorItem.Image=Swatch(color);Notify();}});
   colorItem.Image=Swatch(color);
   fontItem=Item(family,()=>{using(var dialog=new FontDialog{Font=new Font(family,12f),ShowColor=false,FontMustExist=true,MinSize=6,MaxSize=72})if(dialog.ShowDialog(this)==DialogResult.OK){family=dialog.Font.FontFamily.Name;bold=dialog.Font.Bold;boldItem.Checked=bold;fontItem.Text=family;Notify();}});
   fontItem.DisplayStyle=ToolStripItemDisplayStyle.Text;
   sizeInput.Value=(decimal)Math.Max(6,Math.Min(200,size));
   sizeInput.ValueChanged+=(s,e)=>{size=(float)sizeInput.Value;Notify();};
   var sizeHost=new ToolStripControlHost(sizeInput){Margin=new Padding(2,4,2,0)};
   bar.Items.Add(sizeHost);
   boldItem=Item("Negrito",()=>{bold=boldItem.Checked;Notify();});
   boldItem.CheckOnClick=true;boldItem.Checked=bold;boldItem.Image=Theme.Icon("bold",Theme.Ink);
   bar.Items.Add(new ToolStripSeparator());
   confirmItem=Item("Confirmar (Ctrl+Enter)",()=>Confirm());confirmItem.Image=Theme.Icon("check",Theme.OnAccent);confirmItem.Tag="primary";
   cancelItem=Item("Cancelar (Esc)",()=>Cancel());cancelItem.Image=Theme.Icon("close",Theme.Ink);
   var handle=new MoveHandle{Begin=()=>{if(DragBegin!=null)DragBegin();},DragMoved=p=>{if(DragTo!=null)DragTo(p);}};
   bar.Items.Add(new ToolStripControlHost(handle){Margin=new Padding(8,0,2,0)});

   box.Text=initial.Text??"";
   box.Modified=()=>Notify();
   ApplyFont();
   confirmItem.Enabled=CanConfirm;
   bar.CanOverflow=false;
   boxHost.Controls.Add(box);Controls.Add(boxHost);Controls.Add(bar);
  }

  // Texto pode ser confirmado só quando não estiver vazio.
  internal bool CanConfirm{get{return !String.IsNullOrWhiteSpace(box.Text);}}
  internal InlineTextInput Box{get{return box;}}
  internal string Value{get{return box.Text;}}
  internal int BarWidth{get{return bar.GetPreferredSize(Size.Empty).Width;}}
  // Distância do topo deste painel até o ponto onde o texto começa (barra + espaço).
  internal int InputTop{get{return bar.GetPreferredSize(Size.Empty).Height+TextTopPadding;}}

  internal void Confirm(){
   if(!CanConfirm)return;
   confirm(Current());
  }
  internal void Cancel(){cancel();}

  internal void SetScale(float scale){pixelScale=scale;ApplyFont();}

  Style Current(){return new Style{Text=box.Text,Color=color,Family=family,Size=size,Bold=bold};}

  void Notify(){
   confirmItem.Enabled=CanConfirm;
   bar.CanOverflow=false;
   ApplyFont();
   change(Current());
  }

  void ApplyFont(){
   box.Restyle(new Font(family,Math.Max(6f,size*pixelScale),bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel),color);
  }

  ToolStripButton Item(string text,Action action){
   var b=new ToolStripButton{Text=text,DisplayStyle=ToolStripItemDisplayStyle.Image,Padding=new Padding(Dip(6)),Margin=new Padding(2,0,2,0),AccessibleName=text,ToolTipText=text};
   b.Click+=(s,e)=>action();bar.Items.Add(b);return b;
  }

  int Dip(int value){return (int)Math.Round(value*DeviceDpi/96f);}

  // Bolinha com a cor atual, usada como ícone do botão Cor.
  static Bitmap Swatch(Color color){
   var bitmap=new Bitmap(Theme.BarIconSize,Theme.BarIconSize);
   using(var g=Graphics.FromImage(bitmap)){
    g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    int inset=2;
    using(var brush=new SolidBrush(color))g.FillEllipse(brush,inset,inset,bitmap.Width-2*inset,bitmap.Height-2*inset);
    using(var pen=new Pen(Theme.Border))g.DrawEllipse(pen,inset,inset,bitmap.Width-2*inset-1,bitmap.Height-2*inset-1);
   }
   return bitmap;
  }
 }

 // Campo de digitação sem fundo: não desenha texto (o texto já aparece na imagem), só o cursor, e recebe as teclas.
 internal sealed class InlineTextInput : Control {
  string content="";int caret;Font font;Color ink=Color.Black;bool caretOn=true;
  readonly Timer blink=new Timer{Interval=530};
  internal Action Modified;

  internal InlineTextInput(){
   SetStyle(ControlStyles.Selectable|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);
   BackColor=Color.Transparent;TabStop=true;
   blink.Tick+=(s,e)=>{caretOn=!caretOn;Invalidate();};
   blink.Start();
  }

  public override string Text{
   get{return content;}
   set{content=value??"";caret=Math.Min(caret,content.Length);Edited();}
  }

  internal void Restyle(Font f,Color c){font=f;ink=c;Invalidate();}

  protected override bool IsInputKey(Keys key){
   switch(key){
    case Keys.Left:case Keys.Right:case Keys.Up:case Keys.Down:case Keys.Home:case Keys.End:case Keys.Enter:case Keys.Back:case Keys.Delete:return true;
   }
   return base.IsInputKey(key);
  }

  protected override void OnKeyPress(KeyPressEventArgs e){
   if(e.KeyChar>=' '){Insert(e.KeyChar.ToString());e.Handled=true;}
  }

  protected override void OnKeyDown(KeyEventArgs e){
   switch(e.KeyCode){
    case Keys.Back:if(caret>0){content=content.Remove(caret-1,1);caret--;Edited();}break;
    case Keys.Delete:if(caret<content.Length){content=content.Remove(caret,1);Edited();}break;
    case Keys.Left:if(caret>0)caret--;Invalidate();break;
    case Keys.Right:if(caret<content.Length)caret++;Invalidate();break;
    case Keys.Home:caret=LineStart(caret);Invalidate();break;
    case Keys.End:caret=LineEnd(caret);Invalidate();break;
    case Keys.Enter:Insert("\n");break;
    case Keys.V:if(e.Control&&Clipboard.ContainsText())Insert(Clipboard.GetText().Replace("\r",""));break;
   }
   e.Handled=true;
  }

  protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
  protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}

  protected override void OnPaint(PaintEventArgs e){
   if(!caretOn||!Focused||font==null)return;
   var g=e.Graphics;
   string[] lines=content.Substring(0,caret).Split('\n');
   int line=lines.Length-1;string current=lines[line];
   float x=current.Length==0?0:g.MeasureString(current,font,new PointF(0,0),StringFormat.GenericDefault).Width;
   float height=font.GetHeight(g);float y=line*height;
   using(var pen=new Pen(ink,2f))g.DrawLine(pen,x,y,x,y+height);
  }

  protected override void Dispose(bool disposing){
   if(disposing)blink.Dispose();
   base.Dispose(disposing);
  }

  void Insert(string s){content=content.Insert(caret,s);caret+=s.Length;Edited();}
  void Edited(){Invalidate();if(Modified!=null)Modified();}
  int LineStart(int position){int i=position;while(i>0&&content[i-1]!='\n')i--;return i;}
  int LineEnd(int position){int i=position;while(i<content.Length&&content[i]!='\n')i++;return i;}
 }

 // Alça de seis pontos no fim da barra: arraste para mover o texto enquanto ele é editado.
 internal sealed class MoveHandle : Control {
  internal Action Begin;
  internal Action<Point> DragMoved;
  bool dragging;
  internal MoveHandle(){
   Size=new Size(18,30);Cursor=Cursors.SizeAll;DoubleBuffered=true;BackColor=Theme.Surface;
   AccessibleName="Arrastar texto";AccessibleRole=AccessibleRole.Grip;
  }
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   float radius=1.5f,gapX=5f,gapY=5f;float cx=Width/2f,cy=Height/2f;
   using(var brush=new SolidBrush(Theme.Muted))
    for(int col=-1;col<=1;col+=2)
     for(int row=-1;row<=1;row++)
      e.Graphics.FillEllipse(brush,cx+col*gapX/2-radius,cy+row*gapY-radius,radius*2,radius*2);
  }
  protected override void OnMouseDown(MouseEventArgs e){
   base.OnMouseDown(e);
   if(e.Button!=MouseButtons.Left)return;
   dragging=true;Capture=true;if(Begin!=null)Begin();
  }
  protected override void OnMouseMove(MouseEventArgs e){
   base.OnMouseMove(e);
   if(dragging&&DragMoved!=null)DragMoved(System.Windows.Forms.Cursor.Position);
  }
  protected override void OnMouseUp(MouseEventArgs e){
   base.OnMouseUp(e);
   dragging=false;Capture=false;
  }
 }
}

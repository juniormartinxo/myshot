using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyShot {
 // Edição de texto dentro da própria anotação: a caixa fica sobre o texto e uma barra flutuante acima dele
 // oferece cor, fonte, tamanho, negrito, confirmar e cancelar. Mudanças são repassadas ao editor ao vivo.
 internal sealed class InlineTextEditor : Panel {
  internal sealed class Style {
   internal string Text;internal Color Color;internal string Family;internal float Size;internal bool Bold;
  }

  readonly Action<Style> change,confirm;
  readonly Action cancel;
  readonly TextBox box=new TextBox{Multiline=true,Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,AcceptsReturn=true,ScrollBars=ScrollBars.None,AccessibleName="Texto da anotação"};
  readonly ToolStrip bar=new ToolStrip{GripStyle=ToolStripGripStyle.Hidden,Dock=DockStyle.Top,Renderer=new ShotStripRenderer(),BackColor=Theme.Surface,ImageScalingSize=new Size(Theme.BarIconSize,Theme.BarIconSize),Padding=new Padding(4)};
  readonly ToolStripButton colorItem,fontItem,boldItem,confirmItem,cancelItem;
  readonly NumericUpDown sizeInput=new NumericUpDown{Minimum=6,Maximum=200,Width=56,BorderStyle=BorderStyle.FixedSingle,AccessibleName="Tamanho do texto"};
  Color color;string family;float size;bool bold;float pixelScale=1f;

  internal InlineTextEditor(Style initial,float scale,Action<Style> onChange,Action<Style> onConfirm,Action onCancel){
   change=onChange;confirm=onConfirm;cancel=onCancel;pixelScale=scale;
   color=Color.FromArgb(initial.Color.R,initial.Color.G,initial.Color.B);family=initial.Family;size=initial.Size;bold=initial.Bold;
   BackColor=Theme.Surface;

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

   box.Text=initial.Text??"";
   box.TextChanged+=(s,e)=>{Notify();};
   ApplyFont();
   confirmItem.Enabled=CanConfirm;
   Controls.Add(box);Controls.Add(bar);
  }

  // Texto pode ser confirmado só quando não estiver vazio.
  internal bool CanConfirm{get{return !String.IsNullOrWhiteSpace(box.Text);}}
  internal TextBox Box{get{return box;}}
  internal string Value{get{return box.Text;}}

  internal void Confirm(){
   if(!CanConfirm)return;
   confirm(Current());
  }
  internal void Cancel(){cancel();}

  internal void SetScale(float scale){pixelScale=scale;ApplyFont();}

  Style Current(){return new Style{Text=box.Text,Color=color,Family=family,Size=size,Bold=bold};}

  void Notify(){
   confirmItem.Enabled=CanConfirm;
   ApplyFont();
   change(Current());
  }

  void ApplyFont(){
   box.Font=new Font(family,Math.Max(6f,size*pixelScale),bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);
   box.ForeColor=color;
   box.BackColor=Color.White;
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
}

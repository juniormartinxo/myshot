using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MyShot {
 // Janela de ajuda: marca, nome, versão e contato. Abre pelo botão de ajuda da barra.
 internal sealed class HelpWindow : Form {
  internal HelpWindow() {
   UI.Style(this);
   Text="MyShot · Ajuda";ClientSize=new Size(460,344);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;

   var logo=new PictureBox{Image=Logo(),SizeMode=PictureBoxSizeMode.Zoom,Size=new Size(96,96),Location=new Point(28,28),BackColor=Color.Transparent};
   var title=Theme.Label("MyShot",22,true);title.Location=new Point(144,32);
   var tagline=Theme.Label("Captura de tela para Windows",10,false);tagline.Location=new Point(146,76);
   var version=Theme.Label("Versão "+AppInfo.Version,10,true);version.Location=new Point(146,100);
   var rule=new Panel{BackColor=Theme.Border,Location=new Point(28,144),Size=new Size(ClientSize.Width-56,1)};
   Controls.Add(logo);Controls.Add(title);Controls.Add(tagline);Controls.Add(version);Controls.Add(rule);

   Field("Criado por",162,PlainText("Junior Martins"));
   Field("E-mail",196,PlainText("amjr.box@gmail.com"));
   AddRepository(230);

   var close=UI.Button("Fechar",()=>Close(),true);close.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;close.Location=new Point(ClientSize.Width-28-112,ClientSize.Height-28-40);
   Controls.Add(close);
   AcceptButton=close;CancelButton=close;
  }

  // Marca do app, embutida como recurso (PNG com transparência).
  internal static Image Logo() {
   using(var stream=typeof(HelpWindow).Assembly.GetManifestResourceStream("MyShot.myshot-logo.png")){
    if(stream==null)return null;
    return new Bitmap(Image.FromStream(stream));
   }
  }

  // Linha de dados: rótulo discreto à esquerda e valor à direita.
  void Field(string label,int y,Control value) {
   var name=Theme.Label(label,9.5f,false);name.Location=new Point(28,y+3);
   value.Location=new Point(146,y);
   Controls.Add(name);Controls.Add(value);
  }

  static Label PlainText(string text) {
   return new Label{Text=text,AutoSize=true,Font=new Font("Segoe UI",10f),ForeColor=Theme.Ink,BackColor=Color.Transparent};
  }

  // Repositório: texto simples e um botão de link ao lado, que abre no navegador.
  void AddRepository(int y) {
   const string url="https://github.com/juniormartinxo/myshot";
   var name=Theme.Label("Repositório",9.5f,false);name.Location=new Point(28,y+3);
   var text=PlainText("github.com/juniormartinxo/myshot");text.Location=new Point(146,y);
   var size=TextRenderer.MeasureText(text.Text,text.Font);
   var open=(ActionButton)UI.Button("",()=>Open(url),false);
   open.Icon="link";open.IconSize=14;open.AccessibleName="Abrir o repositório no navegador";open.AutoSize=false;open.MinimumSize=new Size(34,34);open.Size=new Size(34,34);
   open.Location=new Point(146+size.Width+8,y-7);
   Controls.Add(name);Controls.Add(text);Controls.Add(open);
  }

  static void Open(string target) {
   try{Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}
   catch(Exception ex){UI.Error(null,ex);}
  }
 }
}

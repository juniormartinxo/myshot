using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MyShot {
 // Janela de ajuda: nome, versão e contato. Abre pelo botão de ajuda da barra.
 internal sealed class HelpWindow : Form {
  internal HelpWindow() {
   UI.Style(this);
   Text="MyShot · Ajuda";ClientSize=new Size(420,250);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;
   var title=Theme.Label("MyShot",18,true);title.Location=new Point(24,20);
   var version=Theme.Label("Versão "+AppInfo.Version,10,false);version.Location=new Point(26,62);
   var author=Theme.Label("Criado por Junior Martins",10,false);author.Location=new Point(26,96);
   var email=Link("amjr.box@gmail.com","mailto:amjr.box@gmail.com");email.Location=new Point(26,126);
   var repo=Link("github.com/juniormartinxo/myshot","https://github.com/juniormartinxo/myshot");repo.Location=new Point(26,156);
   var close=UI.Button("Fechar",()=>Close(),true);close.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;close.Location=new Point(ClientSize.Width-24-100,ClientSize.Height-24-40);
   Controls.Add(title);Controls.Add(version);Controls.Add(author);Controls.Add(email);Controls.Add(repo);Controls.Add(close);
   AcceptButton=close;CancelButton=close;
  }

  // Link clicável que abre no navegador ou no cliente de e-mail.
  static LinkLabel Link(string text,string target) {
   var link=new LinkLabel{Text=text,AutoSize=true,Font=new Font("Segoe UI",10f),LinkColor=Theme.Accent,ActiveLinkColor=Theme.AccentHover,VisitedLinkColor=Theme.Accent,BackColor=Color.Transparent,ForeColor=Theme.Ink};
   link.Links.Add(0,text.Length,target);
   link.LinkClicked+=(s,e)=>{try{Process.Start(new ProcessStartInfo((string)e.Link.LinkData){UseShellExecute=true});}catch(Exception ex){UI.Error(link,ex);}};
   return link;
  }
 }
}

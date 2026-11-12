using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MyShot {
 internal static class Program {
  [STAThread] static void Main(string[] args) {
   bool startup=Array.IndexOf(args,"--startup")>=0,updated=Array.IndexOf(args,"--updated")>=0;
   try{Native.SetProcessDpiAwarenessContext(new IntPtr(-4));}catch(EntryPointNotFoundException){}
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   Updates.CleanupPrevious();
   using(var screen=Graphics.FromHwnd(IntPtr.Zero))Theme.Scale=screen.DpiX/96f;
   string appearanceWarning;Theme.Apply(Preferences.Load(Preferences.PathName,out appearanceWarning).Appearance!="light");
   using(var mutex=new Mutex(false,"Local\\MyShot.Desktop.v1")) {
    // Depois de atualizar, a instância anterior ainda pode estar encerrando: espera a vez em vez de falhar.
    bool owned;
    try{owned=updated?mutex.WaitOne(TimeSpan.FromSeconds(20)):mutex.WaitOne(0);}catch(AbandonedMutexException){owned=true;}
    if(!owned){if(!startup)MessageBox.Show("O MyShot já está aberto. Use o ícone na bandeja.","MyShot");return;}
    Application.ThreadException+=(s,e)=>MessageBox.Show(e.Exception.Message,"MyShot · Erro",MessageBoxButtons.OK,MessageBoxIcon.Error);
    Application.Run(new Home(startup));
   }
  }
 }
 internal static class UI {
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
  // Barra de título escura combina com o grafite do app (atributo 20; 19 em builds anteriores do Windows 10).
  internal static void DarkTitleBar(IntPtr handle,bool dark) {int on=dark?1:0;try{if(DwmSetWindowAttribute(handle,20,ref on,4)!=0)DwmSetWindowAttribute(handle,19,ref on,4);}catch(Exception){}}
  internal static void Style(Form f) {f.Font=new Font("Segoe UI",10);f.BackColor=Theme.Background;f.ForeColor=Theme.Ink;f.Icon=Theme.AppIcon;f.StartPosition=FormStartPosition.CenterScreen;f.AutoScaleDimensions=new SizeF(96,96);f.AutoScaleMode=AutoScaleMode.Dpi;f.HandleCreated+=(s,e)=>DarkTitleBar(f.Handle,Theme.IsDark);}
  internal static Button Button(string text,Action action,bool primary) {
   var b=new ActionButton{Text=text,AutoSize=true,MinimumSize=new Size(100,40),Padding=new Padding(14,5,14,5),Primary=primary,BackColor=Theme.Background,ForeColor=primary?Theme.OnAccent:Theme.Ink,Margin=new Padding(4),AccessibleName=text};
   b.Click+=(s,e)=>action();return b;
  }
  internal static void Error(IWin32Window owner,Exception e) {MessageBox.Show(owner,e.Message,"MyShot",MessageBoxButtons.OK,MessageBoxIcon.Error);}
 }
 internal sealed class Selection : Form {
  readonly Bitmap desktop;Point origin,current;bool dragging;
  // Última região (em coordenadas do desktop): aparece destacada e Enter a confirma sem arrastar de novo.
  readonly Rectangle? previous;
  internal Rectangle Selected;
  internal Selection(Bitmap image,Rectangle bounds,Rectangle? remembered) {
   desktop=image;previous=remembered;AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;MaximumSize=bounds.Size;MinimumSize=bounds.Size;Bounds=bounds;TopMost=true;ShowInTaskbar=false;Cursor=Cursors.Cross;DoubleBuffered=true;KeyPreview=true;
   Shown+=(s,e)=>{Bounds=bounds;Activate();Native.SetForegroundWindow(Handle);};
  }
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.DrawImageUnscaled(desktop,0,0);
   Point mouse=PointToClient(Cursor.Position);
   using(var shade=new SolidBrush(Color.FromArgb(128,Color.Black)))e.Graphics.FillRectangle(shade,ClientRectangle);
   // Guias de cursor atravessam a tela inteira enquanto o usuário ainda não arrastou.
   if(!dragging)using(var guide=new Pen(Color.FromArgb(120,Color.White),1)){e.Graphics.DrawLine(guide,0,mouse.Y,ClientSize.Width,mouse.Y);e.Graphics.DrawLine(guide,mouse.X,0,mouse.X,ClientSize.Height);}
   var r=Geometry.Between(origin,current);
   if(dragging&&r.Width>0&&r.Height>0){e.Graphics.DrawImage(desktop,r,r,GraphicsUnit.Pixel);using(var pen=new Pen(Color.White,1))e.Graphics.DrawRectangle(pen,r);}
   else if(!dragging&&previous.HasValue){var p=previous.Value;e.Graphics.DrawImage(desktop,p,p,GraphicsUnit.Pixel);using(var pen=new Pen(Theme.Accent,2)){pen.DashStyle=DashStyle.Dash;e.Graphics.DrawRectangle(pen,p);}}
   string label=dragging?r.Width+" × "+r.Height+" px   ·   Esc cancela":previous.HasValue?"Enter: última região   ·   Arraste: nova   ·   Esc cancela":"Arraste para selecionar uma região   ·   Esc cancela";
   int x=Math.Max(8,Math.Min(ClientSize.Width-380,mouse.X+18)),y=Math.Max(8,Math.Min(ClientSize.Height-42,mouse.Y+22));
   e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var bg=new SolidBrush(Color.FromArgb(235,Theme.Canvas)))using(var shape=Theme.Round(new RectangleF(x,y,370,32),6))e.Graphics.FillPath(bg,shape);
   TextRenderer.DrawText(e.Graphics,label,SystemFonts.MessageBoxFont,new Point(x+10,y+9),Theme.Ink);
  }
  protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Right){DialogResult=DialogResult.Cancel;Close();}if(e.Button!=MouseButtons.Left)return;origin=current=e.Location;dragging=true;Capture=true;Invalidate();}
  protected override void OnMouseMove(MouseEventArgs e){if(dragging)current=new Point(Math.Max(0,Math.Min(ClientSize.Width,e.X)),Math.Max(0,Math.Min(ClientSize.Height,e.Y)));Invalidate();}
  protected override void OnMouseUp(MouseEventArgs e){if(!dragging||e.Button!=MouseButtons.Left)return;Capture=false;Selected=Rectangle.Intersect(new Rectangle(Point.Empty,desktop.Size),Geometry.Between(origin,current));if(Selected.Width<2||Selected.Height<2){dragging=false;Invalidate();return;}DialogResult=DialogResult.OK;Close();}
  protected override bool ProcessCmdKey(ref Message msg,Keys key){if(key==Keys.Escape){DialogResult=DialogResult.Cancel;Close();return true;}if(key==Keys.Enter&&previous.HasValue&&!dragging){Selected=previous.Value;DialogResult=DialogResult.OK;Close();return true;}return base.ProcessCmdKey(ref msg,key);}
  protected override void WndProc(ref Message m){if(m.Msg==0x02E0)return;base.WndProc(ref m);}
 }
 internal sealed class Canvas : Control {
  internal Bitmap Image;
  internal RectangleF? SelectedBounds {get;set;}
  // Espaço reservado no topo para a barra flutuante do editor, sem cobrir a imagem.
  internal int TopInset=16;
  internal Rectangle View {get{return Image==null?Rectangle.Empty:Geometry.Fit(Image.Size,Rectangle.FromLTRB(16,TopInset,ClientRectangle.Width-16,ClientRectangle.Height-16));}}
  internal Canvas(){DoubleBuffered=true;BackColor=Theme.Canvas;Dock=DockStyle.Fill;Cursor=Cursors.Cross;ResizeRedraw=true;TabStop=true;AccessibleName="Imagem capturada, área de anotação";AccessibleRole=AccessibleRole.Graphic;}
  protected override void OnPaint(PaintEventArgs e){
   base.OnPaint(e);if(Image==null||ClientSize.Width<35||ClientSize.Height<35)return;var shadow=View;shadow.Offset(2,3);using(var b=new SolidBrush(Color.FromArgb(110,Color.Black)))e.Graphics.FillRectangle(b,shadow);e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;e.Graphics.DrawImage(Image,View);using(var p=new Pen(Theme.Border))e.Graphics.DrawRectangle(p,View);
   if(SelectedBounds.HasValue){var bounds=SelectedBounds.Value;var view=View;float sx=(float)view.Width/Image.Width,sy=(float)view.Height/Image.Height;var r=new RectangleF(view.X+bounds.X*sx,view.Y+bounds.Y*sy,Math.Max(1,bounds.Width*sx),Math.Max(1,bounds.Height*sy));r.Inflate(3,3);var state=e.Graphics.Save();e.Graphics.SetClip(Rectangle.Inflate(view,5,5));using(var backdrop=new Pen(Theme.Canvas,3))e.Graphics.DrawRectangle(backdrop,r.X,r.Y,r.Width,r.Height);using(var pen=new Pen(Theme.Accent,1.5f)){pen.DashStyle=DashStyle.Dash;e.Graphics.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);}e.Graphics.Restore(state);}
  }
 }
 internal sealed class SettingsDialog : Form {
  internal Preferences Value;
  readonly WindowsStartup startup=new WindowsStartup(Application.ExecutablePath);
  readonly ToggleBox startWithWindows=new ToggleBox{Text="Iniciar com o Windows",AutoSize=true,AccessibleName="Iniciar com o Windows"};
  bool canConfigureStartup;
  string initialStartupCommand;
  internal void ApplyStartup(){if(!canConfigureStartup)return;if(startWithWindows.Checked){if(!String.Equals(initialStartupCommand,WindowsStartup.Command(Application.ExecutablePath),StringComparison.Ordinal))startup.SetEnabled(true);}else if(!String.IsNullOrWhiteSpace(initialStartupCommand))startup.SetEnabled(false);}
  internal SettingsDialog(Preferences current){
   UI.Style(this);Text="MyShot · Preferências";FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
   var panel=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(32,26,32,26),ColumnCount=2,AutoSize=true};
   panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,176));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   int row=0;
   // Cada linha ocupa a próxima linha da tabela: rótulo à esquerda e controle à direita (ou um item que ocupa as duas colunas).
   Action<Control,Control> put=(a,b)=>{
    panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    if(a==null){panel.Controls.Add(b,0,row);panel.SetColumnSpan(b,2);}
    else{panel.Controls.Add(a,0,row);panel.Controls.Add(b,1,row);}
    row++;
   };
   Func<string,Label> label=t=>new Label{Text=t,AutoSize=true,Font=new Font("Segoe UI",10f),ForeColor=Theme.Ink,Margin=new Padding(0,10,0,0)};
   Func<Control[],FlowLayoutPanel> inline=items=>{var f=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Margin=Padding.Empty};f.Controls.AddRange(items);return f;};
   Action<string,int> section=(t,top)=>{var h=Theme.Label(t,11,true);h.Margin=new Padding(0,top,0,2);put(null,h);};

   // Cabeçalho com a marca do app.
   var brand=new PictureBox{Image=HelpWindow.Logo(),SizeMode=PictureBoxSizeMode.Zoom,Size=new Size(44,44),Margin=new Padding(0,0,14,0)};
   var title=Theme.Label("Preferências",20,true);title.Margin=new Padding(0,6,0,0);
   put(null,inline(new Control[]{brand,title}));
   var subtitle=Theme.Label("Escolha onde salvar, como o app se comporta e quais atalhos usar.",10,false);subtitle.Margin=new Padding(0,0,0,4);
   put(null,subtitle);

   // Saída
   section("Saída",22);
   var folder=new TextBox{Text=current.Folder,AccessibleName="Pasta de saída"};
   var browse=(ActionButton)UI.Button("",()=>{using(var d=new FolderBrowserDialog{SelectedPath=folder.Text})if(d.ShowDialog(this)==DialogResult.OK)folder.Text=d.SelectedPath;},false);browse.Icon="folder";browse.AccessibleName="Escolher pasta";browse.AutoSize=false;browse.MinimumSize=new Size(40,40);browse.Size=new Size(40,40);browse.Margin=new Padding(8,0,0,0);
   var folderRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty,Height=48};folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));folderRow.RowStyles.Add(new RowStyle(SizeType.Percent,100));folderRow.Controls.Add(ThemedFrame.Wrap(folder),0,0);folderRow.Controls.Add(browse,1,0);
   put(label("Pasta"),folderRow);
   var format=new ChoiceBox{Width=140,AccessibleName="Formato da imagem"};format.Items.AddRange(new object[]{"png","jpg","bmp"});format.SelectedItem=current.Format;format.Margin=new Padding(0,4,0,4);
   put(label("Formato"),format);

   // Aparência e comportamento
   section("Aparência e comportamento",24);
   var appearance=new ChoiceBox{Width=140,AccessibleName="Aparência"};appearance.Items.AddRange(new object[]{"Escuro","Claro"});appearance.SelectedIndex=current.Appearance=="light"?1:0;appearance.Margin=new Padding(0,4,0,4);
   put(label("Tema"),appearance);
   var sound=new ToggleBox{Text="Reproduzir som após capturar",Checked=current.Sound,AutoSize=true,Margin=new Padding(0,6,0,6)};
   put(new Label(),sound);
   var barOpacity=new NumberField{Minimum=30,Maximum=100,Value=current.BarOpacity,Width=72,AccessibleName="Opacidade da barra"};
   var opacityRow=inline(new Control[]{barOpacity,new Label{Text="%",AutoSize=true,Font=new Font("Segoe UI",10f),ForeColor=Theme.Muted,Margin=new Padding(8,10,0,0)}});
   put(label("Opacidade da barra"),opacityRow);
   var startupHint=Theme.Label("Abre na bandeja ao entrar na sua conta.",9,false);startupHint.Margin=new Padding(0,0,0,4);
   try{initialStartupCommand=startup.RegisteredCommand();startWithWindows.Checked=!String.IsNullOrWhiteSpace(initialStartupCommand);canConfigureStartup=true;}catch(Exception){startWithWindows.Enabled=false;startupHint.Text="Não foi possível consultar a inicialização nesta sessão.";startupHint.ForeColor=Theme.WarningInk;}
   put(label("Inicialização"),startWithWindows);
   put(new Label(),startupHint);

   // Atalhos globais
   section("Atalhos globais",24);
   var modFlags=new[]{Preferences.ModCtrl,Preferences.ModAlt,Preferences.ModShift,Preferences.ModWin};var modNames=new[]{"Ctrl","Alt","Shift","Win"};
   var modRow=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Margin=new Padding(0,4,0,4)};var modBoxes=new ToggleBox[4];
   for(int i=0;i<4;i++){modBoxes[i]=new ToggleBox{Text=modNames[i],AccessibleName="Modificador "+modNames[i],Checked=(current.HotkeyModifiers&modFlags[i])!=0,Margin=new Padding(0,0,16,0)};modRow.Controls.Add(modBoxes[i]);}
   Func<int> selectedMods=()=>{int m=0;for(int i=0;i<4;i++)if(modBoxes[i].Checked)m|=modFlags[i];return m;};
   put(label("Modificadores"),modRow);
   var capture=new ChoiceBox{Width=72,AccessibleName="Letra do atalho de capturar região"};var file=new ChoiceBox{Width=72,AccessibleName="Letra do atalho de salvar desktop"};var freeKey=new ChoiceBox{Width=72,AccessibleName="Letra do atalho de área livre"};
   for(char c='A';c<='Z';c++){capture.Items.Add(c.ToString());file.Items.Add(c.ToString());freeKey.Items.Add(c.ToString());}
   capture.SelectedItem=((char)current.CaptureKey).ToString();file.SelectedItem=((char)current.FileKey).ToString();freeKey.SelectedItem=((char)current.FreeKey).ToString();
   Func<string> prefixText=()=>Preferences.ModifierText(selectedMods())+"+";
   var capturePrefix=Theme.Label(prefixText(),10,false);var filePrefix=Theme.Label(prefixText(),10,false);var freePrefix=Theme.Label(prefixText(),10,false);
   capturePrefix.Margin=new Padding(0,10,6,0);filePrefix.Margin=new Padding(0,10,6,0);freePrefix.Margin=new Padding(0,10,6,0);
   foreach(var box in modBoxes)box.CheckedChanged+=(s,e)=>{capturePrefix.Text=filePrefix.Text=freePrefix.Text=prefixText();};
   put(label("Capturar região"),inline(new Control[]{capturePrefix,capture}));
   put(label("Salvar desktop"),inline(new Control[]{filePrefix,file}));
   put(label("Área livre"),inline(new Control[]{freePrefix,freeKey}));

   // Rodapé: separador e ações.
   var rule=new Panel{BackColor=Theme.Border,Dock=DockStyle.Fill,Height=1,Margin=new Padding(0,22,0,18)};
   put(null,rule);
   var save=UI.Button("Salvar preferências",()=>{
    if(selectedMods()==0){MessageBox.Show(this,"Escolha pelo menos um modificador (Ctrl, Alt, Shift ou Win).");return;}
    if(!Path.IsPathRooted(folder.Text)||capture.SelectedItem==null||file.SelectedItem==null||freeKey.SelectedItem==null){MessageBox.Show(this,"Informe uma pasta absoluta e os atalhos.");return;}
    if(capture.SelectedItem.ToString()==file.SelectedItem.ToString()||capture.SelectedItem.ToString()==freeKey.SelectedItem.ToString()||file.SelectedItem.ToString()==freeKey.SelectedItem.ToString()){MessageBox.Show(this,"Os três atalhos devem ser diferentes.");return;}
    // Clone through data contract so cancel never mutates live preferences.
    using(var ms=new MemoryStream()){var ser=new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Preferences));ser.WriteObject(ms,current);ms.Position=0;Value=(Preferences)ser.ReadObject(ms);}
    Value.Folder=folder.Text;Value.Format=(string)format.SelectedItem;Value.Appearance=appearance.SelectedIndex==1?"light":"dark";Value.Modifiers=selectedMods();Value.CaptureKey=capture.SelectedItem.ToString()[0];Value.FileKey=file.SelectedItem.ToString()[0];Value.FreeKey=freeKey.SelectedItem.ToString()[0];Value.Sound=sound.Checked;Value.BarOpacity=(int)barOpacity.Value;DialogResult=DialogResult.OK;Close();
   },true);
   var cancel=UI.Button("Cancelar",()=>Close(),false);cancel.DialogResult=DialogResult.Cancel;
   var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,AutoSize=true};actions.Controls.Add(save);actions.Controls.Add(cancel);
   put(null,actions);

   Controls.Add(panel);AcceptButton=save;CancelButton=cancel;
   ClientSize=new Size(640,panel.GetPreferredSize(new Size(640,0)).Height);
  }
 }
}

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
   bool startup=Array.IndexOf(args,"--startup")>=0;
   try{Native.SetProcessDpiAwarenessContext(new IntPtr(-4));}catch(EntryPointNotFoundException){}
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   bool created;
   using(var mutex=new Mutex(true,"Local\\MyShot.Desktop.v1",out created)) {
    if(!created){if(!startup)MessageBox.Show("O MyShot já está aberto. Use o ícone na bandeja.","MyShot");return;}
    Application.ThreadException+=(s,e)=>MessageBox.Show(e.Exception.Message,"MyShot · Erro",MessageBoxButtons.OK,MessageBoxIcon.Error);
    Application.Run(new Home(startup));
   }
  }
 }
 internal static class UI {
  internal static readonly Color Background=Theme.Background, Ink=Theme.Ink, Accent=Theme.Accent;
  internal static void Style(Form f) {f.Font=new Font("Segoe UI",10);f.BackColor=Background;f.ForeColor=Ink;f.Icon=Theme.AppIcon;f.StartPosition=FormStartPosition.CenterScreen;f.AutoScaleDimensions=new SizeF(96,96);f.AutoScaleMode=AutoScaleMode.Dpi;}
  internal static Button Button(string text,Action action,bool primary) {
   var b=new ActionButton{Text=text,AutoSize=true,MinimumSize=new Size(100,40),Padding=new Padding(14,5,14,5),Primary=primary,BackColor=Background,ForeColor=primary?Theme.Surface:Ink,Margin=new Padding(4),AccessibleName=text};
   b.Click+=(s,e)=>action();return b;
  }
  internal static void Error(IWin32Window owner,Exception e) {MessageBox.Show(owner,e.Message,"MyShot",MessageBoxButtons.OK,MessageBoxIcon.Error);}
 }
 internal sealed class Home : Form {
  internal Preferences Prefs;
  readonly Label status=new Label{AutoSize=false,Dock=DockStyle.Fill,Padding=new Padding(16,12,16,12),ForeColor=Theme.Muted,Font=new Font("Segoe UI",9)};
  readonly Label captureShortcut=Theme.Label("",9,false),saveShortcut=Theme.Label("",9,false);
  readonly NotifyIcon tray;
  bool capturing, exiting, startupHidden;
  readonly System.Collections.Generic.List<Editor> editors=new System.Collections.Generic.List<Editor>();
  internal Home(bool startInTray=false) {
   UI.Style(this);Text="MyShot";ClientSize=new Size(672,528);MinimumSize=new Size(688,566);
   string warning;Prefs=Preferences.Load(Preferences.PathName,out warning);
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(28),RowCount=9,ColumnCount=1};
   foreach(int h in new[]{72,30,56,28,56,56,16,94,48})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
   var brand=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};brand.Controls.Add(new BrandMark{Location=new Point(0,4)});var name=Theme.Label("MyShot",24,true);name.Location=new Point(60,-2);brand.Controls.Add(name);var sub=Theme.Label("Capturas e anotações",9,false);sub.Location=new Point(63,42);brand.Controls.Add(sub);layout.Controls.Add(brand,0,0);
   layout.Controls.Add(Theme.Label("Nova captura",12.5f,true),0,1);
   var capture=(ActionButton)UI.Button("Capturar região",()=>BeginCapture(false,false),true);capture.Glyph="capture";capture.Font=new Font("Segoe UI",12,FontStyle.Bold);capture.Dock=DockStyle.Fill;capture.Margin=new Padding(0,0,0,4);layout.Controls.Add(capture,0,2);
   captureShortcut.Text="Arraste para selecionar. Depois, anote e compartilhe.";captureShortcut.Margin=new Padding(0,4,0,0);layout.Controls.Add(captureShortcut,0,3);
   layout.Controls.Add(CaptureAlternative("desktop","Desktop inteiro","Abrir todos os monitores no editor",()=>BeginCapture(false,true),null),0,4);
   layout.Controls.Add(CaptureAlternative("save","Salvar direto","Enviar o desktop inteiro para a pasta de saída",()=>BeginCapture(true,true),saveShortcut),0,5);
   layout.Controls.Add(status,0,7);
   var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=new Padding(0,8,0,0)};footer.Controls.Add(UI.Button("Preferências",Settings,false));var hide=(ActionButton)UI.Button("Ir para a bandeja",Hide,false);hide.Quiet=true;footer.Controls.Add(hide);var exit=(ActionButton)UI.Button("Sair",Exit,false);exit.Quiet=true;footer.Controls.Add(exit);layout.Controls.Add(footer,0,8);Controls.Add(layout);
   var menu=new ContextMenuStrip();menu.Items.Add("Capturar região",null,(s,e)=>BeginCapture(false,false));menu.Items.Add("Salvar desktop direto",null,(s,e)=>BeginCapture(true,true));menu.Items.Add("Abrir MyShot",null,(s,e)=>Restore());menu.Items.Add("Preferências",null,(s,e)=>{Restore();Settings();});menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Sair",null,(s,e)=>Exit());
   tray=new NotifyIcon{Text="MyShot · Captura de tela",Icon=Theme.AppIcon,ContextMenuStrip=menu,Visible=true};tray.DoubleClick+=(s,e)=>Restore();
   Shown+=(s,e)=>{RegisterKeys();if(warning!=null)status.Text+=Environment.NewLine+warning;};
   FormClosing+=(s,e)=>{if(!exiting&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();tray.ShowBalloonTip(2000,"MyShot","Continuo disponível na bandeja.",ToolTipIcon.Info);}};
   startupHidden=startInTray;
  }
  protected override void SetVisibleCore(bool value){if(startupHidden&&value){startupHidden=false;if(!IsHandleCreated)CreateHandle();RegisterKeys();base.SetVisibleCore(false);return;}base.SetVisibleCore(value);}
  Control CaptureAlternative(string glyph,string title,string description,Action action,Label shortcut){
   var row=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0,4,0,0)};row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,210));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var button=(ActionButton)UI.Button(title,action,false);button.Glyph=glyph;button.Dock=DockStyle.Fill;button.Margin=new Padding(0,0,16,4);row.Controls.Add(button,0,0);
   var descriptionLabel=shortcut??Theme.Label("",9,false);descriptionLabel.Text=description;descriptionLabel.AutoSize=false;descriptionLabel.Dock=DockStyle.Fill;descriptionLabel.TextAlign=ContentAlignment.MiddleLeft;row.Controls.Add(descriptionLabel,1,0);return row;
  }
  void Restore(){Show();WindowState=FormWindowState.Normal;Activate();}
  void Exit(){exiting=true;Close();}
  internal void Persist(){Prefs.Save(Preferences.PathName);}
  internal void RegisterKeys() {
   Native.UnregisterHotKey(Handle,1);Native.UnregisterHotKey(Handle,2);
   uint mods=0x4000u|2u|(Prefs.ShiftInsteadOfAlt?4u:1u);
   bool a=Native.RegisterHotKey(Handle,1,mods,(uint)Prefs.CaptureKey);
   bool b=Native.RegisterHotKey(Handle,2,mods,(uint)Prefs.FileKey);
   string prefix=Prefs.ShiftInsteadOfAlt?"Ctrl+Shift+":"Ctrl+Alt+";
   captureShortcut.Text=prefix+(Keys)Prefs.CaptureKey+"   ·   Selecione, anote e compartilhe.";
   saveShortcut.Text="Enviar para a pasta de saída\n"+prefix+(Keys)Prefs.FileKey;
   status.BackColor=!a||!b?Theme.Warning:Theme.Selected;status.ForeColor=!a||!b?Theme.WarningInk:Theme.Ink;
   status.Text=!a||!b?"Atalhos indisponíveis\n"+(!a?prefix+(Keys)Prefs.CaptureKey+"  ":"")+(!b?prefix+(Keys)Prefs.FileKey:"")+"\nAltere em Preferências ou use os botões acima.":"Atalhos prontos\nCapture mesmo com outro aplicativo em foco.\nAo fechar esta janela, o MyShot continua na bandeja.";
  }
  protected override void WndProc(ref Message m){if(m.Msg==0x0312){BeginCapture(m.WParam.ToInt32()==2,m.WParam.ToInt32()==2);return;}base.WndProc(ref m);}
  void Settings(){using(var f=new SettingsDialog(Prefs)){if(f.ShowDialog(this)==DialogResult.OK){try{f.Value.Save(Preferences.PathName);Prefs=f.Value;RegisterKeys();}catch(Exception e){UI.Error(this,e);return;}try{f.ApplyStartup();}catch(Exception e){MessageBox.Show(this,"As outras preferências foram salvas, mas não foi possível alterar a inicialização com o Windows.\n\n"+e.Message,"MyShot",MessageBoxButtons.OK,MessageBoxIcon.Error);}}}}
  internal async void BeginCapture(bool direct,bool whole) {
   if(capturing)return;capturing=true;
   bool restore=Visible;var visibleEditors=editors.FindAll(x=>x.Visible);Editor opened=null;
   try {
    Hide();foreach(var editor in visibleEditors)editor.Hide();
    await System.Threading.Tasks.Task.Delay(180);Native.DwmFlush();
    Rectangle bounds=SystemInformation.VirtualScreen;
    using(var desktop=DesktopCapture.Take(bounds)) {
     Bitmap selected=null;
     try {
      if(whole)selected=(Bitmap)desktop.Clone();
      else using(var selection=new Selection(desktop,bounds)){if(selection.ShowDialog()==DialogResult.OK)selected=desktop.Clone(selection.Selected,System.Drawing.Imaging.PixelFormat.Format32bppArgb);}
      if(selected==null)return;
      if(Prefs.Sound)System.Media.SystemSounds.Asterisk.Play();
      if(direct){Directory.CreateDirectory(Prefs.Folder);string path=Path.Combine(Prefs.Folder,"MyShot-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+"."+Prefs.Format);ImageOutput.Save(selected,path);tray.ShowBalloonTip(2500,"Imagem salva",path,ToolTipIcon.Info);}
      else {var editor=new Editor(selected,this);opened=editor;editors.Add(editor);editor.FormClosed+=(s,e)=>editors.Remove(editor);editor.Show();}
     }finally{if(selected!=null)selected.Dispose();}
    }
   }catch(Exception e){Restore();MessageBox.Show(this,"Não foi possível capturar o desktop. Execute na sessão interativa do Windows, fora da tela de bloqueio.\n\n"+e.Message,"MyShot",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   finally{capturing=false;foreach(var editor in visibleEditors)if(!editor.IsDisposed)editor.Show();if(restore)Restore();if(opened!=null&&!opened.IsDisposed){opened.BringToFront();opened.Activate();}}
  }
  protected override void Dispose(bool disposing){if(disposing){Native.UnregisterHotKey(Handle,1);Native.UnregisterHotKey(Handle,2);tray.Visible=false;tray.ContextMenuStrip.Dispose();tray.Dispose();foreach(var e in editors.ToArray())e.Dispose();}base.Dispose(disposing);}
 }
 internal sealed class Selection : Form {
  readonly Bitmap desktop;Point origin,current;bool dragging;
  internal Rectangle Selected;
  internal Selection(Bitmap image,Rectangle bounds) {
   desktop=image;AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;MaximumSize=bounds.Size;MinimumSize=bounds.Size;Bounds=bounds;TopMost=true;ShowInTaskbar=false;Cursor=Cursors.Cross;DoubleBuffered=true;KeyPreview=true;
   Shown+=(s,e)=>{Bounds=bounds;Activate();Native.SetForegroundWindow(Handle);};
  }
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.DrawImageUnscaled(desktop,0,0);
   using(var shade=new SolidBrush(Color.FromArgb(110,Theme.Ink)))e.Graphics.FillRectangle(shade,ClientRectangle);
   var r=Geometry.Between(origin,current);
   if(dragging&&r.Width>0&&r.Height>0){e.Graphics.DrawImage(desktop,r,r,GraphicsUnit.Pixel);using(var pen=new Pen(Theme.Surface,1))e.Graphics.DrawRectangle(pen,r);}
   string label=dragging?r.Width+" × "+r.Height+" px   ·   Esc cancela":"Arraste para selecionar uma região   ·   Esc cancela";
   Point mouse=PointToClient(Cursor.Position);int x=Math.Max(8,Math.Min(ClientSize.Width-380,mouse.X+18)),y=Math.Max(8,Math.Min(ClientSize.Height-42,mouse.Y+22));
   e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var bg=new SolidBrush(Theme.Ink))using(var shape=Theme.Round(new RectangleF(x,y,370,32),6))e.Graphics.FillPath(bg,shape);
   TextRenderer.DrawText(e.Graphics,label,SystemFonts.MessageBoxFont,new Point(x+10,y+9),Theme.Surface);
  }
  protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Right){DialogResult=DialogResult.Cancel;Close();}if(e.Button!=MouseButtons.Left)return;origin=current=e.Location;dragging=true;Capture=true;Invalidate();}
  protected override void OnMouseMove(MouseEventArgs e){if(dragging){current=new Point(Math.Max(0,Math.Min(ClientSize.Width,e.X)),Math.Max(0,Math.Min(ClientSize.Height,e.Y)));Invalidate();}}
  protected override void OnMouseUp(MouseEventArgs e){if(!dragging||e.Button!=MouseButtons.Left)return;Capture=false;Selected=Rectangle.Intersect(new Rectangle(Point.Empty,desktop.Size),Geometry.Between(origin,current));if(Selected.Width<2||Selected.Height<2){dragging=false;Invalidate();return;}DialogResult=DialogResult.OK;Close();}
  protected override bool ProcessCmdKey(ref Message msg,Keys key){if(key==Keys.Escape){DialogResult=DialogResult.Cancel;Close();return true;}return base.ProcessCmdKey(ref msg,key);}
  protected override void WndProc(ref Message m){if(m.Msg==0x02E0)return;base.WndProc(ref m);}
 }
 internal sealed class Canvas : Control {
  internal Bitmap Image;
  internal RectangleF? SelectedBounds {get;set;}
  internal Rectangle View {get{return Image==null?Rectangle.Empty:Geometry.Fit(Image.Size,Rectangle.Inflate(ClientRectangle,-16,-16));}}
  internal Canvas(){DoubleBuffered=true;BackColor=Theme.Canvas;Dock=DockStyle.Fill;Cursor=Cursors.Cross;ResizeRedraw=true;TabStop=true;AccessibleName="Imagem capturada, área de anotação";AccessibleRole=AccessibleRole.Graphic;}
  protected override void OnPaint(PaintEventArgs e){
   base.OnPaint(e);if(Image==null||ClientSize.Width<35||ClientSize.Height<35)return;var shadow=View;shadow.Offset(2,3);using(var b=new SolidBrush(Color.FromArgb(24,Theme.Ink)))e.Graphics.FillRectangle(b,shadow);e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;e.Graphics.DrawImage(Image,View);using(var p=new Pen(Theme.Border))e.Graphics.DrawRectangle(p,View);
   if(SelectedBounds.HasValue){var bounds=SelectedBounds.Value;var view=View;float sx=(float)view.Width/Image.Width,sy=(float)view.Height/Image.Height;var r=new RectangleF(view.X+bounds.X*sx,view.Y+bounds.Y*sy,Math.Max(1,bounds.Width*sx),Math.Max(1,bounds.Height*sy));r.Inflate(3,3);var state=e.Graphics.Save();e.Graphics.SetClip(Rectangle.Inflate(view,5,5));using(var backdrop=new Pen(Theme.Surface,3))e.Graphics.DrawRectangle(backdrop,r.X,r.Y,r.Width,r.Height);using(var pen=new Pen(Theme.Accent,1.5f)){pen.DashStyle=DashStyle.Dash;e.Graphics.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);}e.Graphics.Restore(state);}
  }
 }
 internal sealed class Editor : Form {
  readonly Document doc;readonly Home home;readonly Canvas canvas=new Canvas();readonly Label info=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,8,0,0),Font=new Font("Segoe UI",9),ForeColor=Theme.Muted,BackColor=Theme.Surface,AutoEllipsis=true};
  readonly ToolStripButton undo,redo,moveButton,toolsToggle,inspectorToggle;
  readonly ToolStrip tools=Sidebar(DockStyle.Left),propertyTools=Sidebar(DockStyle.Fill);
  readonly Panel inspector=new Panel{Dock=DockStyle.Right,BackColor=Theme.Surface},propertyContent=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Surface,AutoScroll=true,Padding=new Padding(16,12,16,16)};
  readonly ToolStripButton compactColor,compactWidth,compactOpacity,compactFill,compactFontSize,compactFont,compactEdit,compactDelete;
  bool toolsExpanded,inspectorExpanded;
  readonly Label propertyTitle=Theme.Label("Seta",12.5f,true),propertyHint=Theme.Label("",9,false);
  readonly ActionButton colorButton;
  readonly NumericUpDown widthInput,opacityInput,fontInput;
  readonly CheckBox fillInput;
  readonly Button fontButton,editTextButton,deleteButton;
  static readonly string[] ToolNames={"Linha","Retângulo","Elipse","Seta","Desenho","Texto","Mosaico","Etapa"};
  Tool selectedTool=Tool.Arrow;Mark pending,selectedMark,movePreview;Point moveStart;bool moving,moveMode,dirty=true;bool copied;
  internal Editor(Bitmap source,Home owner) {
   home=owner;doc=new Document(source);UI.Style(this);Text="MyShot · Editor";ClientSize=new Size(1200,760);MinimumSize=new Size(940,560);
   var top=new ToolStrip{GripStyle=ToolStripGripStyle.Hidden,Padding=new Padding(12,8,12,8),Dock=DockStyle.Top,AutoSize=false,Height=64,BackColor=Theme.Surface,Renderer=new ShotStripRenderer(),ImageScalingSize=new Size(20,20),Font=new Font("Segoe UI",10)};
   var title=new ToolStripLabel("Editor"){Font=new Font("Segoe UI",12.5f,FontStyle.Bold),Margin=new Padding(0,0,20,0)};top.Items.Add(title);
   Add(top,"Nova captura",()=>home.BeginCapture(false,false)).Image=Theme.Icon("capture",Theme.Ink);top.Items.Add(new ToolStripSeparator());
   undo=Add(top,"Desfazer",()=>History(false));undo.Image=Theme.Icon("undo",Theme.Ink);undo.ToolTipText="Desfazer (Ctrl+Z)";
   redo=Add(top,"Refazer",()=>History(true));redo.Image=Theme.Icon("redo",Theme.Ink);redo.ToolTipText="Refazer (Ctrl+Y)";
   var save=Add(top,"Salvar",Save);save.Tag="primary";save.Alignment=ToolStripItemAlignment.Right;save.Image=Theme.Icon("save",Theme.Surface);save.ToolTipText="Salvar imagem (Ctrl+S)";
   var copy=Add(top,"Copiar",Copy);copy.Alignment=ToolStripItemAlignment.Right;copy.Image=Theme.Icon("copy",Theme.Ink);copy.ToolTipText="Copiar imagem (Ctrl+C)";
   toolsToggle=Add(tools,"Ferramentas",()=>{toolsExpanded=!toolsExpanded;ApplyPanelLayout();canvas.Focus();});toolsToggle.AccessibleName="Expandir ferramentas";
   moveButton=Add(tools,"Mover",SelectMove);moveButton.Tag="move";moveButton.Image=Theme.Icon("move",Theme.Ink);moveButton.ToolTipText="Selecionar e mover objetos (V)";
   for(int i=0;i<ToolNames.Length;i++){Tool t=(Tool)i;var b=Add(tools,ToolNames[i],()=>SelectTool(t));b.Tag=t;b.CheckOnClick=false;b.Image=Theme.Icon(t.ToString(),Theme.Ink);b.ToolTipText=ToolNames[i];}
   var props=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Margin=Padding.Empty};props.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   propertyTitle.Margin=new Padding(0,0,0,8);props.Controls.Add(propertyTitle);
   propertyHint.AutoSize=false;propertyHint.Height=62;propertyHint.Dock=DockStyle.Top;propertyHint.Margin=new Padding(0,0,0,14);props.Controls.Add(propertyHint);
   var colorLabel=Theme.Label("Cor",9,true);props.Controls.Add(colorLabel);
   colorButton=(ActionButton)UI.Button("Alterar cor",()=>{using(var dialog=new ColorDialog{Color=Color.FromArgb(home.Prefs.Color),FullOpen=true})if(dialog.ShowDialog(this)==DialogResult.OK){home.Prefs.Color=dialog.Color.ToArgb();colorButton.Swatch=dialog.Color;colorButton.Invalidate();Store();}},false);colorButton.Tag=colorLabel;colorButton.Swatch=Color.FromArgb(home.Prefs.Color);colorButton.Dock=DockStyle.Top;colorButton.Margin=new Padding(0,6,0,20);props.Controls.Add(colorButton);
   widthInput=AddNumber(props,"Espessura (px)",1,40,home.Prefs.Thickness,v=>home.Prefs.Thickness=v);
   opacityInput=AddNumber(props,"Opacidade (%)",5,100,home.Prefs.Opacity,v=>home.Prefs.Opacity=v);
   fillInput=new CheckBox{Text="Preencher forma",AutoSize=true,Checked=home.Prefs.Filled,Margin=new Padding(0,0,0,20)};fillInput.CheckedChanged+=(s,e)=>{home.Prefs.Filled=fillInput.Checked;Store();};props.Controls.Add(fillInput);
   fontInput=AddNumber(props,"Tamanho do texto (px)",8,120,home.Prefs.FontSize,v=>home.Prefs.FontSize=v);
   fontButton=UI.Button("Escolher fonte",()=>{using(var dialog=new FontDialog{Font=new Font(home.Prefs.FontFamily,home.Prefs.FontSize,GraphicsUnit.Pixel),MinSize=8,MaxSize=120})if(dialog.ShowDialog(this)==DialogResult.OK){home.Prefs.FontFamily=dialog.Font.FontFamily.Name;Store();}},false);fontButton.Dock=DockStyle.Top;fontButton.Margin=new Padding(0,0,0,24);props.Controls.Add(fontButton);
   editTextButton=UI.Button("Editar texto",EditSelectedText,false);editTextButton.Dock=DockStyle.Top;editTextButton.Margin=new Padding(0,0,0,12);props.Controls.Add(editTextButton);
   deleteButton=UI.Button("Excluir (Del)",DeleteSelected,false);deleteButton.Dock=DockStyle.Top;deleteButton.Margin=new Padding(0,0,0,24);props.Controls.Add(deleteButton);
   var hint=Theme.Label("A imagem é salva na resolução original.",9,false);hint.AutoSize=false;hint.Height=48;hint.Dock=DockStyle.Top;props.Controls.Add(hint);
   // Explicit rows preserve label/input order when contextual properties are hidden.
   props.RowCount=props.Controls.Count;for(int row=0;row<props.Controls.Count;row++){props.SetCellPosition(props.Controls[row],new TableLayoutPanelCellPosition(0,row));props.RowStyles.Add(new RowStyle(SizeType.AutoSize));}propertyContent.Controls.Add(props);
   inspectorToggle=Add(propertyTools,"Propriedades",()=>{inspectorExpanded=!inspectorExpanded;ApplyPanelLayout();canvas.Focus();});
   compactColor=PropertyButton("Cor","color",()=>{RevealProperty(colorButton);colorButton.PerformClick();});
   compactWidth=PropertyButton("Espessura","width",()=>RevealProperty(widthInput));
   compactOpacity=PropertyButton("Opacidade","opacity",()=>RevealProperty(opacityInput));
   compactFill=PropertyButton("Preencher forma","fill",()=>{fillInput.Checked=!fillInput.Checked;canvas.Focus();});
   compactFontSize=PropertyButton("Tamanho do texto","fontSize",()=>RevealProperty(fontInput));
   compactFont=PropertyButton("Escolher fonte","font",()=>{RevealProperty(fontButton);fontButton.PerformClick();});
   compactEdit=PropertyButton("Editar texto","edit",EditSelectedText);compactDelete=PropertyButton("Excluir (Del)","delete",DeleteSelected);
   inspector.Controls.Add(propertyContent);inspector.Controls.Add(propertyTools);
   Controls.Add(canvas);Controls.Add(tools);Controls.Add(inspector);Controls.Add(info);Controls.Add(top);
   canvas.MouseDown+=Down;canvas.MouseMove+=MovePointer;canvas.MouseUp+=Up;canvas.MouseDoubleClick+=EditDoubleClick;canvas.MouseCaptureChanged+=(s,e)=>{if(!canvas.Capture&&(pending!=null||moving))CancelInteraction();};
   FormClosing+=(s,e)=>{if(dirty&&!copied&&MessageBox.Show(this,"Descartar esta captura sem salvar ou copiar?","MyShot",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)e.Cancel=true;};
   DpiChanged+=(s,e)=>ApplyPanelLayout();ApplyPanelLayout();SelectTool(selectedTool);RefreshImage();
  }
  static ToolStrip Sidebar(DockStyle dock){return new ToolStrip{GripStyle=ToolStripGripStyle.Hidden,Dock=dock,LayoutStyle=ToolStripLayoutStyle.VerticalStackWithOverflow,AutoSize=false,BackColor=Theme.Surface,Renderer=new ShotStripRenderer(true),Font=new Font("Segoe UI",10),AccessibleName=dock==DockStyle.Left?"Ferramentas de anotação":"Propriedades da ferramenta"};}
  int Dip(int value){return (int)Math.Round(value*DeviceDpi/96f);}
  ToolStripButton PropertyButton(string text,string icon,Action action){var button=Add(propertyTools,text,action);button.Image=Theme.Icon(icon,Theme.Ink);button.ToolTipText=text;return button;}
  static void ReplaceIcon(ToolStripButton button,string icon,Color color){var old=button.Image;button.Image=Theme.Icon(icon,color);if(old!=null)old.Dispose();}
  void LayoutSidebar(ToolStrip strip,bool expanded,ToolStripButton toggle){
   strip.Padding=new Padding(Dip(8));strip.ImageScalingSize=new Size(Dip(20),Dip(20));
   foreach(ToolStripItem item in strip.Items){item.AutoSize=false;item.Size=new Size(strip.Width-Dip(16),Dip(item==toggle?32:38));item.Margin=new Padding(0,0,0,Dip(item==toggle?8:2));item.Padding=new Padding(Dip(14),0,Dip(14),0);item.DisplayStyle=expanded?ToolStripItemDisplayStyle.ImageAndText:ToolStripItemDisplayStyle.Image;item.ImageAlign=expanded?ContentAlignment.MiddleLeft:ContentAlignment.MiddleCenter;item.TextAlign=ContentAlignment.MiddleLeft;}
  }
  void ApplyPanelLayout(){
   if(pending!=null||moving)CancelInteraction();SuspendLayout();inspector.SuspendLayout();tools.SuspendLayout();propertyTools.SuspendLayout();
   tools.Width=Dip(toolsExpanded?168:60);inspector.Width=Dip(inspectorExpanded?224:60);
   propertyContent.Visible=inspectorExpanded;propertyTools.Dock=inspectorExpanded?DockStyle.Top:DockStyle.Fill;propertyTools.Width=inspector.Width;if(inspectorExpanded)propertyTools.Height=Dip(56);
   ReplaceIcon(toolsToggle,toolsExpanded?"chevronLeft":"chevronRight",Theme.Ink);ReplaceIcon(inspectorToggle,inspectorExpanded?"chevronRight":"chevronLeft",Theme.Ink);
   toolsToggle.ToolTipText=toolsToggle.AccessibleName=(toolsExpanded?"Recolher":"Expandir")+" ferramentas";inspectorToggle.ToolTipText=inspectorToggle.AccessibleName=(inspectorExpanded?"Recolher":"Expandir")+" propriedades";
   LayoutSidebar(tools,toolsExpanded,toolsToggle);LayoutSidebar(propertyTools,inspectorExpanded,inspectorToggle);UpdateCompactProperties();
   tools.ResumeLayout();propertyTools.ResumeLayout();inspector.ResumeLayout();ResumeLayout(true);
  }
  void RevealProperty(Control control){inspectorExpanded=true;ApplyPanelLayout();propertyContent.ScrollControlIntoView(control);control.Focus();var number=control as NumericUpDown;if(number!=null)number.Select(0,number.Text.Length);}
  void UpdateCompactProperties(){
   bool compact=!inspectorExpanded,text=selectedTool==Tool.Text||selectedTool==Tool.Step;
   compactColor.Available=compact&&!moveMode&&selectedTool!=Tool.Mosaic;compactWidth.Available=compact&&!moveMode&&!text;compactOpacity.Available=compact&&!moveMode&&selectedTool!=Tool.Mosaic;
   compactFill.Available=compact&&!moveMode&&(selectedTool==Tool.Rectangle||selectedTool==Tool.Ellipse);compactFontSize.Available=compactFont.Available=compact&&!moveMode&&text;
   compactEdit.Available=compact&&moveMode&&selectedMark!=null&&selectedMark.Tool==Tool.Text;compactDelete.Available=compact&&moveMode&&selectedMark!=null;
   ReplaceIcon(compactColor,"color",Color.FromArgb(home.Prefs.Color));compactFill.Checked=home.Prefs.Filled;
   compactWidth.ToolTipText=(selectedTool==Tool.Mosaic?"Tamanho do mosaico: ":"Espessura: ")+home.Prefs.Thickness+" px";compactOpacity.ToolTipText="Opacidade: "+home.Prefs.Opacity+"%";compactFontSize.ToolTipText="Tamanho do texto: "+home.Prefs.FontSize+" px";compactFont.ToolTipText="Fonte: "+home.Prefs.FontFamily;
  }
  ToolStripButton Add(ToolStrip bar,string text,Action action){var b=new ToolStripButton(text){Padding=new Padding(10,8,10,8),Margin=new Padding(2,0,2,0),AccessibleName=text};b.Click+=(s,e)=>action();bar.Items.Add(b);return b;}
  NumericUpDown AddNumber(TableLayoutPanel panel,string text,int min,int max,int value,Action<int> set){var label=Theme.Label(text,9,true);panel.Controls.Add(label);var n=new NumericUpDown{Minimum=min,Maximum=max,Value=value,Dock=DockStyle.Top,Margin=new Padding(0,6,0,18),BorderStyle=BorderStyle.FixedSingle,BackColor=Theme.Surface,ForeColor=Theme.Ink,AccessibleName=text,Tag=label};n.ValueChanged+=(s,e)=>{set((int)n.Value);Store();};panel.Controls.Add(n);return n;}
  void Store(){UpdateCompactProperties();try{home.Persist();}catch(Exception e){info.Text="Preferências não salvas: "+e.Message;}}
  void SelectTool(Tool tool){CancelInteraction();moveMode=false;selectedMark=null;selectedTool=tool;UpdateInspector();canvas.Cursor=Cursors.Cross;RefreshImage();}
  void SelectMove(){CancelInteraction();moveMode=true;UpdateInspector();canvas.Cursor=Cursors.Default;canvas.Focus();RefreshImage();}
  void UpdateInspector(){
   foreach(ToolStripItem item in tools.Items){var button=item as ToolStripButton;if(button!=null)button.Checked=button==moveButton?moveMode:!moveMode&&button.Tag is Tool&&(Tool)button.Tag==selectedTool;}
   if(moveMode){propertyTitle.Text=selectedMark==null?"Mover objeto":ToolNames[(int)selectedMark.Tool];propertyHint.Text=selectedMark==null?"Clique em um objeto e arraste para reposicionar.":selectedMark.Tool==Tool.Text?"Duplo clique para editar o texto. Arraste para mover; Del exclui.":"Arraste para mover. Setas: 1 px; Shift + setas: 10 px. Del exclui.";}
   else{propertyTitle.Text=ToolNames[(int)selectedTool];string[] hints={"Arraste entre dois pontos para criar uma linha.","Arraste para marcar uma área retangular.","Arraste para destacar uma área circular.","Arraste na direção do detalhe que quer indicar.","Mantenha o botão pressionado para desenhar.","Clique na imagem para inserir seu texto.","Arraste sobre a área que deseja pixelizar.","Clique para adicionar a próxima etapa numerada."};propertyHint.Text=hints[(int)selectedTool];}
   bool text=selectedTool==Tool.Text||selectedTool==Tool.Step;fillInput.Visible=!moveMode&&(selectedTool==Tool.Rectangle||selectedTool==Tool.Ellipse);PropertyVisible(fontInput,!moveMode&&text);fontButton.Visible=!moveMode&&text;PropertyVisible(widthInput,!moveMode&&!text);PropertyVisible(opacityInput,!moveMode&&selectedTool!=Tool.Mosaic);PropertyVisible(colorButton,!moveMode&&selectedTool!=Tool.Mosaic);((Label)widthInput.Tag).Text=selectedTool==Tool.Mosaic?"Tamanho do mosaico":"Espessura (px)";
   editTextButton.Visible=moveMode&&selectedMark!=null&&selectedMark.Tool==Tool.Text;deleteButton.Visible=moveMode&&selectedMark!=null;
   UpdateCompactProperties();
  }
  static void PropertyVisible(Control control,bool visible){control.Visible=visible;var label=control.Tag as Control;if(label!=null)label.Visible=visible;}
  void RefreshImage(){if(selectedMark!=null&&!doc.Marks.Contains(selectedMark)){selectedMark=null;UpdateInspector();}var old=canvas.Image;canvas.Image=doc.Render(pending,selectedMark,movePreview);if(old!=null)old.Dispose();canvas.SelectedBounds=moveMode&&selectedMark!=null?(RectangleF?)AnnotationGeometry.Bounds(movePreview??selectedMark):null;canvas.Invalidate();undo.Enabled=doc.CanUndo;redo.Enabled=doc.CanRedo;info.Text=doc.Original.Width+" × "+doc.Original.Height+" px  ·  "+doc.Marks.Count+" anotações  ·  "+(moveMode?"Mover: arraste um objeto  ·  Esc cancela":"V: selecionar e mover objetos");}
  void Changed(){dirty=true;copied=false;RefreshImage();}
  void Down(object s,MouseEventArgs e){
   if(e.Button!=MouseButtons.Left||!canvas.View.Contains(e.Location))return;
   var p=Geometry.ImagePoint(e.Location,canvas.View,doc.Original.Size);var pref=home.Prefs;
   canvas.Focus();
   if(moveMode){selectedMark=doc.HitTest(p,HitTolerance());movePreview=null;moving=selectedMark!=null;moveStart=p;UpdateInspector();if(moving){canvas.Capture=true;canvas.Cursor=Cursors.SizeAll;}RefreshImage();return;}
   var mark=new Mark{Tool=selectedTool,A=p,B=p,Color=Color.FromArgb((int)(255*pref.Opacity/100.0),Color.FromArgb(pref.Color)),Width=pref.Thickness,Filled=pref.Filled,FontSize=pref.FontSize,FontFamily=pref.FontFamily,Number=doc.NextStep};
   if(selectedTool==Tool.Text){using(var f=new TextDialog()){if(f.ShowDialog(this)!=DialogResult.OK||String.IsNullOrWhiteSpace(f.Value))return;mark.Text=f.Value;}doc.Add(mark);Changed();return;}
   if(selectedTool==Tool.Step){doc.Add(mark);Changed();return;}
   mark.Points.Add(p);pending=mark;canvas.Capture=true;
  }
  float HitTolerance(){return 6f*canvas.DeviceDpi/96*doc.Original.Width/Math.Max(1,canvas.View.Width);}
  void MovePointer(object s,MouseEventArgs e){
   var p=Geometry.ImagePoint(e.Location,canvas.View,doc.Original.Size);
   if(moving){var delta=AnnotationGeometry.LimitMove(selectedMark,new Point(p.X-moveStart.X,p.Y-moveStart.Y),doc.Original.Size);movePreview=selectedMark.Translated(delta);RefreshImage();return;}
   if(pending==null){if(moveMode)canvas.Cursor=canvas.View.Contains(e.Location)&&doc.HitTest(p,HitTolerance())!=null?Cursors.SizeAll:Cursors.Default;return;}
   pending.B=p;if(pending.Tool==Tool.Pencil)pending.Points.Add(pending.B);RefreshImage();
  }
  void Up(object s,MouseEventArgs e){if(e.Button!=MouseButtons.Left||(!moving&&pending==null))return;MovePointer(s,e);CommitInteraction();}
  void CommitInteraction(){
   bool changed=false;
   if(moving&&movePreview!=null)changed=doc.Move(selectedMark,new Point(movePreview.A.X-selectedMark.A.X,movePreview.A.Y-selectedMark.A.Y));
   if(pending!=null&&(pending.A!=pending.B||pending.Tool==Tool.Pencil)){doc.Add(pending);changed=true;}
   moving=false;movePreview=null;pending=null;canvas.Capture=false;if(changed)Changed();else RefreshImage();
  }
  void CancelInteraction(){moving=false;movePreview=null;pending=null;canvas.Capture=false;RefreshImage();}
  void EditDoubleClick(object sender,MouseEventArgs e){
   if(!moveMode||e.Button!=MouseButtons.Left||!canvas.View.Contains(e.Location))return;
   var mark=doc.HitTest(Geometry.ImagePoint(e.Location,canvas.View,doc.Original.Size),HitTolerance());
   if(mark==null||mark.Tool!=Tool.Text)return;
   CancelInteraction();selectedMark=mark;UpdateInspector();RefreshImage();EditSelectedText();
  }
  void EditSelectedText(){
   if(selectedMark==null||selectedMark.Tool!=Tool.Text)return;
   CancelInteraction();var mark=selectedMark;
   using(var dialog=new TextDialog(mark.Text)){if(dialog.ShowDialog(this)==DialogResult.OK&&doc.EditText(mark,dialog.Value))Changed();}
   canvas.Focus();canvas.Cursor=Cursors.Default;
  }
  void DeleteSelected(){
   if(selectedMark==null)return;CancelInteraction();if(doc.Delete(selectedMark))Changed();canvas.Focus();canvas.Cursor=Cursors.Default;
  }
  void History(bool forward){CancelInteraction();if(forward)doc.Redo();else doc.Undo();Changed();}
  void Save(){if(moving||pending!=null)CommitInteraction();using(var dialog=new SaveFileDialog{Filter="PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg|Bitmap (*.bmp)|*.bmp",FilterIndex=home.Prefs.Format=="jpg"?2:home.Prefs.Format=="bmp"?3:1,InitialDirectory=home.Prefs.Folder,FileName="MyShot-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"),AddExtension=true,OverwritePrompt=true}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{doc.Save(dialog.FileName);dirty=false;info.Text="Salvo: "+dialog.FileName;}catch(Exception e){UI.Error(this,e);}}}
  void Copy(){if(moving||pending!=null)CommitInteraction();try{using(var bitmap=doc.Render(null)){var data=new DataObject();data.SetData(DataFormats.Bitmap,true,bitmap);Clipboard.SetDataObject(data,true,5,100);}copied=true;info.Text="Imagem copiada. Cole com Ctrl+V no aplicativo de destino.";}catch(Exception e){UI.Error(this,e);}}
  protected override bool ProcessCmdKey(ref Message msg,Keys key){
   if(key==(Keys.Control|Keys.S)){Save();return true;}if(key==(Keys.Control|Keys.C)){Copy();return true;}
   if(key==(Keys.Control|Keys.Z)){History(false);return true;}if(key==(Keys.Control|Keys.Y)||key==(Keys.Control|Keys.Shift|Keys.Z)){History(true);return true;}
   if(key==Keys.V&&!(ActiveControl is TextBoxBase)&&!(ActiveControl is UpDownBase)){SelectMove();return true;}
   if(key==Keys.Delete&&moveMode&&selectedMark!=null&&!(ActiveControl is TextBoxBase)&&!(ActiveControl is UpDownBase)){DeleteSelected();return true;}
   if(moveMode&&selectedMark!=null&&canvas.Focused){Keys code=key&Keys.KeyCode;Keys modifiers=key&Keys.Modifiers;if((modifiers==Keys.None||modifiers==Keys.Shift)&&(code==Keys.Left||code==Keys.Right||code==Keys.Up||code==Keys.Down)){if(moving)CommitInteraction();int step=modifiers==Keys.Shift?10:1;var delta=new Point(code==Keys.Left?-step:code==Keys.Right?step:0,code==Keys.Up?-step:code==Keys.Down?step:0);if(doc.Move(selectedMark,delta))Changed();return true;}}
   if(key==Keys.Escape){if(pending!=null||moving)CancelInteraction();else if(selectedMark!=null){selectedMark=null;UpdateInspector();RefreshImage();}else Close();return true;}return base.ProcessCmdKey(ref msg,key);
  }
  protected override void Dispose(bool disposing){if(disposing){Theme.DisposeImages(this);doc.Dispose();if(canvas.Image!=null){canvas.Image.Dispose();canvas.Image=null;}}base.Dispose(disposing);}
 }
 internal sealed class TextDialog : Form {
  readonly TextBox box=new TextBox{Multiline=true,Dock=DockStyle.Fill,AcceptsReturn=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.FixedSingle,BackColor=Theme.Surface,ForeColor=Theme.Ink,AccessibleName="Texto da anotação"};
  internal string Value {get{return box.Text;}}
  internal TextDialog(string existingText=null){
   bool editing=existingText!=null;UI.Style(this);Text=editing?"Editar texto":"Adicionar texto";ClientSize=new Size(460,280);Padding=new Padding(24);MinimumSize=new Size(400,300);
   var title=Theme.Label(editing?"Edite o texto da anotação":"O que você quer destacar?",12.5f,true);title.Dock=DockStyle.Top;title.Height=40;title.AutoSize=false;
   var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=56,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,12,0,0)};
   var b=UI.Button(editing?"Salvar texto":"Adicionar texto",()=>{if(String.IsNullOrWhiteSpace(box.Text))return;DialogResult=DialogResult.OK;Close();},true);
   var cancel=UI.Button("Cancelar",()=>Close(),false);cancel.DialogResult=DialogResult.Cancel;footer.Controls.Add(b);footer.Controls.Add(cancel);
   box.Text=existingText??"";box.TextChanged+=(s,e)=>b.Enabled=!String.IsNullOrWhiteSpace(box.Text);b.Enabled=!String.IsNullOrWhiteSpace(box.Text);
   Controls.Add(box);Controls.Add(title);Controls.Add(footer);CancelButton=cancel;Shown+=(s,e)=>{box.Focus();box.SelectAll();};
  }
 }
 internal sealed class SettingsDialog : Form {
  internal Preferences Value;
  readonly WindowsStartup startup=new WindowsStartup(Application.ExecutablePath);
  readonly CheckBox startWithWindows=new CheckBox{Text="Iniciar com o Windows",AutoSize=true,AccessibleName="Iniciar com o Windows"};
  bool canConfigureStartup;
  string initialStartupCommand;
  internal void ApplyStartup(){if(!canConfigureStartup)return;if(startWithWindows.Checked){if(!String.Equals(initialStartupCommand,WindowsStartup.Command(Application.ExecutablePath),StringComparison.Ordinal))startup.SetEnabled(true);}else if(!String.IsNullOrWhiteSpace(initialStartupCommand))startup.SetEnabled(false);}
  internal SettingsDialog(Preferences current){
   UI.Style(this);Text="MyShot · Preferências";ClientSize=new Size(640,668);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
   var panel=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(28),ColumnCount=2,RowCount=14};panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,168));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   foreach(int height in new[]{42,40,40,52,42,42,40,44,44,40,32,52,24,52})panel.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
   var title=Theme.Label("Preferências",20,true);panel.Controls.Add(title,0,0);panel.SetColumnSpan(title,2);
   var subtitle=Theme.Label("Defina onde salvar e como iniciar suas capturas.",10,false);panel.Controls.Add(subtitle,0,1);panel.SetColumnSpan(subtitle,2);
   var folder=new TextBox{Text=current.Folder,Dock=DockStyle.Fill,BorderStyle=BorderStyle.FixedSingle,BackColor=Theme.Surface,ForeColor=Theme.Ink,AccessibleName="Pasta de saída"};var browse=UI.Button("Escolher pasta…",()=>{using(var d=new FolderBrowserDialog{SelectedPath=folder.Text})if(d.ShowDialog(this)==DialogResult.OK)folder.Text=d.SelectedPath;},false);browse.Margin=new Padding(3,0,0,8);
   var format=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=130,AccessibleName="Formato da imagem"};format.Items.AddRange(new object[]{"png","jpg","bmp"});format.SelectedItem=current.Format;
   var shift=new CheckBox{Text="Usar Ctrl+Shift no lugar de Ctrl+Alt",AutoSize=true,Checked=current.ShiftInsteadOfAlt};
   var capture=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};var file=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};for(char c='A';c<='Z';c++){capture.Items.Add(c.ToString());file.Items.Add(c.ToString());}capture.SelectedItem=((char)current.CaptureKey).ToString();file.SelectedItem=((char)current.FileKey).ToString();
   var sound=new CheckBox{Text="Reproduzir som após capturar",Checked=current.Sound,AutoSize=true};
   panel.Controls.Add(Theme.Label("Pasta de saída",10,true),0,2);panel.Controls.Add(folder,1,2);panel.Controls.Add(browse,1,3);panel.Controls.Add(Theme.Label("Formato",10,true),0,4);panel.Controls.Add(format,1,4);
   var section=Theme.Label("Atalhos globais",12.5f,true);section.Margin=new Padding(0,8,0,0);panel.Controls.Add(section,0,5);panel.SetColumnSpan(section,2);panel.Controls.Add(shift,1,6);
   var capturePrefix=Theme.Label("Ctrl+Alt+",10,false);var filePrefix=Theme.Label("Ctrl+Alt+",10,false);Action update=()=>{capturePrefix.Text=filePrefix.Text=shift.Checked?"Ctrl+Shift+":"Ctrl+Alt+";};shift.CheckedChanged+=(s,e)=>update();update();
   capture.Width=file.Width=72;capture.AccessibleName="Letra do atalho de capturar região";file.AccessibleName="Letra do atalho de salvar desktop";
   var captureKeys=new FlowLayoutPanel{Dock=DockStyle.Fill};capturePrefix.Margin=new Padding(0,6,8,0);captureKeys.Controls.Add(capturePrefix);captureKeys.Controls.Add(capture);var fileKeys=new FlowLayoutPanel{Dock=DockStyle.Fill};filePrefix.Margin=new Padding(0,6,8,0);fileKeys.Controls.Add(filePrefix);fileKeys.Controls.Add(file);
   panel.Controls.Add(Theme.Label("Capturar região",10,true),0,7);panel.Controls.Add(captureKeys,1,7);panel.Controls.Add(Theme.Label("Salvar desktop",10,true),0,8);panel.Controls.Add(fileKeys,1,8);panel.Controls.Add(sound,1,9);
   var startupHint=Theme.Label("Abre na bandeja ao entrar na sua conta.",9,false);startupHint.AutoSize=false;startupHint.Dock=DockStyle.Fill;
   try{initialStartupCommand=startup.RegisteredCommand();startWithWindows.Checked=!String.IsNullOrWhiteSpace(initialStartupCommand);canConfigureStartup=true;}catch(Exception){startWithWindows.Enabled=false;startupHint.Text="Não foi possível consultar a inicialização nesta sessão.";startupHint.ForeColor=Theme.WarningInk;}
   panel.Controls.Add(Theme.Label("Inicialização",10,true),0,10);panel.Controls.Add(startWithWindows,1,10);panel.Controls.Add(startupHint,1,11);
   var save=UI.Button("Salvar preferências",()=>{
    if(!Path.IsPathRooted(folder.Text)||capture.SelectedItem==null||file.SelectedItem==null){MessageBox.Show(this,"Informe uma pasta absoluta e os atalhos.");return;}
    if(capture.SelectedItem.ToString()==file.SelectedItem.ToString()){MessageBox.Show(this,"Os dois atalhos devem ser diferentes.");return;}
    // Clone through data contract so cancel never mutates live preferences.
    using(var ms=new MemoryStream()){var ser=new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Preferences));ser.WriteObject(ms,current);ms.Position=0;Value=(Preferences)ser.ReadObject(ms);}
    Value.Folder=folder.Text;Value.Format=(string)format.SelectedItem;Value.ShiftInsteadOfAlt=shift.Checked;Value.CaptureKey=capture.SelectedItem.ToString()[0];Value.FileKey=file.SelectedItem.ToString()[0];Value.Sound=sound.Checked;DialogResult=DialogResult.OK;Close();
   },true);var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var cancel=UI.Button("Cancelar",()=>Close(),false);cancel.DialogResult=DialogResult.Cancel;actions.Controls.Add(save);actions.Controls.Add(cancel);panel.Controls.Add(actions,0,13);panel.SetColumnSpan(actions,2);Controls.Add(panel);AcceptButton=save;CancelButton=cancel;
  }
 }
}



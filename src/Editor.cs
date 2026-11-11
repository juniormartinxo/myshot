using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyShot {
 // Editor com uma barra flutuante sobre a imagem. Cor, espessura, opacidade e fonte ficam num popover aberto pelo botão Estilo.
 internal sealed class Editor : Form {
  readonly Document doc;readonly Home home;readonly Canvas canvas=new Canvas();
  readonly Label info=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,8,0,0),Font=new Font("Segoe UI",9),ForeColor=Theme.Muted,BackColor=Theme.Surface,AutoEllipsis=true};
  readonly ToolStrip toolbar=new ToolStrip{GripStyle=ToolStripGripStyle.Hidden,Dock=DockStyle.None,AutoSize=true,BackColor=Theme.Surface,Renderer=new ShotStripRenderer(),ImageScalingSize=new Size(Theme.IconSize,Theme.IconSize),Padding=new Padding(6),Font=new Font("Segoe UI",9)};
  readonly Panel popover=new Panel{Size=new Size(264,440),Visible=false,BackColor=Theme.Surface,Padding=new Padding(16,12,16,16),AutoScroll=true};
  readonly ToolStripButton undo,redo,moveButton,styleButton;
  readonly Label propertyTitle=Theme.Label("Seta",12.5f,true),propertyHint=Theme.Label("",9,false);
  readonly ActionButton colorButton;
  readonly NumericUpDown widthInput,opacityInput,fontInput;
  readonly CheckBox fillInput;
  readonly Button fontButton,editTextButton,deleteButton;
  static readonly string[] ToolNames={"Linha","Retângulo","Elipse","Seta","Desenho","Texto","Mosaico","Etapa"};
  InlineTextEditor textEditor;Mark textTarget,textSnapshot;bool textIsNew;
  Tool selectedTool=Tool.Arrow;Mark pending,selectedMark,movePreview;Point moveStart;bool moving,moveMode,dirty=true;bool copied;

  internal Editor(Bitmap source,Home owner) {
   home=owner;doc=new Document(source);UI.Style(this);Text="MyShot · Editor";ClientSize=new Size(1200,760);MinimumSize=new Size(940,560);

   Add("Nova captura",()=>home.BeginCapture(false,false)).Image=Theme.Icon("capture",Theme.Ink);
   toolbar.Items.Add(new ToolStripSeparator());
   undo=Add("Desfazer",()=>History(false));undo.Image=Theme.Icon("undo",Theme.Ink);undo.ToolTipText="Desfazer (Ctrl+Z)";
   redo=Add("Refazer",()=>History(true));redo.Image=Theme.Icon("redo",Theme.Ink);redo.ToolTipText="Refazer (Ctrl+Y)";
   toolbar.Items.Add(new ToolStripSeparator());
   moveButton=Add("Mover",SelectMove);moveButton.Tag="move";moveButton.Image=Theme.Icon("move",Theme.Ink);moveButton.ToolTipText="Selecionar e mover objetos (V)";
   for(int i=0;i<ToolNames.Length;i++){Tool t=(Tool)i;var b=Add(ToolNames[i],()=>SelectTool(t));b.Tag=t;b.Image=Theme.Icon(t.ToString(),Theme.Ink);b.ToolTipText=ToolNames[i];}
   toolbar.Items.Add(new ToolStripSeparator());
   styleButton=Add("Estilo",ToggleStyle);styleButton.Image=Theme.Icon("style",Theme.Ink);styleButton.ToolTipText="Cor, espessura, opacidade e fonte";
   toolbar.Items.Add(new ToolStripSeparator());
   var copy=Add("Copiar",Copy);copy.Image=Theme.Icon("copy",Theme.Ink);copy.ToolTipText="Copiar imagem (Ctrl+C)";
   var save=Add("Salvar",Save);save.Tag="primary";save.Image=Theme.Icon("save",Theme.OnAccent);save.ToolTipText="Salvar imagem (Ctrl+S)";
   toolbar.Items.Add(new ToolStripSeparator());
   var close=Add("Fechar",()=>Close());close.Image=Theme.Icon("close",Theme.Ink);close.ToolTipText="Fechar editor (Esc)";

   var props=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Margin=Padding.Empty};props.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   propertyTitle.Margin=new Padding(0,0,0,8);props.Controls.Add(propertyTitle);
   propertyHint.AutoSize=false;propertyHint.Height=48;propertyHint.Dock=DockStyle.Top;propertyHint.Margin=new Padding(0,0,0,14);props.Controls.Add(propertyHint);
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
   props.RowCount=props.Controls.Count;for(int row=0;row<props.Controls.Count;row++){props.SetCellPosition(props.Controls[row],new TableLayoutPanelCellPosition(0,row));props.RowStyles.Add(new RowStyle(SizeType.AutoSize));}
   popover.Controls.Add(props);

   // A imagem é preenchida primeiro; barra e popover ficam por cima dela.
   Controls.Add(canvas);Controls.Add(info);Controls.Add(toolbar);Controls.Add(popover);
   // Z-order explícito: o canvas fica atrás da barra e do popover, que flutuam sobre a imagem.
   canvas.SendToBack();toolbar.BringToFront();popover.BringToFront();
   canvas.MouseDown+=Down;canvas.MouseMove+=MovePointer;canvas.MouseUp+=Up;canvas.MouseDoubleClick+=EditDoubleClick;canvas.MouseCaptureChanged+=(s,e)=>{if(!canvas.Capture&&(pending!=null||moving))CancelInteraction();};
   FormClosing+=(s,e)=>{if(dirty&&!copied&&MessageBox.Show(this,"Descartar esta captura sem salvar ou copiar?","MyShot",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)e.Cancel=true;};
   Resize+=(s,e)=>LayoutChrome();DpiChanged+=(s,e)=>LayoutChrome();
   LayoutChrome();SelectTool(selectedTool);RefreshImage();
  }

  int Dip(int value){return (int)Math.Round(value*DeviceDpi/96f);}
  // Centraliza a barra no topo e posiciona o popover logo abaixo; a imagem começa abaixo da barra.
  void LayoutChrome(){
   toolbar.Size=toolbar.GetPreferredSize(Size.Empty);
   toolbar.Location=new Point(Math.Max(8,(ClientSize.Width-toolbar.Width)/2),Dip(12));
   popover.Location=new Point(Math.Max(8,(ClientSize.Width-popover.Width)/2),toolbar.Bottom+Dip(8));
   canvas.TopInset=toolbar.Bottom+Dip(12);
   canvas.Invalidate();
  }
  ToolStripButton Add(string text,Action action){var b=new ToolStripButton(text){DisplayStyle=ToolStripItemDisplayStyle.Image,Padding=new Padding(7),Margin=new Padding(1,0,1,0),AccessibleName=text,ToolTipText=text};b.Click+=(s,e)=>action();toolbar.Items.Add(b);return b;}
  NumericUpDown AddNumber(TableLayoutPanel panel,string text,int min,int max,int value,Action<int> set){var label=Theme.Label(text,9,true);panel.Controls.Add(label);var n=new NumericUpDown{Minimum=min,Maximum=max,Value=value,Dock=DockStyle.Top,Margin=new Padding(0,6,0,18),BorderStyle=BorderStyle.FixedSingle,BackColor=Theme.Surface,ForeColor=Theme.Ink,AccessibleName=text,Tag=label};n.ValueChanged+=(s,e)=>{set((int)n.Value);Store();};panel.Controls.Add(n);return n;}
  void Store(){try{home.Persist();}catch(Exception e){info.Text="Preferências não salvas: "+e.Message;}}
  void ToggleStyle(){popover.Visible=!popover.Visible;styleButton.Checked=popover.Visible;if(popover.Visible)popover.BringToFront();else canvas.Focus();}
  void SelectTool(Tool tool){CancelInteraction();moveMode=false;selectedMark=null;selectedTool=tool;UpdateInspector();canvas.Cursor=Cursors.Cross;RefreshImage();}
  void SelectMove(){CancelInteraction();moveMode=true;UpdateInspector();canvas.Cursor=Cursors.Default;canvas.Focus();RefreshImage();}
  void UpdateInspector(){
   foreach(ToolStripItem item in toolbar.Items){var button=item as ToolStripButton;if(button!=null&&button!=styleButton)button.Checked=button==moveButton?moveMode:!moveMode&&button.Tag is Tool&&(Tool)button.Tag==selectedTool;}
   if(moveMode){propertyTitle.Text=selectedMark==null?"Mover objeto":ToolNames[(int)selectedMark.Tool];propertyHint.Text=selectedMark==null?"Clique em um objeto e arraste para reposicionar.":selectedMark.Tool==Tool.Text?"Duplo clique para editar o texto. Arraste para mover; Del exclui.":"Arraste para mover. Setas: 1 px; Shift + setas: 10 px. Del exclui.";}
   else{propertyTitle.Text=ToolNames[(int)selectedTool];string[] hints={"Arraste entre dois pontos para criar uma linha.","Arraste para marcar uma área retangular.","Arraste para destacar uma área circular.","Arraste na direção do detalhe que quer indicar.","Mantenha o botão pressionado para desenhar.","Clique na imagem para inserir seu texto.","Arraste sobre a área que deseja pixelizar.","Clique para adicionar a próxima etapa numerada."};propertyHint.Text=hints[(int)selectedTool];}
   bool text=selectedTool==Tool.Text||selectedTool==Tool.Step;fillInput.Visible=!moveMode&&(selectedTool==Tool.Rectangle||selectedTool==Tool.Ellipse);PropertyVisible(fontInput,!moveMode&&text);fontButton.Visible=!moveMode&&text;PropertyVisible(widthInput,!moveMode&&!text);PropertyVisible(opacityInput,!moveMode&&selectedTool!=Tool.Mosaic);PropertyVisible(colorButton,!moveMode&&selectedTool!=Tool.Mosaic);((Label)widthInput.Tag).Text=selectedTool==Tool.Mosaic?"Tamanho do mosaico":"Espessura (px)";
   editTextButton.Visible=moveMode&&selectedMark!=null&&selectedMark.Tool==Tool.Text;deleteButton.Visible=moveMode&&selectedMark!=null;
  }
  static void PropertyVisible(Control control,bool visible){control.Visible=visible;var label=control.Tag as Control;if(label!=null)label.Visible=visible;}
  void RefreshImage(){if(selectedMark!=null&&!doc.Marks.Contains(selectedMark)){selectedMark=null;UpdateInspector();}var old=canvas.Image;canvas.Image=doc.Render(pending,selectedMark,movePreview);if(old!=null)old.Dispose();canvas.SelectedBounds=moveMode&&selectedMark!=null?(RectangleF?)AnnotationGeometry.Bounds(movePreview??selectedMark):null;canvas.Invalidate();undo.Enabled=doc.CanUndo;redo.Enabled=doc.CanRedo;info.Text=doc.Original.Width+" × "+doc.Original.Height+" px  ·  "+doc.Marks.Count+" anotações  ·  "+(moveMode?"Mover: arraste um objeto  ·  Esc cancela":"V: selecionar e mover objetos");}
  void Changed(){dirty=true;copied=false;RefreshImage();}
  void Down(object s,MouseEventArgs e){
   if(textEditor!=null){textEditor.Confirm();return;}
   if(e.Button!=MouseButtons.Left||!canvas.View.Contains(e.Location))return;
   var p=Geometry.ImagePoint(e.Location,canvas.View,doc.Original.Size);var pref=home.Prefs;
   canvas.Focus();
   if(moveMode){selectedMark=doc.HitTest(p,HitTolerance());movePreview=null;moving=selectedMark!=null;moveStart=p;UpdateInspector();if(moving){canvas.Capture=true;canvas.Cursor=Cursors.SizeAll;}RefreshImage();return;}
   var mark=new Mark{Tool=selectedTool,A=p,B=p,Color=Color.FromArgb((int)(255*pref.Opacity/100.0),Color.FromArgb(pref.Color)),Width=pref.Thickness,Filled=pref.Filled,FontSize=pref.FontSize,FontFamily=pref.FontFamily,Number=doc.NextStep};
   if(selectedTool==Tool.Text){StartTextEdit(mark,true);return;}
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
   StartTextEdit(selectedMark,false);
  }
  void DeleteSelected(){
   if(selectedMark==null)return;CancelInteraction();if(doc.Delete(selectedMark))Changed();canvas.Focus();canvas.Cursor=Cursors.Default;
  }
  void History(bool forward){if(textEditor!=null)textEditor.Confirm();CancelInteraction();if(forward)doc.Redo();else doc.Undo();Changed();}
  void Save(){if(textEditor!=null)textEditor.Confirm();if(moving||pending!=null)CommitInteraction();using(var dialog=new SaveFileDialog{Filter="PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg|Bitmap (*.bmp)|*.bmp",FilterIndex=home.Prefs.Format=="jpg"?2:home.Prefs.Format=="bmp"?3:1,InitialDirectory=home.Prefs.Folder,FileName="MyShot-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"),AddExtension=true,OverwritePrompt=true}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{doc.Save(dialog.FileName);dirty=false;info.Text="Salvo: "+dialog.FileName;}catch(Exception e){UI.Error(this,e);}}}
  void Copy(){if(textEditor!=null)textEditor.Confirm();if(moving||pending!=null)CommitInteraction();try{using(var bitmap=doc.Render(null)){var data=new DataObject();data.SetData(DataFormats.Bitmap,true,bitmap);Clipboard.SetDataObject(data,true,5,100);}copied=true;info.Text="Imagem copiada. Cole com Ctrl+V no aplicativo de destino.";}catch(Exception e){UI.Error(this,e);}}
  protected override bool ProcessCmdKey(ref Message msg,Keys key){
   if(textEditor!=null){if(key==Keys.Escape){textEditor.Cancel();return true;}if(key==(Keys.Control|Keys.Enter)){textEditor.Confirm();return true;}return base.ProcessCmdKey(ref msg,key);}
   if(key==(Keys.Control|Keys.S)){Save();return true;}if(key==(Keys.Control|Keys.C)){Copy();return true;}
   if(key==(Keys.Control|Keys.Z)){History(false);return true;}if(key==(Keys.Control|Keys.Y)||key==(Keys.Control|Keys.Shift|Keys.Z)){History(true);return true;}
   if(key==Keys.V&&!(ActiveControl is TextBoxBase)&&!(ActiveControl is UpDownBase)){SelectMove();return true;}
   if(key==Keys.Delete&&moveMode&&selectedMark!=null&&!(ActiveControl is TextBoxBase)&&!(ActiveControl is UpDownBase)){DeleteSelected();return true;}
   if(moveMode&&selectedMark!=null&&canvas.Focused){Keys code=key&Keys.KeyCode;Keys modifiers=key&Keys.Modifiers;if((modifiers==Keys.None||modifiers==Keys.Shift)&&(code==Keys.Left||code==Keys.Right||code==Keys.Up||code==Keys.Down)){if(moving)CommitInteraction();int step=modifiers==Keys.Shift?10:1;var delta=new Point(code==Keys.Left?-step:code==Keys.Right?step:0,code==Keys.Up?-step:code==Keys.Down?step:0);if(doc.Move(selectedMark,delta))Changed();return true;}}
   if(key==Keys.Escape){
    if(pending!=null||moving)CancelInteraction();
    else if(popover.Visible){popover.Visible=false;styleButton.Checked=false;canvas.Focus();}
    else if(selectedMark!=null){selectedMark=null;UpdateInspector();RefreshImage();}
    else Close();
    return true;
   }
   return base.ProcessCmdKey(ref msg,key);
  }
  protected override void Dispose(bool disposing){if(disposing){Theme.DisposeImages(this);doc.Dispose();if(canvas.Image!=null){canvas.Image.Dispose();canvas.Image=null;}}base.Dispose(disposing);}
  // Edição de texto no lugar: a caixa e a barra flutuante ficam sobre a anotação, com preview ao vivo.
  void StartTextEdit(Mark target,bool isNew){
   CancelInteraction();
   if(textEditor!=null)EndText();
   textTarget=target;textIsNew=isNew;textSnapshot=target.Translated(Point.Empty);
   if(isNew)pending=target;else{selectedMark=target;UpdateInspector();}
   float scale=canvas.View.Width/(float)doc.Original.Width;
   var style=new InlineTextEditor.Style{Text=target.Text,Color=target.Color,Family=target.FontFamily,Size=target.FontSize,Bold=target.Bold};
   textEditor=new InlineTextEditor(style,scale,TextPreview,TextConfirmed,TextCancelled);
   Controls.Add(textEditor);textEditor.BringToFront();
   PlaceTextEditor();RefreshImage();
   textEditor.Box.Focus();
  }
  void PlaceTextEditor(){
   var view=canvas.View;float sx=view.Width/(float)doc.Original.Width,sy=view.Height/(float)doc.Original.Height;
   RectangleF r=AnnotationGeometry.Bounds(textTarget);
   int bar=Dip(40),width=Math.Max(300,(int)(r.Width*sx)+40),height=bar+Math.Max(64,(int)(r.Height*sy)+12);
   int x=Math.Max(8,Math.Min(view.X+(int)(r.X*sx),ClientSize.Width-width-8));
   int y=Math.Max(8,view.Y+(int)(r.Y*sy)-bar);
   textEditor.SetScale(sx);
   textEditor.Bounds=new Rectangle(x,y,width,height);
  }
  void ApplyStyle(InlineTextEditor.Style s){
   textTarget.Text=s.Text;textTarget.Color=Color.FromArgb(textSnapshot.Color.A,s.Color);textTarget.FontFamily=s.Family;textTarget.FontSize=s.Size;textTarget.Bold=s.Bold;
  }
  void ApplySnapshot(){
   textTarget.Text=textSnapshot.Text;textTarget.Color=textSnapshot.Color;textTarget.FontFamily=textSnapshot.FontFamily;textTarget.FontSize=textSnapshot.FontSize;textTarget.Bold=textSnapshot.Bold;
  }
  void TextPreview(InlineTextEditor.Style s){ApplyStyle(s);PlaceTextEditor();RefreshImage();}
  void TextConfirmed(InlineTextEditor.Style s){
   var target=textTarget;bool isNew=textIsNew;
   if(isNew){
    ApplyStyle(s);pending=null;EndText();doc.Add(target);Changed();
   }else{
    ApplySnapshot();
    bool changed=doc.EditTextStyle(target,s.Text,Color.FromArgb(textSnapshot.Color.A,s.Color),s.Family,s.Size,s.Bold);
    EndText();
    if(changed)Changed();else RefreshImage();
   }
  }
  void TextCancelled(){
   if(textIsNew)pending=null;else ApplySnapshot();
   EndText();RefreshImage();
  }
  void EndText(){
   if(textEditor==null)return;
   var editor=textEditor;textEditor=null;textTarget=null;textSnapshot=null;textIsNew=false;
   Controls.Remove(editor);BeginInvoke(new Action(editor.Dispose));canvas.Focus();
  }
 }
}

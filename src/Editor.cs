using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyShot {
 // Editor: barra de ferramentas flutuante no topo e, logo abaixo, uma linha fixa com as opções da ferramenta ativa
 // (cor, espessura, opacidade, preenchimento, fonte, editar e excluir). A imagem fica abaixo das duas.
 internal sealed class Editor : Form {
  readonly Document doc;readonly Home home;readonly Canvas canvas=new Canvas();
  readonly Label info=new Label{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(16,8,0,0),Font=new Font("Segoe UI",9),ForeColor=Theme.Muted,BackColor=Theme.Surface,AutoEllipsis=true};
  readonly ToolStrip toolbar=new ToolStrip{GripStyle=ToolStripGripStyle.Hidden,Dock=DockStyle.None,AutoSize=true,BackColor=Theme.Surface,Renderer=new ShotStripRenderer(),ImageScalingSize=new Size(Theme.BarIconSize,Theme.BarIconSize),Padding=new Padding(6),Font=new Font("Segoe UI",9)};
  readonly FlowLayoutPanel optionsRow=new FlowLayoutPanel{Dock=DockStyle.None,AutoSize=false,WrapContents=false,BackColor=Theme.Surface,Padding=new Padding(12,8,12,8)};
  readonly ToolStripButton undo,redo,moveButton;
  readonly ActionButton colorButton,stepTextButton;
  readonly Label colorLabel=Theme.Label("Cor:",9,true),stepTextLabel=Theme.Label("Fonte:",9,true);
  readonly NumberField widthInput,opacityInput,fontInput;
  readonly CheckBox fillInput;
  readonly Button fontButton,editTextButton,deleteButton;
  static readonly string[] ToolNames={"Linha","Retângulo","Elipse","Seta","Desenho","Texto","Desfoque","Etapa","Polígono","Face"};
  InlineTextEditor textEditor;Mark textTarget,textSnapshot;bool textIsNew;
  Mark polygon;
  Tool selectedTool=Tool.Arrow;Mark pending,selectedMark,movePreview;Point moveStart;bool moving,moveMode,dirty=true;bool copied;

  // Captura de área livre tem transparência: o salvamento oferece só PNG, porque JPG e BMP não guardam transparência.
  internal bool KeepsTransparency;
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
   var copy=Add("Copiar",Copy);copy.Image=Theme.Icon("copy",Theme.Ink);copy.ToolTipText="Copiar imagem (Ctrl+C)";
   var save=Add("Salvar",Save);save.Tag="primary";save.Image=Theme.Icon("save",Theme.OnAccent);save.ToolTipText="Salvar imagem (Ctrl+S)";
   toolbar.Items.Add(new ToolStripSeparator());
   var close=Add("Fechar",()=>Close());close.Image=Theme.Icon("close",Theme.Ink);close.ToolTipText="Fechar editor (Esc)";

   colorButton=(ActionButton)UI.Button("",()=>{using(var dialog=new ColorDialog{Color=Color.FromArgb(home.Prefs.Color),FullOpen=true})if(dialog.ShowDialog(this)==DialogResult.OK){home.Prefs.Color=dialog.Color.ToArgb();colorButton.Swatch=dialog.Color;colorButton.Invalidate();Store();}},false);
   colorButton.AccessibleName="Cor";ColorSwatchButton(colorButton,colorLabel);colorButton.Swatch=Color.FromArgb(home.Prefs.Color);optionsRow.Controls.Add(ColorPair(colorLabel,colorButton));
   stepTextButton=(ActionButton)UI.Button("",()=>{using(var dialog=new ColorDialog{Color=Color.FromArgb(home.Prefs.StepTextColor),FullOpen=true})if(dialog.ShowDialog(this)==DialogResult.OK){home.Prefs.StepTextColor=dialog.Color.ToArgb();stepTextButton.Swatch=dialog.Color;stepTextButton.Invalidate();Store();}},false);
   stepTextButton.AccessibleName="Cor da fonte";ColorSwatchButton(stepTextButton,stepTextLabel);stepTextButton.Swatch=Color.FromArgb(home.Prefs.StepTextColor);optionsRow.Controls.Add(ColorPair(stepTextLabel,stepTextButton));
   widthInput=AddNumber("Espessura (px)",1,40,home.Prefs.Thickness,v=>home.Prefs.Thickness=v);
   opacityInput=AddNumber("Opacidade (%)",5,100,home.Prefs.Opacity,v=>home.Prefs.Opacity=v);
   fillInput=new CheckBox{Text="Preencher",AutoSize=true,Checked=home.Prefs.Filled,Margin=new Padding(0,8,14,0)};fillInput.CheckedChanged+=(s,e)=>{home.Prefs.Filled=fillInput.Checked;Store();};optionsRow.Controls.Add(fillInput);
   fontInput=AddNumber("Tamanho do texto (px)",8,120,home.Prefs.FontSize,v=>home.Prefs.FontSize=v);
   fontButton=UI.Button("Escolher fonte",()=>{using(var dialog=new FontDialog{Font=new Font(home.Prefs.FontFamily,home.Prefs.FontSize,GraphicsUnit.Pixel),MinSize=8,MaxSize=120})if(dialog.ShowDialog(this)==DialogResult.OK){home.Prefs.FontFamily=dialog.Font.FontFamily.Name;Store();}},false);fontButton.Margin=new Padding(0,0,14,0);optionsRow.Controls.Add(fontButton);
   editTextButton=UI.Button("Editar texto",EditSelectedText,false);editTextButton.Margin=new Padding(0,0,10,0);optionsRow.Controls.Add(editTextButton);
   deleteButton=UI.Button("Excluir (Del)",DeleteSelected,false);optionsRow.Controls.Add(deleteButton);

   // A imagem é preenchida primeiro; barra e linha de opções ficam por cima dela.
   Controls.Add(canvas);Controls.Add(info);Controls.Add(toolbar);Controls.Add(optionsRow);
   canvas.SendToBack();toolbar.BringToFront();optionsRow.BringToFront();
   canvas.MouseDown+=Down;canvas.MouseMove+=MovePointer;canvas.MouseUp+=Up;canvas.MouseDoubleClick+=EditDoubleClick;canvas.MouseCaptureChanged+=(s,e)=>{if(polygon==null&&!canvas.Capture&&(pending!=null||moving))CancelInteraction();};
   FormClosing+=(s,e)=>{if(dirty&&!copied&&MessageBox.Show(this,"Descartar esta captura sem salvar ou copiar?","MyShot",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)e.Cancel=true;};
   Resize+=(s,e)=>LayoutChrome();DpiChanged+=(s,e)=>LayoutChrome();
   LayoutChrome();SelectTool(selectedTool);RefreshImage();
  }

  int Dip(int value){return (int)Math.Round(value*DeviceDpi/96f);}
  // Barra no topo, linha de opções logo abaixo (centralizada); a imagem começa abaixo das duas.
  void LayoutChrome(){
   toolbar.Size=toolbar.GetPreferredSize(Size.Empty);
   toolbar.Location=new Point(Math.Max(8,(ClientSize.Width-toolbar.Width)/2),Dip(12));
   // Altura fixa: trocar a seleção não pode deslocar a imagem.
   optionsRow.Size=new Size(optionsRow.GetPreferredSize(Size.Empty).Width,Dip(56));
   optionsRow.Location=new Point(Math.Max(8,(ClientSize.Width-optionsRow.Width)/2),toolbar.Bottom+Dip(6));
   canvas.TopInset=optionsRow.Bottom+Dip(12);
   canvas.Invalidate();
  }
  ToolStripButton Add(string text,Action action){int pad=Dip(7),side=Theme.BarIconSize+2*pad;var b=new ToolStripButton(text){DisplayStyle=ToolStripItemDisplayStyle.Image,AutoSize=false,Size=new Size(side,side),Padding=new Padding(pad),ImageAlign=ContentAlignment.MiddleCenter,Margin=new Padding(1,0,1,0),AccessibleName=text,ToolTipText=text};b.Click+=(s,e)=>action();toolbar.Items.Add(b);return b;}
  // Rótulo e campo numérico lado a lado na linha de opções; o rótulo some junto com o campo.
  NumberField AddNumber(string text,int min,int max,int value,Action<int> set){
   var label=Theme.Label(text,9,true);label.Margin=new Padding(0,10,6,0);optionsRow.Controls.Add(label);
   var n=new NumberField{Minimum=min,Maximum=max,Value=value,Width=64,Margin=new Padding(0,6,16,0),AccessibleName=text,Tag=label};
   n.ValueChanged+=(s,e)=>{set((int)n.Value);Store();};optionsRow.Controls.Add(n);return n;
  }
  void Store(){try{home.Persist();}catch(Exception e){info.Text="Preferências não salvas: "+e.Message;}}
  // Rótulo e seletor de cor sempre juntos, na ordem rótulo → bolinha, dentro de um grupo próprio da linha de opções.
  static FlowLayoutPanel ColorPair(Label label,ActionButton button){
   var pair=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Margin=Padding.Empty,BackColor=Theme.Surface,Dock=DockStyle.None};
   pair.Controls.Add(label);pair.Controls.Add(button);
   pair.Tag=button;
   return pair;
  }
  // Botão só com a bolinha da cor (sem texto); o rótulo "Fundo:"/"Fonte:"/"Cor:" fica num controle à esquerda.
  static void ColorSwatchButton(ActionButton button,Label label){
   button.AutoSize=false;button.MinimumSize=new Size(40,34);button.Size=new Size(40,34);button.Margin=new Padding(0,4,14,0);button.Tag=label;
   label.Margin=new Padding(0,10,6,0);
  }
  void SelectTool(Tool tool){CancelInteraction();moveMode=false;selectedMark=null;selectedTool=tool;UpdateInspector();canvas.Cursor=Cursors.Cross;RefreshImage();}
  void SelectMove(){CancelInteraction();moveMode=true;UpdateInspector();canvas.Cursor=Cursors.Default;canvas.Focus();RefreshImage();}
  // Mostra na linha de opções só o que a ferramenta ativa (ou o objeto selecionado, no modo Mover) usa.
  void UpdateInspector(){
   foreach(ToolStripItem item in toolbar.Items){var button=item as ToolStripButton;if(button!=null)button.Checked=button==moveButton?moveMode:!moveMode&&button.Tag is Tool&&(Tool)button.Tag==selectedTool;}
   bool text=selectedTool==Tool.Text||selectedTool==Tool.Step;
   fillInput.Visible=!moveMode&&(selectedTool==Tool.Rectangle||selectedTool==Tool.Ellipse||selectedTool==Tool.Polygon||selectedTool==Tool.Face);
   PropertyVisible(fontInput,!moveMode&&text);fontButton.Visible=!moveMode&&text;
   PropertyVisible(widthInput,!moveMode&&!text);PropertyVisible(opacityInput,!moveMode&&selectedTool!=Tool.Mosaic);PropertyVisible(colorButton,!moveMode&&selectedTool!=Tool.Mosaic);
   PropertyVisible(stepTextButton,!moveMode&&selectedTool==Tool.Step);colorLabel.Text=selectedTool==Tool.Step?"Fundo:":selectedTool==Tool.Text?"Fonte:":"Cor:";
   ((Label)widthInput.Tag).Text=selectedTool==Tool.Mosaic?"Tamanho do desfoque":"Espessura (px)";
   editTextButton.Visible=moveMode&&selectedMark!=null&&selectedMark.Tool==Tool.Text;deleteButton.Visible=moveMode&&selectedMark!=null;
   LayoutChrome();
  }
  static void PropertyVisible(Control control,bool visible){control.Visible=visible;var label=control.Tag as Control;if(label!=null)label.Visible=visible;}
  void RefreshImage(){if(selectedMark!=null&&!doc.Marks.Contains(selectedMark)){selectedMark=null;UpdateInspector();}var old=canvas.Image;canvas.Image=doc.Render(pending,selectedMark,movePreview);if(old!=null)old.Dispose();canvas.SelectedBounds=moveMode&&selectedMark!=null?(RectangleF?)AnnotationGeometry.Bounds(movePreview??selectedMark):null;canvas.Invalidate();undo.Enabled=doc.CanUndo;redo.Enabled=doc.CanRedo;info.Text=doc.Original.Width+" × "+doc.Original.Height+" px  ·  "+doc.Marks.Count+" anotações  ·  "+(moveMode?"Mover: arraste um objeto  ·  Esc cancela":"V: selecionar e mover objetos");}
  void Changed(){dirty=true;copied=false;RefreshImage();}
  void Down(object s,MouseEventArgs e){
   if(textEditor!=null){textEditor.Confirm();return;}
   if((selectedTool==Tool.Polygon||selectedTool==Tool.Face)&&!moveMode&&e.Button==MouseButtons.Left&&canvas.View.Contains(e.Location)){AddPolygonPoint(Geometry.ImagePoint(e.Location,canvas.View,doc.Original.Size));return;}
   if(e.Button!=MouseButtons.Left||!canvas.View.Contains(e.Location))return;
   var p=Geometry.ImagePoint(e.Location,canvas.View,doc.Original.Size);var pref=home.Prefs;
   canvas.Focus();
   if(moveMode){selectedMark=doc.HitTest(p,HitTolerance());movePreview=null;moving=selectedMark!=null;moveStart=p;UpdateInspector();if(moving){canvas.Capture=true;canvas.Cursor=Cursors.SizeAll;}RefreshImage();return;}
   var mark=new Mark{Tool=selectedTool,A=p,B=p,Color=Color.FromArgb((int)(255*pref.Opacity/100.0),Color.FromArgb(pref.Color)),TextColor=Color.FromArgb(pref.StepTextColor),Width=pref.Thickness,Filled=pref.Filled,FontSize=pref.FontSize,FontFamily=pref.FontFamily,Number=doc.NextStep};
   if(selectedTool==Tool.Text){StartTextEdit(mark,true);return;}
   if(selectedTool==Tool.Step){doc.Add(mark);Changed();return;}
   mark.Points.Add(p);pending=mark;canvas.Capture=true;
  }
  float HitTolerance(){return 6f*canvas.DeviceDpi/96*doc.Original.Width/Math.Max(1,canvas.View.Width);}
  void MovePointer(object s,MouseEventArgs e){
   var p=Geometry.ImagePoint(e.Location,canvas.View,doc.Original.Size);
   if(polygon!=null){var preview=OpenPreview(polygon);preview.Points.Add(p);pending=preview;RefreshImage();return;}
   if(moving){var delta=AnnotationGeometry.LimitMove(selectedMark,new Point(p.X-moveStart.X,p.Y-moveStart.Y),doc.Original.Size);movePreview=selectedMark.Translated(delta);RefreshImage();return;}
   if(pending==null){if(moveMode)canvas.Cursor=canvas.View.Contains(e.Location)&&doc.HitTest(p,HitTolerance())!=null?Cursors.SizeAll:Cursors.Default;return;}
   pending.B=p;if(pending.Tool==Tool.Pencil)pending.Points.Add(pending.B);RefreshImage();
  }
  void Up(object s,MouseEventArgs e){if(polygon!=null)return;if(e.Button!=MouseButtons.Left||(!moving&&pending==null))return;MovePointer(s,e);CommitInteraction();}
  void CommitInteraction(){
   bool changed=false;
   if(moving&&movePreview!=null)changed=doc.Move(selectedMark,new Point(movePreview.A.X-selectedMark.A.X,movePreview.A.Y-selectedMark.A.Y));
   if(pending!=null&&(pending.A!=pending.B||pending.Tool==Tool.Pencil)){doc.Add(pending);changed=true;}
   moving=false;movePreview=null;pending=null;canvas.Capture=false;if(changed)Changed();else RefreshImage();
  }
  void CancelInteraction(){polygon=null;moving=false;movePreview=null;pending=null;canvas.Capture=false;RefreshImage();}
  void EditDoubleClick(object sender,MouseEventArgs e){
   if(polygon!=null){FinishPolygon();return;}
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
  void Save(){if(textEditor!=null)textEditor.Confirm();if(moving||pending!=null)CommitInteraction();using(var dialog=new SaveFileDialog{Filter=KeepsTransparency?"PNG (*.png)|*.png":"PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg|Bitmap (*.bmp)|*.bmp",FilterIndex=KeepsTransparency?1:home.Prefs.Format=="jpg"?2:home.Prefs.Format=="bmp"?3:1,InitialDirectory=home.Prefs.Folder,FileName="MyShot-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"),AddExtension=true,OverwritePrompt=true}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{doc.Save(dialog.FileName);dirty=false;Close();}catch(Exception e){UI.Error(this,e);}}}
  void Copy(){if(textEditor!=null)textEditor.Confirm();if(moving||pending!=null)CommitInteraction();try{using(var bitmap=doc.Render(null)){var data=new DataObject();data.SetData(DataFormats.Bitmap,true,bitmap);Clipboard.SetDataObject(data,true,5,100);}copied=true;Close();}catch(Exception e){UI.Error(this,e);}}
  protected override bool ProcessCmdKey(ref Message msg,Keys key){
   if(polygon!=null){if(key==Keys.Enter){FinishPolygon();return true;}if(key==Keys.Escape){CancelPolygon();return true;}}
   if(textEditor!=null){if(key==Keys.Escape){textEditor.Cancel();return true;}if(key==(Keys.Control|Keys.Enter)){textEditor.Confirm();return true;}return base.ProcessCmdKey(ref msg,key);}
   if(key==(Keys.Control|Keys.S)){Save();return true;}if(key==(Keys.Control|Keys.C)){Copy();return true;}
   if(key==(Keys.Control|Keys.Z)){History(false);return true;}if(key==(Keys.Control|Keys.Y)||key==(Keys.Control|Keys.Shift|Keys.Z)){History(true);return true;}
   if(key==Keys.V&&!(ActiveControl is TextBoxBase)&&!(ActiveControl is UpDownBase)){SelectMove();return true;}
   if(key==Keys.Delete&&moveMode&&selectedMark!=null&&!(ActiveControl is TextBoxBase)&&!(ActiveControl is UpDownBase)){DeleteSelected();return true;}
   if(moveMode&&selectedMark!=null&&canvas.Focused){Keys code=key&Keys.KeyCode;Keys modifiers=key&Keys.Modifiers;if((modifiers==Keys.None||modifiers==Keys.Shift)&&(code==Keys.Left||code==Keys.Right||code==Keys.Up||code==Keys.Down)){if(moving)CommitInteraction();int step=modifiers==Keys.Shift?10:1;var delta=new Point(code==Keys.Left?-step:code==Keys.Right?step:0,code==Keys.Up?-step:code==Keys.Down?step:0);if(doc.Move(selectedMark,delta))Changed();return true;}}
   if(key==Keys.Escape){
    if(pending!=null||moving)CancelInteraction();
    else if(selectedMark!=null){selectedMark=null;UpdateInspector();RefreshImage();}
    else Close();
    return true;
   }
   return base.ProcessCmdKey(ref msg,key);
  }
  protected override void Dispose(bool disposing){if(disposing){Theme.DisposeImages(this);doc.Dispose();if(canvas.Image!=null){canvas.Image.Dispose();canvas.Image=null;}}base.Dispose(disposing);}
  // Polígono: cada clique coloca um vértice; fecha ao voltar ao primeiro ponto, com duplo clique ou Enter.
  void AddPolygonPoint(Point p){
   if(selectedTool==Tool.Face)p=SnapToFaceVertex(p);
   if(polygon==null){
    var pref=home.Prefs;
    polygon=new Mark{Tool=selectedTool==Tool.Face?Tool.Face:Tool.Polygon,A=p,B=p,Color=Color.FromArgb((int)(255*pref.Opacity/100.0),Color.FromArgb(pref.Color)),Width=pref.Thickness,Filled=pref.Filled};
    polygon.Points.Add(p);pending=OpenPreview(polygon);RefreshImage();
    return;
   }
   var last=polygon.Points[polygon.Points.Count-1];
   if(Math.Abs(p.X-last.X)<=3&&Math.Abs(p.Y-last.Y)<=3)return;
   var first=polygon.Points[0];
   if(polygon.Points.Count>=3&&Math.Abs(p.X-first.X)<=HitTolerance()&&Math.Abs(p.Y-first.Y)<=HitTolerance()){FinishPolygon();return;}
   polygon.Points.Add(p);pending=OpenPreview(polygon);RefreshImage();
  }
  void FinishPolygon(){
   var done=polygon;polygon=null;pending=null;
   if(done!=null&&done.Points.Count>=3){doc.Add(done);Changed();}else RefreshImage();
  }
  void CancelPolygon(){polygon=null;pending=null;RefreshImage();}
  // Face aberta enquanto é desenhada: aparece como linha, sem fechar o contorno.
  static Mark OpenPreview(Mark m){var copy=m.Translated(Point.Empty);if(copy.Tool==Tool.Face)copy.Tool=Tool.Pencil;return copy;}
  // Um vértice novo se encaixa num vértice de face já criada, para as faces formarem um sólido juntas.
  Point SnapToFaceVertex(Point p){
   float tol=HitTolerance();
   foreach(var mark in doc.Marks)if(mark.Tool==Tool.Face)foreach(var v in mark.Points)if(Math.Abs(p.X-v.X)<=tol&&Math.Abs(p.Y-v.Y)<=tol)return v;
   return p;
  }
  // Edição de texto no lugar: a caixa e a barra flutuante ficam sobre a anotação, com preview ao vivo.
  void StartTextEdit(Mark target,bool isNew){
   CancelInteraction();
   if(textEditor!=null)EndText();
   textTarget=target;textIsNew=isNew;textSnapshot=target.Translated(Point.Empty);
   if(isNew)pending=target;else{selectedMark=target;UpdateInspector();}
   float scale=canvas.View.Width/(float)doc.Original.Width;
   var style=new InlineTextEditor.Style{Text=target.Text,Color=target.Color,Family=target.FontFamily,Size=target.FontSize,Bold=target.Bold};
   textEditor=new InlineTextEditor(style,scale,TextPreview,TextConfirmed,TextCancelled);
   textEditor.DragBegin=BeginTextDrag;textEditor.DragTo=TextDragTo;
   canvas.Controls.Add(textEditor);textEditor.BringToFront();
   PlaceTextEditor();RefreshImage();
   textEditor.Box.Focus();
  }
  void PlaceTextEditor(){
   var view=canvas.View;float sx=view.Width/(float)doc.Original.Width,sy=view.Height/(float)doc.Original.Height;
   RectangleF r=AnnotationGeometry.Bounds(textTarget);
   int width=Math.Max(Math.Max(300,(int)(r.Width*sx)+40),textEditor.BarWidth+8),height=textEditor.InputTop+Math.Max(64,(int)(r.Height*sy)+12);
   int x=Math.Max(8,Math.Min(view.X+(int)(r.X*sx),ClientSize.Width-width-8));
   int y=Math.Max(8,view.Y+(int)(r.Y*sy)-textEditor.InputTop);
   textEditor.SetScale(sx);
   textEditor.Bounds=new Rectangle(x,y,width,height);
  }
  void ApplyStyle(InlineTextEditor.Style s){
   textTarget.Text=s.Text;textTarget.Color=Color.FromArgb(textSnapshot.Color.A,s.Color);textTarget.FontFamily=s.Family;textTarget.FontSize=s.Size;textTarget.Bold=s.Bold;
  }
  void ApplySnapshot(){
   textTarget.Translate(new Point(textSnapshot.A.X-textTarget.A.X,textSnapshot.A.Y-textTarget.A.Y));
   textTarget.Text=textSnapshot.Text;textTarget.Color=textSnapshot.Color;textTarget.FontFamily=textSnapshot.FontFamily;textTarget.FontSize=textSnapshot.FontSize;textTarget.Bold=textSnapshot.Bold;
  }
  void TextPreview(InlineTextEditor.Style s){ApplyStyle(s);PlaceTextEditor();RefreshImage();}
  // Alça de arrasto da barra: move o texto ao vivo, na proporção da tela.
  Point textDragStart,textDragOrigin;
  void BeginTextDrag(){textDragStart=System.Windows.Forms.Cursor.Position;textDragOrigin=textTarget.A;}
  void TextDragTo(Point screen){
   float scale=canvas.View.Width/(float)doc.Original.Width;
   var origin=new Point(textDragOrigin.X+(int)Math.Round((screen.X-textDragStart.X)/scale),textDragOrigin.Y+(int)Math.Round((screen.Y-textDragStart.Y)/scale));
   textTarget.Translate(new Point(origin.X-textTarget.A.X,origin.Y-textTarget.A.Y));
   PlaceTextEditor();RefreshImage();
  }
  void TextConfirmed(InlineTextEditor.Style s){
   var target=textTarget;bool isNew=textIsNew;
   if(isNew){
    ApplyStyle(s);pending=null;EndText();doc.Add(target);Changed();
   }else{
    var origin=target.A;ApplySnapshot();
    bool changed=doc.EditTextStyle(target,s.Text,Color.FromArgb(textSnapshot.Color.A,s.Color),s.Family,s.Size,s.Bold,origin);
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
   canvas.Controls.Remove(editor);BeginInvoke(new Action(editor.Dispose));canvas.Focus();
  }
 }
}

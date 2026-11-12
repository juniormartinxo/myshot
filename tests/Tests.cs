using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MyShot {
 internal static class Tests {
  static int count;
  static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
  static bool Same(Bitmap a,Bitmap b){if(a.Size!=b.Size)return false;for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y))return false;return true;}
  static void Mouse(Control control,string eventName,Point point){typeof(Control).GetMethod("OnMouse"+eventName,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(control,new object[]{new MouseEventArgs(MouseButtons.Left,1,point.X,point.Y,0)});}
  static void Key(Editor editor,Keys key){typeof(Editor).GetMethod("ProcessCmdKey",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(editor,new object[]{Message.Create(editor.Handle,0,IntPtr.Zero,IntPtr.Zero),key});}
  static ToolStripButton StripButton(Control root,string text){foreach(Control control in root.Controls){var strip=control as ToolStrip;if(strip!=null)foreach(ToolStripItem item in strip.Items){var button=item as ToolStripButton;if(button!=null&&button.Text==text)return button;}var nested=StripButton(control,text);if(nested!=null)return nested;}return null;}
  static void ToolButton(Editor editor,string text){var button=StripButton(editor,text);if(button==null)throw new Exception("Tool not found: "+text);button.PerformClick();}
  static Point OnCanvas(Canvas canvas,Point pixel){var view=canvas.View;return new Point(view.X+(int)Math.Round(pixel.X*(double)view.Width/canvas.Image.Width),view.Y+(int)Math.Round(pixel.Y*(double)view.Height/canvas.Image.Height));}
  static T Find<T>(Control root,string text=null) where T:Control {foreach(Control child in root.Controls){if(child is T&&(text==null||child.Text==text))return (T)child;var nested=Find<T>(child,text);if(nested!=null)return nested;}return null;}
  // Abre a edição inline da anotação e entrega a caixa ao teste (o editor é o último formulário aberto).
  static void TextModal(Action open,Action<InlineTextEditor> interact){
   open();
   Editor editor=null;foreach(Form form in Application.OpenForms)if(form is Editor)editor=(Editor)form;
   var inline=editor==null?null:Find<InlineTextEditor>(editor);
   Check(inline!=null,"inline text editor opened");
   interact(inline);
  }
  static void Editing(Bitmap source,string output){
   foreach(Tool tool in Enum.GetValues(typeof(Tool)))using(var doc=new Document(source)){
    var bottom=new Mark{Tool=Tool.Rectangle,A=new Point(70,70),B=new Point(270,220),Color=Color.Green,Width=4,Filled=true};
    var mark=new Mark{Tool=tool,A=new Point(100,100),B=new Point(240,180),Color=Color.Red,Width=5,Text="Anotação",FontSize=24,Number=1};mark.Points.Add(mark.A);mark.Points.Add(mark.B);
    var top=new Mark{Tool=Tool.Ellipse,A=new Point(170,110),B=new Point(260,190),Color=Color.Blue,Width=4,Filled=true};doc.Add(bottom);doc.Add(top);
    using(var without=doc.Render(null)){
     doc.Undo();doc.Add(mark);doc.Add(top);
     using(var before=doc.Render(null)){
      Check(doc.Delete(mark)&&doc.Marks.Count==2,tool+" selected deletion removes only one annotation");
      using(var after=doc.Render(null))Check(Same(without,after),tool+" deletion restores pixels beneath object");
      doc.Undo();using(var undone=doc.Render(null))Check(doc.Marks[1]==mark&&Same(before,undone),tool+" deletion undo restores exact pixels and layer order");
      doc.Redo();using(var redone=doc.Render(null))Check(Same(without,redone),tool+" deletion redo restores pixels");
      doc.Undo();Check(!doc.Delete(new Mark())&&doc.CanRedo,tool+" missing-object deletion preserves redo");
     }
    }
   }
   using(var doc=new Document(source)){
    var mark=new Mark{Tool=Tool.Text,A=new Point(50,60),B=new Point(50,60),Text="Original",FontSize=24,Color=Color.Red};doc.Add(mark);
    using(var original=doc.Render(null)){
     doc.Move(mark,new Point(30,20));Point position=mark.A;
     Check(doc.EditText(mark,"Texto editado\nsegunda linha çã"),"text content can be edited with Unicode and line breaks");
     using(var edited=doc.Render(null)){
      Check(mark.A==position&&mark.FontSize==24&&mark.Color==Color.Red&&!Same(original,edited),"editing preserves text position and style");
      doc.Delete(mark);doc.Undo();using(var restored=doc.Render(null))Check(Same(edited,restored),"deletion undo restores edited text");
      doc.Undo();Check(mark.Text=="Original"&&mark.A==position,"text edit undo is separate from movement");
      Check(!doc.EditText(mark,"Original")&&!doc.EditText(mark," \r\n ")&&!doc.EditText(new Mark{Tool=Tool.Text},"absent")&&doc.CanRedo,"unchanged, blank and missing-object edits preserve redo");
      doc.Undo();using(var restored=doc.Render(null))Check(Same(original,restored),"mixed edit and move undo restores original text pixels");
      doc.Undo();Check(doc.Marks.Count==0,"mixed history can undo text creation");
      doc.Redo();doc.Redo();doc.Redo();using(var restored=doc.Render(null))Check(Same(edited,restored),"mixed history redo restores edited and moved text pixels");
      doc.EditText(mark,"Nova edição");Check(!doc.CanRedo,"new text edit invalidates deletion redo");
     }
    }
    var shape=new Mark{Tool=Tool.Rectangle};doc.Add(shape);Check(!doc.EditText(shape,"invalid"),"text editing rejects shapes");doc.Undo();doc.Delete(mark);Check(!doc.CanRedo,"new deletion invalidates redo branch");
    var styled=new Mark{Tool=Tool.Text,Text="a",A=new Point(5,5),Color=Color.Black,FontFamily="Segoe UI",FontSize=20};doc.Add(styled);Check(doc.EditTextStyle(styled,"b",Color.Red,"Arial",32,true,styled.A)&&styled.Text=="b"&&styled.Color==Color.Red&&styled.FontFamily=="Arial"&&styled.FontSize==32&&styled.Bold,"text style (color, font, size, bold) is editable");doc.Undo();Check(styled.Text=="a"&&styled.Color==Color.Black&&styled.FontFamily=="Segoe UI"&&styled.FontSize==20&&!styled.Bold,"single undo restores the whole text style");doc.Undo();
   }
   using(var home=new Home())using(var editor=new Editor(source,home)){
    editor.Show();Application.DoEvents();var canvas=Find<Canvas>(editor);
    var doc=(Document)typeof(Editor).GetField("doc",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(editor);
    ToolButton(editor,"Texto");
    TextModal(()=>Mouse(canvas,"Down",OnCanvas(canvas,new Point(100,100))),dialog=>{dialog.Box.Text="Texto original";dialog.Confirm();});
    Check(doc.Marks.Count==1,"text creation still works through dialog");var mark=doc.Marks[0];Point position=mark.A;
    Key(editor,Keys.V);Point hit=OnCanvas(canvas,new Point(110,110));Mouse(canvas,"Down",hit);Mouse(canvas,"Up",hit);
    var editButton=Find<Button>(editor,"Editar texto");Check(editButton.Visible&&Find<Button>(editor,"Excluir (Del)").Visible,"text selection exposes edit and delete actions");
    Mouse(canvas,"Down",hit);
    TextModal(()=>Mouse(canvas,"DoubleClick",hit),dialog=>{
     Check(dialog.Value=="Texto original"&&!canvas.Capture,"double click prefills text and releases mouse capture");
     dialog.Box.Text="Texto editado\r\ncom duas linhas";Check(mark.Text=="Texto editado\r\ncom duas linhas","edits show live on the annotation while typing");
     using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(output,"text-edit-dialog.png"));}
     dialog.Confirm();
    });Mouse(canvas,"Up",hit);
    Check(doc.Marks.Count==1&&mark.Text=="Texto editado\r\ncom duas linhas"&&mark.A==position,"double click edits in place without duplicate object or movement");
    Check(canvas.SelectedBounds.HasValue&&canvas.Focused,"editing preserves selection and returns keyboard focus to canvas");
    string path=Path.Combine(output,"edited-text-export.png");doc.Save(path);using(var saved=new Bitmap(path))Check(Same(saved,canvas.Image),"edited text export matches preview without selection decoration");
    Key(editor,Keys.Control|Keys.Z);Check(mark.Text=="Texto original","editor Ctrl+Z restores previous text");
    TextModal(()=>editButton.PerformClick(),dialog=>{dialog.Box.Text="cancelar esta alteração";dialog.Cancel();});
    Check(mark.Text=="Texto original"&&doc.CanRedo,"canceling text edit changes neither object nor redo history");
    TextModal(()=>editButton.PerformClick(),dialog=>{dialog.Confirm();});
    Check(doc.CanRedo,"saving unchanged text preserves redo history");
    TextModal(()=>editButton.PerformClick(),dialog=>{dialog.Box.Text=" \r\n ";Check(!dialog.CanConfirm,"blank text disables save instead of deleting object");dialog.Cancel();});
    Key(editor,Keys.Control|Keys.Y);Check(mark.Text.StartsWith("Texto editado"),"editor redo restores edited text after canceled dialogs");
    editor.Size=editor.MinimumSize;Application.DoEvents();using(var image=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(output,"editor-text-selected.png"));}
    using(var input=new TextBox{Text="123",Location=new Point(220,100)}){editor.Controls.Add(input);input.BringToFront();input.Focus();Key(editor,Keys.Delete);Check(doc.Marks.Count==1,"Delete does not remove annotation while a text input has focus");editor.Controls.Remove(input);}
    canvas.Focus();hit=OnCanvas(canvas,new Point(110,110));Mouse(canvas,"Down",hit);Mouse(canvas,"Move",OnCanvas(canvas,new Point(150,140)));Key(editor,Keys.Delete);
    Check(doc.Marks.Count==0&&!canvas.SelectedBounds.HasValue&&!canvas.Capture&&Same(source,canvas.Image),"Del cancels drag and removes selected text and selection contour");
    Check(!editButton.Visible&&!Find<Button>(editor,"Excluir (Del)").Visible,"deletion clears contextual object actions");
    Key(editor,Keys.Delete);Key(editor,Keys.Control|Keys.Z);Check(doc.Marks.Count==1&&mark.A==position&&mark.Text.StartsWith("Texto editado"),"single undo restores deleted text without committing pending drag");
    Key(editor,Keys.Control|Keys.Y);Check(doc.Marks.Count==0,"editor redo repeats deletion");
    ToolButton(editor,"Retângulo");Mouse(canvas,"Down",OnCanvas(canvas,new Point(80,80)));Mouse(canvas,"Up",OnCanvas(canvas,new Point(220,180)));Key(editor,Keys.V);hit=OnCanvas(canvas,new Point(110,110));Mouse(canvas,"Down",hit);Mouse(canvas,"Up",hit);
    Check(!editButton.Visible,"shape selection hides text editing action");Find<Button>(editor,"Excluir (Del)").PerformClick();Check(doc.Marks.Count==0,"delete button removes selected shape");Key(editor,Keys.Control|Keys.Z);
    Mouse(canvas,"Down",hit);Mouse(canvas,"Up",hit);Key(editor,Keys.Delete);Check(doc.Marks.Count==0,"Del also removes selected non-text object");
   }
  }
  static void SidebarFlow(Bitmap source,string output){
   using(var home=new Home())using(var editor=new Editor(source,home)){
    editor.Show();editor.Size=editor.MinimumSize;Application.DoEvents();var canvas=Find<Canvas>(editor);
    ToolButton(editor,"Linha");Mouse(canvas,"Down",OnCanvas(canvas,new Point(80,100)));Mouse(canvas,"Up",OnCanvas(canvas,new Point(280,240)));
    var doc=(Document)typeof(Editor).GetField("doc",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(editor);var mark=doc.Marks[0];var original=mark.A;
    using(var before=doc.Render(null)){
     Snapshot(editor,output,"editor-toolbar-only.png");Snapshot(editor,output,"editor-style-popover.png");
     using(var after=doc.Render(null))Check(Same(before,after)&&mark.A==original,"style popover toggles preserve annotations and exported pixels");
    }
    Check(Find<NumberField>(editor).Visible,"style popover shows numeric inputs for the active tool");
    Key(editor,Keys.V);Point hit=OnCanvas(canvas,new Point(180,170));Mouse(canvas,"Down",hit);Mouse(canvas,"Up",hit);Check(canvas.SelectedBounds.HasValue,"object remains selectable after repeated panel layout changes");
    Find<Button>(editor,"Excluir (Del)").PerformClick();Check(doc.Marks.Count==0,"style popover deletes the selected object");Key(editor,Keys.Control|Keys.Z);Check(doc.Marks.Count==1,"collapsed-rail deletion can be undone");
    ToolButton(editor,"Texto");TextModal(()=>Mouse(canvas,"Down",OnCanvas(canvas,new Point(350,80))),dialog=>{dialog.Box.Text="Texto";dialog.Confirm();});Key(editor,Keys.V);
    hit=OnCanvas(canvas,new Point(360,90));Mouse(canvas,"Down",hit);Mouse(canvas,"Up",hit);
    TextModal(()=>Find<Button>(editor,"Editar texto").PerformClick(),dialog=>{dialog.Box.Text="Texto atualizado";dialog.Confirm();});
    Check(doc.Marks[1].Text=="Texto atualizado"&&canvas.SelectedBounds.HasValue,"collapsed property rail edits text while preserving selection");Snapshot(editor,output,"editor-panels-text-selected.png");
   }
  }
  static void Snapshot(Editor editor,string output,string name){Application.DoEvents();using(var image=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(output,name));}}
  static void Movement(Bitmap source,string output){
   foreach(Tool tool in Enum.GetValues(typeof(Tool)))using(var doc=new Document(source)){
    var mark=new Mark{Tool=tool,A=new Point(100,100),B=new Point(240,180),Color=Color.FromArgb(200,230,50,70),Width=5,Text="Texto\nem duas linhas",FontSize=24,Number=1};mark.Points.Add(mark.A);mark.Points.Add(new Point(160,140));mark.Points.Add(mark.B);doc.Add(mark);
    Point hit=tool==Tool.Text?new Point(110,110):tool==Tool.Step?mark.A:new Point(170,140);
    Check(doc.HitTest(hit,6)==mark,tool+" can be selected");
    var delta=new Point(35,25);var moved=mark.Translated(delta);
    using(var original=doc.Render(null))using(var preview=doc.Render(null,mark,moved)){
     Check(mark.A==new Point(100,100)&&mark.Points[0]==mark.A,tool+" drag preview does not mutate committed geometry");
     Check(doc.Move(mark,delta),tool+" move commits");
     using(var committed=doc.Render(null)){
      Check(!Same(original,committed)&&Same(preview,committed),tool+" committed pixels match drag preview");
      doc.Undo();using(var undone=doc.Render(null))Check(Same(original,undone),tool+" undo restores position and pixels");
      Check(!doc.Move(mark,Point.Empty)&&doc.CanRedo,tool+" zero movement preserves redo");
      doc.Redo();using(var redone=doc.Render(null))Check(Same(committed,redone),tool+" redo restores moved pixels");
      doc.Undo();doc.Undo();Check(doc.Marks.Count==0,tool+" move and creation undo independently");doc.Redo();doc.Redo();using(var restored=doc.Render(null))Check(Same(committed,restored),tool+" mixed creation/movement redo restores image");
     }
    }
    doc.Move(mark,new Point(10000,10000));RectangleF bounds=AnnotationGeometry.Bounds(mark);Check(bounds.Right<=source.Width&&bounds.Bottom<=source.Height,tool+" cannot be dragged beyond right/bottom bounds");
    doc.Move(mark,new Point(-10000,-10000));bounds=AnnotationGeometry.Bounds(mark);Check(bounds.Left>=0&&bounds.Top>=0,tool+" cannot be dragged beyond left/top bounds");
   }
   using(var doc=new Document(source)){
    var bottom=new Mark{Tool=Tool.Rectangle,A=new Point(80,80),B=new Point(200,200),Color=Color.Red,Width=4,Filled=true};var top=new Mark{Tool=Tool.Rectangle,A=new Point(120,100),B=new Point(260,240),Color=Color.Blue,Width=4,Filled=true};doc.Add(bottom);doc.Add(top);
    Check(doc.HitTest(new Point(150,150),4)==top,"overlapping objects select the topmost annotation");doc.Move(bottom,new Point(20,20));using(var b=doc.Render(null))Check(b.GetPixel(150,150).ToArgb()==Color.Blue.ToArgb(),"moving preserves layer order");
    doc.Undo();doc.Move(bottom,new Point(10,10));Check(!doc.CanRedo,"new move invalidates redo branch");
    var line=new Mark{Tool=Tool.Line,A=new Point(20,20),B=new Point(80,80),Width=2};Check(!AnnotationGeometry.Hit(line,new Point(20,80),3),"line hit test excludes empty corners of bounding box");
   }
   using(var home=new Home())using(var editor=new Editor(source,home)){
    editor.Show();Application.DoEvents();Canvas canvas=null;foreach(Control c in editor.Controls)if(c is Canvas)canvas=(Canvas)c;
    var doc=(Document)typeof(Editor).GetField("doc",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(editor);
    ToolButton(editor,"Retângulo");Mouse(canvas,"Down",OnCanvas(canvas,new Point(80,80)));Mouse(canvas,"Move",OnCanvas(canvas,new Point(220,160)));Mouse(canvas,"Up",OnCanvas(canvas,new Point(220,160)));
    Check(doc.Marks.Count==1,"editor pointer events create an annotation");var mark=doc.Marks[0];Point original=mark.A;
    Key(editor,Keys.V);Point start=OnCanvas(canvas,new Point(140,120)),end=OnCanvas(canvas,new Point(190,150));Point startImage=Geometry.ImagePoint(start,canvas.View,source.Size),endImage=Geometry.ImagePoint(end,canvas.View,source.Size);var delta=new Point(endImage.X-startImage.X,endImage.Y-startImage.Y);
    Mouse(canvas,"Down",start);Mouse(canvas,"Move",OnCanvas(canvas,new Point(160,130)));Mouse(canvas,"Move",end);
    Check(mark.A==original&&canvas.SelectedBounds.HasValue,"editor drag renders selection without prematurely committing");Mouse(canvas,"Up",end);Point moved=mark.A;Check(moved==new Point(original.X+delta.X,original.Y+delta.Y),"editor drag maps scaled preview to original pixels");
    Key(editor,Keys.Control|Keys.Z);Check(mark.A==original,"one undo reverses an entire multi-event drag");Key(editor,Keys.Control|Keys.Y);Check(mark.A==moved,"editor redo restores drag");
    Key(editor,Keys.Shift|Keys.Right);Check(mark.A.X==moved.X+10,"Shift+arrow nudges selected object ten pixels");Key(editor,Keys.Control|Keys.Z);
    start=OnCanvas(canvas,new Point(190,150));Mouse(canvas,"Down",start);Mouse(canvas,"Move",OnCanvas(canvas,new Point(240,180)));Key(editor,Keys.Escape);Check(mark.A==moved&&!canvas.Capture,"Escape cancels a drag without moving the object");
    Mouse(canvas,"Down",start);Mouse(canvas,"Move",OnCanvas(canvas,new Point(230,180)));canvas.Capture=false;Check(mark.A==moved,"capture loss cancels a drag");
    string path=Path.Combine(output,"moved-export.png");doc.Save(path);using(var saved=new Bitmap(path))using(var rendered=doc.Render(null))Check(Same(saved,rendered),"selected object exports without selection decorations");
    using(var image=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(output,"editor-move.png"));}
    Key(editor,Keys.Escape);Check(!canvas.SelectedBounds.HasValue,"Escape clears selection without closing the editor");
    ToolButton(editor,"Mover");Mouse(canvas,"Down",OnCanvas(canvas,new Point(20,20)));Mouse(canvas,"Up",OnCanvas(canvas,new Point(20,20)));Check(doc.Marks.Count==1&&!canvas.SelectedBounds.HasValue,"clicking empty space in move mode adds no annotation");
   }
  }
  static void UpdateChecks(){
   Check(Updates.IsNewer("v0.2.0")&&Updates.IsNewer("0.0.9")&&!Updates.IsNewer("v0.0.8")&&!Updates.IsNewer("v0.0.7")&&!Updates.IsNewer("v0.0.5")&&!Updates.IsNewer("v0.0.4")&&!Updates.IsNewer("v0.0.3")&&!Updates.IsNewer("v0.0.2")&&!Updates.IsNewer("v0.0.1")&&!Updates.IsNewer("nao-e-versao"),"update compares semantic versions");
   Check(Updates.IsTrusted("https://github.com/juniormartinxo/myshot/releases/download/v0.2.0/MyShot.exe")&&!Updates.IsTrusted("http://github.com/x")&&!Updates.IsTrusted("https://evil.example/MyShot.exe")&&Updates.IsTrusted("https://release-assets.githubusercontent.com/github-production-release-asset/x/MyShot.manifest")&&!Updates.IsTrusted("https://release-assets.githubusercontent.com.evil.example/MyShot.manifest"),"update accepts only HTTPS GitHub hosts");
   string sum="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad  MyShot.exe\n";
   Check(Updates.ExpectedHash(sum,"MyShot.exe")=="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"&&Updates.ExpectedHash(sum,"Other.exe")==null,"checksum file selects the MyShot.exe entry");
   Check(Updates.HashMatches(System.Text.Encoding.ASCII.GetBytes("abc"),"ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")&&!Updates.HashMatches(System.Text.Encoding.ASCII.GetBytes("abd"),"ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"),"SHA-256 verification rejects altered bytes");
  }
  static void LightSnapshots(string output){
   Theme.Apply(false);
   try{
    using(var home=new Home())using(var fixture=new Bitmap(640,400)){
     using(var g=Graphics.FromImage(fixture))g.Clear(Color.White);
     using(var editor=new Editor(fixture,home)){editor.Show();Application.DoEvents();using(var preview=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(preview,new Rectangle(Point.Empty,preview.Size));preview.Save(Path.Combine(output,"editor-light.png"));}}
     home.ShowBar();Application.DoEvents();
     using(var preview=new Bitmap(home.Bar.Width,home.Bar.Height)){home.Bar.DrawToBitmap(preview,new Rectangle(Point.Empty,preview.Size));preview.Save(Path.Combine(output,"home-light.png"));}
    }
   }finally{Theme.Apply(true);}
  }
  // Captura da tela real: mostra a ordem de empilhamento que o usuário vê, o que DrawToBitmap não garante.
  static void ScreenSnapshot(string output){
   using(var home=new Home())using(var fixture=new Bitmap(640,400)){
    using(var g=Graphics.FromImage(fixture))g.Clear(Color.SteelBlue);
    using(var editor=new Editor(fixture,home)){
     editor.Show();editor.Activate();Application.DoEvents();
     Application.DoEvents();System.Threading.Thread.Sleep(400);Application.DoEvents();
     using(var image=new Bitmap(editor.Width,editor.Height))using(var g=Graphics.FromImage(image)){g.CopyFromScreen(editor.Location,Point.Empty,image.Size);image.Save(Path.Combine(output,"editor-screen-popover.png"));}
    }
   }
  }
  // Barra de captura na tela real (inclui a alça de arrasto e a ordem de empilhamento de verdade).
  static void BarScreenSnapshot(string output){
   using(var home=new Home()){
    home.ShowBar();Application.DoEvents();System.Threading.Thread.Sleep(400);Application.DoEvents();
    var bar=home.Bar;
    using(var image=new Bitmap(bar.Width,bar.Height))using(var g=Graphics.FromImage(image)){g.CopyFromScreen(bar.Location,Point.Empty,image.Size);image.Save(Path.Combine(output,"bar-screen.png"));}
    bar.Hide();
   }
  }
  // Polígono: três cliques em volta e clique de volta no primeiro vértice fecham um triângulo; Enter também fecha; Esc cancela.
  static void PolygonChecks(){
   using(var home=new Home())using(var fixture=new Bitmap(300,200))using(var editor=new Editor(fixture,home)){
    editor.Show();Application.DoEvents();ToolButton(editor,"Polígono");var canvas=Find<Canvas>(editor);
    var doc=(Document)typeof(Editor).GetField("doc",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(editor);
    Mouse(canvas,"Down",OnCanvas(canvas,new Point(20,20)));Mouse(canvas,"Down",OnCanvas(canvas,new Point(120,20)));Mouse(canvas,"Down",OnCanvas(canvas,new Point(70,100)));
    Check(doc.Marks.Count==0,"polygon stays in progress until it is closed");
    typeof(Control).GetMethod("OnMouseCaptureChanged",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(canvas,new object[]{EventArgs.Empty});
    Mouse(canvas,"Down",OnCanvas(canvas,new Point(20,20)));
    Check(doc.Marks.Count==1&&doc.Marks[0].Tool==Tool.Polygon&&doc.Marks[0].Points.Count==3,"clicking the first vertex closes a triangle");
    doc.Undo();Check(doc.Marks.Count==0,"closing a polygon is undoable");
    Mouse(canvas,"Down",OnCanvas(canvas,new Point(20,20)));Mouse(canvas,"Down",OnCanvas(canvas,new Point(120,20)));Mouse(canvas,"Down",OnCanvas(canvas,new Point(120,100)));
    Key(editor,Keys.Enter);
    Check(doc.Marks.Count==1&&doc.Marks[0].Points.Count==3,"Enter closes the polygon");
    Mouse(canvas,"Down",OnCanvas(canvas,new Point(10,150)));Mouse(canvas,"Down",OnCanvas(canvas,new Point(60,150)));Key(editor,Keys.Escape);
    Check(doc.Marks.Count==1,"Esc cancels an unfinished polygon");
   }
  }
  // Face: fica aberta até ser confirmada (Enter), e a face seguinte se encaixa nos vértices já criados.
  static void FaceChecks(){
   using(var home=new Home())using(var fixture=new Bitmap(300,200))using(var editor=new Editor(fixture,home)){
    editor.Show();Application.DoEvents();ToolButton(editor,"Face");var canvas=Find<Canvas>(editor);
    var doc=(Document)typeof(Editor).GetField("doc",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(editor);
    Mouse(canvas,"Down",OnCanvas(canvas,new Point(20,20)));Mouse(canvas,"Down",OnCanvas(canvas,new Point(120,20)));Mouse(canvas,"Down",OnCanvas(canvas,new Point(70,100)));
    Check(doc.Marks.Count==0,"face stays open after three vertices");
    Key(editor,Keys.Enter);
    Check(doc.Marks.Count==1&&doc.Marks[0].Tool==Tool.Face&&doc.Marks[0].Points.Count==3,"Enter finishes a face");
    Mouse(canvas,"Down",OnCanvas(canvas,new Point(121,21)));Mouse(canvas,"Down",OnCanvas(canvas,new Point(200,150)));Mouse(canvas,"Down",OnCanvas(canvas,new Point(200,60)));
    Key(editor,Keys.Enter);
    Check(doc.Marks.Count==2&&doc.Marks[1].Points[0]==doc.Marks[0].Points[1],"next face snaps its vertex to an existing face vertex");
   }
  }
  // Etapa: cor do fundo e cor da fonte são independentes; a fonte é usada pelas etapas novas.
  static void StepTextChecks(){
   using(var home=new Home())using(var fixture=new Bitmap(300,200))using(var editor=new Editor(fixture,home)){
    editor.Show();Application.DoEvents();
    home.Prefs.StepTextColor=Color.Yellow.ToArgb();
    ToolButton(editor,"Etapa");var canvas=Find<Canvas>(editor);
    var doc=(Document)typeof(Editor).GetField("doc",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(editor);
    Check(Find<Label>(editor,"Fonte:").Visible,"step shows a font color option");
    Mouse(canvas,"Down",OnCanvas(canvas,new Point(50,50)));
    Check(doc.Marks.Count==1&&doc.Marks[0].Tool==Tool.Step&&doc.Marks[0].TextColor.ToArgb()==Color.Yellow.ToArgb(),"new step uses the chosen font color");
   }
   // Número da etapa grande continua centralizado no ponto da etapa (centro do contorno dos dígitos).
   using(var source=new Bitmap(400,400))using(var doc=new Document(source)){
    var step=new Mark{Tool=Tool.Step,A=new Point(200,200),B=new Point(200,200),Color=Color.Red,TextColor=Color.White,FontSize=80,FontFamily="Segoe UI",Number=7};
    doc.Add(step);
    using(var rendered=doc.Render(null)){
     int minX=int.MaxValue,minY=int.MaxValue,maxX=-1,maxY=-1;
     for(int y=0;y<rendered.Height;y++)for(int x=0;x<rendered.Width;x++){var c=rendered.GetPixel(x,y);if(c.R>240&&c.G>240&&c.B>240){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}}
     Check(maxX>=0&&Math.Abs((minX+maxX)/2-200)<=2&&Math.Abs((minY+maxY)/2-200)<=2,"big step number is centered on the step point");
    }
   }
  }
  static void NumberFieldSnapshot(string output){
   using(var field=new NumberField{Maximum=200,Minimum=0,Value=100,Size=new Size(120,36)}){
    field.DrawToBitmap(new Bitmap(120,36),new Rectangle(0,0,120,36));
    using(var image=new Bitmap(120,36)){field.DrawToBitmap(image,new Rectangle(0,0,120,36));image.Save(Path.Combine(output,"number-field.png"));}
   }
  }
  // Mosaico cobre a última linha e coluna: o pixel de canto (azul) deixa de ser o original.
  static void MosaicEdgeChecks(){
   using(var source=new Bitmap(100,60))using(var doc=new Document(source)){
    using(var g=Graphics.FromImage(source))g.Clear(Color.Gray);
    source.SetPixel(99,59,Color.Blue);
    var mosaic=new Mark{Tool=Tool.Mosaic,A=new Point(0,0),B=new Point(99,59),Width=1};
    doc.Add(mosaic);
    using(var rendered=doc.Render(null))Check(rendered.GetPixel(99,59)!=Color.Blue,"mosaic covers the last row and column");
   }
  }
  // Assinatura do manifesto: aceita o original e recusa qualquer alteração ou assinatura de outra chave.
  static void SignatureChecks(){
   using(var signer=new System.Security.Cryptography.RSACryptoServiceProvider(2048))using(var other=new System.Security.Cryptography.RSACryptoServiceProvider(2048)){
    string publicXml=signer.ToXmlString(false);
    byte[] manifest=System.Text.Encoding.UTF8.GetBytes("version=0.0.2\nsha256="+new string('a',64)+"\nsize=123\n");
    byte[] signature=signer.SignData(manifest,"SHA256");
    Check(Updates.VerifySignature(manifest,signature,publicXml),"signed manifest verifies with its public key");
    byte[] altered=(byte[])manifest.Clone();altered[9]^=1;
    Check(!Updates.VerifySignature(altered,signature,publicXml),"altered manifest is rejected");
    Check(!Updates.VerifySignature(manifest,other.SignData(manifest,"SHA256"),publicXml),"signature from another key is rejected");
    var parsed=Updates.ParseManifest("version=0.0.2\nsha256="+new string('A',64)+"\nsize=123\n");
    Check(parsed.Version=="0.0.2"&&parsed.Size==123&&parsed.Sha256==new string('a',64),"manifest parses version, size and lowercase sha256");
    bool rejected=false;try{Updates.ParseManifest("version=0.0.2\nsize=0\n");}catch(Exception){rejected=true;}
    Check(rejected,"incomplete or invalid manifest is rejected");
   }
  }
  [STAThread] static int Main(string[] args){
   string output=args.Length>0?args[0]:Path.GetFullPath("research/private/validation");Directory.CreateDirectory(output);
   Home.Quiet=true;
   try{
    Native.SetProcessDpiAwarenessContext(new IntPtr(-4));Application.EnableVisualStyles();
    Check(Geometry.Between(new Point(300,200),new Point(20,30))==new Rectangle(20,30,280,170),"reverse drag bounds");
    Rectangle virtualRect=new Rectangle(-1920,0,5360,1440);var physical=new Point(-1800,220);var local=new Point(physical.X-virtualRect.X,physical.Y-virtualRect.Y);
    Check(local==new Point(120,220),"negative monitor origin mapping");
    var view=Geometry.Fit(new Size(1600,1000),new Rectangle(0,0,800,600));Check(Geometry.ImagePoint(new Point(400,300),view,new Size(1600,1000))==new Point(800,500),"scaled preview maps to source pixels");
    using(var source=new Bitmap(640,400)){
     for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++)source.SetPixel(x,y,Color.FromArgb(x%256,y%256,(x+y)%256));
     source.Save(Path.Combine(output,"fixture.png"));
     Movement(source,output);
     Editing(source,output);
     SidebarFlow(source,output);
     using(var doc=new Document(source)){
      using(var b=doc.Render(null))Check(Same(source,b),"unannotated image preserves every pixel");
      foreach(Tool t in Enum.GetValues(typeof(Tool))){
       var m=new Mark{Tool=t,A=new Point(100,100),B=new Point(240,180),Color=Color.FromArgb(200,240,30,50),Width=5,Text="MyShot 123",Number=doc.NextStep,FontSize=24};m.Points.Add(m.A);m.Points.Add(new Point(150,150));m.Points.Add(m.B);
       using(var before=doc.Render(null)){
        doc.Add(m);using(var after=doc.Render(null)){Check(!Same(before,after),t+" changes exported pixels");doc.Undo();using(var undone=doc.Render(null))Check(Same(before,undone),t+" undo restores exact pixels");doc.Redo();using(var redone=doc.Render(null))Check(Same(after,redone),t+" redo restores exact pixels");}
       }
      }
      doc.Save(Path.Combine(output,"annotated.png"));
      using(var saved=new Bitmap(Path.Combine(output,"annotated.png")))using(var preview=doc.Render(null))Check(Same(saved,preview),"PNG export equals editor rendering pixel-for-pixel");
      doc.Save(Path.Combine(output,"annotated.jpg"));doc.Save(Path.Combine(output,"annotated.bmp"));
      using(var jpg=new Bitmap(Path.Combine(output,"annotated.jpg")))using(var bmp=new Bitmap(Path.Combine(output,"annotated.bmp")))Check(jpg.Size==source.Size&&bmp.Size==source.Size,"JPEG and BMP decode at original dimensions");
      doc.Undo();doc.Add(new Mark{Tool=Tool.Line,A=new Point(5,5),B=new Point(50,50),Color=Color.Black,Width=2});Check(!doc.CanRedo,"new edit invalidates redo branch");
      using(var crop=source.Clone(new Rectangle(40,60,220,140),source.PixelFormat))using(var cropped=new Document(crop))using(var image=cropped.Render(null))Check(image.Size==new Size(220,140)&&image.GetPixel(0,0)==source.GetPixel(40,60),"region crop retains source coordinates");
      using(var canvas=new Canvas{Size=new Size(800,560)})using(var rendered=doc.Render(null))using(var preview=new Bitmap(800,560)){canvas.Image=rendered;canvas.DrawToBitmap(preview,new Rectangle(0,0,800,560));preview.Save(Path.Combine(output,"canvas-preview.png"));canvas.Image=null;Check(File.Exists(Path.Combine(output,"canvas-preview.png")),"WinForms canvas renders offline");}
     }
    }
    var p=new Preferences{Folder=output,Format="bmp",ShiftInsteadOfAlt=true,Color=Color.Blue.ToArgb(),FontSize=31,Filled=true,Opacity=60,Thickness=7};string prefPath=Path.Combine(output,"preferences-test.json");p.Save(prefPath);p.Format="jpg";p.Save(prefPath);string warning;var loaded=Preferences.Load(prefPath,out warning);
    Check(warning==null&&loaded.Format=="jpg"&&loaded.ShiftInsteadOfAlt&&loaded.Thickness==7&&loaded.Filled&&loaded.Opacity==60&&loaded.FontSize==31,"preferences persist and atomically replace");
    Check(new Preferences().LastRegion==null&&new Preferences{RegionWidth=1,RegionHeight=50}.LastRegion==null,"no last region unless width and height are at least 2");
    p.SetLastRegion(new Rectangle(-1280,40,800,600));p.Save(prefPath);loaded=Preferences.Load(prefPath,out warning);
    Check(warning==null&&loaded.LastRegion==new Rectangle(-1280,40,800,600),"last region persists across restarts");
    using(var source=new Bitmap(100,100,System.Drawing.Imaging.PixelFormat.Format32bppArgb)){
     using(var sg=Graphics.FromImage(source))sg.Clear(Color.Red);
     using(var cut=PolygonCapture.Crop(source,new[]{new Point(10,10),new Point(60,10),new Point(10,60)})){
      Check(cut!=null&&cut.Width==51&&cut.Height==51&&cut.GetPixel(5,5).A==255&&cut.GetPixel(50,50).A==0,"free-form capture keeps the polygon and makes the outside transparent");
     }
     using(var none=PolygonCapture.Crop(source,new[]{new Point(1,1),new Point(1,1),new Point(1,1)}))Check(none==null,"degenerate free-form selection is rejected");
    }
    Check(Preferences.ModifierText(Preferences.ModCtrl|Preferences.ModWin)=="Ctrl+Win"&&new Preferences{Modifiers=Preferences.ModAlt|Preferences.ModShift}.HotkeyModifiers==(Preferences.ModAlt|Preferences.ModShift)&&new Preferences().HotkeyModifiers==(Preferences.ModCtrl|Preferences.ModAlt),"hotkey modifiers accept any combination and keep the old default");
    File.WriteAllText(prefPath,"corrupt");loaded=Preferences.Load(prefPath,out warning);Check(warning!=null&&loaded.Format=="png","corrupt preferences fall back with warning");
    Check(WindowsStartup.Command(@"C:\Pasta com espaço\ação\MyShot.exe")=="\"C:\\Pasta com espaço\\ação\\MyShot.exe\" --startup","startup command quotes spaces and Unicode correctly");
    string isolatedRegistryPath=@"Software\MyShotTests\"+Guid.NewGuid().ToString("N");bool registryCreated=false;
    try{
     using(var isolated=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(isolatedRegistryPath)){registryCreated=true;isolated.SetValue("OtherApp","preserve-me");}
     var startup=new WindowsStartup(Microsoft.Win32.Registry.CurrentUser,isolatedRegistryPath,@"C:\Pasta com espaço\MyShot.exe");
     Check(!startup.IsEnabled(),"startup disabled when no registration exists");startup.SetEnabled(true);
     Check(startup.IsEnabled()&&startup.RegisteredCommand()=="\"C:\\Pasta com espaço\\MyShot.exe\" --startup","startup registration round-trip uses tray argument");
     startup.SetEnabled(false);startup.SetEnabled(false);Check(!startup.IsEnabled(),"startup unregister is idempotent");
     using(var isolated=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(isolatedRegistryPath))Check((string)isolated.GetValue("OtherApp")=="preserve-me","startup changes preserve other registry values");
    }catch(System.Security.SecurityException e){Console.WriteLine("BLOCKED isolated registry test: "+e.Message);}catch(UnauthorizedAccessException e){Console.WriteLine("BLOCKED isolated registry test: "+e.Message);}
    finally{if(registryCreated)Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(isolatedRegistryPath,false);}
    using(var startupHome=new Home(true)){Application.DoEvents();Check(!startupHome.BarVisible&&startupHome.IsHandleCreated,"startup keeps only tray and hotkeys");startupHome.ShowBar();Application.DoEvents();Check(startupHome.BarVisible,"capture bar opens from tray after startup");}
    using(var first=new Form())using(var second=new Form()){
     bool acquired=Native.RegisterHotKey(first.Handle,91,0x4006,0x4a);
     if(acquired){try{bool duplicate=Native.RegisterHotKey(second.Handle,92,0x4006,0x4a);Check(!duplicate,"Win32 rejects conflicting hotkey");if(duplicate)Native.UnregisterHotKey(second.Handle,92);}finally{Native.UnregisterHotKey(first.Handle,91);}bool again=Native.RegisterHotKey(second.Handle,92,0x4006,0x4a);Check(again,"Win32 hotkey can be registered after release");Native.UnregisterHotKey(second.Handle,92);}
     else Console.WriteLine("UNVERIFIED hotkey registration: "+Marshal.GetLastWin32Error());
    }
    foreach(var screen in Screen.AllScreens)Console.WriteLine("MONITOR "+screen.DeviceName+" "+screen.Bounds);
    using(var home=new Home())using(var fixture=new Bitmap(640,400))using(var g=Graphics.FromImage(fixture)) {
     home.ShowBar();Application.DoEvents();
     g.Clear(Color.White);using(var font=new Font("Segoe UI",28))g.DrawString("MyShot — teste de edição",font,Brushes.Black,20,70);
     using(var editor=new Editor(fixture,home))using(var preview=new Bitmap(editor.Width,editor.Height)){
      editor.Show();Application.DoEvents();editor.DrawToBitmap(preview,new Rectangle(Point.Empty,preview.Size));preview.Save(Path.Combine(output,"editor-window.png"));
      editor.Size=editor.MinimumSize;Application.DoEvents();using(var compact=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(compact,new Rectangle(Point.Empty,compact.Size));compact.Save(Path.Combine(output,"editor-compact.png"));}
      foreach(Control control in editor.Controls){var strip=control as ToolStrip;if(strip!=null)foreach(ToolStripItem item in strip.Items){var button=item as ToolStripButton;if(button!=null&&button.Text=="Texto")button.PerformClick();}}
      Application.DoEvents();using(var textTool=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(textTool,new Rectangle(Point.Empty,textTool.Size));textTool.Save(Path.Combine(output,"editor-text.png"));}
     }
     using(var preview=new Bitmap(home.Bar.Width,home.Bar.Height)){home.Bar.DrawToBitmap(preview,new Rectangle(Point.Empty,preview.Size));preview.Save(Path.Combine(output,"home-window.png"));}
     using(var settings=new SettingsDialog(home.Prefs))using(var preview=new Bitmap(settings.Width,settings.Height)){settings.Show();Application.DoEvents();settings.DrawToBitmap(preview,new Rectangle(Point.Empty,preview.Size));preview.Save(Path.Combine(output,"settings-window.png"));}
     Console.WriteLine("OBSERVED home/editor/settings DrawToBitmap completed; inspect PNGs for layout.");
    }
    try{using(var desktop=DesktopCapture.Take(SystemInformation.VirtualScreen)){desktop.Save(Path.Combine(output,"desktop.png"));Console.WriteLine("PASS desktop acquisition (not interactive workflow)");}}catch(Exception e){Console.WriteLine("BLOCKED desktop acquisition: "+e.GetType().Name+": "+e.Message);}
    UpdateChecks();
    LightSnapshots(output);
    ScreenSnapshot(output);
    BarScreenSnapshot(output);
    NumberFieldSnapshot(output);
    PolygonChecks();
    FaceChecks();
    StepTextChecks();
    SignatureChecks();
    MosaicEdgeChecks();
    Console.WriteLine("RESULT "+count+" assertions passed; interactive workflow is a separate required check.");return 0;
   }catch(Exception e){Console.Error.WriteLine(e);return 1;}
  }
 }
}

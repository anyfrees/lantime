using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LanTime {
 public class ChromeForm:Form {
  protected readonly Panel Body=new Panel{Dock=DockStyle.Fill};
  readonly Panel caption=new Panel{Dock=DockStyle.Top,Height=46,BackColor=Color.White};
  readonly Label captionText=new Label{AutoEllipsis=true,Location=new Point(50,0),Height=46,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Muted,Font=Theme.Font(9)};
  readonly FlatButton minimize,maximize,close;
  public ChromeForm(){FormBorderStyle=FormBorderStyle.None;Padding=new Padding(1);BackColor=Theme.Paper;DoubleBuffered=true;Controls.Add(Body);Controls.Add(caption);Body.BringToFront();caption.Controls.Add(new Mark{Location=new Point(17,12),Size=new Size(22,22),Filled=true,BackColor=Color.White});caption.Controls.Add(captionText);
   minimize=CaptionButton("min","最小化",delegate{WindowState=FormWindowState.Minimized;});maximize=CaptionButton("max","最大化 / 还原",delegate{ToggleMaximize();});close=CaptionButton("close","关闭窗口",delegate{Close();});
   MouseEventHandler drag=delegate(object sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;if(e.Clicks==2){ToggleMaximize();return;}NativeUi.ReleaseCapture();NativeUi.SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);};caption.MouseDown+=drag;captionText.MouseDown+=drag;caption.Resize+=delegate{LayoutCaption();};LayoutCaption();
  }
  FlatButton CaptionButton(string glyph,string name,EventHandler action){var button=new FlatButton{GlyphName=glyph,AccessibleName=name,Size=new Size(38,30),BackColor=Color.White,ForeColor=Theme.Muted,Text=""};button.Click+=action;caption.Controls.Add(button);return button;}
  void ToggleMaximize(){if(!MaximizeBox)return;MaximizedBounds=Screen.FromHandle(Handle).WorkingArea;WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;}
  internal void BodyForDialog(Control content){Body.Controls.Add(content);}
  protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);if(captionText!=null)captionText.Text=Text;}
  protected override void OnResize(EventArgs e){base.OnResize(e);LayoutCaption();UpdateOutline();Invalidate();}
  void UpdateOutline(){if(Width<2||Height<2)return;Region previous=Region;if(WindowState==FormWindowState.Maximized){Region=null;}else{using(var path=new GraphicsPath()){float d=28;path.AddArc(0,0,d,d,180,90);path.AddArc(Width-d,0,d,d,270,90);path.AddArc(Width-d,Height-d,d,d,0,90);path.AddArc(0,Height-d,d,d,90,90);path.CloseFigure();Region=new Region(path);}}if(previous!=null)previous.Dispose();}
  void LayoutCaption(){if(close==null)return;int right=caption.ClientSize.Width-9;close.Location=new Point(right-38,8);right-=44;maximize.Visible=MaximizeBox;if(MaximizeBox){maximize.Location=new Point(right-38,8);maximize.GlyphName=WindowState==FormWindowState.Maximized?"restore":"max";maximize.Invalidate();right-=44;}minimize.Visible=MinimizeBox;if(MinimizeBox){minimize.Location=new Point(right-38,8);right-=44;}captionText.Width=Math.Max(1,right-58);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Theme.Round(e.Graphics,new RectangleF(.5f,.5f,Width-1.5f,Height-1.5f),WindowState==FormWindowState.Maximized?1:14,Color.Transparent,Theme.Line);}
  protected override void WndProc(ref Message m){
   if(m.Msg==0x84&&WindowState==FormWindowState.Normal&&MaximizeBox){long position=m.LParam.ToInt64();Point p=PointToClient(new Point((short)(position&0xffff),(short)((position>>16)&0xffff)));int edge=6;bool l=p.X<edge,r=p.X>=ClientSize.Width-edge,t=p.Y<edge,b=p.Y>=ClientSize.Height-edge;int hit=t?(l?13:r?14:12):b?(l?16:r?17:15):l?10:r?11:0;if(hit!=0){m.Result=new IntPtr(hit);return;}}
   base.WndProc(ref m);
  }
 }
 internal static class AppDialog {
  public static DialogResult Show(string text,string title){return Show(text,title,MessageBoxButtons.OK,MessageBoxIcon.Information);}
  public static DialogResult Show(string text,string title,MessageBoxButtons buttons,MessageBoxIcon icon){
   using(var form=new ChromeForm{Text=title,ClientSize=new Size(540,320),StartPosition=FormStartPosition.CenterScreen,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,Font=Theme.Font(10)}){
    var content=new Panel{Dock=DockStyle.Fill,Padding=new Padding(26,14,26,22),BackColor=Theme.Paper};form.BodyForDialog(content);
    var label=new Label{Text=icon==MessageBoxIcon.Error||icon==MessageBoxIcon.Warning?"请检查以下提示":"操作提示",Dock=DockStyle.Top,Height=34,Font=Theme.Font(15,FontStyle.Bold),ForeColor=Theme.Ink};
    var message=new TextInput{Text=text,Multiline=true,ReadOnly=true,WordWrap=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,BackColor=Color.White,ForeColor=Theme.Ink};
    var actions=new Panel{Dock=DockStyle.Bottom,Height=58};var ok=new FlatButton{Text="知道了",Primary=true,Size=new Size(100,38),Anchor=AnchorStyles.Right|AnchorStyles.Bottom,DialogResult=DialogResult.OK};actions.Controls.Add(ok);actions.Resize+=delegate{ok.Location=new Point(actions.Width-100,20);};form.AcceptButton=ok;form.CancelButton=ok;content.Controls.Add(message);content.Controls.Add(label);content.Controls.Add(actions);message.BringToFront();return form.ShowDialog(Form.ActiveForm);
   }
  }
 }
 internal static class NativeUi {
  [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam);
  [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
 }
 public sealed class SurfacePanel:Panel {
  public SurfacePanel(){DoubleBuffered=true;BackColor=Color.White;Padding=new Padding(1);}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Theme.Paper:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Theme.Round(e.Graphics,new RectangleF(.5f,.5f,Width-1.5f,Height-1.5f),11,BackColor,Theme.Line);}
 }
 // Keep the native editor for caret, selection, IME, clipboard, keyboard and accessibility.
 public sealed class TextInput:UserControl {
  readonly TextBox editor;bool hover;ScrollBars scrollBars;
  public TextInput(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);AutoScaleMode=AutoScaleMode.None;TabStop=false;BackColor=Color.White;Size=new Size(200,40);Font=Theme.Font(10);
   editor=new TextBox{BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink,Font=Font};Controls.Add(editor);editor.GotFocus+=delegate{Invalidate();};editor.LostFocus+=delegate{Invalidate();};editor.MouseEnter+=delegate{hover=true;Invalidate();};editor.MouseLeave+=delegate{hover=false;Invalidate();};editor.TextChanged+=delegate{base.OnTextChanged(EventArgs.Empty);};LayoutEditor();
  }
  public TextBox Editor {get{return editor;}}
  public override string Text {get{return editor==null?base.Text:editor.Text;}set{if(editor==null)base.Text=value;else editor.Text=value;}}
  public string[] Lines {get{return editor.Lines;}set{editor.Lines=value;}}
  public bool Multiline {get{return editor.Multiline;}set{editor.Multiline=value;LayoutEditor();}}
  public bool ReadOnly {get{return editor.ReadOnly;}set{editor.ReadOnly=value;Invalidate();}}
  public bool WordWrap {get{return editor.WordWrap;}set{editor.WordWrap=value;}}
  public ScrollBars ScrollBars {get{return scrollBars;}set{scrollBars=value;editor.ScrollBars=value==ScrollBars.Both?ScrollBars.Horizontal:ScrollBars.None;}}
  protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);if(editor!=null){editor.Font=Font;LayoutEditor();}}
  protected override void OnForeColorChanged(EventArgs e){base.OnForeColorChanged(e);if(editor!=null)editor.ForeColor=ForeColor;}
  protected override void OnBackColorChanged(EventArgs e){base.OnBackColorChanged(e);if(editor!=null)editor.BackColor=BackColor;}
  protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);Invalidate();}
  protected override void OnResize(EventArgs e){base.OnResize(e);LayoutEditor();}
  void LayoutEditor(){if(editor==null)return;int top=editor.Multiline?12:Math.Max(8,(Height-editor.PreferredHeight)/2);editor.SetBounds(13,top,Math.Max(1,Width-26),Math.Max(1,editor.Multiline?Height-top-12:editor.PreferredHeight));}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(Enabled)editor.Focus();}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Theme.Paper:Parent.BackColor);}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Color border=!Enabled?Theme.Line:editor.Focused?Theme.Blue:hover?Color.FromArgb(166,188,218):Theme.Line;Theme.Round(e.Graphics,new RectangleF(.75f,.75f,Width-1.5f,Height-1.5f),9,Enabled?BackColor:Theme.Paper,border);}
 }
 public sealed class SelectInput:ComboBox {
  bool hover;
  public SelectInput(){DrawMode=DrawMode.OwnerDrawFixed;DropDownStyle=ComboBoxStyle.DropDownList;FlatStyle=FlatStyle.Flat;Font=Theme.Font(10);ItemHeight=30;IntegralHeight=false;DropDownHeight=218;BackColor=Color.White;ForeColor=Theme.Ink;}
  protected override void OnDrawItem(DrawItemEventArgs e){if(e.Index<0)return;bool selected=(e.State&DrawItemState.Selected)!=0;using(var b=new SolidBrush(selected?Color.FromArgb(232,240,254):Color.White))e.Graphics.FillRectangle(b,e.Bounds);TextRenderer.DrawText(e.Graphics,GetItemText(Items[e.Index]),Font,new Rectangle(e.Bounds.X+12,e.Bounds.Y,e.Bounds.Width-24,e.Bounds.Height),selected?Theme.Blue:Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);if((e.State&DrawItemState.Focus)!=0)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(e.Bounds,-3,-3),Theme.Blue,Color.White);}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
  protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
  protected override void OnDropDown(EventArgs e){base.OnDropDown(e);Invalidate();}
  protected override void OnDropDownClosed(EventArgs e){base.OnDropDownClosed(e);Invalidate();}
  void DrawChrome(Graphics g){g.SmoothingMode=SmoothingMode.AntiAlias;using(var background=new SolidBrush(Parent==null?Theme.Paper:Parent.BackColor))g.FillRectangle(background,new Rectangle(0,0,Width,Height));Theme.Round(g,new RectangleF(.75f,.75f,Width-1.5f,Height-1.5f),9,Enabled?Color.White:Theme.Paper,Focused||DroppedDown?Theme.Blue:hover?Color.FromArgb(166,188,218):Theme.Line);TextRenderer.DrawText(g,Text,Font,new Rectangle(13,0,Math.Max(1,Width-51),Height),Enabled?Theme.Ink:Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);float x=Width-22,y=Height/2f;using(var p=new Pen(Enabled?Theme.Muted:Theme.Line,1.6f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})g.DrawLines(p,new[]{new PointF(x-4,y-2),new PointF(x,y+2),new PointF(x+4,y-2)});}
  protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg==0x000F){using(var g=CreateGraphics())DrawChrome(g);}else if((m.Msg==0x0317||m.Msg==0x0318)&&m.WParam!=IntPtr.Zero){using(var g=Graphics.FromHdc(m.WParam))DrawChrome(g);}}
 }
 public sealed class ToggleInput:CheckBox {
  bool hover;
  public ToggleInput(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);AutoSize=false;Height=44;Font=Theme.Font(10);Cursor=Cursors.Hand;}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
  protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
  protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent==null?Theme.Paper:Parent.BackColor);float y=(Height-23)/2f;Color fill=!Enabled?Theme.Line:Checked?Theme.Blue:hover?Color.FromArgb(208,222,241):Color.FromArgb(220,229,241);Theme.Round(g,new RectangleF(1,y,41,23),11.5f,fill);using(var shadow=new SolidBrush(Color.FromArgb(24,48,72,30)))g.FillEllipse(shadow,Checked?23:4,y+3,18,18);using(var b=new SolidBrush(Color.White))g.FillEllipse(b,Checked?22:4,y+2.5f,18,18);TextRenderer.DrawText(g,Text,Font,new Rectangle(55,0,Math.Max(1,Width-58),Height),Enabled?ForeColor:Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);if(Focused)Theme.Round(g,new RectangleF(.5f,y-3,43,29),14.5f,Color.Transparent,Theme.Blue);}
 }
 public sealed class DataTable:ListView {
  public DataTable(){OwnerDraw=true;DoubleBuffered=true;BorderStyle=BorderStyle.None;}
  internal void FitColumns(){if(Columns.Count==0)return;double[] ratios=Columns.Count==5?new[]{.30,.27,.14,.19,.10}:new[]{.40,.34,.26};int available=Math.Max(100,ClientSize.Width-24);for(int i=0;i<Columns.Count;i++)Columns[i].Width=(int)(available*ratios[Math.Min(i,ratios.Length-1)]);}
  protected override void OnResize(EventArgs e){base.OnResize(e);FitColumns();}
  protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);BeginInvoke(new Action(FitColumns));}
  protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e){using(var b=new SolidBrush(Color.FromArgb(245,248,252)))e.Graphics.FillRectangle(b,e.Bounds);using(var p=new Pen(Theme.Line))e.Graphics.DrawLine(p,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);TextRenderer.DrawText(e.Graphics,e.Header.Text,Theme.Font(9),new Rectangle(e.Bounds.X+12,e.Bounds.Y,e.Bounds.Width-18,e.Bounds.Height),Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);}
  protected override void OnDrawItem(DrawListViewItemEventArgs e){}
  protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e){bool selected=e.Item.Selected;Color fill=selected?Color.FromArgb(233,241,254):e.ItemIndex%2==0?Color.White:Color.FromArgb(250,252,254);using(var b=new SolidBrush(fill))e.Graphics.FillRectangle(b,e.Bounds);TextRenderer.DrawText(e.Graphics,e.SubItem.Text,Font,new Rectangle(e.Bounds.X+12,e.Bounds.Y,e.Bounds.Width-18,e.Bounds.Height),selected?Theme.Blue:e.Item.ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);if(Focused&&selected&&e.ColumnIndex==0)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(e.Bounds,-3,-3),Theme.Blue,fill);}
 }
}

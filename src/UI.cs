using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LanTime {
 public static class Theme {
  public static Color Ink=Color.FromArgb(28,48,72),Muted=Color.FromArgb(100,119,140),Blue=Color.FromArgb(35,99,214),Teal=Color.FromArgb(0,123,117),Line=Color.FromArgb(224,232,240),Paper=Color.FromArgb(244,247,251),Navy=Color.FromArgb(20,39,63),Amber=Color.FromArgb(160,95,10);
  public static Font Font(float size,FontStyle style=FontStyle.Regular){return new Font("Microsoft YaHei UI",size*96f/72f,style,GraphicsUnit.Pixel);}
  public static void Glyph(Graphics g,string name,RectangleF box,Color color) {
   // Every glyph shares a square viewport and a centered visible outline.
   var saved=g.Save();float size=Math.Min(box.Width,box.Height);g.TranslateTransform(box.X+(box.Width-size)/2,box.Y+(box.Height-size)/2);g.ScaleTransform(size/24f,size/24f);
   using(var p=new Pen(color,1.7f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round}) {
    if(name=="devices") {g.DrawRectangle(p,3,4,18,12);g.DrawLine(p,12,16,12,20);g.DrawLine(p,7,20,17,20);}
    else if(name=="settings") {g.DrawEllipse(p,4,4,16,16);g.DrawEllipse(p,9,9,6,6);for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(p,12+(float)Math.Cos(a)*8,12+(float)Math.Sin(a)*8,12+(float)Math.Cos(a)*10,12+(float)Math.Sin(a)*10);}}
    else if(name=="logs") {g.DrawRectangle(p,5,3,14,18);g.DrawLine(p,9,8,15,8);g.DrawLine(p,9,12,15,12);g.DrawLine(p,9,16,13,16);}
    else if(name=="about") {g.DrawEllipse(p,3,3,18,18);g.DrawLine(p,12,11,12,17);using(var b=new SolidBrush(color))g.FillEllipse(b,11,7,2,2);}
    else if(name=="min"){g.DrawLine(p,6,12,18,12);}
    else if(name=="max"){g.DrawRectangle(p,6,6,12,12);}
    else if(name=="restore"){g.DrawLines(p,new[]{new Point(9,6),new Point(18,6),new Point(18,15)});g.DrawRectangle(p,6,9,9,9);}
    else if(name=="close"){g.DrawLine(p,7,7,17,17);g.DrawLine(p,7,17,17,7);}
    else {g.DrawEllipse(p,3,3,18,18);g.DrawLine(p,12,6.5f,12,12);g.DrawLine(p,12,12,16.5f,14.5f);}
   }
   g.Restore(saved);
  }
  public static void AppMark(Graphics g,RectangleF box) {
   float size=Math.Min(box.Width,box.Height);var circle=new RectangleF(box.X+(box.Width-size)/2,box.Y+(box.Height-size)/2,size,size);
   using(var b=new SolidBrush(Blue))g.FillEllipse(b,circle);Glyph(g,"clock",new RectangleF(circle.X+size*.12f,circle.Y+size*.12f,size*.76f,size*.76f),Color.White);
  }
  public static void Text(Graphics g,string text,float size,Color color,RectangleF r,FontStyle style=FontStyle.Regular,StringAlignment align=StringAlignment.Near) {
   using(var f=Font(size,style))using(var b=new SolidBrush(color))using(var sf=new StringFormat{Alignment=align,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(text,f,b,r,sf);
  }
  public static void Round(Graphics g,RectangleF r,float radius,Color fill,Color? border=null) {
   using(var path=new GraphicsPath()) {
    float d=radius*2;path.AddArc(r.X,r.Y,d,d,180,90);path.AddArc(r.Right-d,r.Y,d,d,270,90);path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.X,r.Bottom-d,d,d,90,90);path.CloseFigure();
    using(var b=new SolidBrush(fill))g.FillPath(b,path);if(border.HasValue)using(var p=new Pen(border.Value))g.DrawPath(p,path);
   }
  }
 }
 public sealed class Mark:Control {
  public bool Filled;
  public Mark(){DoubleBuffered=true;TabStop=false;}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var box=new RectangleF(2,2,Width-4,Height-4);if(Filled)Theme.AppMark(e.Graphics,box);else Theme.Glyph(e.Graphics,"clock",box,Color.FromArgb(113,180,255));}
 }
 public sealed class FlatButton:Button {
  public bool Primary,Selected;public string GlyphName;bool hover,pressed;
  public FlatButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;Font=Theme.Font(10);Height=40;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left)pressed=true;Invalidate();base.OnMouseDown(e);}
  protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
  protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture)pressed=false;Invalidate();base.OnMouseCaptureChanged(e);}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;bool side=BackColor==Theme.Navy;Color bg=Primary?(pressed?Color.FromArgb(24,75,171):hover?Color.FromArgb(29,87,194):Theme.Blue):Selected?Color.FromArgb(43,65,90):side?(hover?Color.FromArgb(32,53,78):Theme.Navy):pressed?Color.FromArgb(224,234,248):hover?Color.FromArgb(240,245,252):BackColor;
   Color line=Focused?Theme.Blue:side||Selected?bg:hover?Color.FromArgb(177,198,225):Theme.Line;e.Graphics.Clear(Parent==null?Theme.Paper:Parent.BackColor);Theme.Round(e.Graphics,new RectangleF(.5f,.5f,Width-1.5f,Height-1.5f),9,Enabled?bg:Theme.Paper,Enabled?(Primary?bg:line):Theme.Line);Color color=Enabled?(Primary||Selected?Color.White:ForeColor):Theme.Muted;float left=10;
   if(!String.IsNullOrEmpty(GlyphName)){Theme.Glyph(e.Graphics,GlyphName,new RectangleF(String.IsNullOrEmpty(Text)?(Width-22)/2f:12,(Height-22)/2f,22,22),color);left=46;}
   Theme.Text(e.Graphics,Text,10,color,new RectangleF(left,0,Width-left-10,Height),FontStyle.Regular,TextAlign==ContentAlignment.MiddleLeft?StringAlignment.Near:StringAlignment.Center);
   if(Focused)Theme.Round(e.Graphics,new RectangleF(3,3,Width-7,Height-7),6,Color.Transparent,Primary?Color.White:Theme.Blue);
  }
 }
 public sealed class Board:Control {
  public Snapshot State;public double DisplayUtcTicks;
  public Board(){DoubleBuffered=true;BackColor=Theme.Paper;}
  protected override void OnPaint(PaintEventArgs e){
   base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;Snapshot s=State;if(s==null)return;
   float w=Width;float heroH=195;
   Theme.Round(g,new RectangleF(0,0,w-1,heroH),12,Color.White,Theme.Line);
   Theme.Text(g,"校准时间  /  "+TimeZoneInfo.Local.StandardName,10,Theme.Muted,new RectangleF(26,20,w-190,25));
   bool ok=s.Running&&s.Trusted;Color accent=ok?Theme.Teal:Theme.Amber;
   Theme.Round(g,new RectangleF(w-150,21,124,30),15,ok?Color.FromArgb(227,246,240):Color.FromArgb(255,243,221));
   Theme.Text(g,s.State,9,accent,new RectangleF(w-146,21,116,30),FontStyle.Bold,StringAlignment.Center);
   DateTime t=new DateTime((long)(DisplayUtcTicks>0?DisplayUtcTicks:s.UtcTicks),DateTimeKind.Utc).ToLocalTime();
   using(var f=new Font("Consolas",44*96f/72f,FontStyle.Regular,GraphicsUnit.Pixel))using(var b=new SolidBrush(Theme.Ink))g.DrawString(t.ToString("HH:mm:ss"),f,b,new PointF(23,51));
   Theme.Text(g,t.ToString("fff"),17,Theme.Muted,new RectangleF(327,79,80,30));
   Theme.Text(g,t.ToString("yyyy 年 MM 月 dd 日  dddd",CultureInfo.GetCultureInfo("zh-CN")),10,Theme.Muted,new RectangleF(28,125,w-260,25));
   DrawDial(g,w-101,101,44,t,accent);
   using(var p=new Pen(Theme.Line))g.DrawLine(p,26,157,w-26,157);
   Theme.Text(g,s.Detail,9,Theme.Muted,new RectangleF(27,161,w-54,29));
   float gap=12,cardw=(w-gap*3)/4;string[] labels={"相对系统时间","一致来源","服务请求","估计误差上界"};
   string[] values={Signed(s.SystemOffsetMs)+" ms",s.Agreeing+" / "+(s.Sources==null?0:s.Sources.Length),s.Requests.ToString("N0"),s.Trusted?"± "+s.UncertaintyMs.ToString("F1")+" ms":"—"};
   for(int i=0;i<4;i++){float x=i*(cardw+gap);Theme.Round(g,new RectangleF(x,207,cardw,81),9,Color.White,Theme.Line);Theme.Text(g,labels[i],9,Theme.Muted,new RectangleF(x+16,216,cardw-25,23));Theme.Text(g,values[i],i==0?17:20,i==1?Theme.Teal:Theme.Ink,new RectangleF(x+15,242,cardw-24,36),FontStyle.Bold);}
  }
  void DrawDial(Graphics g,float x,float y,float radius,DateTime t,Color accent){
   using(var p=new Pen(Theme.Line,2))g.DrawEllipse(p,x-radius,y-radius,radius*2,radius*2);
   for(int i=0;i<60;i++){double a=i*Math.PI/30;float r=radius-6,r2=radius-(i%5==0?13:8);using(var p=new Pen(i%5==0?Theme.Muted:Theme.Line,i%5==0?2:1))g.DrawLine(p,x+(float)Math.Sin(a)*r,y-(float)Math.Cos(a)*r,x+(float)Math.Sin(a)*r2,y-(float)Math.Cos(a)*r2);}
   Hand(g,x,y,(t.Hour%12+t.Minute/60.0)*Math.PI/6,22,3,Theme.Ink);Hand(g,x,y,(t.Minute+t.Second/60.0)*Math.PI/30,30,2,Theme.Ink);Hand(g,x,y,(t.Second+t.Millisecond/1000.0)*Math.PI/30,33,1,accent);
   using(var b=new SolidBrush(accent))g.FillEllipse(b,x-3,y-3,6,6);
  }
  void Hand(Graphics g,float x,float y,double a,float length,float width,Color c){using(var p=new Pen(c,width){StartCap=LineCap.Round,EndCap=LineCap.Round})g.DrawLine(p,x,y,x+(float)Math.Sin(a)*length,y-(float)Math.Cos(a)*length);}
  public static string Signed(double n){return n.ToString("+0.0;-0.0;0.0",CultureInfo.InvariantCulture);}
 }
 public sealed class MainForm:ChromeForm {
  Settings settings;Engine engine;readonly bool preview;bool remote;Snapshot state;readonly Stopwatch received=Stopwatch.StartNew();double receivedTicks;
  public bool AllowExit;
  readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();NotifyIcon tray;
  Panel sidebar,main,overview,clientsPage,settingsPage,logsPage,aboutPage;Label title,subtitle,endpoint,footer,tableTitle,help;ComboBox addressChoice;readonly Stopwatch uiClock=Stopwatch.StartNew();double adapterRefresh=-100,serviceRefresh=-100,listRefresh=-100;
  Board board;ListView sources,clients;TextInput logs;FlatButton start,sync,copy;FlatButton[] nav;Label settingsNote,localStatus,localSettingStatus;FlatButton runAdmin;
  TextInput bind,port,interval,holdover,quorum,hosts,networks;CheckBox startup,autoSystemTime;bool ready;
  public MainForm(bool render){
   preview=render;settings=Store.Load();remote=!render&&Program.Installed();
   Text="LanTime · 局域网时间站";Font=Theme.Font(10);BackColor=Theme.Paper;ForeColor=Theme.Ink;StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(Math.Min(1136,Screen.PrimaryScreen.WorkingArea.Width-40),Math.Min(792,Screen.PrimaryScreen.WorkingArea.Height-70));MinimumSize=new Size(1000,700);
   Icon=MakeIcon();Build();
   state=new Snapshot{State="服务已停止",Detail="点击启动服务，自动向外部时间源校准。",UtcTicks=DateTime.UtcNow.Ticks,Sources=settings.Servers.Select(x=>new SourceState{Host=x,Status="等待查询"}).ToArray(),Clients=new ClientState[0],Address=settings.Bind+":"+settings.Port,LastSync="—",SelectedSource="—",Age=-1,AutoSetSystemTime=settings.AutoSetSystemTime,SystemTimeState=settings.AutoSetSystemTime?"等待校准":"未开启",SystemTimeDetail=settings.AutoSetSystemTime?"多源比对通过后自动校准 Windows 时间。":"可在服务设置中开启自动校准本机时间。",LastSystemSync="—"};
   if(!preview){tray=new NotifyIcon{Icon=Icon,Text="LanTime · 局域网时间站",Visible=true};var menu=new ContextMenuStrip();menu.Items.Add("打开时间站",null,delegate{Restore();});menu.Items.Add("退出界面"+(remote?"（后台服务继续运行）":"并停止服务"),null,delegate{AllowExit=true;Close();});tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate{Restore();};SystemEvents.PowerModeChanged+=PowerChanged;}
   LoadFields();ShowPage(0);timer.Interval=250;timer.Tick+=delegate{RefreshState();};timer.Start();
   Shown+=delegate{if(!preview&&!remote)StartLocal();RefreshState();};
  }
  static Icon MakeIcon(){using(var b=new Bitmap(32,32))using(var g=Graphics.FromImage(b)){g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);Theme.AppMark(g,new RectangleF(1,1,30,30));IntPtr h=b.GetHicon();try{return (Icon)Icon.FromHandle(h).Clone();}finally{DestroyIcon(h);}}}
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr h);
  Label LabelAt(Control parent,string text,int x,int y,int w,int h,float size=10,Color? c=null,FontStyle weight=FontStyle.Regular){var l=new Label{Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=Theme.Font(size,weight),ForeColor=c??Theme.Ink,TextAlign=ContentAlignment.MiddleLeft};parent.Controls.Add(l);return l;}
  FlatButton ButtonAt(Control parent,string text,int x,int y,int w,EventHandler action,bool primary=false){var b=new FlatButton{Text=text,Location=new Point(x,y),Width=w,Primary=primary,BackColor=Color.White,ForeColor=Theme.Ink};b.Click+=action;parent.Controls.Add(b);return b;}
  void Build(){
   sidebar=new Panel{Dock=DockStyle.Left,Width=190,BackColor=Theme.Navy};Body.Controls.Add(sidebar);
   sidebar.Controls.Add(new Mark{Location=new Point(25,28),Size=new Size(34,34),BackColor=Theme.Navy});LabelAt(sidebar,"LanTime",65,28,123,34,16,Color.White,FontStyle.Bold);LabelAt(sidebar,"局域网时间站",27,75,145,25,10,Color.FromArgb(156,179,204));
   string[] names={"运行总览","连接设备","服务设置","运行日志","关于"};string[] glyphs={"clock","devices","settings","logs","about"};nav=new FlatButton[5];
   for(int i=0;i<5;i++){int index=i;nav[i]=ButtonAt(sidebar,names[i],16,140+i*54,158,delegate{ShowPage(index);});nav[i].GlyphName=glyphs[i];nav[i].BackColor=Theme.Navy;nav[i].ForeColor=Color.FromArgb(179,197,218);nav[i].TextAlign=ContentAlignment.MiddleLeft;}
   var sideBottom=new Panel{Dock=DockStyle.Bottom,Height=117,BackColor=Theme.Navy};sidebar.Controls.Add(sideBottom);LabelAt(sideBottom,"NTP / SNTP",26,12,142,24,10,Color.FromArgb(159,183,210));LabelAt(sideBottom,"IPv4 · UDP 123",26,40,142,22,9,Color.FromArgb(132,158,187));LabelAt(sideBottom,"v"+AppInfo.Version+"  ·  Windows",26,76,150,20,8,Color.FromArgb(132,158,187));
   main=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Paper,Padding=new Padding(28,0,28,0)};Body.Controls.Add(main);main.BringToFront();
   var header=new Panel{Dock=DockStyle.Top,Height=112};main.Controls.Add(header);
   title=LabelAt(header,"运行总览",0,22,300,40,23,Theme.Ink,FontStyle.Bold);subtitle=LabelAt(header,"为局域网里的每一台设备，提供同一刻。",2,65,620,26,10,Theme.Muted);
   start=ButtonAt(header,"启动服务",header.Width-112,32,112,Toggle,true);sync=ButtonAt(header,"立即校准",header.Width-236,32,112,Sync);header.Resize+=delegate{start.Left=header.Width-112;sync.Left=header.Width-236;};
   var bottom=new Panel{Dock=DockStyle.Bottom,Height=42};main.Controls.Add(bottom);footer=LabelAt(bottom,"",0,5,830,27,9,Theme.Muted);footer.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;
   var pages=new Panel{Dock=DockStyle.Fill};main.Controls.Add(pages);pages.BringToFront();
   overview=new Panel{Dock=DockStyle.Fill};clientsPage=new Panel{Dock=DockStyle.Fill};settingsPage=new Panel{Dock=DockStyle.Fill,AutoScroll=true};logsPage=new Panel{Dock=DockStyle.Fill};aboutPage=new Panel{Dock=DockStyle.Fill,AutoScroll=true};pages.Controls.AddRange(new Control[]{overview,clientsPage,settingsPage,logsPage,aboutPage});
   board=new Board{Dock=DockStyle.Top,Height=300};overview.Controls.Add(board);
   var sourceArea=new Panel{Dock=DockStyle.Fill};overview.Controls.Add(sourceArea);sourceArea.BringToFront();
   var sourceHeader=new Panel{Dock=DockStyle.Top,Height=44};sourceArea.Controls.Add(sourceHeader);tableTitle=LabelAt(sourceHeader,"外部时间源",0,3,220,32,12,Theme.Ink,FontStyle.Bold);LabelAt(sourceHeader,"多源比对 · 排除异常偏差",250,5,440,30,9,Theme.Muted);
   var connect=new Panel{Dock=DockStyle.Bottom,Height=112};sourceArea.Controls.Add(connect);
   endpoint=LabelAt(connect,"设备填写地址",0,8,112,30,10,Theme.Ink,FontStyle.Bold);
   addressChoice=new SelectInput{Location=new Point(119,7),Width=connect.Width-239,DropDownStyle=ComboBoxStyle.DropDownList,Font=Theme.Font(10),DisplayMember="Value",ValueMember="Key",BackColor=Color.White};connect.Controls.Add(addressChoice);
   copy=ButtonAt(connect,"复制地址",connect.Width-100,8,100,delegate{try{if(addressChoice.SelectedItem!=null)Clipboard.SetText(((KeyValuePair<string,string>)addressChoice.SelectedItem).Key);}catch(Exception e){Error(e);}});connect.Resize+=delegate{copy.Left=connect.Width-100;addressChoice.Width=Math.Max(150,connect.Width-239);};
   localStatus=LabelAt(connect,"",0,42,830,27,9,Theme.Teal);localStatus.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;localStatus.AutoEllipsis=true;
   help=LabelAt(connect,"",0,75,830,32,9,Theme.Muted);help.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;
   sources=Table(new[]{"时间源 / 域名","连接状态","往返延迟","校准前偏差","层级"},new[]{255,215,110,140,65});var sourceCard=new SurfacePanel{Dock=DockStyle.Fill,Padding=new Padding(10)};sourceArea.Controls.Add(sourceCard);sourceCard.Controls.Add(sources);sourceCard.BringToFront();sources.Resize+=delegate{int available=sources.ClientSize.Width-24;double[] ratios={.30,.27,.14,.19,.10};for(int i=0;i<5;i++)sources.Columns[i].Width=(int)(available*ratios[i]);};
   clients=Table(new[]{"设备 IP","最近请求时间","累计请求"},new[]{310,260,190});var clientCard=new SurfacePanel{Dock=DockStyle.Fill,Padding=new Padding(10)};clientsPage.Controls.Add(clientCard);clientCard.Controls.Add(clients);var ct=new Panel{Dock=DockStyle.Top,Height=62};clientsPage.Controls.Add(ct);LabelAt(ct,"记录最近访问的设备（最多显示 200 台）",0,0,760,28,11,Theme.Ink,FontStyle.Bold);LabelAt(ct,"设备按 IP 合并；拒绝或限流的请求不计入成功请求。",0,29,800,25,9,Theme.Muted);
   logs=new TextInput{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Both,WordWrap=false,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink,Font=new Font("Consolas",13,FontStyle.Regular,GraphicsUnit.Pixel)};logsPage.Controls.Add(logs);var loghead=new Panel{Dock=DockStyle.Top,Height=60};logsPage.Controls.Add(loghead);LabelAt(loghead,"运行记录",0,4,200,36,12,Theme.Ink,FontStyle.Bold);ButtonAt(loghead,"打开数据目录",220,3,150,delegate{Directory.CreateDirectory(Store.DirectoryPath);Process.Start("explorer.exe",Store.DirectoryPath);});
   BuildSettings();BuildAbout();
  }
  void BuildAbout(){
   var card=new SurfacePanel{Location=new Point(0,0),Width=aboutPage.ClientSize.Width-2,Height=620,BackColor=Color.White};aboutPage.Controls.Add(card);
   var mark=new Mark{Size=new Size(72,72),Top=23,Filled=true,BackColor=Color.White};card.Controls.Add(mark);
   var name=LabelAt(card,"LanTime",0,103,card.Width,39,25,Theme.Ink,FontStyle.Bold);name.TextAlign=ContentAlignment.MiddleCenter;
   var version=LabelAt(card,"局域网时间站  ·  v"+AppInfo.Version,0,147,card.Width,25,10,Theme.Muted);version.TextAlign=ContentAlignment.MiddleCenter;
   var author=LabelAt(card,"作者："+AppInfo.Author,0,186,card.Width,25,11);author.TextAlign=ContentAlignment.MiddleCenter;
   var website=new LinkLabel{Text=AppInfo.Website,Top=217,Height=28,Font=Theme.Font(11),TextAlign=ContentAlignment.MiddleCenter,LinkColor=Theme.Blue,ActiveLinkColor=Theme.Teal,VisitedLinkColor=Theme.Blue,BackColor=Color.White,AccessibleName="作者网站 "+AppInfo.Website};card.Controls.Add(website);
   website.LinkClicked+=delegate{try{Process.Start(new ProcessStartInfo(AppInfo.Website){UseShellExecute=true});}catch(Exception e){Error(e);}};
   var caption=LabelAt(card,"开源协议  /  MIT License",26,282,card.Width-52,29,12,Theme.Ink,FontStyle.Bold);
   var summary=LabelAt(card,"允许使用、修改与分发；保留版权及许可声明。完整条款如下。",26,316,card.Width-52,25,9,Theme.Muted);
   var license=new TextInput{Location=new Point(26,354),Size=new Size(card.Width-52,205),Multiline=true,ReadOnly=true,WordWrap=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Theme.Paper,ForeColor=Theme.Ink,Font=new Font("Consolas",12,FontStyle.Regular,GraphicsUnit.Pixel),Text=AppInfo.LicenseText.Replace("\r\n","\n").Replace("\n",Environment.NewLine),AccessibleName="MIT 开源协议全文"};card.Controls.Add(license);
   var copyright=LabelAt(card,AppInfo.Copyright,26,574,card.Width-52,25,9,Theme.Muted);copyright.TextAlign=ContentAlignment.MiddleCenter;
   EventHandler layout=delegate{card.Width=Math.Max(550,aboutPage.ClientSize.Width-2);mark.Left=(card.Width-mark.Width)/2;name.Width=version.Width=author.Width=website.Width=card.Width;caption.Width=summary.Width=license.Width=copyright.Width=card.Width-52;};
   aboutPage.Resize+=layout;layout(null,EventArgs.Empty);
  }
  static ListView Table(string[] headers,int[] widths){var v=new DataTable{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,GridLines=false,HeaderStyle=ColumnHeaderStyle.Nonclickable,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink,Font=Theme.Font(10),HideSelection=false};for(int i=0;i<headers.Length;i++)v.Columns.Add(headers[i],widths[i]);var images=new ImageList{ImageSize=new Size(1,32)};images.Images.Add(new Bitmap(1,38));v.SmallImageList=images;return v;}
  TextInput Field(string label,int x,int y,int width,Control parent){var caption=LabelAt(parent,label,x,y,width,24,9,Theme.Muted);var t=new TextInput{Location=new Point(x,y+29),Width=width,Font=Theme.Font(10),AccessibleName=label};t.Editor.AccessibleName=label;t.Tag=caption;parent.Controls.Add(t);return t;}
  static void PlaceField(TextInput input,int x,int width){input.Left=x;input.Width=width;var caption=input.Tag as Label;if(caption!=null){caption.Left=x;caption.Width=width;}}
  void BuildSettings(){
   var content=new Panel{Location=new Point(0,0),Size=new Size(840,970),BackColor=Theme.Paper};settingsPage.Controls.Add(content);settingsNote=LabelAt(content,"",0,0,820,35,10,Theme.Amber);
   bind=Field("监听 IPv4（0.0.0.0 表示所有网卡）",0,43,315,content);port=Field("UDP 端口",345,43,145,content);quorum=Field("最少一致来源数",520,43,160,content);
   interval=Field("自动校时间隔 / 秒",0,127,315,content);holdover=Field("断网保持时限 / 秒",345,127,335,content);
   hosts=Field("外部 NTP 时间源（每行一个，至少两个）",0,211,397,content);hosts.Multiline=true;hosts.Height=132;hosts.ScrollBars=ScrollBars.Vertical;
   networks=Field("允许访问的 IPv4 网段（每行一个 CIDR）",425,211,397,content);networks.Multiline=true;networks.Height=132;networks.ScrollBars=ScrollBars.Vertical;
   LabelAt(content,"超过保持时限后对外标记未同步。本机校时仅使用本轮多源一致时间。",0,384,810,29,9,Theme.Muted);
   autoSystemTime=new ToggleInput{Text="自动校准本机 Windows 时间（每轮多源比对通过后）",Location=new Point(0,423),Size=new Size(810,30),Font=Theme.Font(10)};content.Controls.Add(autoSystemTime);
   localSettingStatus=LabelAt(content,"",0,458,820,36,9,Theme.Muted);localSettingStatus.AutoEllipsis=true;
   startup=new ToggleInput{Text="登录 Windows 后自动启动界面（当前用户，便携模式）",Location=new Point(0,502),Size=new Size(790,30),Font=Theme.Font(10)};content.Controls.Add(startup);
   ButtonAt(content,"保存设置",0,552,126,SaveSettings,true);ButtonAt(content,"恢复默认值",144,552,126,delegate{LoadFields(new Settings());});
   runAdmin=ButtonAt(content,"管理员运行",288,552,142,RestartAdmin);
   LabelAt(content,"局域网与后台运行",0,626,800,32,15,Theme.Ink,FontStyle.Bold);
   LabelAt(content,"配置防火墙后，将本机 IPv4 地址填写到监控设备的 NTP 服务器中。",0,665,810,27,9,Theme.Muted);
   ButtonAt(content,"允许局域网访问",0,710,164,delegate{AdminAction("--admin-firewall",false);});ButtonAt(content,"移除防火墙规则",181,710,164,delegate{AdminAction("--admin-remove-firewall",false);});
   ButtonAt(content,"安装开机后台服务",0,765,164,delegate{AdminAction("--admin-install",true);});ButtonAt(content,"卸载后台服务",181,765,164,delegate{AdminAction("--admin-uninstall",true);});
   LabelAt(content,"便携校时需管理员权限；登录自启时也需授权。需要免登录持续校时，请安装开机后台服务。",0,820,810,48,9,Theme.Muted);
   EventHandler layout=delegate{int w=Math.Max(580,settingsPage.ClientSize.Width-22);content.Width=w;int first=(int)((w-40)*.45),second=(int)((w-40)*.22);PlaceField(bind,0,first);PlaceField(port,first+20,second);PlaceField(quorum,first+second+40,w-first-second-40);int half=(w-28)/2;PlaceField(interval,0,half);PlaceField(holdover,half+28,w-half-28);PlaceField(hosts,0,half);PlaceField(networks,half+28,w-half-28);autoSystemTime.Width=startup.Width=w;localSettingStatus.Width=settingsNote.Width=w;foreach(Control control in content.Controls)if(control is Label&&control.Left==0&&control.Width>400)control.Width=w;};settingsPage.Resize+=layout;layout(null,EventArgs.Empty);

  }
  void LoadFields(){LoadFields(settings);}
  void LoadFields(Settings value){ready=false;bind.Text=value.Bind;port.Text=value.Port.ToString();interval.Text=value.IntervalSeconds.ToString();holdover.Text=value.HoldoverSeconds.ToString();quorum.Text=value.Quorum.ToString();autoSystemTime.Checked=value.AutoSetSystemTime;hosts.Lines=value.Servers;networks.Lines=value.Networks;using(var key=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run")){startup.Checked=key!=null&&key.GetValue("LanTime")!=null;}startup.Enabled=!remote;ready=true;}
  void ShowPage(int page){Panel[] a={overview,clientsPage,settingsPage,logsPage,aboutPage};for(int i=0;i<a.Length;i++){a[i].Visible=i==page;nav[i].Selected=i==page;nav[i].Invalidate();}string[] labels={"运行总览","连接设备","服务设置","运行日志","关于 LanTime"};title.Text=labels[page];subtitle.Text=page==4?"多源校准 · 局域网授时 · 开源共享":"为局域网里的每一台设备，提供同一刻。";if(page==3)logs.Text=Store.Tail();}
  public void CapturePage(int page){ShowPage(Math.Max(0,Math.Min(4,page)));ActiveControl=null;RefreshState();Application.DoEvents();}
  void StartLocal(){
   try{if(engine!=null)engine.Dispose();engine=new Engine(settings);engine.Start();}
   catch(Exception e){Store.Log("启动失败："+e.Message);Error(new Exception("无法监听 UDP "+settings.Port+"："+e.Message+"\n如端口被占用，请检查 Windows Time（W32Time）或其他 NTP 程序。"));}
  }
  void Toggle(object sender,EventArgs e){if(preview)return;try{if(remote){Program.Elevate(Program.ServiceRunning()?"--admin-stop":"--admin-start");}else if(engine!=null&&engine.GetSnapshot().Running)engine.Stop();else StartLocal();RefreshState();}catch(Exception ex){Error(ex);}}
  void Sync(object sender,EventArgs e){if(preview)return;try{if(remote)Program.Elevate("--admin-sync");else if(engine!=null)engine.RequestSync();}catch(Exception ex){Error(ex);}}
  void RefreshState(){
   if(IsDisposed||!ready)return;
   if(!preview){
    if(uiClock.Elapsed.TotalSeconds-serviceRefresh>=2){serviceRefresh=uiClock.Elapsed.TotalSeconds;bool installed=Program.Installed();if(installed!=remote){if(engine!=null){engine.Dispose();engine=null;}remote=installed;startup.Enabled=!remote;Store.Log("界面切换到"+(remote?"后台服务模式":"便携模式"));}}
    if(remote){
     bool running=Program.ServiceRunning();
     try{string p=Path.Combine(Store.DirectoryPath,"status.json");Snapshot next=Store.Read<Snapshot>(p);if(!running){next.Running=false;next.Trusted=false;next.State="服务已停止";next.UtcTicks=DateTime.UtcNow.Ticks;}else if((DateTime.UtcNow-File.GetLastWriteTimeUtc(p)).TotalSeconds>10){next.Trusted=false;next.State="等待后台状态";next.UtcTicks=DateTime.UtcNow.Ticks;}if(next.UtcTicks!=receivedTicks){state=next;receivedTicks=next.UtcTicks;received.Restart();}}
     catch{state.Running=running;state.Trusted=false;state.State=running?"等待后台状态":"服务已停止";state.UtcTicks=DateTime.UtcNow.Ticks;receivedTicks=state.UtcTicks;received.Restart();}
    }else if(engine!=null){state=engine.GetSnapshot();receivedTicks=state.UtcTicks;received.Restart();}
   }
   if(preview||engine==null&&!remote)state.UtcTicks=DateTime.UtcNow.Ticks;
   // Only the displayed clock is extrapolated between service status snapshots.
   board.State=state;board.DisplayUtcTicks=remote&&state.Running?receivedTicks+received.Elapsed.Ticks:state.UtcTicks;board.Invalidate();
   start.Enabled=!preview;start.Text=state.Running?"停止服务":"启动服务";sync.Enabled=state.Running&&!state.Syncing;sync.Text=state.Syncing?"正在比对…":"立即校准";
   if(uiClock.Elapsed.TotalSeconds-adapterRefresh>15){adapterRefresh=uiClock.Elapsed.TotalSeconds;string previous=addressChoice.SelectedItem==null?null:((KeyValuePair<string,string>)addressChoice.SelectedItem).Key;addressChoice.Items.Clear();foreach(var pair in Engine.LocalInterfaces())addressChoice.Items.Add(pair);if(addressChoice.Items.Count>0){addressChoice.SelectedIndex=0;for(int i=0;i<addressChoice.Items.Count;i++)if(((KeyValuePair<string,string>)addressChoice.Items[i]).Key==previous)addressChoice.SelectedIndex=i;}}
   help.Text="UDP "+settings.Port+"  ·  选择监控设备可达的网卡  ·  设备时区选 UTC+8  ·  首次使用请在设置中放行防火墙";
   footer.Text=(remote?"后台服务模式":"便携模式")+"  ·  "+(state.Running?"UDP "+state.Address:"服务未监听")+"  ·  上次校准 "+state.LastSync;
   localStatus.Text="本机校时 · "+(state.SystemTimeState??(settings.AutoSetSystemTime?"等待校准":"未开启"))+(String.IsNullOrEmpty(state.LastSystemSync)||state.LastSystemSync=="—"?"":" · "+state.LastSystemSync);localStatus.ForeColor=state.SystemTimeNeedsAdmin||state.SystemTimeState=="校准失败"?Theme.Amber:Theme.Teal;
   localSettingStatus.Text=(state.SystemTimeDetail??(settings.AutoSetSystemTime?"多源比对通过后自动校准 Windows 时间。":"可勾选上方开关，保存后生效。"));runAdmin.Enabled=!preview&&!remote&&!Program.IsAdministrator;
   settingsNote.Text=remote&&state.Running?"后台服务运行中。请先停止服务，再保存配置。":"配置目录："+Store.DirectoryPath;
   if(uiClock.Elapsed.TotalSeconds-listRefresh>=1){listRefresh=uiClock.Elapsed.TotalSeconds;FillSources();FillClients();if(logsPage.Visible){string text=Store.Tail();if(logs.Text!=text)logs.Text=text;}}
   if(tray!=null)tray.Text="LanTime · "+state.State;
  }
  void FillSources(){sources.BeginUpdate();sources.Items.Clear();if(state.Sources!=null)foreach(var s in state.Sources){var item=new ListViewItem(s.Host);item.SubItems.Add(s.Status??"等待查询");item.SubItems.Add(s.Address==null?"—":s.DelayMs.ToString("F1")+" ms");item.SubItems.Add(s.Address==null?"—":Board.Signed(s.OffsetMs)+" ms");item.SubItems.Add(s.Address==null?"—":"S"+s.Stratum);item.ForeColor=s.Selected?Theme.Teal:Theme.Ink;sources.Items.Add(item);}sources.EndUpdate();}
  void FillClients(){clients.BeginUpdate();clients.Items.Clear();if(state.Clients!=null)foreach(var c in state.Clients){var row=new ListViewItem(c.Address);row.SubItems.Add(c.LastSeen);row.SubItems.Add(c.Requests.ToString("N0"));clients.Items.Add(row);}clients.EndUpdate();}
  static string[] Lines(TextInput box){return box.Lines.Select(x=>x.Trim()).Where(x=>x.Length>0).ToArray();}
  void SaveSettings(object sender,EventArgs e){if(preview){AppDialog.Show("当前为界面预览，输入与选择仅用于检查样式，配置不会保存。","界面预览");return;}try{
   if(remote&&Program.ServiceRunning())throw new Exception("请先停止后台服务再保存设置。");
   var c=new Settings{Bind=bind.Text.Trim(),Port=Int32.Parse(port.Text),IntervalSeconds=Int32.Parse(interval.Text),HoldoverSeconds=Int32.Parse(holdover.Text),Quorum=Int32.Parse(quorum.Text),Servers=Lines(hosts),Networks=Lines(networks),AutoSetSystemTime=autoSystemTime.Checked};c.Validate();
   bool wasRunning=!remote&&engine!=null&&engine.GetSnapshot().Running;Store.Write(Store.ConfigPath,c);settings=c;
   using(var key=Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run")){if(startup.Checked&&!remote)key.SetValue("LanTime","\""+Program.Exe+"\"");else key.DeleteValue("LanTime",false);}
   if(!remote&&c.AutoSetSystemTime&&!Program.IsAdministrator&&!preview){RestartAdmin(sender,e);if(AllowExit)return;}
   if(wasRunning)StartLocal();else if(!remote){if(engine!=null)engine.Dispose();engine=new Engine(settings);}
   AppDialog.Show("设置已保存。"+(wasRunning?"服务已使用新配置重新启动。":"下次启动服务时生效。")+"\n如果修改了端口或网段，请重新添加防火墙规则。","LanTime",MessageBoxButtons.OK,MessageBoxIcon.Information);
  }catch(Exception ex){Error(ex);}}
  void RestartAdmin(object sender,EventArgs e){if(preview||remote||Program.IsAdministrator)return;try{Program.Elevate("--wait-for-exit "+Process.GetCurrentProcess().Id);AllowExit=true;Close();}catch(Exception ex){Error(ex);}}
  void AdminAction(string command,bool stopFirst){if(preview)return;try{if(stopFirst&&!remote&&engine!=null)engine.Stop();Program.Elevate(command);}catch(Exception e){Error(e);}}
  void PowerChanged(object sender,PowerModeChangedEventArgs e){if(e.Mode==PowerModes.Resume&&engine!=null)engine.Invalidate();}
  void Restore(){Show();WindowState=FormWindowState.Normal;Activate();}
  static void Error(Exception e){AppDialog.Show(e.Message,"LanTime",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  protected override void OnFormClosing(FormClosingEventArgs e){if(!AllowExit&&!preview&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();if(tray!=null)tray.ShowBalloonTip(2500,"LanTime 继续运行","已收起到托盘。右键托盘图标可以退出。",ToolTipIcon.Info);return;}timer.Stop();SystemEvents.PowerModeChanged-=PowerChanged;if(engine!=null)engine.Dispose();if(tray!=null){tray.Visible=false;tray.Dispose();}base.OnFormClosing(e);}
 }
}

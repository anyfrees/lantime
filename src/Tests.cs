using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LanTime {
 public static class Tests {
  static readonly List<string> report=new List<string>();static int passed,failed;
  static void Check(bool condition,string name){if(!condition){failed++;report.Add("FAIL "+name);}else{passed++;report.Add("PASS "+name);}}
  static void Reject(Action action,string name){bool threw=false;try{action();}catch{threw=true;}Check(threw,name);}
  static int FreePort(){using(var s=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp)){s.Bind(new IPEndPoint(IPAddress.Loopback,0));return ((IPEndPoint)s.LocalEndPoint).Port;}}
  public static int Run(string path){
   Store.DirectoryPath=Path.Combine(Path.GetTempPath(),"LanTime-tests-"+Guid.NewGuid().ToString("N"));
   try{
    byte[] p=new byte[48];DateTime now=DateTime.UtcNow;
    foreach(int year in new[]{2026,2036,2060}){DateTime t=new DateTime(year,12,31,23,59,59,DateTimeKind.Utc).AddTicks(1234567);Wire.PutTime(p,40,t);Check(Math.Abs((Wire.Time(p,40,t)-t).TotalMilliseconds)<.001,"NTP timestamp era "+year);}
    Wire.PutTime(p,40,new DateTime(2036,2,7,6,28,16,DateTimeKind.Utc));Check(Wire.U32(p,40)==0,"2036 seconds rollover");
    Check(new Cidr("192.168.1.0/24").Contains(IPAddress.Parse("192.168.1.255")),"CIDR last address");Check(!new Cidr("192.168.1.0/24").Contains(IPAddress.Parse("192.168.2.1")),"CIDR rejects other subnet");Check(new Cidr("0.0.0.0/0").Contains(IPAddress.Parse("8.8.8.8")),"CIDR /0");Check(!new Cidr("127.0.0.1/32").Contains(IPAddress.Parse("127.0.0.2")),"CIDR /32");Reject(()=>new Cidr("::1/32"),"reject IPv6 CIDR");
    var original=new Settings();Store.Write(Store.ConfigPath,original);Check(Store.Load().Servers.Length==4,"settings JSON roundtrip");var invalid=new Settings{Quorum=1};Reject(invalid.Validate,"requires multi-source quorum");invalid=new Settings{IntervalSeconds=0};Reject(invalid.Validate,"reject zero polling interval");
    Check(Store.DataDirectory(@"C:\Portable",@"C:\ProgramData",false)==@"C:\Portable\data","portable keeps data next to executable");Check(Store.DataDirectory(@"C:\Program Files\LanTime",@"C:\ProgramData",true)==@"C:\ProgramData\LanTime","installed layout stores data outside Program Files");
    Check(Program.ServiceExecutable("\"C:\\Program Files\\LanTime\\LanTime.exe\" --service")==@"C:\Program Files\LanTime\LanTime.exe","installer service ownership parses quoted executable path");Check(Program.ServiceExecutable(@"C:\Tools\LanTime.exe --service")==@"C:\Tools\LanTime.exe","installer service ownership parses unquoted executable path");Reject(()=>Program.ServiceExecutable("\"broken path"),"installer rejects malformed service executable path");
    int serviceStops=0,serviceRemoves=0;string serviceLog=null;Action stopService=delegate{serviceStops++;};Action removeService=delegate{serviceRemoves++;};Action<string> maintenanceLog=delegate(string text){serviceLog=text;};
    Check(Program.MaintainService("\"D:\\Users\\Administrator\\Desktop\\LanTime.exe\" --service",@"C:\Program Files (x86)\LanTime\LanTime.exe",stopService,removeService,false,maintenanceLog)==0&&serviceStops==0&&serviceRemoves==0&&serviceLog.Contains(@"Desktop\LanTime.exe"),"Win7 desktop-service regression: install succeeds and preserves original service");
    Check(Program.MaintainService("\"D:\\Users\\Administrator\\Desktop\\LanTime.exe\" --service",@"C:\Program Files (x86)\LanTime\LanTime.exe",stopService,removeService,true,maintenanceLog)==0&&serviceStops==0&&serviceRemoves==0,"uninstall preserves service owned by another directory");
    Check(Program.MaintainService("\"C:\\Program Files (x86)\\LanTime\\LanTime.exe\" --service",@"c:\program files (x86)\lantime\LanTime.exe",stopService,removeService,false,maintenanceLog)==0&&serviceStops==1&&serviceRemoves==0,"repair stops only service owned by installed path");
    Check(Program.MaintainService("\"C:\\Program Files (x86)\\LanTime\\LanTime.exe\" --service",@"C:\Program Files (x86)\LanTime\LanTime.exe",stopService,removeService,true,maintenanceLog)==0&&serviceStops==2&&serviceRemoves==1,"uninstall stops and removes owned service");
    Reject(()=>Program.MaintainService("\"broken",@"C:\LanTime\LanTime.exe",stopService,removeService,false,maintenanceLog),"malformed service command fails instead of operating on an unknown service");
    Check(serviceStops==2&&serviceRemoves==1,"invalid service path cannot stop or delete any service");
    SourceState a=new SourceState{Address="1.1.1.1",OffsetMs=10},b=new SourceState{Address="2.2.2.2",OffsetMs=12},outlier=new SourceState{Address="3.3.3.3",OffsetMs=10000};
    Check(Upstream.Consensus(new List<SourceState>{a,b,outlier},2).Count==2,"consensus rejects distant source");Check(Upstream.Consensus(new List<SourceState>{a,outlier},2).Count==0,"two disagreeing sources rejected");Check(Upstream.Consensus(new List<SourceState>{a,new SourceState{Address=a.Address,OffsetMs=11}},2).Count==0,"same upstream IP not double-counted");Check(Upstream.Consensus(new List<SourceState>{a,b,outlier,new SourceState{Address="4.4.4.4",OffsetMs=10002}},2).Count==0,"split vote rejected");Check(Upstream.Consensus(new List<SourceState>{a,b,new SourceState{Address="3.3.3.3",OffsetMs=11,Leap=1}},2).Count==2,"conflicting leap announcement excluded");
    byte[] request=new byte[48];request[0]=0x23;Wire.PutTime(request,40,now);byte[] good=Mock.Reply(request,now.AddMilliseconds(10),3);var valid=Upstream.Validate("mock",IPAddress.Loopback,request,good,48,now,now.AddMilliseconds(30));Check(Math.Abs(valid.OffsetMs+5)<1,"four timestamp offset formula");Check(Math.Abs(valid.DelayMs-30)<1,"roundtrip formula");
    byte[] bad=(byte[])good.Clone();bad[24]^=1;Reject(()=>Upstream.Validate("mock",IPAddress.Loopback,request,bad,48,now,now),"reject mismatched originate timestamp");bad=(byte[])good.Clone();bad[0]=0xe4;Reject(()=>Upstream.Validate("mock",IPAddress.Loopback,request,bad,48,now,now),"reject unsynchronized upstream");bad=(byte[])good.Clone();bad[1]=0;Reject(()=>Upstream.Validate("mock",IPAddress.Loopback,request,bad,48,now,now),"reject kiss of death");Reject(()=>Upstream.Validate("mock",IPAddress.Loopback,request,good,20,now,now),"reject truncated packet");
    Clock clock=new Clock();clock.Correct(120,true);Check(Math.Abs((clock.UtcNow-DateTime.UtcNow).TotalSeconds-120)<.1,"initial virtual clock calibration");DateTime before=clock.UtcNow;clock.Correct(-10,false);Thread.Sleep(20);Check(clock.UtcNow>=before,"negative correction does not reverse synchronized clock");Check(clock.Pending< -9,"negative correction is slewed");
    Check(!original.AutoSetSystemTime,"system clock changes are opt-in");
    original.AutoSetSystemTime=true;Store.Write(Store.ConfigPath,original);Check(Store.Load().AutoSetSystemTime,"automatic system time setting persists");
    string oldJson=File.ReadAllText(Store.ConfigPath);oldJson=oldJson.Replace("\"AutoSetSystemTime\":true,","").Replace(",\"AutoSetSystemTime\":true","");File.WriteAllText(Store.ConfigPath,oldJson);Check(!Store.Load().AutoSetSystemTime,"legacy settings do not silently enable Windows clock changes");
    var fake=new FakeSystemClock();var local=new LocalTimeSync(true,fake);
    local.Apply(false,true,clock);local.Apply(true,false,clock);Check(fake.Writes==0,"disabled and unconfirmed time never writes system clock");
    local.Apply(true,true,clock);Check(fake.Writes==1&&local.State=="校准成功","accepted time writes injected system clock");Check(Math.Abs((fake.UtcNow-DateTime.UtcNow).TotalSeconds-110)<.15,"system target includes negative pending slew correction");Check(Math.Abs(clock.Pending)>9,"setting Windows time does not step or rebase the LAN clock");
    local.Apply(true,true,clock);Check(fake.Writes==1&&local.State=="时间一致","sub-50ms difference avoids unnecessary clock steps");
    local.NoConsensus(true);Check(fake.Writes==1&&local.State=="等待一致时间"&&local.LastSuccess!="—","loss of consensus preserves history and never reapplies holdover time");
    local.Invalidate(true);Check(local.State=="等待重新校准","resume requires a new consensus for system time");
    var denied=new FakeSystemClock{Error=1314};var deniedLocal=new LocalTimeSync(true,denied);deniedLocal.Apply(true,true,clock);Check(deniedLocal.NeedsAdmin&&deniedLocal.State=="需要校时权限"&&deniedLocal.LastSuccess=="—","missing system-time privilege is visible and not reported as success");
    var wrongClock=new FakeSystemClock{IgnoreWrite=true};var badLocal=new LocalTimeSync(true,wrongClock);badLocal.Apply(true,true,clock);Check(badLocal.State=="校准失败"&&badLocal.LastSuccess=="—","failed post-write verification does not report success");
    var backwardClock=new Clock();backwardClock.Correct(-120,true);var backwardFake=new FakeSystemClock();var backwardLocal=new LocalTimeSync(true,backwardFake);backwardLocal.Apply(true,true,backwardClock);Check(backwardFake.Writes==1&&backwardLocal.LastCorrectionMs< -119900,"system clock can be corrected backwards using accepted time");
    var nativeTime=WindowsSystemClock.SystemTime.FromUtc(new DateTime(2026,11,7,23,59,58,987,DateTimeKind.Utc));Check(nativeTime.Hour==23&&nativeTime.Day==7&&nativeTime.Milliseconds==987,"Windows SYSTEMTIME mapping preserves UTC date and milliseconds");Reject(()=>WindowsSystemClock.SystemTime.FromUtc(DateTime.SpecifyKind(DateTime.Now,DateTimeKind.Local)),"Windows system-time adapter rejects local time input");
    int up=FreePort(),down=FreePort();while(down==up)down=FreePort();
    using(var m1=new Mock("127.0.0.1",up,120))using(var m2=new Mock("127.0.0.2",up,120))using(var m3=new Mock("127.0.0.3",up,600)){
     var c=new Settings{Bind="127.0.0.1",Port=down,Servers=new[]{"127.0.0.1","127.0.0.2","127.0.0.3"},IntervalSeconds=60,HoldoverSeconds=120,AutoSetSystemTime=true};var liveFake=new FakeSystemClock();
     using(var engine=new Engine(c,up,liveFake)){
      byte[] unsynced=engine.BuildReply(request,engine.Clock.UtcNow);Check(unsynced[1]==16&&(unsynced[0]>>6)==3,"uninitialized server advertises LI3 / stratum16");
      engine.Start();Stopwatch timeout=Stopwatch.StartNew();while(!engine.GetSnapshot().Trusted&&timeout.Elapsed.TotalSeconds<10)Thread.Sleep(50);
      Snapshot snap=engine.GetSnapshot();Check(snap.Trusted&&snap.Agreeing==2,"live UDP multi-source synchronization");Check(Math.Abs(snap.SystemOffsetMs-120000)<150,"outlier excluded from actual clock");
      Check(liveFake.Writes==1&&snap.AutoSetSystemTime&&snap.SystemTimeState=="校准成功","engine automatically calibrates system clock after fresh independent quorum");Check(Math.Abs((liveFake.UtcNow-DateTime.UtcNow).TotalSeconds-120)<.15,"outlier cannot influence Windows clock target");
      using(var client=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp)){
       client.ReceiveTimeout=2000;client.Connect(IPAddress.Loopback,down);byte[] query=new byte[48];query[0]=0x23;query[2]=6;Wire.PutTime(query,40,DateTime.UtcNow);client.Send(query);byte[] answer=new byte[512];int n=client.Receive(answer);
       Check(n==48&&(answer[0]&7)==4&&((answer[0]>>3)&7)==4,"48-byte NTPv4 server response");Check(answer[1]==4&&(answer[0]>>6)==0,"live response stratum / leap");Check(answer.Skip(24).Take(8).SequenceEqual(query.Skip(40).Take(8)),"echoes exact 64-bit client timestamp");DateTime transmit=Wire.Time(answer,40,DateTime.UtcNow);Check(Math.Abs((transmit-DateTime.UtcNow).TotalSeconds-120)<.2,"served time uses calibrated clock");Check(Wire.Time(answer,32,transmit)<=transmit,"receive timestamp precedes transmit timestamp");
       query[0]=0x1b;client.Send(query);n=client.Receive(answer);Check(n==48&&((answer[0]>>3)&7)==3,"NTPv3 client compatibility");
       query[0]=0x23;Array.Clear(query,40,8);client.Send(query);Thread.Sleep(100);Check(engine.GetSnapshot().Dropped>=1,"zero client timestamp is dropped");
      }
      Check(engine.GetSnapshot().Clients.Length==1&&engine.GetSnapshot().Requests==2,"client accounting");
      using(var second=new Engine(c,up,new FakeSystemClock()))Reject(second.Start,"exclusive UDP port binding");
      typeof(Engine).GetField("lastSync",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(engine,engine.Clock.Seconds-121);byte[] expired=engine.BuildReply(request,engine.Clock.UtcNow);Check(expired[1]==16&&(expired[0]>>6)==3,"expired holdover marks unsynchronized");
      Check(liveFake.Writes==1,"holdover expiry and status queries do not set system time");
      engine.Stop();Check(!engine.GetSnapshot().Running,"listener and worker stop cleanly");
     }
    }
    using(var firstSource=new Mock("127.0.0.1",up,120))using(var otherSource=new Mock("127.0.0.2",up,120)){
     var rightsClock=new FakeSystemClock{Error=1314};var rightsSettings=new Settings{Bind="127.0.0.1",Port=FreePort(),Servers=new[]{"127.0.0.1","127.0.0.2"},IntervalSeconds=60,HoldoverSeconds=120,AutoSetSystemTime=true};
     using(var noRights=new Engine(rightsSettings,up,rightsClock)){noRights.Start();var wait=Stopwatch.StartNew();while(!noRights.GetSnapshot().Trusted&&wait.ElapsedMilliseconds<6000)Thread.Sleep(30);var status=noRights.GetSnapshot();Check(status.Trusted&&status.SystemTimeNeedsAdmin&&status.SystemTimeState=="需要校时权限","local permission failure leaves LAN NTP trusted and reports separate failure");}
     var resumeClock=new FakeSystemClock();rightsSettings.Port=FreePort();using(var resumed=new Engine(rightsSettings,up,resumeClock)){resumed.Start();var wait=Stopwatch.StartNew();while(!resumed.GetSnapshot().Syncing&&wait.ElapsedMilliseconds<1000)Thread.Sleep(1);resumed.Invalidate();while(resumed.GetSnapshot().SystemTimeState!="校准成功"&&wait.ElapsedMilliseconds<6000)Thread.Sleep(30);Check(resumeClock.Writes==1&&resumed.GetSnapshot().Trusted,"resume discards in-flight pre-resume consensus before setting Windows time");}
    }
    using(var firstSource=new Mock("127.0.0.1",up,120))using(var otherSource=new Mock("127.0.0.2",up,600)){
     var conflictClock=new FakeSystemClock();var conflictSettings=new Settings{Bind="127.0.0.1",Port=FreePort(),Servers=new[]{"127.0.0.1","127.0.0.2"},IntervalSeconds=60,HoldoverSeconds=120,AutoSetSystemTime=true};
     using(var conflict=new Engine(conflictSettings,up,conflictClock)){conflict.Start();var wait=Stopwatch.StartNew();while(conflict.GetSnapshot().SystemTimeState=="等待校准"&&wait.ElapsedMilliseconds<6000)Thread.Sleep(30);Check(conflictClock.Writes==0&&!conflict.GetSnapshot().Trusted&&conflict.GetSnapshot().SystemTimeState=="等待一致时间","live disagreeing sources cannot change Windows time");}
    }
    var blockedConfig=new Settings{Bind="127.0.0.1",Port=FreePort(),Networks=new[]{"10.0.0.0/8"},Servers=new[]{"127.0.0.1","127.0.0.2"},IntervalSeconds=60,HoldoverSeconds=120};
    using(var blocked=new Engine(blockedConfig,up)){
     blocked.Start();using(var client=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp)){client.ReceiveTimeout=200;client.Connect(IPAddress.Loopback,blockedConfig.Port);client.Send(request);bool rejected=false;try{client.Receive(new byte[512]);}catch(SocketException){rejected=true;}Check(rejected&&blocked.GetSnapshot().Dropped==1,"live CIDR ACL rejects disallowed LAN client");}
    }
    var serviceConfig=new Settings{Bind="127.0.0.1",Port=FreePort(),Servers=new[]{"127.0.0.1","127.0.0.2"},IntervalSeconds=60,HoldoverSeconds=120};Store.Write(Store.ConfigPath,serviceConfig);
    using(var service=new TimeService()){
     typeof(TimeService).GetMethod("OnStart",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(service,new object[]{new string[0]});string statusPath=Path.Combine(Store.DirectoryPath,"status.json");var wait=Stopwatch.StartNew();while(!File.Exists(statusPath)&&wait.ElapsedMilliseconds<2500)Thread.Sleep(30);
     Check(File.Exists(statusPath)&&Store.Read<Snapshot>(statusPath).Running,"Windows service lifecycle publishes status snapshot");
     typeof(TimeService).GetMethod("OnStop",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(service,null);Check(!Store.Read<Snapshot>(statusPath).Running,"Windows service lifecycle stops listener and persists final status");
    }
    Check(Math.Abs((DateTime.UtcNow-now).TotalSeconds)<30,"tests do not alter Windows clock");
    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    using(var form=new MainForm(true)){form.Show();Application.DoEvents();var choice=(ComboBox)typeof(MainForm).GetField("addressChoice",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);Check(choice.Items.Count==0||choice.SelectedIndex>=0&&!String.IsNullOrWhiteSpace(choice.Text),"UI address picker displays selected network interface");report.Add("Address picker text: "+choice.Text);form.CapturePage(2);Check(form.Text.Contains("LanTime"),"settings view renders without errors");var auto=(CheckBox)typeof(MainForm).GetField("autoSystemTime",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);var startup=(CheckBox)typeof(MainForm).GetField("startup",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);Check(auto.Bottom<startup.Top,"system-time setting and startup option do not overlap");Check(!auto.Checked,"preview does not enable system-time writes");form.AllowExit=true;form.Close();}
   }catch(Exception e){failed++;report.Add("FAIL unhandled: "+e);}
   report.Add("RESULT: "+passed+" passed, "+failed+" failed");report.Add("Runtime: "+Environment.Version+" / OS: "+Environment.OSVersion);report.Add("Test data: "+Store.DirectoryPath);
   string output=String.Join(Environment.NewLine,report.ToArray());if(path!=null)File.WriteAllText(Path.GetFullPath(path),output,Encoding.UTF8);Console.WriteLine(output);return failed==0?0:1;
  }
  public static void DumpView(Control root,string path){var lines=new List<string>();Action<Control,int> walk=null;walk=delegate(Control c,int depth){lines.Add(new string(' ',depth*2)+c.GetType().Name+" "+c.Bounds+" visible="+c.Visible+" dock="+c.Dock+" "+(c.Text??"").Split('\n')[0]);foreach(Control child in c.Controls)walk(child,depth+1);};walk(root,0);File.WriteAllLines(path,lines.ToArray());}
  static IEnumerable<Control> Descendants(Control root){foreach(Control child in root.Controls){yield return child;foreach(var next in Descendants(child))yield return next;}}
  public static void Capture(Control form,string path){using(var image=new System.Drawing.Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,form.Width,form.Height));if(form.Region!=null)using(var outside=new System.Drawing.Region(new System.Drawing.Rectangle(0,0,form.Width,form.Height)))using(var g=System.Drawing.Graphics.FromImage(image)){outside.Exclude(form.Region);g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;using(var clear=new System.Drawing.SolidBrush(System.Drawing.Color.Transparent))g.FillRegion(clear,outside);}image.Save(path);}}
  public static int UiCheck(string path){
   path=Path.GetFullPath(path);Directory.CreateDirectory(path);Store.DirectoryPath=Path.Combine(Path.GetTempPath(),"LanTime-ui-"+Guid.NewGuid().ToString("N"));Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);var lines=new List<string>();int errors=0;Action<bool,string> check=delegate(bool ok,string text){lines.Add((ok?"PASS ":"FAIL ")+text);if(!ok)errors++;};
   try{using(var form=new MainForm(true)){form.Show();Application.DoEvents();
    foreach(var size in new[]{new System.Drawing.Size(1000,700),new System.Drawing.Size(1136,792),new System.Drawing.Size(1320,900)}){
     form.ClientSize=size;form.CapturePage(2);Application.DoEvents();var inputs=Descendants(form).OfType<TextInput>().Where(c=>c.Visible).ToArray();var page=(Panel)typeof(MainForm).GetField("settingsPage",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
     check(inputs.Length==7,"seven settings inputs render at "+size);check(!page.HorizontalScroll.Visible,"settings avoid horizontal scrolling at "+size);
     foreach(var input in inputs){check(input.Editor.BorderStyle==BorderStyle.None&&input.Editor.Right<=input.Width-10&&input.Editor.Top>=8&&input.Editor.Bottom<=input.Height-8,"native editor fits styled input at "+size+": "+input.AccessibleName);}
     var caption=Descendants(form).OfType<FlatButton>().Where(b=>b.AccessibleName=="最小化"||b.AccessibleName=="最大化 / 还原"||b.AccessibleName=="关闭窗口").ToArray();check(caption.Length==3&&caption.All(b=>b.Bounds.Left>=size.Width-150&&b.Right<=size.Width&&b.Top>=6&&b.Bottom<=44),"window buttons stay centered in title area at "+size);check(form.FormBorderStyle==FormBorderStyle.None,"window uses unified frame at "+size);
     check(form.Region!=null&&!form.Region.IsVisible(0,0)&&!form.Region.IsVisible(form.Width-1,form.Height-1)&&form.Region.IsVisible(form.Width/2,0)&&form.Region.IsVisible(form.Width/2,form.Height-1),"window has true rounded corners and continuous edges at "+size);
     Capture(form,Path.Combine(path,"settings-"+size.Width+".png"));page.ScrollControlIntoView(Descendants(page).OfType<FlatButton>().First(b=>b.Text=="卸载后台服务"));Application.DoEvents();check(Descendants(page).OfType<FlatButton>().First(b=>b.Text=="卸载后台服务").RectangleToScreen(Descendants(page).OfType<FlatButton>().First(b=>b.Text=="卸载后台服务").ClientRectangle).Bottom<=page.RectangleToScreen(page.ClientRectangle).Bottom,"bottom settings actions reachable at "+size);Capture(form,Path.Combine(path,"settings-bottom-"+size.Width+".png"));page.AutoScrollPosition=System.Drawing.Point.Empty;
     form.CapturePage(0);Application.DoEvents();var table=Descendants(form).OfType<DataTable>().First(c=>c.Visible);table.FitColumns();check(table.Columns.Cast<ColumnHeader>().Sum(c=>c.Width)<=table.ClientSize.Width,"table columns fit without horizontal bar at "+size);Capture(form,Path.Combine(path,"overview-"+size.Width+".png"));
    }
    form.ClientSize=new System.Drawing.Size(1136,792);form.CapturePage(2);var host=(TextInput)typeof(MainForm).GetField("hosts",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);host.Lines=new[]{"ntp.example.com","中文输入验证"};check(host.Editor.Text.Contains("中文输入验证")&&host.Lines.Length==2,"styled multiline input retains Unicode and line breaks");host.Editor.SelectAll();check(host.Editor.SelectedText.Contains("ntp.example.com"),"native selection preserved inside styled input");host.Editor.SelectedText="time.example.com";check(host.Text=="time.example.com","native selected-text replacement retained");var toggle=(ToggleInput)typeof(MainForm).GetField("autoSystemTime",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);toggle.Checked=true;check(toggle.CheckState==CheckState.Checked&&toggle.AccessibilityObject.Role==AccessibleRole.CheckButton,"switch appearance preserves checkbox accessibility and state");Application.DoEvents();Capture(form,Path.Combine(path,"input-active.png"));
    form.CapturePage(4);Capture(form,Path.Combine(path,"about.png"));
    using(var popupTimer=new System.Windows.Forms.Timer{Interval=200}){popupTimer.Tick+=delegate{foreach(Form popup in Application.OpenForms)if(popup!=form&&popup.Text=="样式检查"){popupTimer.Stop();check(popup is ChromeForm&&popup.Region!=null,"message dialogs share rounded window style");Capture(popup,Path.Combine(path,"message.png"));popup.Close();break;}};popupTimer.Start();AppDialog.Show("配置已保存。\r\n本机自动校时将按多源校准周期运行。\r\n此窗口用于检查样式，没有保存或修改系统设置。","样式检查");}
    form.AllowExit=true;form.Close();
   }}catch(Exception e){errors++;lines.Add("FAIL "+e);}
   lines.Add("RESULT: "+lines.Count(s=>s.StartsWith("PASS "))+" passed, "+errors+" failed");lines.Add("No Windows clock, firewall or startup setting changed. Layout checks do not replace Win7 hardware QA.");File.WriteAllLines(Path.Combine(path,"ui-checks.txt"),lines.ToArray(),Encoding.UTF8);return errors==0?0:1;
  }
  public static int Probe(string host,int port){try{var s=Upstream.Query(host,new Clock(),port);string text=host+":"+port+"  stratum="+s.Stratum+"  offset="+s.OffsetMs.ToString("F3")+" ms  RTT="+s.DelayMs.ToString("F3")+" ms";Console.WriteLine(text);return 0;}catch(Exception e){Console.WriteLine(e.Message);return 1;}}
  public static int ExternalCheck(string path){var lines=new List<string>();var samples=new List<SourceState>();Clock clock=new Clock();foreach(string h in new Settings().Servers){try{var s=Upstream.Query(h,clock,123);samples.Add(s);lines.Add(h+" ["+s.Address+"] RTT "+s.DelayMs.ToString("F2")+" ms; offset "+s.OffsetMs.ToString("F2")+" ms; stratum "+s.Stratum);}catch(Exception e){lines.Add(h+" FAILED: "+e.Message);}}var group=Upstream.Consensus(samples,2);lines.Add("Independent agreeing sources: "+group.Count);File.WriteAllLines(path,lines.ToArray(),Encoding.UTF8);return group.Count>=2?0:1;}
  sealed class FakeSystemClock:ISystemClock {
   readonly Stopwatch watch=Stopwatch.StartNew();DateTime origin=DateTime.UtcNow;public int Writes,Error;public bool IgnoreWrite;
   public DateTime UtcNow {get{return origin.AddTicks(watch.Elapsed.Ticks);}}
   public void SetUtc(Func<DateTime> utcNow){if(Error!=0)throw new System.ComponentModel.Win32Exception(Error);Writes++;if(!IgnoreWrite)origin=utcNow().AddTicks(-watch.Elapsed.Ticks);}
  }
  sealed class Mock:IDisposable{
   readonly Socket socket;readonly Thread thread;readonly double offset;volatile bool stopped;
   public Mock(string ip,int port,double correction){offset=correction;socket=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);socket.Bind(new IPEndPoint(IPAddress.Parse(ip),port));thread=new Thread(Loop){IsBackground=true};thread.Start();}
   public static byte[] Reply(byte[] request,DateTime time,int stratum){byte[] r=new byte[48];r[0]=0x24;r[1]=(byte)stratum;r[2]=6;r[3]=unchecked((byte)-6);Wire.PutFixed(r,8,.001);r[12]=71;r[13]=80;r[14]=83;Wire.PutTime(r,16,time.AddSeconds(-60));Array.Copy(request,40,r,24,8);Wire.PutTime(r,32,time);Wire.PutTime(r,40,time);return r;}
   void Loop(){while(!stopped)try{byte[] q=new byte[512];EndPoint remote=new IPEndPoint(IPAddress.Any,0);int n=socket.ReceiveFrom(q,ref remote);if(n>=48)socket.SendTo(Reply(q,DateTime.UtcNow.AddSeconds(offset),3),remote);}catch{if(stopped)return;}}
   public void Dispose(){stopped=true;socket.Close();thread.Join(2000);}
  }
 }
}

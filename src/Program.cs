using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("LanTime 局域网时间站")]
[assembly: AssemblyDescription("多源校准 · 局域网 NTP/SNTP 服务端")]
[assembly: AssemblyCompany("chengkai")]
[assembly: AssemblyCopyright("Copyright (c) 2026 chengkai")]
[assembly: AssemblyVersion("1.0.4.0")]
namespace LanTime {
 public static class Program {
  public const string ServiceName="LanTimeNtp";
  public static string Exe {get{return Assembly.GetExecutingAssembly().Location;}}
  public static bool IsAdministrator {get{using(var identity=WindowsIdentity.GetCurrent())return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);}}
  [STAThread] public static int Main(string[] args) {
   if(args.Length>0 && args[0]=="--self-test") return Tests.Run(args.Length>1?args[1]:null);
   if(args.Length==2&&(args[0]=="--installer-stop"||args[0]=="--installer-uninstall"))return InstallerServiceAction(args[0],args[1]);
   if(args.Length==2&&args[0]=="--ui-check")return Tests.UiCheck(args[1]);
   if(args.Length>0 && args[0]=="--probe") return Tests.Probe(args.Length>1?args[1]:"127.0.0.1",args.Length>2?Int32.Parse(args[2]):123);
   if(args.Length>1 && args[0]=="--upstream-check") return Tests.ExternalCheck(args[1]);
   if(args.Length>0 && args[0]=="--service") { ServiceBase.Run(new TimeService());return 0; }
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   if(args.Length>0 && args[0].StartsWith("--admin-")) {
    try { Admin(args[0]);return 0; } catch(Exception e) {AppDialog.Show(e.Message,"操作失败",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
   }
   if(args.Length==1&&args[0]=="--preview"){Application.Run(new MainForm(true));return 0;}
   bool render=args.Length>1&&(args[0]=="--render"||args[0]=="--render-live");
   if(args.Length==2&&args[0]=="--wait-for-exit") {
    int parent;if(!Int32.TryParse(args[1],out parent)||parent<=0)return 1;
    try{using(var process=Process.GetProcessById(parent))if(!process.WaitForExit(30000)){AppDialog.Show("旧界面尚未退出，请退出后重新启动。","LanTime");return 1;}}catch(ArgumentException){}
   }
   if(render) { using(var f=new MainForm(args[0]=="--render")) {f.Show();Application.DoEvents();if(args.Length>2)f.CapturePage(Int32.Parse(args[2]));if(args[0]=="--render-live"){var timer=Stopwatch.StartNew();while(timer.Elapsed.TotalSeconds<10){Application.DoEvents();Thread.Sleep(30);}}Tests.DumpView(f,Path.GetFullPath(args[1])+".layout.txt");Tests.Capture(f,Path.GetFullPath(args[1]));f.AllowExit=true;f.Close();}return 0; }
   try {
    if((Store.InstalledLayout||Store.Load().AutoSetSystemTime&&!Installed())&&!IsAdministrator){Elevate("--wait-for-exit "+Process.GetCurrentProcess().Id);return 0;}
   }catch(System.ComponentModel.Win32Exception e){AppDialog.Show(e.NativeErrorCode==1223?"已取消管理员授权。本次继续提供局域网授时，本机自动校时需要管理员权限。":e.Message,"本机校时权限");}
   catch(Exception e){Store.Log("启动权限检查："+e.Message);}
   using(var mutex=new Mutex(false,"Local\\LanTimeDesktop-"+Exe.ToLowerInvariant().GetHashCode())) {
    if(!mutex.WaitOne(0,false)) {AppDialog.Show("LanTime 已在运行，请查看任务栏或系统托盘。","LanTime");return 0;}
    try {
     Application.ThreadException+=delegate(object s,ThreadExceptionEventArgs e){Store.Log(e.Exception.ToString());AppDialog.Show(e.Exception.Message,"LanTime",MessageBoxButtons.OK,MessageBoxIcon.Error);};
     Application.Run(new MainForm(false));
    } catch(Exception e){AppDialog.Show("启动失败："+e.Message+"\n请将压缩包完整解压到可写目录后运行。","LanTime",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
    finally{mutex.ReleaseMutex();}
   }
   return 0;
  }
  public static bool Installed() {try {using(var s=new ServiceController(ServiceName)){return s.ServiceName==ServiceName&&s.Status>=ServiceControllerStatus.Stopped;}}catch{return false;}}
  public static bool ServiceRunning() {try{using(var s=new ServiceController(ServiceName))return s.Status==ServiceControllerStatus.Running;}catch{return false;}}
  public static void Elevate(string command) {
   var p=Process.Start(new ProcessStartInfo(Exe,command){UseShellExecute=true,Verb="runas"});if(p!=null)p.Dispose();
  }
  static void RunTool(string filename,string arguments) {
   var info=new ProcessStartInfo(filename,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   using(var p=Process.Start(info)) {string output=p.StandardOutput.ReadToEnd()+p.StandardError.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new Exception(filename+" 返回 "+p.ExitCode+"\n"+output);}
  }
  internal static string ServiceExecutable(string imagePath){
   string value=Environment.ExpandEnvironmentVariables(imagePath??"").Trim();if(value.StartsWith("\"")){int end=value.IndexOf('"',1);if(end<0)throw new ArgumentException("服务路径无效。");return Path.GetFullPath(value.Substring(1,end-1));}
   int exe=value.IndexOf(".exe",StringComparison.OrdinalIgnoreCase);if(exe<0)throw new ArgumentException("服务路径无效。");return Path.GetFullPath(value.Substring(0,exe+4));
  }
  static int InstallerServiceAction(string action,string target){
   try{
    if(!IsAdministrator)return 5;target=Path.GetFullPath(target);if(!String.Equals(Path.GetFileName(target),"LanTime.exe",StringComparison.OrdinalIgnoreCase))return 87;
    string image;using(var key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\"+ServiceName)){if(key==null)return 0;image=Convert.ToString(key.GetValue("ImagePath"));}
    if(!String.Equals(ServiceExecutable(image),target,StringComparison.OrdinalIgnoreCase)){Store.Log("安装操作未触碰其他目录的 LanTime 服务。");return action=="--installer-stop"?183:0;}
    using(var service=new ServiceController(ServiceName)){if(service.Status!=ServiceControllerStatus.Stopped){service.Stop();service.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(30));}}
    if(action=="--installer-uninstall")RunTool(Path.Combine(Environment.SystemDirectory,"sc.exe"),"delete "+ServiceName);
    return 0;
   }catch(Exception e){Store.Log("安装维护失败："+e.Message);return 1;}
  }
  static void Admin(string cmd) {
   if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))throw new Exception("此操作需要管理员权限。");
   if(cmd=="--admin-install") {
    if(Installed())throw new Exception("服务已安装，请先卸载旧服务。");
    var c=Store.Load();Store.Write(Store.ConfigPath,c);
    string bin="\\\""+Exe+"\\\" --service";
    RunTool("sc.exe","create "+ServiceName+" binPath= \""+bin+"\" start= auto DisplayName= \"LanTime 局域网时间站\"");
    RunTool("sc.exe","description "+ServiceName+" \"多源校准的局域网 NTP 服务。配置位于程序目录 data。\"");
    RunTool("sc.exe","failure "+ServiceName+" reset= 86400 actions= restart/5000/restart/15000/restart/60000");
    // Installation deliberately leaves the service stopped; the desktop exits its listener first.
    AppDialog.Show("后台服务已安装。\n界面将自动切换到后台服务模式，然后可点击“启动服务”。\n服务将在下次开机时自动启动，请勿移动程序目录。","LanTime");
   } else if(cmd=="--admin-uninstall") {
    using(var s=new ServiceController(ServiceName)){if(s.Status!=ServiceControllerStatus.Stopped){s.Stop();s.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(25));}}
    RunTool("sc.exe","delete "+ServiceName);AppDialog.Show("后台服务已卸载。重新打开 LanTime 后可使用便携模式。","LanTime");
   } else if(cmd=="--admin-start") {
    using(var s=new ServiceController(ServiceName)){if(s.Status==ServiceControllerStatus.Stopped){s.Start();s.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(20));}}
   } else if(cmd=="--admin-stop") {
    using(var s=new ServiceController(ServiceName)){if(s.Status!=ServiceControllerStatus.Stopped){s.Stop();s.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(25));}}
   } else if(cmd=="--admin-sync") {using(var s=new ServiceController(ServiceName))s.ExecuteCommand(128);}
   else if(cmd=="--admin-firewall") {
    var c=Store.Load();
    RunTool("netsh.exe","advfirewall firewall add rule name=\"LanTime NTP UDP\" dir=in action=allow protocol=UDP localport="+c.Port+" remoteip="+String.Join(",",c.Networks)+" profile=private,domain program=\""+Exe+"\"");
    AppDialog.Show("已添加专用网络 / 域网络的 UDP 入站规则。\n访问来源仍受软件中允许网段限制。","LanTime");
   } else if(cmd=="--admin-remove-firewall") {
    RunTool("netsh.exe","advfirewall firewall delete rule name=\"LanTime NTP UDP\" program=\""+Exe+"\"");AppDialog.Show("已删除 LanTime 防火墙规则。","LanTime");
   } else throw new Exception("未知操作。");
  }
 }
 public sealed class TimeService : ServiceBase {
  Engine engine;System.Threading.Timer status;
  public TimeService(){ServiceName=Program.ServiceName;CanStop=true;CanHandlePowerEvent=true;AutoLog=true;}
  protected override void OnStart(string[] args) {
   try {engine=new Engine(Store.Load());engine.Start();status=new System.Threading.Timer(delegate{try{Store.Write(System.IO.Path.Combine(Store.DirectoryPath,"status.json"),engine.GetSnapshot());}catch(Exception e){Store.Log("状态文件写入失败："+e.Message);}},null,0,2000);}
   catch(Exception e){Store.Log("后台服务启动失败："+e.Message);throw;}
  }
  protected override void OnCustomCommand(int command){if(command==128&&engine!=null)engine.RequestSync();}
  protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus){if(engine!=null&&(powerStatus==PowerBroadcastStatus.ResumeAutomatic||powerStatus==PowerBroadcastStatus.ResumeSuspend||powerStatus==PowerBroadcastStatus.ResumeCritical))engine.Invalidate();return true;}
  protected override void OnStop(){if(status!=null){using(var done=new ManualResetEvent(false)){status.Dispose(done);done.WaitOne(5000);}}if(engine!=null){engine.Stop();try{Store.Write(Path.Combine(Store.DirectoryPath,"status.json"),engine.GetSnapshot());}catch{}}}
 }
}

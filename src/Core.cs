using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LanTime {
 [DataContract] public sealed class Settings {
  [DataMember] public string Bind = "0.0.0.0";
  [DataMember] public int Port = 123;
  [DataMember] public int IntervalSeconds = 300;
  [DataMember] public int HoldoverSeconds = 3600;
  [DataMember] public int Quorum = 2;
  [DataMember] public bool AutoSetSystemTime;
  [DataMember] public string[] Servers = { "ntp.aliyun.com", "ntp.tencent.com", "time.cloudflare.com", "time.windows.com" };
  [DataMember] public string[] Networks = { "127.0.0.0/8", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "169.254.0.0/16" };
  public void Validate() {
   IPAddress ip;
   if (!IPAddress.TryParse(Bind, out ip) || ip.AddressFamily != AddressFamily.InterNetwork) throw new Exception("监听地址必须是 IPv4 地址。");
   if (Port < 1 || Port > 65535) throw new Exception("端口范围为 1—65535。");
   if (IntervalSeconds < 60 || IntervalSeconds > 86400) throw new Exception("自动校时间隔应为 60—86400 秒。");
   if (HoldoverSeconds < IntervalSeconds * 2 || HoldoverSeconds > 86400) throw new Exception("保持时限应至少为校时间隔的两倍，且不超过 86400 秒。");
   if (Servers == null || Servers.Length < 2 || Servers.Length > 12 || Quorum < 2 || Quorum > Servers.Length) throw new Exception("请配置 2—12 个时间源；一致来源数至少为 2，且不能超过时间源总数。");
   var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach (string s in Servers) {
    if (String.IsNullOrWhiteSpace(s) || Uri.CheckHostName(s) == UriHostNameType.Unknown || s.Length > 253 || !seen.Add(s)) throw new Exception("时间源须为不同的有效域名或 IP，不带端口：" + s);
    if (s.IndexOf(':') >= 0) throw new Exception("目前支持 IPv4 时间源。");
   }
   if (Networks == null || Networks.Length == 0 || Networks.Length > 32) throw new Exception("请至少保留一个允许访问的 IPv4 网段。");
   foreach (string s in Networks) { new Cidr(s); }
  }
 }
 public static class Store {
  public static bool InstalledLayout {get{return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"installed.mode"));}}
  internal static string DataDirectory(string basePath,string commonData,bool installed){return Path.Combine(installed?commonData:basePath,installed?"LanTime":"data");}
  public static string DirectoryPath = DataDirectory(AppDomain.CurrentDomain.BaseDirectory,Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),InstalledLayout);
  public static string ConfigPath { get { return Path.Combine(DirectoryPath, "settings.json"); } }
  public static T Read<T>(string path) { using (var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(f); }
  public static void Write<T>(string path, T data) {
   Directory.CreateDirectory(Path.GetDirectoryName(path));
   string temp = path + ".tmp";
   using (var f = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) new DataContractJsonSerializer(typeof(T)).WriteObject(f, data);
   if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
  }
  public static Settings Load() {
   Settings c = File.Exists(ConfigPath) ? Read<Settings>(ConfigPath) : new Settings(); c.Validate(); return c;
  }
  static readonly object LogLock = new object();
  public static void Log(string message) {
   lock (LogLock) try {
    Directory.CreateDirectory(DirectoryPath);
    string p = Path.Combine(DirectoryPath, "lantime.log");
    if (File.Exists(p) && new FileInfo(p).Length > 2 * 1024 * 1024) { string old = p + ".1"; if (File.Exists(old)) File.Delete(old); File.Move(p, old); }
    using (var f = new FileStream(p, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) using (var w = new StreamWriter(f, Encoding.UTF8)) w.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message);
   } catch { /* Logging must never stop the time server. */ }
  }
  public static string Tail() {
   try {
    using (var f = new FileStream(Path.Combine(DirectoryPath, "lantime.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
     if (f.Length > 40000) f.Seek(-40000, SeekOrigin.End);
     using (var r = new StreamReader(f, Encoding.UTF8)) { string s = r.ReadToEnd(); return String.Join(Environment.NewLine, s.Split(new[]{'\n'}).Reverse().Take(180).ToArray()); }
    }
   } catch { return "暂无运行日志。"; }
  }
 }
 public sealed class Cidr {
  readonly uint network, mask;
  public Cidr(string value) {
   string[] a = value.Trim().Split('/'); IPAddress ip; int bits;
   if (a.Length != 2 || !IPAddress.TryParse(a[0], out ip) || ip.AddressFamily != AddressFamily.InterNetwork || !Int32.TryParse(a[1], out bits) || bits < 0 || bits > 32) throw new Exception("网段格式错误：" + value + "，例如 192.168.1.0/24。");
   mask = bits == 0 ? 0 : UInt32.MaxValue << (32 - bits); network = Number(ip) & mask;
  }
  static uint Number(IPAddress ip) { byte[] a = ip.GetAddressBytes(); return (uint)a[0] << 24 | (uint)a[1] << 16 | (uint)a[2] << 8 | a[3]; }
  public bool Contains(IPAddress ip) { return ip.AddressFamily == AddressFamily.InterNetwork && (Number(ip) & mask) == network; }
 }
 public static class Wire {
  public static readonly DateTime Epoch = new DateTime(1900,1,1,0,0,0,DateTimeKind.Utc);
  public static uint U32(byte[] b, int o) { return (uint)b[o]<<24 | (uint)b[o+1]<<16 | (uint)b[o+2]<<8 | b[o+3]; }
  public static void Put32(byte[] b, int o, uint x) { b[o]=(byte)(x>>24); b[o+1]=(byte)(x>>16); b[o+2]=(byte)(x>>8); b[o+3]=(byte)x; }
  public static void PutTime(byte[] b, int o, DateTime time) {
   long ticks = (time.ToUniversalTime()-Epoch).Ticks; long seconds = ticks / TimeSpan.TicksPerSecond; long fraction = ticks % TimeSpan.TicksPerSecond;
   if (fraction < 0) { seconds--; fraction += TimeSpan.TicksPerSecond; }
   Put32(b,o,unchecked((uint)seconds)); Put32(b,o+4,(uint)((ulong)fraction * 4294967296UL / 10000000UL));
  }
  public static DateTime Time(byte[] b, int o, DateTime pivot) {
   // Unfold the 2036 NTP era against the local monotonic-clock date.
   long seconds = U32(b,o); long near = (long)(pivot-Epoch).TotalSeconds;
   long era = (long)Math.Round((near-seconds)/4294967296.0);
   long ticks = (seconds+era*4294967296L)*10000000L + (long)((ulong)U32(b,o+4)*10000000UL/4294967296UL);
   return Epoch.AddTicks(ticks);
  }
  public static bool Zero(byte[] b,int o) { for(int i=o;i<o+8;i++) if(b[i]!=0) return false; return true; }
  public static double Fixed(byte[] b,int o,bool signed) { uint x=U32(b,o); return (signed ? (double)unchecked((int)x) : x)/65536.0; }
  public static void PutFixed(byte[] b,int o,double x) { Put32(b,o,(uint)Math.Min(UInt32.MaxValue,Math.Max(0,x*65536.0))); }
 }
 public sealed class Clock {
  readonly object gate = new object();
  DateTime origin = DateTime.UtcNow;
  readonly Stopwatch watch = Stopwatch.StartNew();
  double applied, target, evaluated;
  public double Seconds { get { return watch.Elapsed.TotalSeconds; } }
  public DateTime UtcNow { get { lock(gate) { double s=Seconds; double allowance=Math.Max(0,s-evaluated)*0.001; applied += Math.Max(-allowance,Math.Min(allowance,target-applied)); evaluated=s; return origin.AddSeconds(s+applied); } } }
  public double Pending { get { lock(gate) return target-applied; } }
  public DateTime EstimatedUtcNow { get { lock(gate) { var now=UtcNow;return now.AddSeconds(target-applied); } } }
  public void Correct(double offset,bool first) {
   lock(gate) {
    DateTime now=UtcNow;
    if(first) { origin=now.AddSeconds(offset-Seconds); applied=target=0; evaluated=Seconds; }
    else target=applied+offset;
   }
  }
 }
 [DataContract] public sealed class SourceState {
  [DataMember] public string Host, Address, Status;
  [DataMember] public double OffsetMs, DelayMs, ErrorMs;
  [DataMember] public int Stratum;
  [DataMember] public bool Selected;
  public double Offset { get { return OffsetMs/1000; } }
  public int Leap;
  public double RootDelay, RootDispersion;
 }
 [DataContract] public sealed class ClientState {
  [DataMember] public string Address, LastSeen;
  [DataMember] public long Requests;
 }
 [DataContract] public sealed class Snapshot {
  [DataMember] public bool Running, Syncing, Trusted;
  [DataMember] public string State, Detail, Address, LastSync, SelectedSource;
  [DataMember] public double UtcTicks, Age, SystemOffsetMs, PendingMs, UncertaintyMs;
  [DataMember] public int Stratum, Agreeing;
  [DataMember] public long Requests, Dropped;
  [DataMember] public SourceState[] Sources;
  [DataMember] public ClientState[] Clients;
  [DataMember] public bool AutoSetSystemTime, SystemTimeNeedsAdmin;
  [DataMember] public string SystemTimeState, SystemTimeDetail, LastSystemSync;
  [DataMember] public double LastSystemCorrectionMs;
 }
 public static class Upstream {
  public static SourceState Query(string host,Clock clock,int port) {
   IPAddress literal; IPAddress[] addresses;
   if(IPAddress.TryParse(host,out literal)) addresses=new[]{literal};
   else {
    IAsyncResult pending=Dns.BeginGetHostAddresses(host,null,null);
    using(var ready=pending.AsyncWaitHandle) { if(!ready.WaitOne(2500)) throw new Exception("DNS 解析超时"); }
    addresses=Dns.EndGetHostAddresses(pending).Where(x=>x.AddressFamily==AddressFamily.InterNetwork).ToArray();
   }
   if(addresses.Length==0) throw new Exception("没有可用的 IPv4 地址");
   SourceState best=null; Exception last=null;
   for(int attempt=0;attempt<2;attempt++) {
    try { var sample=Probe(host,addresses[attempt%addresses.Length],clock,port); if(best==null||sample.DelayMs<best.DelayMs) best=sample; }
    catch(Exception e) { last=e; }
    if(attempt==0) Thread.Sleep(250);
   }
   if(best==null) throw last ?? new Exception("未收到有效响应"); return best;
  }
  public static SourceState Probe(string host,IPAddress ip,Clock clock,int port) {
   using(var socket=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp)) {
    socket.ReceiveTimeout=1800; socket.SendTimeout=1800; socket.Connect(new IPEndPoint(ip,port));
    byte[] request=new byte[48]; request[0]=0x23; DateTime t1=clock.UtcNow; Wire.PutTime(request,40,t1);
    socket.Send(request); byte[] reply=new byte[1024]; int n=socket.Receive(reply); DateTime t4=clock.UtcNow;
    return Validate(host,ip,request,reply,n,t1,t4);
   }
  }
  public static SourceState Validate(string host,IPAddress ip,byte[] request,byte[] p,int length,DateTime t1,DateTime t4) {
   if(length<48 || (p[0]&7)!=4 || ((p[0]>>3)&7)<3 || ((p[0]>>3)&7)>4) throw new Exception("无效 NTP 服务端报文");
   if((p[0]>>6)==3) throw new Exception("上游未同步");
   if(p[1]==0) throw new Exception("上游限流/拒绝："+Encoding.ASCII.GetString(p,12,4));
   if(p[1]>14) throw new Exception("上游层级不可用");
   for(int i=0;i<8;i++) if(p[24+i]!=request[40+i]) throw new Exception("响应时间戳不匹配");
   if(Wire.Zero(p,32)||Wire.Zero(p,40)||Wire.Zero(p,16)) throw new Exception("缺少有效时间戳");
   DateTime t2=Wire.Time(p,32,t1),t3=Wire.Time(p,40,t1),reference=Wire.Time(p,16,t1);
   double processing=(t3-t2).TotalSeconds,delay=(t4-t1).TotalSeconds-processing;
   if(processing<0 || processing>2 || delay<-.005 || delay>2 || reference>t3.AddSeconds(1)) throw new Exception("往返延迟或时间戳异常");
   double rd=Wire.Fixed(p,4,true),disp=Wire.Fixed(p,8,false);
   if(Math.Abs(rd)>4||disp>2) throw new Exception("上游时间误差过大");
   return new SourceState { Host=host, Address=ip.ToString(),Status="待比对",OffsetMs=((t2-t1).TotalMilliseconds+(t3-t4).TotalMilliseconds)/2,
    DelayMs=Math.Max(0,delay)*1000, ErrorMs=(Math.Max(0,delay)/2+Math.Max(0,rd)/2+disp+.016)*1000,Stratum=p[1],Leap=p[0]>>6,RootDelay=rd,RootDispersion=disp };
  }
  public static List<SourceState> Consensus(List<SourceState> all,int quorum) {
   var unique=all.Where(x=>x.Address!=null).GroupBy(x=>x.Address).Select(g=>g.OrderBy(x=>x.DelayMs).First()).OrderBy(x=>x.Offset).ToList();
   List<SourceState> best=new List<SourceState>();
   for(int i=0;i<unique.Count;i++) {
    var group=unique.Skip(i).TakeWhile(x=>x.Offset-unique[i].Offset<=.100).ToList();
    // Leap indicators must agree before advertising an impending leap second.
    group=group.GroupBy(x=>x.Leap).OrderByDescending(g=>g.Count()).First().ToList();
    if(group.Count>best.Count) best=group;
   }
   if(best.Count<quorum || best.Count<=unique.Count/2) return new List<SourceState>();
   return best;
  }
 }
 public sealed class Engine : IDisposable {
  readonly object gate=new object(); readonly Settings config; readonly Cidr[] networks; readonly int upstreamPort;
  public readonly Clock Clock=new Clock();
  Socket listener; Thread receiver,worker; readonly AutoResetEvent wake=new AutoResetEvent(false); readonly ManualResetEvent stop=new ManualResetEvent(false);
  volatile bool running,syncing; bool everSynced; double lastSync=-1,lastAttempt=-100; int generation,trustEpoch;
  readonly LocalTimeSync localTime;
  double rootDelay,rootDispersion; DateTime reference; int stratum=16,leap=3,agreeing;
  string selected="—",detail="尚未取得多源一致时间"; long requests,dropped;
  List<SourceState> sources; readonly Dictionary<string,ClientRecord> clients=new Dictionary<string,ClientRecord>();
  readonly Dictionary<string,Rate> rates=new Dictionary<string,Rate>(); double globalStart; int globalCount;
  class Rate { public double Start; public int Count; }
  class ClientRecord { public string Address; public DateTime Last; public long Count; }
  public Engine(Settings c) : this(c,123) { }
  internal Engine(Settings c,int sourcePort) : this(c,sourcePort,new WindowsSystemClock()) { }
  internal Engine(Settings c,int sourcePort,ISystemClock systemClock) { c.Validate();config=c;upstreamPort=sourcePort;localTime=new LocalTimeSync(c.AutoSetSystemTime,systemClock);networks=c.Networks.Select(x=>new Cidr(x)).ToArray();sources=c.Servers.Select(x=>new SourceState{Host=x,Status="等待查询"}).ToList(); }
  public void Start() {
   lock(gate) {
    if(running)return;
    var s=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);
    s.ExclusiveAddressUse=true;
    try { s.Bind(new IPEndPoint(IPAddress.Parse(config.Bind),config.Port)); } catch { s.Close(); throw; }
    listener=s;running=true;stop.Reset();generation++;
    receiver=new Thread(ReceiveLoop){IsBackground=true,Name="NTP UDP"};worker=new Thread(SyncLoop){IsBackground=true,Name="NTP upstream"};
    receiver.Start(s);worker.Start(generation);
   }
   Store.Log("服务启动，监听 "+config.Bind+":"+config.Port+" / UDP；允许网段 "+String.Join(", ",config.Networks));
  }
  public void Stop() {
   Thread a,b;
   lock(gate) { if(!running)return;running=false;generation++;stop.Set();wake.Set();if(listener!=null)listener.Close();a=receiver;b=worker; }
   if(a!=null)a.Join(2500);if(b!=null)b.Join(12000);
   lock(gate) syncing=false;
   Store.Log("服务已停止。");
  }
  public void Invalidate() { lock(gate) { trustEpoch++;lastSync=-1;lastAttempt=-100;everSynced=false;detail="休眠恢复后重新校时";localTime.Invalidate(config.AutoSetSystemTime); } RequestSync(); }
  public void RequestSync() { wake.Set(); }
  void SyncLoop(object token) {
   int id=(int)token;
   do {
    if(!running||id!=generation)return;
    if(Clock.Seconds-lastAttempt>=30) Synchronize(id);
    else Store.Log("手动校时过于频繁，30 秒内沿用当前结果。");
   } while(!stop.WaitOne(0) && WaitNext());
  }
  bool WaitNext() { return WaitHandle.WaitAny(new WaitHandle[]{stop,wake},config.IntervalSeconds*1000)!=0; }
  void Synchronize(int id) {
   int epoch;lock(gate){syncing=true;lastAttempt=Clock.Seconds;epoch=trustEpoch;}
   try {
    var jobs=config.Servers.Select(host=>Task.Factory.StartNew(()=>{
     try { return Upstream.Query(host,Clock,upstreamPort); }
     catch(Exception e) { return new SourceState{Host=host,Status=e is SocketException?"网络不可达 / 超时":e.Message}; }
    })).ToArray();
    Task.WaitAll(jobs); var results=jobs.Select(x=>x.Result).ToList();var group=Upstream.Consensus(results,config.Quorum);
    lock(gate) {
     if(!running||id!=generation||epoch!=trustEpoch)return;
     sources=results;
     foreach(var s in results) if(s.Address!=null) s.Status="偏差异常 / 无多数一致";
     if(group.Count==0) { detail="有效独立来源不足或时间分歧；保留上次校准";localTime.NoConsensus(config.AutoSetSystemTime);Store.Log("本轮未校准："+detail);return; }
     double[] offsets=group.Select(x=>x.Offset).OrderBy(x=>x).ToArray();
     double correction=(offsets[(offsets.Length-1)/2]+offsets[offsets.Length/2])/2;
     // Median resists one noisy source; best RTT selects reference/stratum metadata.
     var chosen=group.OrderBy(x=>x.DelayMs).First();
     foreach(var s in group) {s.Selected=true;s.Status="参与校准";}
     bool first=!everSynced || lastSync<0 || Clock.Seconds-lastSync>config.HoldoverSeconds;
     Clock.Correct(correction,first);everSynced=true;lastSync=Clock.Seconds;reference=Clock.UtcNow;
     rootDelay=Math.Max(0,chosen.RootDelay)+chosen.DelayMs/1000;
     rootDispersion=group.Max(x=>x.ErrorMs)/1000+group.Max(x=>Math.Abs(x.Offset-correction));
     selected=chosen.Address;stratum=chosen.Stratum+1;leap=chosen.Leap;agreeing=group.Count;
     detail=group.Count+" 个独立来源一致；"+(first?"已建立校准时钟":"正在平滑修正时钟");
     Store.Log("多源校准成功："+group.Count+" 个来源，偏差 "+(correction*1000).ToString("F3")+" ms；参考 "+chosen.Host);
     localTime.Apply(config.AutoSetSystemTime,true,Clock);
    }
   } catch(Exception e) { Store.Log("校时失败："+e.Message); }
   finally { lock(gate) syncing=false; }
  }
  bool Trusted { get { return lastSync>=0 && Clock.Seconds-lastSync<=config.HoldoverSeconds && Math.Abs(Clock.Pending)<.100; } }
  double Dispersion { get { return rootDispersion+(lastSync<0?16:Math.Max(0,Clock.Seconds-lastSync)*.0001)+Math.Abs(Clock.Pending); } }
  public Snapshot GetSnapshot() {
   lock(gate) {
    DateTime now=Clock.UtcNow;double age=lastSync<0?-1:Math.Max(0,Clock.Seconds-lastSync);bool valid=Trusted;
    string state=!running?"服务已停止":!valid?(lastSync<0?"等待首次校准":Math.Abs(Clock.Pending)>=.100?"时钟收敛中":"校准已过期"):age>config.IntervalSeconds*2?"离线保持":"已同步";
    return new Snapshot {Running=running,Syncing=syncing,Trusted=valid,State=state,Detail=detail,Address=config.Bind+":"+config.Port,
     LastSync=lastSync<0?"—":reference.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),SelectedSource=selected,UtcTicks=now.Ticks,
     Age=age,SystemOffsetMs=(now-DateTime.UtcNow).TotalMilliseconds,PendingMs=Clock.Pending*1000,UncertaintyMs=Dispersion*1000,
     Stratum=valid?stratum:16,Agreeing=agreeing,Requests=requests,Dropped=Interlocked.Read(ref dropped),Sources=sources.ToArray(),
     Clients=clients.Values.OrderByDescending(x=>x.Last).Take(200).Select(x=>new ClientState{Address=x.Address,LastSeen=x.Last.ToLocalTime().ToString("MM-dd HH:mm:ss"),Requests=x.Count}).ToArray(),
     AutoSetSystemTime=config.AutoSetSystemTime,SystemTimeState=localTime.State,SystemTimeDetail=localTime.Detail,LastSystemSync=localTime.LastSuccess,SystemTimeNeedsAdmin=localTime.NeedsAdmin,LastSystemCorrectionMs=localTime.LastCorrectionMs};
   }
  }
  bool Permit(IPAddress ip) {
   if(!networks.Any(n=>n.Contains(ip)))return false;
   double now=Clock.Seconds;
   if(now-globalStart>=1){globalStart=now;globalCount=0;} if(++globalCount>500)return false;
   string key=ip.ToString();Rate rate;
   if(!rates.TryGetValue(key,out rate)) { if(rates.Count>=4096)rates.Clear();rates[key]=rate=new Rate{Start=now}; }
   if(now-rate.Start>=1){rate.Start=now;rate.Count=0;} return ++rate.Count<=10;
  }
  void ReceiveLoop(object state) {
   var socket=(Socket)state; byte[] buffer=new byte[1024];
   while(running) try {
    EndPoint from=new IPEndPoint(IPAddress.Any,0);int n=socket.ReceiveFrom(buffer,ref from);var endpoint=(IPEndPoint)from;
    if(!Permit(endpoint.Address) || n<48 || (buffer[0]&7)!=3 || ((buffer[0]>>3)&7)<3 || ((buffer[0]>>3)&7)>4 || Wire.Zero(buffer,40)) {Interlocked.Increment(ref dropped);continue;}
    byte[] response;lock(gate)response=BuildReply(buffer,Clock.UtcNow);
    socket.SendTo(response,from);
    lock(gate) {
     requests++;string key=endpoint.Address.ToString();ClientRecord record;
     if(!clients.TryGetValue(key,out record)) {if(clients.Count>=1024)clients.Remove(clients.OrderBy(x=>x.Value.Last).First().Key);clients[key]=record=new ClientRecord{Address=key};}
     record.Last=Clock.UtcNow;record.Count++;
    }
   } catch(SocketException e) { if(!running)return; if(e.SocketErrorCode!=SocketError.MessageSize && e.SocketErrorCode!=SocketError.ConnectionReset) {Store.Log("UDP 接收错误："+e.SocketErrorCode);Thread.Sleep(100);} }
     catch(ObjectDisposedException){return;} catch(Exception e){if(running){Store.Log("请求处理错误："+e.Message);Thread.Sleep(100);}}
  }
  public byte[] BuildReply(byte[] request,DateTime received) {
   lock(gate) {
    bool valid=Trusted;byte[] p=new byte[48];p[0]=(byte)(((valid?leap:3)<<6)|(request[0]&0x38)|4);p[1]=(byte)(valid?stratum:16);
    p[2]=(byte)Math.Max(6,Math.Min(17,(int)unchecked((sbyte)request[2])));p[3]=unchecked((byte)-6);
    Wire.PutFixed(p,4,valid?rootDelay:0);Wire.PutFixed(p,8,valid?Dispersion:16);
    IPAddress ip;if(valid&&IPAddress.TryParse(selected,out ip))Array.Copy(ip.GetAddressBytes(),0,p,12,4);else Array.Copy(Encoding.ASCII.GetBytes("INIT"),0,p,12,4);
    if(lastSync>=0)Wire.PutTime(p,16,reference);
    Array.Copy(request,40,p,24,8);Wire.PutTime(p,32,received);Wire.PutTime(p,40,Clock.UtcNow);return p;
   }
  }
  public void Dispose() { Stop(); /* Wait handles live until process exit: timed-out DNS tasks may still finish. */ }
  public static KeyValuePair<string,string>[] LocalInterfaces() {
   try {return NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback)
    .SelectMany(n=>n.GetIPProperties().UnicastAddresses.Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork)
     .Select(a=>new {IP=a.Address.ToString(),Name=n.Name,Score=(n.GetIPProperties().GatewayAddresses.Any(g=>g.Address.AddressFamily==AddressFamily.InterNetwork&&g.Address.ToString()!="0.0.0.0")?20:0)+(a.Address.ToString().StartsWith("192.168.")?5:0)}))
    .OrderByDescending(a=>a.Score).Select(a=>new KeyValuePair<string,string>(a.IP,a.IP+"  ·  "+a.Name)).ToArray();}
   catch {return new[]{new KeyValuePair<string,string>("127.0.0.1","127.0.0.1  ·  本机")};}
  }
  public static string[] LocalAddresses() {return LocalInterfaces().Select(x=>x.Key).Distinct().ToArray();}
 }
}

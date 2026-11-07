using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LanTime {
 internal interface ISystemClock {
  DateTime UtcNow { get; }
  void SetUtc(Func<DateTime> utcNow);
 }
 internal sealed class WindowsSystemClock : ISystemClock {
  [StructLayout(LayoutKind.Sequential)] internal struct SystemTime {
   public ushort Year,Month,DayOfWeek,Day,Hour,Minute,Second,Milliseconds;
   internal static SystemTime FromUtc(DateTime utc) {
    if(utc.Kind!=DateTimeKind.Utc||utc.Year<1601)throw new ArgumentException("系统校时必须使用有效的 UTC 时间。");
    return new SystemTime{Year=(ushort)utc.Year,Month=(ushort)utc.Month,DayOfWeek=(ushort)utc.DayOfWeek,Day=(ushort)utc.Day,Hour=(ushort)utc.Hour,Minute=(ushort)utc.Minute,Second=(ushort)utc.Second,Milliseconds=(ushort)utc.Millisecond};
   }
  }
  [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool SetSystemTime(ref SystemTime utc);
  public DateTime UtcNow { get { return DateTime.UtcNow; } }
  public void SetUtc(Func<DateTime> utcNow) {
   // SetSystemTime enables SeSystemtimePrivilege for this call. It fails if the
   // process token lacks that right; it does not change the computer's time zone.
   var value=SystemTime.FromUtc(utcNow());
   if(!SetSystemTime(ref value))throw new Win32Exception(Marshal.GetLastWin32Error());
  }
 }
 internal sealed class LocalTimeSync {
  readonly ISystemClock system;
  public string State,Detail,LastSuccess="—";
  public bool NeedsAdmin;
  public double LastCorrectionMs;
  public LocalTimeSync(bool enabled,ISystemClock clock) {
   system=clock;State=enabled?"等待校准":"未开启";
   Detail=enabled?"多源比对通过后自动校准 Windows 时间。":"可在服务设置中开启自动校准本机时间。";
  }
  public void NoConsensus(bool enabled) {if(!enabled)return;State="等待一致时间";Detail="本轮来源不足或分歧，未修改本机时间。";}
  public void Invalidate(bool enabled) {if(!enabled)return;State="等待重新校准";Detail="休眠恢复后，须重新取得多源一致时间。";}
  public void Apply(bool enabled,bool freshConsensus,Clock clock) {
   if(!enabled||!freshConsensus)return;
   try {
    // Include the pending correction: the LAN clock may still be slewing while
    // the system clock must be set to this round's accepted upstream estimate.
    DateTime target=clock.EstimatedUtcNow;LastCorrectionMs=(target-system.UtcNow).TotalMilliseconds;
    if(Math.Abs(LastCorrectionMs)<50){State="时间一致";Detail="本机偏差小于 50 ms，无需调整。";NeedsAdmin=false;LastSuccess=target.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");return;}
    system.SetUtc(delegate{return clock.EstimatedUtcNow;});
    DateTime actual=system.UtcNow;double remaining=(clock.EstimatedUtcNow-actual).TotalMilliseconds;
    if(Math.Abs(remaining)>250)throw new InvalidOperationException("写入后偏差仍为 "+remaining.ToString("F0")+" ms，请检查其他校时软件或系统策略。");
    State="校准成功";Detail="已调整 Windows 时间 "+LastCorrectionMs.ToString("+0.0;-0.0;0.0")+" ms。";NeedsAdmin=false;LastSuccess=actual.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    Store.Log("本机校时成功："+Detail);
   } catch(Win32Exception e) {
    NeedsAdmin=e.NativeErrorCode==5||e.NativeErrorCode==1314||e.NativeErrorCode==1300;
    State=NeedsAdmin?"需要校时权限":"校准失败";Detail=NeedsAdmin?"请以管理员运行，或使用开机后台服务；系统策略须允许更改系统时间。":"Windows 校时失败（"+e.NativeErrorCode+"）："+e.Message;
    Store.Log("本机校时失败："+Detail);
   } catch(Exception e) {NeedsAdmin=false;State="校准失败";Detail=e.Message;Store.Log("本机校时失败："+Detail);}
  }
 }
}

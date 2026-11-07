using System;
using System.IO;
using System.Reflection;

namespace LanTime {
 public static class AppInfo {
  public const string Version="1.0.4";
  public const string Author="chengkai";
  public const string Website="https://nnu.cn";
  public const string Copyright="Copyright (c) 2026 chengkai";
  public static string LicenseText {
   get {
    using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("LanTime.LICENSE.txt")) {
     if(stream==null)throw new InvalidOperationException("程序缺少嵌入的开源协议资源，请重新编译。");
     using(var reader=new StreamReader(stream))return reader.ReadToEnd();
    }
   }
  }
 }
}

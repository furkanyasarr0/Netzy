using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace Netzy.Core
{
    /// <summary>Uygulama verileri %LOCALAPPDATA%\Netzy altında tutulur (ayarlar, günlükler, yedekler).</summary>
    internal static class Paths
    {
        public static readonly string Root =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Netzy");
        public static readonly string Backups = Path.Combine(Root, "backups");
        public static readonly string Logs = Path.Combine(Root, "logs");
        public static readonly string ConfigFile = Path.Combine(Root, "config.ini");
        public static string ExePath => Assembly.GetEntryAssembly()?.Location ?? Process.GetCurrentProcess().MainModule.FileName;
    }

    /// <summary>Basit anahtar=değer ayar dosyası.</summary>
    internal static class Config
    {
        static readonly Dictionary<string, string> Values = new Dictionary<string, string>();

        public static bool TermsAccepted
        {
            get => Get("terms_accepted") == "1";
            set => Values["terms_accepted"] = value ? "1" : "0";
        }

        public static string Lang
        {
            get => Get("lang");
            set => Values["lang"] = value;
        }

        static string Get(string key) => Values.TryGetValue(key, out var v) ? v : null;

        public static void Load()
        {
            try
            {
                foreach (var line in File.ReadAllLines(Paths.ConfigFile))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) Values[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Paths.Root);
                var sb = new StringBuilder();
                foreach (var kv in Values) sb.Append(kv.Key).Append('=').Append(kv.Value).AppendLine();
                File.WriteAllText(Paths.ConfigFile, sb.ToString(), Encoding.UTF8);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal static class Elevation
    {
        public static bool IsAdmin
        {
            get
            {
                using (var id = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        /// <summary>Uygulamayı UAC ile yönetici olarak yeniden başlatır. Kullanıcı reddederse false döner.</summary>
        public static bool Relaunch()
        {
            try
            {
                Process.Start(new ProcessStartInfo(Paths.ExePath) { UseShellExecute = true, Verb = "runas" });
                return true;
            }
            catch (Win32Exception)
            {
                return false;
            }
        }

        /// <summary>Bağlantıyı yönetici olmayan masaüstü kabuğu üzerinden açar; tarayıcı yönetici yetkisiyle başlamaz.</summary>
        public static void OpenUrl(string url)
        {
            try { Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "\"" + url + "\""); }
            catch (Win32Exception) { }
        }

        public static void OpenFolder(string path)
        {
            Directory.CreateDirectory(path);
            OpenUrl(path);
        }
    }

    internal static class Native
    {
        [DllImport("kernel32.dll")] public static extern uint GetOEMCP();
        [DllImport("kernel32.dll")] public static extern bool AttachConsole(int processId);
        public const int ATTACH_PARENT_PROCESS = -1;

        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("dnsapi.dll", CharSet = CharSet.Unicode, EntryPoint = "DnsQuery_W")]
        public static extern int DnsQuery(string name, ushort type, uint options, IntPtr extra, out IntPtr results, IntPtr reserved);
        [DllImport("dnsapi.dll")] public static extern void DnsRecordListFree(IntPtr list, int freeType);
        public const ushort DNS_TYPE_A = 1;
        public const uint DNS_QUERY_BYPASS_CACHE = 0x8;
        public const int DnsFreeRecordList = 1;
    }

    internal sealed class CmdResult
    {
        public string CommandLine;
        public int ExitCode;
        public string StdOut = "";
        public string Output = "";   // stdout + stderr
        public bool TimedOut;
    }

    /// <summary>
    /// Windows araçlarını kabuk (cmd/PowerShell) kullanmadan, System32 içindeki tam yoluyla çalıştırır.
    /// Tam yol, EXE'nin klasörüne bırakılmış sahte bir netsh.exe'nin yönetici yetkisiyle çalışmasını engeller.
    /// </summary>
    internal static class ProcessRunner
    {
        public static readonly Encoding Oem = GetOem();

        static Encoding GetOem()
        {
            try { return Encoding.GetEncoding((int)Native.GetOEMCP()); }
            catch (Exception) { return Encoding.Default; }
        }

        public static string SystemTool(string exe) => Path.Combine(Environment.SystemDirectory, exe);

        public static CmdResult Run(string exe, string args, int timeoutMs = 90000)
        {
            var result = new CmdResult { CommandLine = Path.GetFileNameWithoutExtension(exe) + " " + args };
            var psi = new ProcessStartInfo(SystemTool(exe), args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Oem,
                StandardErrorEncoding = Oem,
                WorkingDirectory = Environment.SystemDirectory,
            };
            using (var p = Process.Start(psi))
            {
                var stdout = p.StandardOutput.ReadToEndAsync();
                var stderr = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs))
                {
                    result.TimedOut = true;
                    try { p.Kill(); } catch (InvalidOperationException) { } catch (Win32Exception) { }
                }
                p.WaitForExit(5000);
                stdout.Wait(5000);
                stderr.Wait(5000);
                result.ExitCode = result.TimedOut ? -1 : p.ExitCode;
                result.StdOut = stdout.IsCompleted ? stdout.Result : "";
                result.Output = (result.StdOut + "\n" + (stderr.IsCompleted ? stderr.Result : "")).Trim();
            }
            return result;
        }
    }

    internal static class SuccessRules
    {
        public static bool LooksOk(CmdResult r) => LooksOk(r.CommandLine, r.ExitCode, r.Output, r.TimedOut);

        /// <summary>Komut çıktısından başarı kararı verir.</summary>
        public static bool LooksOk(string commandLine, int exitCode, string output, bool timedOut = false)
        {
            if (timedOut) return false;
            var o = (output ?? "").ToLowerInvariant();
            if (o.Contains("requires elevation") || o.Contains("yükseltme gerekiyor")) return false;
            if (exitCode == 0) return true;
            var keys = new List<string> { "ok!", "successfully", "başarıyla" };
            // netsh int ip/ipv6 reset bazı kayıt anahtarlarında hata verip yine de işi tamamlar
            if (commandLine.StartsWith("netsh int ip", StringComparison.OrdinalIgnoreCase))
                keys.AddRange(new[] { "restart the computer", "yeniden başlat" });
            foreach (var k in keys)
                if (o.Contains(k)) return true;
            return false;
        }
    }

    internal static class Versioning
    {
        public static string Current
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return $"v{v.Major}.{v.Minor}.{v.Build}";
            }
        }

        /// <summary>"v1.2.3" → [1,2,3]. Geçersiz etiketlerde null.</summary>
        public static int[] Parse(string tag)
        {
            var t = (tag ?? "").Trim().TrimStart('v', 'V');
            if (t.Length == 0) return null;
            var parts = t.Split('.');
            var nums = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                if (!int.TryParse(parts[i], out nums[i]) || nums[i] < 0) return null;
            return nums;
        }

        public static bool IsNewer(string latest, string current)
        {
            var a = Parse(latest);
            var b = Parse(current);
            if (a == null || b == null) return false;
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length ? a[i] : 0, y = i < b.Length ? b[i] : 0;
                if (x != y) return x > y;
            }
            return false;
        }
    }

    /// <summary>Bir işlemin adım adım kaydı; "Rapor" penceresinde gösterilir ve dosyaya yazılır.</summary>
    internal sealed class RunLog
    {
        const int KeepLogs = 20;
        readonly StringBuilder _sb = new StringBuilder();

        public RunLog(string title)
        {
            _sb.AppendLine($"Netzy {Versioning.Current} — {title}");
            _sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} · Windows {Environment.OSVersion.Version} · {(Environment.Is64BitProcess ? "x64" : "x86")}");
            _sb.AppendLine(new string('-', 60));
        }

        public string Text => _sb.ToString();

        public void Line(string text) => _sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] {text}");

        public void Cmd(CmdResult r, bool includeOutput = true)
        {
            bool ok = SuccessRules.LooksOk(r);
            Line($"{(ok ? "OK " : "ERR")} {r.CommandLine}  (exit {r.ExitCode}{(r.TimedOut ? ", timeout" : "")})");
            if (!includeOutput || string.IsNullOrWhiteSpace(r.Output)) return;
            var output = r.Output.Length > 1500 ? r.Output.Substring(0, 1500) + " …" : r.Output;
            foreach (var l in output.Split('\n'))
                if (l.Trim().Length > 0) _sb.AppendLine("           " + l.TrimEnd());
        }

        public string Save()
        {
            try
            {
                Directory.CreateDirectory(Paths.Logs);
                var path = Path.Combine(Paths.Logs, $"{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                File.WriteAllText(path, Text, Encoding.UTF8);
                var old = Directory.GetFiles(Paths.Logs, "*.txt");
                Array.Sort(old, StringComparer.Ordinal);
                for (int i = 0; i < old.Length - KeepLogs; i++) File.Delete(old[i]);
                return path;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        public static string LatestText()
        {
            if (!Directory.Exists(Paths.Logs)) return null;
            var files = Directory.GetFiles(Paths.Logs, "*.txt");
            if (files.Length == 0) return null;
            Array.Sort(files, StringComparer.Ordinal);
            try { return File.ReadAllText(files[files.Length - 1], Encoding.UTF8); }
            catch (IOException) { return null; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace Netzy.Core
{
    internal sealed class Adapter
    {
        public string Name;     // "Wi-Fi", "Ethernet" (netsh/ipconfig bu adı kullanır)
        public string Id;       // {GUID}
        public bool Dhcp;
        public bool HasValidIPv4;
        public IPAddress Gateway;
    }

    internal static class NetInfo
    {
        /// <summary>İnternete çıkan (ya da IP alamamış) fiziksel bağdaştırıcılar. Hyper-V/VPN gibi ağ geçidi olmayan sanal kartlar hariç.</summary>
        public static List<Adapter> InternetAdapters()
        {
            var list = new List<Adapter>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                IPInterfaceProperties props;
                IPv4InterfaceProperties v4;
                try
                {
                    props = ni.GetIPProperties();
                    v4 = props.GetIPv4Properties();
                }
                catch (NetworkInformationException) { continue; }
                if (v4 == null) continue;

                var ipv4 = props.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork).ToList();
                var adapter = new Adapter
                {
                    Name = ni.Name,
                    Id = ni.Id,
                    Dhcp = v4.IsDhcpEnabled,
                    HasValidIPv4 = ipv4.Any(a => !a.Address.ToString().StartsWith("169.254.")),
                    Gateway = props.GatewayAddresses.Select(g => g.Address)
                        .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any)),
                };
                if (adapter.Gateway != null || !adapter.HasValidIPv4) list.Add(adapter);
            }
            // İnterneti sağlayan bağdaştırıcı önce gelsin
            return list.OrderByDescending(a => a.Gateway != null && a.HasValidIPv4).ToList();
        }

        public static Adapter Primary() => InternetAdapters().FirstOrDefault();

        public static bool IsUp(string name) =>
            NetworkInterface.GetAllNetworkInterfaces().Any(n => n.Name == name && n.OperationalStatus == OperationalStatus.Up);

        /// <summary>Bağdaştırıcı yeniden başlatıldıktan sonra tekrar bağlanmasını bekler.</summary>
        public static void WaitUntilUp(IEnumerable<string> names, TimeSpan max)
        {
            var sw = Stopwatch.StartNew();
            var pending = names.ToList();
            while (pending.Count > 0 && sw.Elapsed < max)
            {
                pending.RemoveAll(IsUp);
                if (pending.Count > 0) Thread.Sleep(1000);
            }
        }

        /// <summary>Bağdaştırıcıya elle girilmiş DNS sunucuları (boşsa DNS otomatik/DHCP'den gelir).</summary>
        public static string StaticDnsServers(string adapterId)
        {
            var key = $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{adapterId}";
            return Registry.GetValue(key, "NameServer", "") as string ?? "";
        }
    }

    internal sealed class Metrics
    {
        public int? LatencyMs;
        public int? DnsMs;
    }

    internal enum WebCheck { Ok, Captive, Fail }

    /// <summary>Ağ ölçümleri. Hepsi yalnızca okur; hiçbir ayarı değiştirmez.</summary>
    internal static class Probes
    {
        public static int? TcpMs(string host, int port, int timeoutMs = 1500)
        {
            using (var client = new TcpClient())
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var task = client.ConnectAsync(host, port);
                    if (!task.Wait(timeoutMs) || !client.Connected) return null;
                    return (int)sw.ElapsedMilliseconds;
                }
                catch (AggregateException) { return null; }
                catch (SocketException) { return null; }
            }
        }

        public static int? InternetMs(int timeoutMs = 1500) =>
            TcpMs("1.1.1.1", 443, timeoutMs) ?? TcpMs("8.8.8.8", 443, timeoutMs);

        /// <summary>Önbelleği atlayarak DNS sunucusuna sorar; cevap süresini döner.</summary>
        public static int? DnsMs(string host)
        {
            var sw = Stopwatch.StartNew();
            int status = Native.DnsQuery(host, Native.DNS_TYPE_A, Native.DNS_QUERY_BYPASS_CACHE, IntPtr.Zero, out var records, IntPtr.Zero);
            var ms = (int)sw.ElapsedMilliseconds;
            if (records != IntPtr.Zero) Native.DnsRecordListFree(records, Native.DnsFreeRecordList);
            return status == 0 ? ms : (int?)null;
        }

        public static int? GatewayMs(IPAddress gateway)
        {
            if (gateway == null) return null;
            using (var ping = new Ping())
            {
                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        var reply = ping.Send(gateway, 1000);
                        if (reply != null && reply.Status == IPStatus.Success) return (int)reply.RoundtripTime;
                    }
                    catch (PingException) { }
                }
            }
            return null;
        }

        /// <summary>Windows'un kendi bağlantı testi. Giriş sayfası (otel/kafe Wi-Fi) yönlendirmesini de yakalar.</summary>
        public static WebCheck ConnectTest()
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://www.msftconnecttest.com/connecttest.txt");
                req.Timeout = 4000;
                req.ReadWriteTimeout = 4000;
                req.AllowAutoRedirect = false;
                req.UserAgent = "Netzy";
                using (var res = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(res.GetResponseStream()))
                {
                    var body = reader.ReadToEnd().Trim();
                    return res.StatusCode == HttpStatusCode.OK && body == "Microsoft Connect Test" ? WebCheck.Ok : WebCheck.Captive;
                }
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse r && (int)r.StatusCode >= 300 && (int)r.StatusCode < 400)
            {
                return WebCheck.Captive;
            }
            catch (WebException) { return WebCheck.Fail; }
            catch (IOException) { return WebCheck.Fail; }
        }

        public static Metrics Measure()
        {
            var m = new Metrics();
            for (int i = 0; i < 3; i++)
            {
                var v = InternetMs();
                if (v.HasValue && (m.LatencyMs == null || v < m.LatencyMs)) m.LatencyMs = v;
            }
            for (int i = 0; i < 2; i++)
            {
                var d = DnsMs("www.microsoft.com");
                if (d.HasValue && (m.DnsMs == null || d < m.DnsMs)) m.DnsMs = d;
            }
            return m;
        }

        public static void WaitForInternet(TimeSpan max)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < max && InternetMs(1000) == null) Thread.Sleep(1000);
        }
    }

    internal sealed class ProxySettings
    {
        public int Flags = WinInetProxy.PROXY_TYPE_DIRECT;
        public string Server;
        public string Bypass;
        public string AutoConfigUrl;

        public bool IsActive =>
            ((Flags & WinInetProxy.PROXY_TYPE_PROXY) != 0 && !string.IsNullOrEmpty(Server)) ||
            ((Flags & WinInetProxy.PROXY_TYPE_AUTO_PROXY_URL) != 0 && !string.IsNullOrEmpty(AutoConfigUrl));
    }

    /// <summary>Tarayıcıların kullandığı (WinINET) proxy ayarları; Windows'un kendi API'siyle okunur/yazılır.</summary>
    internal static class WinInetProxy
    {
        public const int PROXY_TYPE_DIRECT = 1, PROXY_TYPE_PROXY = 2, PROXY_TYPE_AUTO_PROXY_URL = 4, PROXY_TYPE_AUTO_DETECT = 8;
        const int INTERNET_OPTION_PER_CONNECTION_OPTION = 75, INTERNET_OPTION_SETTINGS_CHANGED = 39, INTERNET_OPTION_REFRESH = 37;
        const int FLAGS = 1, PROXY_SERVER = 2, PROXY_BYPASS = 3, AUTOCONFIG_URL = 4;

        [StructLayout(LayoutKind.Sequential)]
        struct OptionList { public int dwSize; public IntPtr pszConnection; public int dwOptionCount; public int dwOptionError; public IntPtr pOptions; }

        [StructLayout(LayoutKind.Sequential)]
        struct Option { public int dwOption; public OptionValue Value; }

        // C birleşimi (DWORD | LPWSTR | FILETIME): 8 bayt, hizalama işaretçi boyutu kadar (x86'da 4, x64'te 8)
        [StructLayout(LayoutKind.Explicit, Size = 8)]
        struct OptionValue
        {
            [FieldOffset(0)] public int dwValue;
            [FieldOffset(0)] public IntPtr pszValue;
        }

        [DllImport("wininet.dll", SetLastError = true)]
        static extern bool InternetSetOption(IntPtr hInternet, int option, IntPtr buffer, int length);
        [DllImport("wininet.dll", SetLastError = true)]
        static extern bool InternetQueryOption(IntPtr hInternet, int option, IntPtr buffer, ref int length);
        [DllImport("kernel32.dll")]
        static extern IntPtr GlobalFree(IntPtr hMem);

        static readonly int[] Ids = { FLAGS, PROXY_SERVER, PROXY_BYPASS, AUTOCONFIG_URL };

        public static ProxySettings Query()
        {
            var values = Call(query: true, new[] { new Option { dwOption = FLAGS }, new Option { dwOption = PROXY_SERVER }, new Option { dwOption = PROXY_BYPASS }, new Option { dwOption = AUTOCONFIG_URL } });
            if (values == null) return new ProxySettings();
            var s = new ProxySettings { Flags = values[0].Value.dwValue };
            s.Server = TakeString(values[1].Value.pszValue);
            s.Bypass = TakeString(values[2].Value.pszValue);
            s.AutoConfigUrl = TakeString(values[3].Value.pszValue);
            return s;
        }

        public static bool Apply(ProxySettings s)
        {
            var strings = new[] { Marshal.StringToHGlobalUni(s.Server ?? ""), Marshal.StringToHGlobalUni(s.Bypass ?? ""), Marshal.StringToHGlobalUni(s.AutoConfigUrl ?? "") };
            try
            {
                var opts = new Option[4];
                opts[0].dwOption = FLAGS; opts[0].Value.dwValue = s.Flags;
                for (int i = 0; i < 3; i++) { opts[i + 1].dwOption = Ids[i + 1]; opts[i + 1].Value.pszValue = strings[i]; }
                bool ok = Call(query: false, opts) != null;
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
                return ok;
            }
            finally
            {
                foreach (var p in strings) Marshal.FreeHGlobal(p);
            }
        }

        static Option[] Call(bool query, Option[] opts)
        {
            int optSize = Marshal.SizeOf(typeof(Option));
            IntPtr pOpts = Marshal.AllocCoTaskMem(optSize * opts.Length);
            var list = new OptionList { dwSize = Marshal.SizeOf(typeof(OptionList)), dwOptionCount = opts.Length, pOptions = pOpts };
            IntPtr pList = Marshal.AllocCoTaskMem(list.dwSize);
            try
            {
                for (int i = 0; i < opts.Length; i++) Marshal.StructureToPtr(opts[i], pOpts + i * optSize, false);
                Marshal.StructureToPtr(list, pList, false);
                int size = list.dwSize;
                bool ok = query
                    ? InternetQueryOption(IntPtr.Zero, INTERNET_OPTION_PER_CONNECTION_OPTION, pList, ref size)
                    : InternetSetOption(IntPtr.Zero, INTERNET_OPTION_PER_CONNECTION_OPTION, pList, size);
                if (!ok) return null;
                var result = new Option[opts.Length];
                for (int i = 0; i < opts.Length; i++) result[i] = (Option)Marshal.PtrToStructure(pOpts + i * optSize, typeof(Option));
                return result;
            }
            finally
            {
                Marshal.FreeCoTaskMem(pOpts);
                Marshal.FreeCoTaskMem(pList);
            }
        }

        static string TakeString(IntPtr p)
        {
            if (p == IntPtr.Zero) return null;
            var s = Marshal.PtrToStringUni(p);
            GlobalFree(p);
            return s;
        }
    }

    /// <summary>Proxy durumunun yedeği: tarayıcı (WinINET) ayarları + sistem servislerinin WinHTTP ayarı.</summary>
    internal sealed class ProxyBackup
    {
        const string WinHttpKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings\Connections";
        public ProxySettings WinInet = new ProxySettings();
        public byte[] WinHttp;

        public static ProxyBackup Capture()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(WinHttpKey))
                return new ProxyBackup { WinInet = WinInetProxy.Query(), WinHttp = key?.GetValue("WinHttpSettings") as byte[] };
        }

        public bool Restore()
        {
            bool ok = WinInetProxy.Apply(WinInet);
            using (var key = Registry.LocalMachine.CreateSubKey(WinHttpKey))
            {
                if (WinHttp == null) key.DeleteValue("WinHttpSettings", false);
                else key.SetValue("WinHttpSettings", WinHttp, RegistryValueKind.Binary);
            }
            return ok;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.AppendLine("flags=" + WinInet.Flags);
            sb.AppendLine("server=" + WinInet.Server);
            sb.AppendLine("bypass=" + WinInet.Bypass);
            sb.AppendLine("pac=" + WinInet.AutoConfigUrl);
            sb.AppendLine("winhttp=" + (WinHttp == null ? "" : Convert.ToBase64String(WinHttp)));
            return sb.ToString();
        }

        public static ProxyBackup Parse(string text)
        {
            var b = new ProxyBackup();
            foreach (var line in text.Split('\n'))
            {
                int i = line.IndexOf('=');
                if (i <= 0) continue;
                string k = line.Substring(0, i), v = line.Substring(i + 1).TrimEnd('\r');
                switch (k)
                {
                    case "flags": int.TryParse(v, out b.WinInet.Flags); break;
                    case "server": b.WinInet.Server = v.Length == 0 ? null : v; break;
                    case "bypass": b.WinInet.Bypass = v.Length == 0 ? null : v; break;
                    case "pac": b.WinInet.AutoConfigUrl = v.Length == 0 ? null : v; break;
                    case "winhttp": b.WinHttp = v.Length == 0 ? null : Convert.FromBase64String(v); break;
                }
            }
            return b;
        }
    }

    internal static class TempCleaner
    {
        /// <summary>
        /// minAge'den eski dosyaları siler, silinen bayt sayısını döner.
        /// Junction/sembolik bağlantıları asla izlemez (TEMP dışındaki dosyalar silinmesin) ve protectedDir'e dokunmaz.
        /// </summary>
        public static long Clean(IEnumerable<string> targets, string protectedDir, TimeSpan minAge, DateTime nowUtc)
        {
            long bytes = 0;
            var cutoff = nowUtc - minAge;
            var protectedNorm = Normalize(protectedDir);

            foreach (var root in targets)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                var walked = new List<string>();
                var stack = new Stack<string>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    var dir = stack.Pop();
                    string[] files, subdirs;
                    try
                    {
                        files = Directory.GetFiles(dir);
                        subdirs = Directory.GetDirectories(dir);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { continue; }

                    foreach (var f in files)
                    {
                        try
                        {
                            var fi = new FileInfo(f);
                            if (fi.LastWriteTimeUtc > cutoff) continue;
                            long len = fi.Length;
                            fi.Delete();
                            bytes += len;
                        }
                        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
                    }

                    foreach (var d in subdirs)
                    {
                        try
                        {
                            if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) continue;
                        }
                        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { continue; }
                        if (Normalize(d) == protectedNorm) continue;
                        walked.Add(d);
                        stack.Push(d);
                    }
                }
                // Boşalan klasörleri en derinden başlayarak kaldır
                foreach (var d in walked.OrderByDescending(p => p.Length))
                {
                    try { Directory.Delete(d, false); }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
                }
            }
            return bytes;
        }

        static string Normalize(string p) =>
            string.IsNullOrEmpty(p) ? "" : Path.GetFullPath(p).TrimEnd('\\').ToLowerInvariant();
    }
}

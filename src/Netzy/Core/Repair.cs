using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Netzy.Core
{
    /// <summary>
    /// Ayar yedekleri. Her yedek, o ayar değiştirilmeden hemen önce alınır.
    /// "Geri Al" her ayar türü için en yeni yedeği kullanır: yani her ayarı son değişiklikten önceki haline döndürür.
    /// </summary>
    internal static class Backup
    {
        public const string FirewallFile = "firewall.wfw", InterfacesFile = "interfaces.txt", ProxyFile = "proxy.txt";
        public static readonly string[] Kinds = { FirewallFile, InterfacesFile, ProxyFile };
        const int KeepSets = 10;

        public static string Create(RunLog log, bool firewall, bool interfaces, bool proxy)
        {
            var baseDir = Path.Combine(Paths.Backups, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            var dir = baseDir;
            for (int n = 2; Directory.Exists(dir); n++) dir = baseDir + "-" + n;
            try
            {
                Directory.CreateDirectory(dir);
                if (firewall)
                {
                    var file = Path.Combine(dir, FirewallFile);
                    var r = ProcessRunner.Run("netsh.exe", $"advfirewall export \"{file}\"");
                    log.Cmd(r);
                    if (!SuccessRules.LooksOk(r) || !File.Exists(file)) throw new IOException("firewall export failed");
                }
                if (interfaces)
                {
                    var r = ProcessRunner.Run("netsh.exe", "-c interface dump");
                    log.Cmd(r, includeOutput: false);
                    if (r.ExitCode != 0 || r.StdOut.Trim().Length == 0) throw new IOException("interface dump failed");
                    // netsh -f dosyayı OEM kod sayfasıyla okur; Türkçe bağdaştırıcı adları bozulmasın
                    File.WriteAllText(Path.Combine(dir, InterfacesFile), r.StdOut, ProcessRunner.Oem);
                }
                if (proxy)
                    File.WriteAllText(Path.Combine(dir, ProxyFile), ProxyBackup.Capture().Serialize(), Encoding.UTF8);
                log.Line("backup: " + dir);
                Prune();
                return dir;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is System.ComponentModel.Win32Exception)
            {
                log.Line("backup failed: " + e.Message);
                try { Directory.Delete(dir, true); } catch (IOException) { }
                return null;
            }
        }

        static string[] Sets() =>
            Directory.Exists(Paths.Backups)
                ? Directory.GetDirectories(Paths.Backups).OrderByDescending(Path.GetFileName, StringComparer.Ordinal).ToArray()
                : new string[0];

        /// <summary>Her ayar türü için en yeni yedek dosyası (tür → dosya yolu).</summary>
        public static Dictionary<string, string> Latest()
        {
            var result = new Dictionary<string, string>();
            foreach (var set in Sets())
                foreach (var kind in Kinds)
                {
                    var f = Path.Combine(set, kind);
                    if (!result.ContainsKey(kind) && File.Exists(f)) result[kind] = f;
                }
            return result;
        }

        static void Prune()
        {
            var sets = Sets();
            var keep = new HashSet<string>(sets.Take(KeepSets));
            foreach (var f in Latest().Values) keep.Add(Path.GetDirectoryName(f));
            foreach (var set in sets.Where(s => !keep.Contains(s)))
            {
                try { Directory.Delete(set, true); } catch (IOException) { }
            }
        }

        public static string KindKey(string kind) =>
            kind == FirewallFile ? "kind.firewall" : kind == InterfacesFile ? "kind.interfaces" : "kind.proxy";

        public static string KindName(string kind) => Loc.T(KindKey(kind));

        public static string Describe(Dictionary<string, string> latest) =>
            string.Join("\n", latest.Select(kv => "• " + KindName(kv.Key) + " — " + SetTime(kv.Value)));

        static string SetTime(string file)
        {
            var name = Path.GetFileName(Path.GetDirectoryName(file)) ?? "";
            return name.Length >= 15 && DateTime.TryParseExact(name.Substring(0, 15), "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
                ? t.ToString("g", Loc.Culture)
                : name;
        }

        /// <summary>En yeni yedekleri geri yükler, geçmişe yazar; (tür, başarılı mı) listesi döner.</summary>
        public static List<(string Kind, bool Ok)> Restore()
        {
            var log = new RunLog("Undo");
            var results = new List<(string Kind, bool Ok)>();
            foreach (var kv in Latest())
            {
                bool ok;
                try
                {
                    if (kv.Key == FirewallFile)
                    {
                        var r = ProcessRunner.Run("netsh.exe", $"advfirewall import \"{kv.Value}\"");
                        log.Cmd(r);
                        ok = SuccessRules.LooksOk(r);
                    }
                    else if (kv.Key == InterfacesFile)
                    {
                        var r = ProcessRunner.Run("netsh.exe", $"-f \"{kv.Value}\"", 120000);
                        log.Cmd(r);
                        ok = r.ExitCode == 0 && !r.TimedOut;
                    }
                    else
                    {
                        ok = ProxyBackup.Parse(File.ReadAllText(kv.Value, Encoding.UTF8)).Restore();
                        log.Line($"{(ok ? "OK " : "ERR")} proxy restore");
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is FormatException || e is System.ComponentModel.Win32Exception)
                {
                    log.Line("ERR restore " + kv.Key + ": " + e.Message);
                    ok = false;
                }
                results.Add((kv.Key, ok));
            }
            History.Add(new HistoryEntry
            {
                Kind = "undo",
                Ok = results.All(r => r.Ok),
                Log = log.Text,
                Steps = results.Select(r => new HistoryStep { Key = KindKey(r.Kind), Ok = r.Ok }).ToList(),
            });
            return results;
        }
    }

    internal static class DnsSwitcher
    {
        public static readonly Dictionary<string, string[]> Providers = new Dictionary<string, string[]>
        {
            ["cloudflare"] = new[] { "1.1.1.1", "1.0.0.1" },
            ["google"] = new[] { "8.8.8.8", "8.8.4.4" },
            ["quad9"] = new[] { "9.9.9.9", "149.112.112.112" },
        };

        public static bool IsChoice(string p) => p == "auto" || Providers.ContainsKey(p);

        /// <summary>Kayıt defterindeki NameServer değerinden seçimi bulur: "auto", sağlayıcı adı veya "custom".</summary>
        public static string Match(string nameServerValue)
        {
            var servers = (nameServerValue ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (servers.Length == 0) return "auto";
            foreach (var kv in Providers)
                if (kv.Value.Contains(servers[0])) return kv.Key;
            return "custom";
        }

        public static string Current()
        {
            var a = NetInfo.Primary();
            return a == null ? null : Match(NetInfo.StaticDnsServers(a.Id));
        }

        public static string DisplayName(string choice) =>
            choice == "auto" ? Loc.T("dns.auto") : choice == "quad9" ? "Quad9" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(choice);

        public static (bool Ok, long? HistoryId) Apply(string choice)
        {
            var log = new RunLog("DNS → " + choice);
            var adapters = NetInfo.InternetAdapters();
            bool ok = adapters.Count > 0 && Backup.Create(log, firewall: false, interfaces: true, proxy: false) != null;
            if (adapters.Count == 0) log.Line("no active adapter");
            if (ok)
            {
                foreach (var a in adapters)
                {
                    if (choice == "auto")
                    {
                        ok &= Run(log, $"interface ipv4 set dnsservers name=\"{a.Name}\" source=dhcp");
                    }
                    else
                    {
                        var s = Providers[choice];
                        ok &= Run(log, $"interface ipv4 set dnsservers name=\"{a.Name}\" source=static address={s[0]} register=primary validate=no");
                        ok &= Run(log, $"interface ipv4 add dnsservers name=\"{a.Name}\" address={s[1]} index=2 validate=no");
                    }
                }
                log.Cmd(ProcessRunner.Run("ipconfig.exe", "/flushdns"));
            }
            var id = History.Add(new HistoryEntry { Kind = "dns", Ok = ok, Summary = "DNS → " + DisplayName(choice), Log = log.Text });
            return (ok, id);
        }

        static bool Run(RunLog log, string args)
        {
            var r = ProcessRunner.Run("netsh.exe", args);
            log.Cmd(r);
            return SuccessRules.LooksOk(r);
        }
    }

    internal sealed class DiagnosisResult
    {
        public bool AdapterUp, HasIp, GatewayOk, DnsOk, TcpOk, HttpOk, CaptivePortal, ProxyActive;
        public string AdapterName;
        public IPAddress Gateway;
        public int? GatewayMs, DnsMs, InternetMs;
    }

    internal enum Advice { Healthy, CheckCable, QuickIp, QuickProxy, QuickDns, Captive, Router, Isp, Deep }

    internal static class Diagnostics
    {
        public static DiagnosisResult Run()
        {
            var r = new DiagnosisResult();
            var a = NetInfo.Primary();
            r.AdapterUp = a != null;
            r.HasIp = a != null && a.HasValidIPv4;
            r.AdapterName = a?.Name;
            r.Gateway = a?.Gateway;
            r.GatewayMs = Probes.GatewayMs(r.Gateway);
            r.GatewayOk = r.GatewayMs != null;
            r.DnsMs = Probes.DnsMs("www.msftconnecttest.com");
            r.DnsOk = r.DnsMs != null;
            r.InternetMs = Probes.InternetMs();
            r.TcpOk = r.InternetMs != null;
            var web = Probes.ConnectTest();
            r.HttpOk = web == WebCheck.Ok;
            r.CaptivePortal = web == WebCheck.Captive;
            r.ProxyActive = WinInetProxy.Query().IsActive;
            return r;
        }

        /// <summary>Teşhis sonucundan öneri üretir. Sıra önemlidir: ilk eşleşen kural kazanır.</summary>
        public static Advice Recommend(DiagnosisResult r)
        {
            if (!r.AdapterUp) return Advice.CheckCable;
            if (!r.HasIp) return Advice.QuickIp;
            if (r.CaptivePortal) return Advice.Captive;
            if (r.HttpOk && r.DnsOk) return Advice.Healthy;
            if (r.ProxyActive && !r.HttpOk) return Advice.QuickProxy;
            if (r.TcpOk && !r.DnsOk) return Advice.QuickDns;
            if (!r.GatewayOk && !r.TcpOk) return Advice.Router;
            if (!r.TcpOk) return Advice.Isp;
            return Advice.Deep;
        }

        public static string Describe(DiagnosisResult r) => string.Join("\n",
            $"{Loc.T("diag.adapter")}: {(!r.AdapterUp ? Loc.T("diag.none") : r.HasIp ? r.AdapterName : r.AdapterName + " — " + Loc.T("diag.noip"))}",
            $"{Loc.T("diag.gateway")}: {(r.GatewayOk ? r.Gateway + " (" + Format.Ms(r.GatewayMs) + ")" : r.Gateway == null ? "—" : r.Gateway + " — " + Loc.T("diag.unreachable"))}",
            $"{Loc.T("diag.dns")}: {(r.DnsOk ? Format.Ms(r.DnsMs) : Loc.T("diag.fail"))}",
            $"{Loc.T("diag.internet")}: {(r.HttpOk ? Format.Ms(r.InternetMs) : r.CaptivePortal ? Loc.T("diag.captive") : r.TcpOk ? Loc.T("diag.blocked") : Loc.T("diag.fail"))}",
            $"Proxy: {(r.ProxyActive ? Loc.T("diag.proxyon") : "—")}");

        /// <summary>Sorun bulan teşhisleri geçmişe yazar. Sağlıklı sonuçlar yazılmaz; yoksa her açılış bir kayıt eklerdi.</summary>
        public static void RecordIfProblem(DiagnosisResult r)
        {
            var advice = Recommend(r);
            if (advice == Advice.Healthy) return;
            var log = new RunLog("Diagnosis");
            foreach (var line in Describe(r).Split('\n')) log.Line(line);
            History.Add(new HistoryEntry { Kind = "diagnose", Ok = false, Summary = Loc.T("advice." + advice.ToString().ToLowerInvariant()), Log = log.Text });
        }
    }

    internal sealed class StepResult
    {
        public bool Ok;
        public string Detail;
        public static StepResult Success(string detail = null) => new StepResult { Ok = true, Detail = detail };
        public static StepResult Fail(string detail = null) => new StepResult { Ok = false, Detail = detail };
    }

    internal sealed class RepairContext
    {
        public RunLog Log;
        public List<Adapter> Adapters;   // çalışma başında yakalanır; TCP/IP sıfırlanırken liste değişebilir

        public bool RunOk(string exe, string args, int timeoutMs = 90000)
        {
            var r = ProcessRunner.Run(exe, args, timeoutMs);
            Log.Cmd(r);
            return SuccessRules.LooksOk(r);
        }

        public StepResult RunAll(params (string Exe, string Args)[] cmds)
        {
            bool ok = true;
            foreach (var c in cmds) ok &= RunOk(c.Exe, c.Args);   // biri başarısız olsa da hepsi çalışır
            return ok ? StepResult.Success() : StepResult.Fail();
        }
    }

    internal sealed class RepairStep
    {
        public string Key;
        public bool Critical;   // başarısız olursa sonraki adımlar çalışmaz (yedek alınamadıysa hiçbir şey değiştirilmez)
        public Func<RepairContext, StepResult> Run;
    }

    internal sealed class RunSummary
    {
        public readonly List<(string Key, StepResult Result)> Results = new List<(string, StepResult)>();
        public Metrics Before, After;
        public long? HistoryId;
        public bool InternetBack => After?.LatencyMs != null;
        public int OkCount => Results.Count(r => r.Result.Ok);
        public bool AllOk => Results.All(r => r.Result.Ok);
    }

    internal static class Repairs
    {
        static RepairStep S(string key, Func<RepairContext, StepResult> run, bool critical = false) =>
            new RepairStep { Key = key, Run = run, Critical = critical };

        public static List<RepairStep> Steps(bool deep)
        {
            var list = new List<RepairStep> { S("backup", c => DoBackup(c, deep), critical: true) };
            if (deep)
            {
                list.Add(S("winsock", c => c.RunAll(("netsh.exe", "winsock reset"))));
                list.Add(S("tcpip", c => c.RunAll(("netsh.exe", "int ip reset"), ("netsh.exe", "int ipv6 reset"))));
                list.Add(S("firewall", c => c.RunAll(("netsh.exe", "advfirewall reset"))));
            }
            // DHCP servisi ve ağ kartları bilerek yeniden başlatılmaz: DHCP servisini durdurmak Wi-Fi servisini (WlanSvc)
            // ve VPN servislerini de durdurur; kartı kapatıp açmak ise başarısız olursa kartı kalıcı olarak kapalı bırakır.
            // IP yenileme ve Derin Onarım sonrası yeniden başlatma aynı işi bu riskler olmadan yapar.
            list.Add(S("proxy", ResetProxy));
            list.Add(S("arp", c => c.RunAll(("netsh.exe", "interface ip delete arpcache"))));
            list.Add(S("netbios", c => c.RunAll(("nbtstat.exe", "-R"), ("nbtstat.exe", "-RR"))));
            list.Add(S("renew", Renew));
            list.Add(S("dns", c => c.RunAll(("ipconfig.exe", "/flushdns"), ("ipconfig.exe", "/registerdns"))));
            if (deep) list.Add(S("temp", CleanTemp));
            return list;
        }

        /// <summary>Tüm onarım akışı: önce ölçüm, adımlar, bağlantının geri gelmesini bekleme, sonra ölçüm.</summary>
        public static RunSummary Run(bool deep, Action<int, int, string, StepResult> onStep)
        {
            var summary = new RunSummary();
            var log = new RunLog(deep ? "Deep repair" : "Quick repair");
            summary.Before = Probes.Measure();
            log.Line($"before: latency={summary.Before.LatencyMs?.ToString() ?? "-"} ms, dns={summary.Before.DnsMs?.ToString() ?? "-"} ms");

            var ctx = new RepairContext { Log = log, Adapters = NetInfo.InternetAdapters() };
            log.Line("adapters: " + string.Join(", ", ctx.Adapters.Select(a => $"{a.Name}{(a.Dhcp ? " (dhcp)" : " (static)")}")));
            var steps = Steps(deep);
            bool aborted = false;
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                onStep?.Invoke(i, steps.Count, step.Key, null);
                log.Line($"== {step.Key}");
                StepResult result;
                if (aborted) result = StepResult.Fail(Loc.T("detail.aborted"));
                else
                {
                    try { result = step.Run(ctx); }
                    catch (Exception e) { result = StepResult.Fail(e.Message); log.Line("exception: " + e); }
                }
                if (!result.Ok && step.Critical) aborted = true;
                log.Line($"=> {(result.Ok ? "OK" : "FAILED")} {result.Detail}");
                summary.Results.Add((step.Key, result));
                onStep?.Invoke(i, steps.Count, step.Key, result);
            }

            Probes.WaitForInternet(TimeSpan.FromSeconds(25));
            summary.After = Probes.Measure();
            log.Line($"after: latency={summary.After.LatencyMs?.ToString() ?? "-"} ms, dns={summary.After.DnsMs?.ToString() ?? "-"} ms");
            summary.HistoryId = History.Add(new HistoryEntry
            {
                Kind = deep ? "deep" : "quick",
                Ok = summary.AllOk && summary.InternetBack,
                Summary = summary.InternetBack ? null : Loc.T("done.nointernet"),
                Before = summary.Before,
                After = summary.After,
                Log = log.Text,
                Steps = summary.Results.Select(r => new HistoryStep { Key = "step." + r.Key, Ok = r.Result.Ok, Detail = r.Result.Detail }).ToList(),
            });
            return summary;
        }

        static StepResult DoBackup(RepairContext c, bool deep)
        {
            var dir = Backup.Create(c.Log, firewall: deep, interfaces: deep, proxy: true);
            return dir != null ? StepResult.Success() : StepResult.Fail(Loc.T("detail.backupfail"));
        }

        static StepResult ResetProxy(RepairContext c)
        {
            var before = WinInetProxy.Query();
            // Proxy ve PAC kapatılır; "Ayarları otomatik algıla" kullanıcının seçtiği gibi kalır
            bool ok = WinInetProxy.Apply(new ProxySettings { Flags = WinInetProxy.PROXY_TYPE_DIRECT | (before.Flags & WinInetProxy.PROXY_TYPE_AUTO_DETECT) });
            c.Log.Line($"{(ok ? "OK " : "ERR")} wininet proxy (was active: {before.IsActive})");
            ok &= c.RunOk("netsh.exe", "winhttp reset proxy");
            return ok ? StepResult.Success(before.IsActive ? Loc.T("detail.proxyoff") : null) : StepResult.Fail();
        }

        static StepResult Renew(RepairContext c)
        {
            var dhcp = c.Adapters.Where(a => a.Dhcp).Select(a => a.Name).ToList();
            if (dhcp.Count == 0) return StepResult.Success(Loc.T("detail.staticip"));
            bool ok = true;
            foreach (var name in dhcp)
            {
                c.RunOk("ipconfig.exe", $"/release \"{name}\"", 30000);
                ok &= c.RunOk("ipconfig.exe", $"/renew \"{name}\"", 60000);
            }
            return ok ? StepResult.Success() : StepResult.Fail();
        }

        static StepResult CleanTemp(RepairContext c)
        {
            var targets = new[]
            {
                Environment.GetEnvironmentVariable("TEMP"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
            };
            long bytes = TempCleaner.Clean(targets, AppDomain.CurrentDomain.BaseDirectory, TimeSpan.FromHours(24), DateTime.UtcNow);
            c.Log.Line($"temp cleaned: {bytes} bytes");
            return StepResult.Success(Loc.T("detail.cleaned", bytes / 1048576.0));
        }
    }
}

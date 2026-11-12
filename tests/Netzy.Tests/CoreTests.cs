using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Netzy.Core;
using Xunit;

namespace Netzy.Tests
{
    public class SuccessRulesTests
    {
        [Fact]
        public void ElevationErrorIsFailureEvenWithExitZero() =>
            Assert.False(SuccessRules.LooksOk("netsh winsock reset", 0, "The requested operation requires elevation"));

        [Fact]
        public void NetshIpResetPartialFailureCountsAsDone() =>
            Assert.True(SuccessRules.LooksOk("netsh int ip reset", 1, "Resetting Interface, failed.\nAccess is denied.\nRestart the computer to complete this action."));

        [Fact]
        public void RestartTextDoesNotHideOtherFailures() =>
            Assert.False(SuccessRules.LooksOk("ipconfig /renew \"Wi-Fi\"", 1, "Bağdaştırıcı yeniden başlatılamadı"));

        [Fact]
        public void TimeoutIsFailure() =>
            Assert.False(SuccessRules.LooksOk("ipconfig /renew", 0, "", timedOut: true));
    }

    public class VersioningTests
    {
        [Theory]
        [InlineData("v2.0.1", "v2.0.0", true)]
        [InlineData("v2.0.10", "v2.0.9", true)]
        [InlineData("2.0.0", "v2.0.0", false)]
        [InlineData("v2.0", "v2.0.0", false)]
        [InlineData("v1.9.9", "v2.0.0", false)]
        [InlineData("v2.1.0-beta", "v2.0.0", false)]
        [InlineData("", "v2.0.0", false)]
        public void IsNewer(string latest, string current, bool expected) =>
            Assert.Equal(expected, Versioning.IsNewer(latest, current));
    }

    public class TempCleanerTests : IDisposable
    {
        readonly string _root = Path.Combine(Path.GetTempPath(), "netzy-test-" + Guid.NewGuid().ToString("N"));
        static readonly DateTime Now = DateTime.UtcNow;

        public TempCleanerTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            // Junction'ı önce kaldır ki temizlik hedefini silmesin
            foreach (var d in Directory.GetDirectories(_root, "*", SearchOption.AllDirectories))
                if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) Directory.Delete(d);
            Directory.Delete(_root, true);
        }

        string MakeFile(string relative, bool old = true)
        {
            var path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "data");
            if (old) File.SetLastWriteTimeUtc(path, Now.AddDays(-3));
            return path;
        }

        [Fact]
        public void DeletesOnlyOldFilesOutsideProtectedDir()
        {
            var own = MakeFile(@"temp\app\logo.png");
            var fresh = MakeFile(@"temp\installer\setup.dat", old: false);
            var junk = MakeFile(@"temp\junk.tmp");
            var junkSub = MakeFile(@"temp\olddir\x.tmp");

            long bytes = TempCleaner.Clean(new[] { Path.Combine(_root, "temp") }, Path.Combine(_root, "temp", "app"), TimeSpan.FromHours(24), Now);

            Assert.True(File.Exists(own));
            Assert.True(File.Exists(fresh));
            Assert.False(File.Exists(junk));
            Assert.False(Directory.Exists(Path.GetDirectoryName(junkSub)));
            Assert.Equal(8, bytes);
        }

        [Fact]
        public void DoesNotFollowJunctionOutOfTemp()
        {
            var outside = MakeFile(@"documents\thesis.docx");
            Directory.CreateDirectory(Path.Combine(_root, "temp"));
            var link = Path.Combine(_root, "temp", "link");
            var mk = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{Path.GetDirectoryName(outside)}\"") { CreateNoWindow = true, UseShellExecute = false });
            mk.WaitForExit();
            Assert.Equal(0, mk.ExitCode);

            TempCleaner.Clean(new[] { Path.Combine(_root, "temp") }, null, TimeSpan.FromHours(24), Now);

            Assert.True(File.Exists(outside));
        }

        [Fact]
        public void RefusesFolderNotNamedTemp()
        {
            // TEMP ayarı yanlışlıkla Belgeler gibi bir klasörü gösterirse hiçbir şey silinmemeli
            var doc = MakeFile(@"Documents\thesis.docx");
            var sub = MakeFile(@"Documents\old\notes.txt");

            long bytes = TempCleaner.Clean(new[] { Path.Combine(_root, "Documents") }, null, TimeSpan.FromHours(24), Now);

            Assert.Equal(0, bytes);
            Assert.True(File.Exists(doc));
            Assert.True(File.Exists(sub));
        }
    }

    public class RepairStepTests
    {
        [Fact]
        public void NeverRestartsDhcpServiceOrAdapters()
        {
            // DHCP servisini durdurmak Wi-Fi (WlanSvc) ve VPN servislerini de durdurur; kartı kapatıp açmak kartı kapalı bırakabilir
            var keys = Repairs.Steps(deep: true).Concat(Repairs.Steps(deep: false)).Select(s => s.Key).ToList();
            Assert.DoesNotContain("dhcp", keys);
            Assert.DoesNotContain("adapters", keys);
        }

        [Fact]
        public void BackupComesFirstAndStopsTheRunIfItFails()
        {
            foreach (var deep in new[] { false, true })
            {
                var first = Repairs.Steps(deep)[0];
                Assert.Equal("backup", first.Key);
                Assert.True(first.Critical);
            }
        }
    }

    public class HistoryTests : IDisposable
    {
        readonly string _oldPath = History.DbPath;
        readonly string _dir = Path.Combine(Path.GetTempPath(), "netzy-history-" + Guid.NewGuid().ToString("N"));

        public HistoryTests() => History.DbPath = Path.Combine(_dir, "history.db");

        public void Dispose()
        {
            History.DbPath = _oldPath;
            Directory.Delete(_dir, true);
        }

        [Fact]
        public void RecordsEveryRunWithStepsErrorsAndLog()
        {
            var deepId = History.Add(new HistoryEntry
            {
                Kind = "deep",
                Ok = false,
                Summary = "İnternet geri gelmedi",
                Before = new Metrics { LatencyMs = 140, DnsMs = 80 },
                After = new Metrics { LatencyMs = null, DnsMs = 12 },
                Log = "netsh winsock reset (exit 1)\nAccess is denied — 'quoted' text",
                Steps =
                {
                    new HistoryStep { Key = "step.backup", Ok = true },
                    new HistoryStep { Key = "step.winsock", Ok = false, Detail = "exit 1" },
                },
            });
            var dnsId = History.Add(new HistoryEntry { Kind = "dns", Ok = true, Summary = "DNS → Cloudflare", Log = "ok" });

            Assert.NotNull(deepId);
            Assert.NotNull(dnsId);

            var all = History.List();
            Assert.Equal(new[] { dnsId.Value, deepId.Value }, all.Select(e => e.Id));   // en yeni üstte
            Assert.Equal(2, all[1].StepTotal);
            Assert.Equal(1, all[1].StepOk);

            var deep = History.Get(deepId.Value);
            Assert.False(deep.Ok);
            Assert.Equal("İnternet geri gelmedi", deep.Summary);
            Assert.Equal(140, deep.Before.LatencyMs);
            Assert.Null(deep.After.LatencyMs);
            Assert.Contains("'quoted'", deep.Log);
            Assert.Equal(new[] { "step.backup", "step.winsock" }, deep.Steps.Select(s => s.Key));
            Assert.Equal("exit 1", deep.Steps[1].Detail);
            Assert.False(deep.Steps[1].Ok);

            History.Clear();
            Assert.Empty(History.List());
        }
    }

    public class DiagnosisTests
    {
        static DiagnosisResult Healthy() => new DiagnosisResult
        {
            AdapterUp = true, HasIp = true, GatewayOk = true, DnsOk = true, TcpOk = true, HttpOk = true,
        };

        [Fact]
        public void HealthyConnection() => Assert.Equal(Advice.Healthy, Diagnostics.Recommend(Healthy()));

        [Fact]
        public void HealthyEvenWhenRouterIgnoresPing()
        {
            var r = Healthy(); r.GatewayOk = false;
            Assert.Equal(Advice.Healthy, Diagnostics.Recommend(r));
        }

        [Fact]
        public void NoAdapter()
        {
            var r = new DiagnosisResult();
            Assert.Equal(Advice.CheckCable, Diagnostics.Recommend(r));
        }

        [Fact]
        public void NoIpAddress()
        {
            var r = Healthy(); r.HasIp = false;
            Assert.Equal(Advice.QuickIp, Diagnostics.Recommend(r));
        }

        [Fact]
        public void CaptivePortalWinsOverRepairs()
        {
            var r = Healthy(); r.HttpOk = false; r.CaptivePortal = true;
            Assert.Equal(Advice.Captive, Diagnostics.Recommend(r));
        }

        [Fact]
        public void BrokenDnsOnly()
        {
            var r = Healthy(); r.DnsOk = false; r.HttpOk = false;
            Assert.Equal(Advice.QuickDns, Diagnostics.Recommend(r));
        }

        [Fact]
        public void ProxyBlockingWeb()
        {
            var r = Healthy(); r.HttpOk = false; r.ProxyActive = true;
            Assert.Equal(Advice.QuickProxy, Diagnostics.Recommend(r));
        }

        [Fact]
        public void RouterDown()
        {
            var r = Healthy(); r.GatewayOk = false; r.TcpOk = false; r.HttpOk = false; r.DnsOk = false;
            Assert.Equal(Advice.Router, Diagnostics.Recommend(r));
        }

        [Fact]
        public void ProviderDown()
        {
            var r = Healthy(); r.TcpOk = false; r.HttpOk = false; r.DnsOk = false;
            Assert.Equal(Advice.Isp, Diagnostics.Recommend(r));
        }

        [Fact]
        public void WebBlockedWithoutProxy()
        {
            var r = Healthy(); r.HttpOk = false;
            Assert.Equal(Advice.Deep, Diagnostics.Recommend(r));
        }
    }

    public class DnsTests
    {
        [Theory]
        [InlineData("", "auto")]
        [InlineData("1.1.1.1,1.0.0.1", "cloudflare")]
        [InlineData("8.8.8.8 8.8.4.4", "google")]
        [InlineData("149.112.112.112", "quad9")]
        [InlineData("192.168.1.1", "custom")]
        public void MatchesProvider(string nameServer, string expected) => Assert.Equal(expected, DnsSwitcher.Match(nameServer));
    }

    public class CliTests
    {
        [Fact]
        public void NoArgsOpensGui() => Assert.Equal(CliKind.Gui, Cli.Parse(new string[0]).Kind);

        [Fact]
        public void DnsWithChoiceAndLang()
        {
            var c = Cli.Parse(new[] { "--dns", "Cloudflare", "--lang", "en" });
            Assert.Equal(CliKind.Dns, c.Kind);
            Assert.Equal("cloudflare", c.DnsChoice);
            Assert.Equal("en", c.Lang);
        }

        [Theory]
        [InlineData("--dns")]
        [InlineData("--dns", "opendns")]
        [InlineData("--quick", "--deep")]
        [InlineData("--lang", "de")]
        [InlineData("--unknown")]
        public void BadUsageIsInvalid(params string[] args) => Assert.Equal(CliKind.Invalid, Cli.Parse(args).Kind);
    }

    public class ProxyBackupTests
    {
        [Fact]
        public void RoundTrip()
        {
            var b = new ProxyBackup { WinHttp = new byte[] { 1, 2, 3 } };
            b.WinInet.Flags = WinInetProxy.PROXY_TYPE_PROXY | WinInetProxy.PROXY_TYPE_DIRECT;
            b.WinInet.Server = "proxy.local:8080";
            b.WinInet.Bypass = "<local>;*.corp";

            var r = ProxyBackup.Parse(b.Serialize());

            Assert.Equal(b.WinInet.Flags, r.WinInet.Flags);
            Assert.Equal(b.WinInet.Server, r.WinInet.Server);
            Assert.Equal(b.WinInet.Bypass, r.WinInet.Bypass);
            Assert.Null(r.WinInet.AutoConfigUrl);
            Assert.Equal(b.WinHttp, r.WinHttp);
            Assert.True(r.WinInet.IsActive);
        }
    }

    public class LocalizationTests
    {
        static string SourceDir()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Netzy"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "src", "Netzy");
        }

        [Fact]
        public void TurkishAndEnglishHaveSameKeys()
        {
            Assert.Empty(Loc.Tr.Keys.Except(Loc.En.Keys));
            Assert.Empty(Loc.En.Keys.Except(Loc.Tr.Keys));
        }

        [Fact]
        public void EveryKeyUsedInCodeAndXamlExists()
        {
            var src = SourceDir();
            var used = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), "Loc\\.T\\(\"([^\"]+)\"\\s*[,)]").Cast<Match>())
                .Concat(Directory.GetFiles(src, "*.xaml", SearchOption.AllDirectories)
                    .SelectMany(f => Regex.Matches(File.ReadAllText(f), "DynamicResource S\\.([\\w.]+)").Cast<Match>()))
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();
            Assert.NotEmpty(used);
            Assert.All(used, k => Assert.True(Loc.Tr.ContainsKey(k), "missing key: " + k));
        }

        [Fact]
        public void EveryStepAndAdviceHasText()
        {
            var keys = Repairs.Steps(deep: true).SelectMany(s => new[] { "step." + s.Key, "step." + s.Key + ".desc" })
                .Concat(Enum.GetNames(typeof(Advice)).Select(a => "advice." + a.ToLowerInvariant()))
                .Concat(new[] { "quick", "deep", "dns", "undo", "diagnose", "error" }.Select(k => "run." + k))   // geçmiş kayıt türleri
                .Concat(Backup.Kinds.Select(Backup.KindKey));
            Assert.All(keys, k => Assert.True(Loc.Tr.ContainsKey(k), "missing key: " + k));
        }

        [Fact]
        public void FormatStringsAreValid()
        {
            foreach (var d in new[] { Loc.Tr, Loc.En })
                foreach (var kv in d)
                    string.Format(kv.Value, 1, 2, 3, 4);   // geçersiz { } varsa FormatException fırlatır
        }
    }
}

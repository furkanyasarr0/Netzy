using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Netzy.Core;
using Netzy.Ui;

namespace Netzy
{
    public partial class MainWindow : Window
    {
        const string RepoUrl = "https://github.com/furkanyasarr0/Netzy";
        const string ReleaseApi = "https://api.github.com/repos/furkanyasarr0/Netzy/releases/latest";

        static readonly Dictionary<string, string> DnsNames = new Dictionary<string, string>
        {
            ["cloudflare"] = "Cloudflare", ["google"] = "Google", ["quad9"] = "Quad9",
        };

        readonly CancellationTokenSource _closing = new CancellationTokenSource();
        readonly Dictionary<Button, string> _dnsButtons;
        DiagnosisResult _diag;
        RunSummary _lastRun;
        string _latestVersion;
        string _currentDns;
        int? _pingMs;
        bool _pingKnown, _busy;

        public MainWindow()
        {
            InitializeComponent();
            Icon = LogoImage.Source = Dialogs.Logo;
            VersionText.Text = Versioning.Current;
            _dnsButtons = new Dictionary<Button, string>
            {
                [DnsAuto] = "auto", [DnsCloudflare] = "cloudflare", [DnsGoogle] = "google", [DnsQuad9] = "quad9",
            };
            UpdateLangButton();
            SetProgress(0);
            SourceInitialized += (s, e) => Dialogs.DarkTitleBar(this);
            Loaded += OnLoaded;
            Closed += (s, e) => _closing.Cancel();
        }

        async void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshUndo();
            RefreshDns();
            _ = PingLoop(_closing.Token);
            _ = CheckForUpdate();
            await RunDiagnosis();
        }

        // ---------- Bağlantı göstergesi ----------

        async Task PingLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                _pingMs = await Task.Run(() => Probes.InternetMs());
                _pingKnown = true;
                if (ct.IsCancellationRequested) return;
                RenderPing();
                try { await Task.Delay(3000, ct); }
                catch (TaskCanceledException) { return; }
            }
        }

        void RenderPing()
        {
            if (!_pingKnown) return;
            if (_pingMs is int ms)
            {
                NetDot.Foreground = Dialogs.Res(ms < 60 ? "GreenBrush" : "AmberBrush");
                NetText.Text = Loc.T("net.connected", ms);
                NetText.Foreground = Dialogs.Res("TextBrush");
            }
            else
            {
                NetDot.Foreground = Dialogs.Res("RedBrush");
                NetText.Text = Loc.T("net.offline");
                NetText.Foreground = Dialogs.Res("RedBrush");
            }
        }

        // ---------- Sürüm kontrolü ----------

        async Task CheckForUpdate()
        {
            var latest = await Task.Run(() =>
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(ReleaseApi);
                    req.UserAgent = "Netzy";
                    req.Timeout = 5000;
                    using (var res = req.GetResponse())
                    using (var reader = new StreamReader(res.GetResponseStream()))
                    {
                        var m = Regex.Match(reader.ReadToEnd(), "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                        return m.Success ? m.Groups[1].Value : null;
                    }
                }
                catch (WebException) { return null; }
                catch (IOException) { return null; }
            });
            if (latest == null || !Versioning.IsNewer(latest, Versioning.Current)) return;
            _latestVersion = latest;
            RenderUpdate();
        }

        void RenderUpdate()
        {
            if (_latestVersion == null) return;
            UpdateText.Text = Loc.T("update.available", _latestVersion);
            UpdateBanner.Visibility = Visibility.Visible;
        }

        // ---------- Teşhis ----------

        async Task RunDiagnosis()
        {
            DiagButton.IsEnabled = false;
            AdviceText.Text = Loc.T("diag.running");
            AdviceText.Foreground = Dialogs.Res("MutedBrush");
            foreach (var dot in new[] { DotAdapter, DotGateway, DotDns, DotInternet }) dot.Foreground = Dialogs.Res("IdleBrush");
            _diag = await Task.Run(() => Diagnostics.Run());
            RenderDiagnosis();
            DiagButton.IsEnabled = !_busy;
        }

        void RenderDiagnosis()
        {
            var r = _diag;
            if (r == null) return;
            SetCheck(DotAdapter, ValAdapter,
                !r.AdapterUp ? "RedBrush" : r.HasIp ? "GreenBrush" : "AmberBrush",
                !r.AdapterUp ? Loc.T("diag.none") : r.HasIp ? r.AdapterName : Loc.T("diag.noip"));
            SetCheck(DotGateway, ValGateway,
                r.GatewayOk ? "GreenBrush" : "RedBrush",
                r.GatewayOk ? $"{r.Gateway} · {Format.Ms(r.GatewayMs)}" : r.Gateway == null ? "—" : Loc.T("diag.unreachable"));
            SetCheck(DotDns, ValDns, r.DnsOk ? "GreenBrush" : "RedBrush", r.DnsOk ? Format.Ms(r.DnsMs) : Loc.T("diag.fail"));
            SetCheck(DotInternet, ValInternet,
                r.HttpOk ? "GreenBrush" : r.CaptivePortal || r.TcpOk ? "AmberBrush" : "RedBrush",
                r.HttpOk ? Format.Ms(r.InternetMs) : r.CaptivePortal ? Loc.T("diag.captive") : r.TcpOk ? Loc.T("diag.blocked") : Loc.T("diag.fail"));

            var advice = Diagnostics.Recommend(r);
            AdviceText.Text = Loc.T("advice." + advice.ToString().ToLowerInvariant());
            AdviceText.Foreground = Dialogs.Res(advice == Advice.Healthy ? "GreenBrush" : "AccentBrush");
        }

        static void SetCheck(TextBlock dot, TextBlock value, string brush, string text)
        {
            dot.Foreground = Dialogs.Res(brush);
            value.Text = text;
        }

        // ---------- Onarım ----------

        async void Quick_Click(object sender, RoutedEventArgs e) => await RunRepair(deep: false);

        async void Deep_Click(object sender, RoutedEventArgs e)
        {
            if (!Dialogs.Confirm(this, Loc.T("confirm.deep.title"), Loc.T("confirm.deep.body"))) return;
            await RunRepair(deep: true);
        }

        async Task RunRepair(bool deep)
        {
            if (!EnsureAdmin()) return;
            SetBusy(true);
            _lastRun = null;
            ResultText.Visibility = Visibility.Collapsed;
            SetProgress(0);

            var summary = await Task.Run(() => Repairs.Run(deep, (i, total, key, result) =>
                Dispatcher.BeginInvoke(new Action(() => OnStep(i, total, key, result)))));

            _lastRun = summary;
            SetBusy(false);
            SetProgress(1);
            RenderRunResult();
            RefreshUndo();
            RefreshDns();

            var body = Loc.T("result.summary", summary.OkCount, summary.Results.Count) + "\n" + Format.Metrics(summary.Before, summary.After);
            if (deep) body += "\n\n" + Loc.T("done.deepnote");
            Dialogs.Message(this, Loc.T("done.title"), body);
            await RunDiagnosis();
        }

        void OnStep(int index, int total, string key, StepResult result)
        {
            var name = Loc.T("step." + key);
            if (result == null)
            {
                StatusText.Text = Loc.T("status.running", index + 1, total, name);
                SetProgress((double)index / total);
                return;
            }
            var outcome = result.Detail ?? Loc.T(result.Ok ? "status.ok" : "status.error");
            StatusText.Text = Loc.T("status.step", index + 1, total, name, outcome);
            SetProgress((double)(index + 1) / total);
        }

        void RenderRunResult()
        {
            if (_lastRun == null) return;
            StatusText.Text = Loc.T("status.done", _lastRun.OkCount, _lastRun.Results.Count);
            var failed = _lastRun.Results.Where(r => !r.Result.Ok).Select(r => Loc.T("step." + r.Key)).ToList();
            ResultText.Text = Format.Metrics(_lastRun.Before, _lastRun.After) +
                              (failed.Count > 0 ? "\n" + Loc.T("status.error") + ": " + string.Join(", ", failed) : "");
            ResultText.Foreground = Dialogs.Res(failed.Count == 0 ? "GreenBrush" : "AmberBrush");
            ResultText.Visibility = Visibility.Visible;
        }

        void SetProgress(double value)
        {
            Progress.Value = value;
            PercentText.Text = Loc.T("status.percent", (int)Math.Round(value * 100));
        }

        void SetBusy(bool busy)
        {
            _busy = busy;
            foreach (var b in new[] { QuickButton, DeepButton, DiagButton, LangButton }) b.IsEnabled = !busy;
            foreach (var b in _dnsButtons.Keys) b.IsEnabled = !busy;
            RefreshUndo();
        }

        bool EnsureAdmin()
        {
            if (Elevation.IsAdmin) return true;
            if (!Dialogs.Confirm(this, Loc.T("admin.title"), Loc.T("admin.body"))) return false;
            if (Elevation.Relaunch()) Application.Current.Shutdown();
            else Dialogs.Message(this, Loc.T("admin.title"), Loc.T("admin.denied"));
            return false;
        }

        // ---------- DNS ve Geri Al ----------

        void RefreshDns()
        {
            _currentDns = DnsSwitcher.Current();
            foreach (var kv in _dnsButtons) kv.Key.Tag = kv.Value == _currentDns ? "selected" : null;
        }

        async void Dns_Click(object sender, RoutedEventArgs e)
        {
            var choice = _dnsButtons[(Button)sender];
            if (choice == _currentDns) return;
            var name = choice == "auto" ? Loc.T("dns.auto") : DnsNames[choice];
            if (!Dialogs.Confirm(this, Loc.T("dns.confirm.title"), Loc.T("dns.confirm.body", name))) return;
            if (!EnsureAdmin()) return;

            SetBusy(true);
            StatusText.Text = Loc.T("dns.applying");
            var (ok, _) = await Task.Run(() => DnsSwitcher.Apply(choice));
            SetBusy(false);
            RefreshDns();
            StatusText.Text = Loc.T(ok ? "dns.done" : "dns.failed");
            await RunDiagnosis();
        }

        void RefreshUndo() => UndoButton.IsEnabled = !_busy && Backup.Latest().Count > 0;

        async void Undo_Click(object sender, RoutedEventArgs e)
        {
            var latest = Backup.Latest();
            if (latest.Count == 0) return;
            if (!Dialogs.Confirm(this, Loc.T("undo.confirm.title"), Loc.T("undo.confirm.body", Backup.Describe(latest)))) return;
            if (!EnsureAdmin()) return;

            SetBusy(true);
            var results = await Task.Run(() =>
            {
                var log = new RunLog("Undo");
                var r = Backup.Restore(log);
                log.Save();
                return r;
            });
            SetBusy(false);
            RefreshDns();
            bool all = results.All(r => r.Ok);
            var lines = string.Join("\n", results.Select(r => $"• {Backup.KindName(r.Kind)}: {(r.Ok ? Loc.T("status.ok") : Loc.T("status.error"))}"));
            Dialogs.Message(this, Loc.T("undo.confirm.title"), Loc.T(all ? "undo.done" : "undo.failed") + "\n\n" + lines);
            await RunDiagnosis();
        }

        // ---------- Üst bar ----------

        async void Diagnose_Click(object sender, RoutedEventArgs e) => await RunDiagnosis();

        void Operations_Click(object sender, RoutedEventArgs e) => Dialogs.Operations(this);

        void Report_Click(object sender, RoutedEventArgs e) => Dialogs.Report(this);

        void Privacy_Click(object sender, RoutedEventArgs e) => Dialogs.Privacy(this, firstRun: false);

        void GitHub_Click(object sender, RoutedEventArgs e) => Elevation.OpenUrl(RepoUrl);

        void Download_Click(object sender, RoutedEventArgs e) => Elevation.OpenUrl(RepoUrl + "/releases/latest");

        void Lang_Click(object sender, RoutedEventArgs e)
        {
            Loc.Set(Loc.Lang == "tr" ? "en" : "tr");
            Config.Lang = Loc.Lang;
            Config.Save();
            UpdateLangButton();
            // Kodla yazılan metinleri yeni dilde yeniden çiz
            RenderPing();
            RenderUpdate();
            RenderDiagnosis();
            SetProgress(Progress.Value);
            if (_lastRun != null) RenderRunResult();
            else StatusText.Text = Loc.T("status.ready");
        }

        void UpdateLangButton() => LangButton.Content = Loc.Lang == "tr" ? "EN" : "TR";
    }
}

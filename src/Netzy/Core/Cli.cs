using System;
using System.IO;

namespace Netzy.Core
{
    internal enum CliKind { Gui, Help, Invalid, Diagnose, Quick, Deep, Dns, Undo }

    internal sealed class CliCommand
    {
        public CliKind Kind = CliKind.Gui;
        public string DnsChoice;
        public string Lang;
    }

    /// <summary>
    /// Komut satırı modu (BT yöneticileri ve betikler için):
    ///   Netzy.exe --diagnose | --quick | --deep | --dns auto|cloudflare|google|quad9 | --undo  [--lang tr|en]
    /// Çıkış kodu: 0 başarılı, 1 bir adım başarısız / sorun bulundu, 2 hatalı kullanım, 3 yönetici yetkisi yok.
    /// </summary>
    internal static class Cli
    {
        public static CliCommand Parse(string[] args)
        {
            var c = new CliCommand();
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i].ToLowerInvariant();
                string Next() => i + 1 < args.Length ? args[++i].ToLowerInvariant() : null;
                CliKind kind;
                switch (a)
                {
                    case "--lang":
                        var lang = Next();
                        if (lang == "tr" || lang == "en") c.Lang = lang;
                        else c.Kind = CliKind.Invalid;
                        continue;
                    case "--help": case "-h": case "/?": kind = CliKind.Help; break;
                    case "--diagnose": kind = CliKind.Diagnose; break;
                    case "--quick": kind = CliKind.Quick; break;
                    case "--deep": kind = CliKind.Deep; break;
                    case "--undo": kind = CliKind.Undo; break;
                    case "--dns":
                        c.DnsChoice = Next();
                        kind = c.DnsChoice != null && DnsSwitcher.IsChoice(c.DnsChoice) ? CliKind.Dns : CliKind.Invalid;
                        break;
                    default: kind = CliKind.Invalid; break;
                }
                // Tek komut kabul edilir
                c.Kind = c.Kind == CliKind.Gui ? kind : CliKind.Invalid;
            }
            return c;
        }

        public static int Run(CliCommand c)
        {
            // Konsoldan çalıştırıldıysa çıktıyı o konsola yaz (çift tıklamada konsol yoktur, çıktı sessizce yok sayılır)
            Native.AttachConsole(Native.ATTACH_PARENT_PROCESS);
            var o = Console.Out;
            o.WriteLine();

            if (c.Kind == CliKind.Help || c.Kind == CliKind.Invalid)
            {
                o.WriteLine(Loc.T("cli.usage", Versioning.Current));
                return c.Kind == CliKind.Help ? 0 : 2;
            }
            if (c.Kind != CliKind.Diagnose && !Elevation.IsAdmin)
            {
                o.WriteLine(Loc.T("cli.needadmin"));
                return 3;
            }

            switch (c.Kind)
            {
                case CliKind.Diagnose:
                {
                    var r = Diagnostics.Run();
                    o.WriteLine($"{Loc.T("diag.adapter")}: {(r.AdapterUp ? r.AdapterName : "-")}");
                    o.WriteLine($"{Loc.T("diag.gateway")}: {(r.GatewayOk ? r.Gateway + " (" + Format.Ms(r.GatewayMs) + ")" : "-")}");
                    o.WriteLine($"{Loc.T("diag.dns")}: {(r.DnsOk ? Format.Ms(r.DnsMs) : "-")}");
                    o.WriteLine($"{Loc.T("diag.internet")}: {(r.HttpOk ? Format.Ms(r.InternetMs) : "-")}");
                    var advice = Diagnostics.Recommend(r);
                    o.WriteLine();
                    o.WriteLine(Loc.T("advice." + advice.ToString().ToLowerInvariant()));
                    return advice == Advice.Healthy ? 0 : 1;
                }
                case CliKind.Quick:
                case CliKind.Deep:
                {
                    var s = Repairs.Run(c.Kind == CliKind.Deep, (i, total, key, res) =>
                    {
                        if (res != null)
                            o.WriteLine($"[{i + 1}/{total}] {Loc.T("step." + key)}: {(res.Ok ? "OK" : Loc.T("status.error"))}{(res.Detail != null ? " — " + res.Detail : "")}");
                    });
                    o.WriteLine();
                    o.WriteLine(Loc.T("result.summary", s.OkCount, s.Results.Count));
                    o.WriteLine(Format.Metrics(s.Before, s.After));
                    if (s.LogPath != null) o.WriteLine(Loc.T("cli.log", s.LogPath));
                    return s.AllOk ? 0 : 1;
                }
                case CliKind.Dns:
                {
                    var (ok, logPath) = DnsSwitcher.Apply(c.DnsChoice);
                    o.WriteLine(ok ? Loc.T("dns.done") : Loc.T("dns.failed"));
                    if (logPath != null) o.WriteLine(Loc.T("cli.log", logPath));
                    return ok ? 0 : 1;
                }
                case CliKind.Undo:
                {
                    if (Backup.Latest().Count == 0)
                    {
                        o.WriteLine(Loc.T("undo.none"));
                        return 1;
                    }
                    var log = new RunLog("Undo");
                    var results = Backup.Restore(log);
                    bool all = true;
                    foreach (var (kind, ok) in results)
                    {
                        o.WriteLine($"{Backup.KindName(kind)}: {(ok ? "OK" : Loc.T("status.error"))}");
                        all &= ok;
                    }
                    var path = log.Save();
                    if (path != null) o.WriteLine(Loc.T("cli.log", path));
                    return all ? 0 : 1;
                }
            }
            return 2;
        }
    }
}

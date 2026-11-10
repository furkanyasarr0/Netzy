using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace Netzy.Core
{
    /// <summary>
    /// Arayüz metinleri (Türkçe / İngilizce). XAML metinleri {DynamicResource S.anahtar} ile bağlanır,
    /// dil değişince Application kaynakları güncellenir ve ekran anında yenilenir.
    /// </summary>
    internal static class Loc
    {
        public static string Lang { get; private set; } = "tr";
        public static CultureInfo Culture => CultureInfo.GetCultureInfo(Lang == "tr" ? "tr-TR" : "en-US");

        public static string SystemDefault() =>
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr" ? "tr" : "en";

        public static void Set(string lang)
        {
            Lang = lang == "en" ? "en" : "tr";
            var app = Application.Current;
            if (app == null) return;
            foreach (var kv in Current) app.Resources["S." + kv.Key] = kv.Value;
        }

        static Dictionary<string, string> Current => Lang == "tr" ? Tr : En;

        public static string T(string key, params object[] args)
        {
            if (!Current.TryGetValue(key, out var s)) s = key;
            return args.Length == 0 ? s : string.Format(Culture, s, args);
        }

        internal static readonly Dictionary<string, string> Tr = new Dictionary<string, string>
        {
            ["net.checking"] = "Kontrol...",
            ["net.connected"] = "Bağlı • {0} ms",
            ["net.offline"] = "Bağlantı yok",

            ["btn.operations"] = "İşlemler",
            ["btn.report"] = "Rapor",
            ["btn.privacy"] = "Gizlilik",
            ["btn.download"] = "İndir",
            ["btn.rediagnose"] = "Yeniden teşhis et",
            ["btn.quick"] = "⚡ Hızlı Onarım",
            ["btn.deep"] = "Derin Onarım",
            ["btn.undo"] = "↺ Geri Al",
            ["btn.close"] = "Kapat",
            ["btn.copy"] = "Raporu kopyala",
            ["btn.copied"] = "Kopyalandı ✓",
            ["btn.openfolder"] = "Klasörü aç",
            ["btn.yes"] = "Evet",
            ["btn.no"] = "Hayır",
            ["btn.ok"] = "Tamam",
            ["btn.accept"] = "Kabul ediyorum ve başla",

            ["diag.title"] = "Bağlantı Teşhisi",
            ["diag.running"] = "Bağlantı kontrol ediliyor...",
            ["diag.adapter"] = "Bağdaştırıcı",
            ["diag.gateway"] = "Modem",
            ["diag.dns"] = "DNS",
            ["diag.internet"] = "İnternet",
            ["diag.none"] = "Bağlı değil",
            ["diag.noip"] = "IP alınamadı",
            ["diag.unreachable"] = "Ulaşılamıyor",
            ["diag.fail"] = "Başarısız",
            ["diag.captive"] = "Giriş sayfası",
            ["diag.blocked"] = "Web engelli",

            ["advice.healthy"] = "Bağlantı sağlıklı görünüyor. Onarım gerekmiyor.",
            ["advice.checkcable"] = "Bağlı bir ağ bağdaştırıcısı yok. Kabloyu veya Wi-Fi bağlantısını kontrol edin.",
            ["advice.quickip"] = "Bilgisayar IP adresi alamadı. Hızlı Onarım önerilir.",
            ["advice.quickproxy"] = "Etkin bir proxy ayarı web trafiğini engelliyor olabilir. Hızlı Onarım önerilir.",
            ["advice.quickdns"] = "Alan adları çözülemiyor (DNS). Hızlı Onarım önerilir; sorun sürerse aşağıdan DNS sunucusunu değiştirin.",
            ["advice.captive"] = "Bu ağ bir giriş sayfası istiyor (otel/kafe Wi-Fi). Tarayıcıda herhangi bir siteyi açın.",
            ["advice.router"] = "Modeme ulaşılamıyor. Modemi kapatıp açın; sorun sürerse Derin Onarım'ı deneyin.",
            ["advice.isp"] = "Modeme ulaşılıyor ama internete çıkılamıyor. Sorun servis sağlayıcınızda olabilir; Derin Onarım'ı deneyebilirsiniz.",
            ["advice.deep"] = "Temel bağlantı var ama web trafiği engelleniyor. Derin Onarım önerilir.",

            ["quick.desc"] = "DNS, ARP ve NetBIOS önbelleklerini temizler, IP adresini yeniler, proxy'yi sıfırlar. Hiçbir ayarı kalıcı bozmaz, yeniden başlatma gerekmez.",
            ["deep.desc"] = "Hızlı Onarım'a ek olarak Winsock, TCP/IP ve güvenlik duvarını sıfırlar, bağdaştırıcıları yeniden başlatır. Önce yedek alınır.",

            ["step.backup"] = "Ayarları yedekle",
            ["step.backup.desc"] = "Değiştirilecek ayarlar (güvenlik duvarı, IP/DNS, proxy) önce yedeklenir. Yedek alınamazsa hiçbir şey değiştirilmez.",
            ["step.winsock"] = "Winsock sıfırlama",
            ["step.winsock.desc"] = "Ağ soket kütüphanesini fabrika ayarlarına döndürür.",
            ["step.tcpip"] = "TCP/IP sıfırlama",
            ["step.tcpip.desc"] = "IPv4 ve IPv6 protokol yığınını sıfırlar. Elle girilmiş sabit IP ayarları silinebilir (yedekten geri alınabilir).",
            ["step.firewall"] = "Güvenlik duvarını sıfırla",
            ["step.firewall.desc"] = "Güvenlik duvarı kurallarını varsayılana döndürür (yedekten geri alınabilir).",
            ["step.proxy"] = "Proxy ayarlarını sıfırla",
            ["step.proxy.desc"] = "Tarayıcı ve sistem proxy'sini kapatır. VPN veya zararlı yazılımdan kalan proxy, \"bağlı ama internet yok\" sorununa yol açar.",
            ["step.dhcp"] = "DHCP istemci servisini yeniden başlat",
            ["step.dhcp.desc"] = "Otomatik IP adresi alma servisini yeniden başlatır.",
            ["step.adapters"] = "Ağ bağdaştırıcılarını yeniden başlat",
            ["step.adapters.desc"] = "İnternete bağlı bağdaştırıcıları kapatıp açar.",
            ["step.arp"] = "ARP önbelleğini temizle",
            ["step.arp.desc"] = "IP-fiziksel adres eşleme tablosunu temizler.",
            ["step.netbios"] = "NetBIOS önbelleğini yenile",
            ["step.netbios.desc"] = "NetBIOS ad önbelleğini temizler ve kayıtları yeniler.",
            ["step.renew"] = "IP adresini yenile",
            ["step.renew.desc"] = "DHCP kullanan bağdaştırıcılarda IP adresini bırakıp yeniden alır (ipconfig /release + /renew).",
            ["step.dns"] = "DNS önbelleğini temizle",
            ["step.dns.desc"] = "DNS çözümleme önbelleğini temizler ve kayıtları yeniler.",
            ["step.temp"] = "Geçici dosyaları temizle",
            ["step.temp.desc"] = "%TEMP% ve Windows\\Temp içindeki 24 saatten eski dosyaları siler. Çalışan programların dosyalarına dokunmaz.",

            ["detail.aborted"] = "atlandı (yedek alınamadı)",
            ["detail.backupfail"] = "yedek alınamadı, hiçbir ayar değiştirilmedi",
            ["detail.proxyoff"] = "etkin proxy kapatıldı",
            ["detail.skipped"] = "servis durdurulamıyor, atlandı",
            ["detail.noadapter"] = "bağlı bağdaştırıcı yok, atlandı",
            ["detail.staticip"] = "sabit IP kullanılıyor, atlandı",
            ["detail.cleaned"] = "{0:0.0} MB temizlendi",

            ["status.ready"] = "Hazır.",
            ["status.percent"] = "%{0}",
            ["status.running"] = "[{0}/{1}] {2}...",
            ["status.step"] = "[{0}/{1}] {2}: {3}",
            ["status.ok"] = "tamam",
            ["status.error"] = "Hata",
            ["status.done"] = "Tamamlandı: {0}/{1} adım başarılı.",
            ["result.summary"] = "{0}/{1} adım başarılı.",
            ["result.metrics"] = "Gecikme: {0} → {1}   ·   DNS: {2} → {3}",

            ["dns.auto"] = "Otomatik",
            ["dns.applying"] = "DNS sunucusu değiştiriliyor...",
            ["dns.done"] = "DNS sunucusu değiştirildi.",
            ["dns.failed"] = "DNS sunucusu değiştirilemedi. Ayrıntılar Rapor'da.",
            ["dns.confirm.title"] = "DNS sunucusunu değiştir",
            ["dns.confirm.body"] = "İnternete bağlı bağdaştırıcılar {0} DNS sunucusunu kullanacak.\n\nMevcut ayarlar önce yedeklenir; \"Geri Al\" ile eski haline dönebilirsiniz. Devam edilsin mi?",

            ["undo.confirm.title"] = "Geri Al",
            ["undo.confirm.body"] = "Şu ayarlar son değişiklikten önceki haline döndürülecek:\n\n{0}\n\nDevam edilsin mi?",
            ["undo.done"] = "Geri alma tamamlandı.",
            ["undo.failed"] = "Bazı ayarlar geri alınamadı. Ayrıntılar Rapor'da.",
            ["undo.none"] = "Geri alınacak bir yedek yok.",

            ["confirm.deep.title"] = "Derin Onarım",
            ["confirm.deep.body"] = "• Güvenlik duvarı kuralları varsayılana döner.\n• TCP/IP sıfırlanır; elle girilmiş sabit IP/DNS ayarları silinebilir.\n• İnternet bağlantısı birkaç saniye kesilir.\n\nTüm bu ayarlar önce yedeklenir ve \"Geri Al\" ile geri yüklenebilir. Devam edilsin mi?",
            ["admin.title"] = "Yönetici izni gerekli",
            ["admin.body"] = "Bu işlem Windows ağ ayarlarını değiştirdiği için yönetici yetkisi gerektirir.\n\nNetzy yönetici olarak yeniden başlatılsın mı?",
            ["admin.denied"] = "Yönetici izni verilmediği için işlem başlatılamadı.",
            ["done.title"] = "Onarım tamamlandı",
            ["done.deepnote"] = "Winsock ve TCP/IP sıfırlamasının tamamen devreye girmesi için bilgisayarınızı yeniden başlatmanız önerilir.",

            ["report.title"] = "Son işlem raporu",
            ["report.empty"] = "Henüz bir işlem yapılmadı.",
            ["ops.title"] = "Onarım adımları",
            ["ops.quick"] = "⚡ Hızlı Onarım",
            ["ops.deep"] = "Derin Onarım",
            ["update.available"] = "Yeni Netzy sürümü mevcut ({0})",
            ["error.title"] = "Beklenmeyen hata",

            ["kind.firewall"] = "Güvenlik duvarı kuralları",
            ["kind.interfaces"] = "IP ve DNS ayarları",
            ["kind.proxy"] = "Proxy ayarları",

            ["privacy.title"] = "Gizlilik ve Kullanım Koşulları",
            ["privacy.welcome"] = "Netzy'ye hoş geldiniz",
            ["privacy.body"] =
                "GİZLİLİK (SIFIR VERİ TOPLAMA)\n" +
                "• Netzy hiçbir kişisel veri, IP adresi, donanım kimliği veya kullanım istatistiği toplamaz, saklamaz ve göndermez.\n" +
                "• İnternet yalnızca şunlar için kullanılır: bağlantı göstergesi (1.1.1.1 / 8.8.8.8 adreslerine kısa bağlantı), teşhis (Microsoft'un bağlantı testi msftconnecttest.com ve bir DNS sorgusu) ve GitHub'daki yeni sürüm kontrolü. Bu bağlantılarda hiçbir kişisel veri gönderilmez.\n" +
                "• Ayarlar, raporlar ve yedekler yalnızca bu bilgisayarda (%LOCALAPPDATA%\\Netzy) tutulur.\n\n" +
                "AÇIK KAYNAK (MIT LİSANSI)\n" +
                "• Kaynak kodunun tamamı GitHub'da incelenebilir: github.com/furkanyasarr0/Netzy\n\n" +
                "KULLANIM KOŞULLARI\n" +
                "1. Ağ ayarlarını değiştirmek için yönetici izni gerekir.\n" +
                "2. Onarım sırasında internet birkaç saniye kesilip yeniden bağlanır.\n" +
                "3. Derin Onarım güvenlik duvarı, IP/DNS ve proxy ayarlarını önce yedekler; \"Geri Al\" ile eski haline dönebilirsiniz.\n" +
                "4. Netzy olduğu gibi sunulur; kullanım sorumluluğu kullanıcıya aittir.",

            ["cli.needadmin"] = "Bu komut yönetici yetkisi gerektirir. Komut istemini yönetici olarak açın.",
            ["cli.log"] = "Rapor: {0}",
            ["cli.usage"] =
                "Netzy {0} — Windows ağ onarım aracı\n\n" +
                "Kullanım: Netzy.exe [komut] [--lang tr|en]\n\n" +
                "  --diagnose              Bağlantıyı teşhis et (değişiklik yapmaz)\n" +
                "  --quick                 Hızlı Onarım\n" +
                "  --deep                  Derin Onarım (önce yedek alır)\n" +
                "  --dns <seçim>           DNS sunucusu: auto, cloudflare, google, quad9\n" +
                "  --undo                  Son yedekleri geri yükle\n" +
                "  --help                  Bu yardım\n\n" +
                "Çıkış kodu: 0 başarılı, 1 hata/sorun bulundu, 2 hatalı kullanım, 3 yönetici yetkisi yok.\n" +
                "Betiklerde çıkış kodunu beklemek için: start /wait Netzy.exe --quick",
        };

        internal static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["net.checking"] = "Checking...",
            ["net.connected"] = "Online • {0} ms",
            ["net.offline"] = "Offline",

            ["btn.operations"] = "Steps",
            ["btn.report"] = "Report",
            ["btn.privacy"] = "Privacy",
            ["btn.download"] = "Download",
            ["btn.rediagnose"] = "Diagnose again",
            ["btn.quick"] = "⚡ Quick Repair",
            ["btn.deep"] = "Deep Repair",
            ["btn.undo"] = "↺ Undo",
            ["btn.close"] = "Close",
            ["btn.copy"] = "Copy report",
            ["btn.copied"] = "Copied ✓",
            ["btn.openfolder"] = "Open folder",
            ["btn.yes"] = "Yes",
            ["btn.no"] = "No",
            ["btn.ok"] = "OK",
            ["btn.accept"] = "I agree, let's start",

            ["diag.title"] = "Connection Check",
            ["diag.running"] = "Checking the connection...",
            ["diag.adapter"] = "Adapter",
            ["diag.gateway"] = "Router",
            ["diag.dns"] = "DNS",
            ["diag.internet"] = "Internet",
            ["diag.none"] = "Not connected",
            ["diag.noip"] = "No IP address",
            ["diag.unreachable"] = "Unreachable",
            ["diag.fail"] = "Failed",
            ["diag.captive"] = "Sign-in page",
            ["diag.blocked"] = "Web blocked",

            ["advice.healthy"] = "The connection looks healthy. No repair needed.",
            ["advice.checkcable"] = "No network adapter is connected. Check the cable or Wi-Fi connection.",
            ["advice.quickip"] = "This PC could not get an IP address. Quick Repair is recommended.",
            ["advice.quickproxy"] = "An active proxy setting may be blocking web traffic. Quick Repair is recommended.",
            ["advice.quickdns"] = "Domain names cannot be resolved (DNS). Quick Repair is recommended; if it persists, change the DNS server below.",
            ["advice.captive"] = "This network wants a sign-in page (hotel/café Wi-Fi). Open any website in your browser.",
            ["advice.router"] = "The router cannot be reached. Turn it off and on again; if that does not help, try Deep Repair.",
            ["advice.isp"] = "The router answers but the internet cannot be reached. The problem may be at your provider; you can try Deep Repair.",
            ["advice.deep"] = "Basic connectivity works but web traffic is blocked. Deep Repair is recommended.",

            ["quick.desc"] = "Clears the DNS, ARP and NetBIOS caches, renews the IP address and resets the proxy. Changes nothing permanently, no restart needed.",
            ["deep.desc"] = "Quick Repair plus a reset of Winsock, TCP/IP and the firewall, and a restart of the adapters. Settings are backed up first.",

            ["step.backup"] = "Back up settings",
            ["step.backup.desc"] = "Settings that will change (firewall, IP/DNS, proxy) are backed up first. If the backup fails, nothing is changed.",
            ["step.winsock"] = "Reset Winsock",
            ["step.winsock.desc"] = "Restores the network socket library to its defaults.",
            ["step.tcpip"] = "Reset TCP/IP",
            ["step.tcpip.desc"] = "Resets the IPv4 and IPv6 stack. Manually entered static IP settings may be removed (restorable from backup).",
            ["step.firewall"] = "Reset the firewall",
            ["step.firewall.desc"] = "Restores the default firewall rules (restorable from backup).",
            ["step.proxy"] = "Reset proxy settings",
            ["step.proxy.desc"] = "Turns off the browser and system proxy. A proxy left behind by a VPN or malware causes \"connected but no internet\".",
            ["step.dhcp"] = "Restart the DHCP client service",
            ["step.dhcp.desc"] = "Restarts the service that gets an IP address automatically.",
            ["step.adapters"] = "Restart network adapters",
            ["step.adapters.desc"] = "Turns the adapters that connect to the internet off and on again.",
            ["step.arp"] = "Clear the ARP cache",
            ["step.arp.desc"] = "Clears the IP-to-hardware address table.",
            ["step.netbios"] = "Refresh the NetBIOS cache",
            ["step.netbios.desc"] = "Clears the NetBIOS name cache and refreshes registrations.",
            ["step.renew"] = "Renew the IP address",
            ["step.renew.desc"] = "Releases and renews the IP address on DHCP adapters (ipconfig /release + /renew).",
            ["step.dns"] = "Clear the DNS cache",
            ["step.dns.desc"] = "Clears the DNS resolver cache and refreshes registrations.",
            ["step.temp"] = "Clean temporary files",
            ["step.temp.desc"] = "Deletes files older than 24 hours in %TEMP% and Windows\\Temp. Files of running programs are left alone.",

            ["detail.aborted"] = "skipped (backup failed)",
            ["detail.backupfail"] = "backup failed, no settings were changed",
            ["detail.proxyoff"] = "active proxy turned off",
            ["detail.skipped"] = "service cannot be stopped, skipped",
            ["detail.noadapter"] = "no connected adapter, skipped",
            ["detail.staticip"] = "static IP in use, skipped",
            ["detail.cleaned"] = "{0:0.0} MB cleaned",

            ["status.ready"] = "Ready.",
            ["status.percent"] = "{0}%",
            ["status.running"] = "[{0}/{1}] {2}...",
            ["status.step"] = "[{0}/{1}] {2}: {3}",
            ["status.ok"] = "done",
            ["status.error"] = "Error",
            ["status.done"] = "Finished: {0}/{1} steps succeeded.",
            ["result.summary"] = "{0}/{1} steps succeeded.",
            ["result.metrics"] = "Latency: {0} → {1}   ·   DNS: {2} → {3}",

            ["dns.auto"] = "Automatic",
            ["dns.applying"] = "Changing the DNS server...",
            ["dns.done"] = "DNS server changed.",
            ["dns.failed"] = "Could not change the DNS server. See the Report for details.",
            ["dns.confirm.title"] = "Change DNS server",
            ["dns.confirm.body"] = "The adapters that connect to the internet will use the {0} DNS server.\n\nCurrent settings are backed up first; you can go back with \"Undo\". Continue?",

            ["undo.confirm.title"] = "Undo",
            ["undo.confirm.body"] = "These settings will return to how they were before the last change:\n\n{0}\n\nContinue?",
            ["undo.done"] = "Undo finished.",
            ["undo.failed"] = "Some settings could not be restored. See the Report for details.",
            ["undo.none"] = "There is no backup to restore.",

            ["confirm.deep.title"] = "Deep Repair",
            ["confirm.deep.body"] = "• Firewall rules return to their defaults.\n• TCP/IP is reset; manually entered static IP/DNS settings may be removed.\n• The internet connection drops for a few seconds.\n\nAll of these settings are backed up first and can be restored with \"Undo\". Continue?",
            ["admin.title"] = "Administrator rights needed",
            ["admin.body"] = "This changes Windows network settings, so it needs administrator rights.\n\nRestart Netzy as administrator?",
            ["admin.denied"] = "Administrator rights were not granted, so nothing was started.",
            ["done.title"] = "Repair finished",
            ["done.deepnote"] = "Restart your computer so the Winsock and TCP/IP resets fully take effect.",

            ["report.title"] = "Last run report",
            ["report.empty"] = "Nothing has been run yet.",
            ["ops.title"] = "Repair steps",
            ["ops.quick"] = "⚡ Quick Repair",
            ["ops.deep"] = "Deep Repair",
            ["update.available"] = "A new Netzy version is available ({0})",
            ["error.title"] = "Unexpected error",

            ["kind.firewall"] = "Firewall rules",
            ["kind.interfaces"] = "IP and DNS settings",
            ["kind.proxy"] = "Proxy settings",

            ["privacy.title"] = "Privacy and Terms of Use",
            ["privacy.welcome"] = "Welcome to Netzy",
            ["privacy.body"] =
                "PRIVACY (ZERO DATA COLLECTION)\n" +
                "• Netzy does not collect, store or send any personal data, IP address, hardware ID or usage statistics.\n" +
                "• The internet is used only for: the connection badge (a short connection to 1.1.1.1 / 8.8.8.8), diagnosis (Microsoft's connection test msftconnecttest.com and one DNS query) and the new-version check on GitHub. No personal data is sent in these connections.\n" +
                "• Settings, reports and backups stay on this computer only (%LOCALAPPDATA%\\Netzy).\n\n" +
                "OPEN SOURCE (MIT LICENSE)\n" +
                "• All source code can be reviewed on GitHub: github.com/furkanyasarr0/Netzy\n\n" +
                "TERMS OF USE\n" +
                "1. Changing network settings requires administrator rights.\n" +
                "2. During a repair the internet drops for a few seconds and reconnects.\n" +
                "3. Deep Repair backs up the firewall, IP/DNS and proxy settings first; you can go back with \"Undo\".\n" +
                "4. Netzy is provided as is; you use it at your own responsibility.",

            ["cli.needadmin"] = "This command needs administrator rights. Open the command prompt as administrator.",
            ["cli.log"] = "Report: {0}",
            ["cli.usage"] =
                "Netzy {0} — Windows network repair tool\n\n" +
                "Usage: Netzy.exe [command] [--lang tr|en]\n\n" +
                "  --diagnose              Check the connection (changes nothing)\n" +
                "  --quick                 Quick Repair\n" +
                "  --deep                  Deep Repair (backs up first)\n" +
                "  --dns <choice>          DNS server: auto, cloudflare, google, quad9\n" +
                "  --undo                  Restore the latest backups\n" +
                "  --help                  This help\n\n" +
                "Exit code: 0 success, 1 error/problem found, 2 bad usage, 3 not administrator.\n" +
                "To wait for the exit code in scripts: start /wait Netzy.exe --quick",
        };
    }

    internal static class Format
    {
        public static string Metrics(Metrics before, Metrics after) =>
            Loc.T("result.metrics", Ms(before?.LatencyMs), Ms(after?.LatencyMs), Ms(before?.DnsMs), Ms(after?.DnsMs));

        public static string Ms(int? v) => v == null ? "—" : v < 1 ? "<1 ms" : v + " ms";
    }
}

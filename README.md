# Netzy

Windows 10 ve 11 için açık kaynaklı ağ teşhis ve onarım aracı. Önce sorunu bulur, sonra yalnızca gerekeni onarır.

[![Build](https://github.com/furkanyasarr0/Netzy/actions/workflows/build.yml/badge.svg)](https://github.com/furkanyasarr0/Netzy/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Privacy: Zero Data Collection](https://img.shields.io/badge/Privacy-Zero%20Data%20Collection-success.svg)](#-gizlilik)
[![Platform: Windows 10/11](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6.svg)](#-gereksinimler)

---

## ✨ Özellikler

- **Bağlantı teşhisi:** Açılışta bağdaştırıcı, modem, DNS ve internet erişimini kontrol eder; sorunun nerede olduğunu ve hangi onarımın gerektiğini söyler. Otel/kafe Wi-Fi giriş sayfalarını ve etkin proxy ayarlarını da tanır.
- **⚡ Hızlı Onarım:** Hiçbir ayarı kalıcı bozmadan çoğu günlük sorunu çözer, yeniden başlatma gerektirmez.
- **Derin Onarım:** Winsock, TCP/IP ve güvenlik duvarını sıfırlar, bağdaştırıcıları yeniden başlatır. Önce yedek alır.
- **↺ Geri Al:** Güvenlik duvarı kuralları, IP/DNS ayarları ve proxy ayarları değiştirilmeden önce yedeklenir. Tek tıkla son değişiklikten önceki haline döner.
- **Önce/sonra sonucu:** Onarımdan önce ve sonra gecikme ile DNS süresini ölçer (ör. `Gecikme: 140 ms → 32 ms`).
- **DNS seçimi:** Otomatik, Cloudflare, Google veya Quad9 arasında tek tıkla geçiş.
- **Rapor:** Her işlemin komut komut kaydı; "Raporu kopyala" ile GitHub issue'suna yapıştırılabilir.
- **Komut satırı modu:** BT yöneticileri ve betikler için (`Netzy.exe --quick`).
- **Türkçe / English** arayüz.

## 📋 Onarım Adımları

| Adım | Hızlı | Derin |
|---|:---:|:---:|
| Ayarları yedekle (yedek alınamazsa hiçbir şey değiştirilmez) | ✓ | ✓ |
| Winsock sıfırlama (`netsh winsock reset`) | | ✓ |
| TCP/IP sıfırlama (`netsh int ip reset`, `netsh int ipv6 reset`) | | ✓ |
| Güvenlik duvarını sıfırla (`netsh advfirewall reset`) | | ✓ |
| Proxy ayarlarını sıfırla (tarayıcı + `netsh winhttp reset proxy`) | ✓ | ✓ |
| DHCP istemci servisini yeniden başlat | | ✓ |
| Ağ bağdaştırıcılarını yeniden başlat | | ✓ |
| ARP önbelleğini temizle (`netsh interface ip delete arpcache`) | ✓ | ✓ |
| NetBIOS önbelleğini yenile (`nbtstat -R`, `-RR`) | ✓ | ✓ |
| IP adresini yenile (`ipconfig /release` + `/renew`, yalnızca DHCP) | ✓ | ✓ |
| DNS önbelleğini temizle (`ipconfig /flushdns`, `/registerdns`) | ✓ | ✓ |
| Geçici dosyaları temizle (24 saatten eski, junction'ları izlemez) | | ✓ |

Derin Onarım'dan sonra Winsock ve TCP/IP sıfırlamasının tamamen devreye girmesi için bilgisayarı yeniden başlatmanız önerilir.

## 💻 Komut Satırı

Yönetici olarak açılmış bir komut isteminden:

```bat
Netzy.exe --diagnose            :: teşhis (değişiklik yapmaz)
Netzy.exe --quick               :: Hızlı Onarım
Netzy.exe --deep                :: Derin Onarım
Netzy.exe --dns cloudflare      :: auto | cloudflare | google | quad9
Netzy.exe --undo                :: son yedekleri geri yükle
Netzy.exe --help --lang en
```

Çıkış kodu: `0` başarılı, `1` hata/sorun bulundu, `2` hatalı kullanım, `3` yönetici yetkisi yok. Betiklerde çıkış kodunu beklemek için `start /wait Netzy.exe --quick` kullanın.

## 🔒 Gizlilik

- Netzy hiçbir kişisel veri, IP adresi, donanım kimliği veya kullanım istatistiği **toplamaz, saklamaz ve göndermez**.
- İnternet yalnızca şunlar için kullanılır: bağlantı göstergesi (1.1.1.1 / 8.8.8.8'e kısa bağlantı), teşhis (Microsoft'un bağlantı testi `msftconnecttest.com` ve bir DNS sorgusu) ve GitHub'daki yeni sürüm kontrolü.
- Ayarlar, raporlar ve yedekler yalnızca bilgisayarınızda tutulur: `%LOCALAPPDATA%\Netzy`.

## 🛡️ Güvenlik ve Antivirüs

Netzy, antivirüslerin yanlış alarm vermemesi için şöyle tasarlandı:

- **Yerel .NET uygulaması:** Python/PyInstaller gibi kendini `%TEMP%`'e açan bir paketleyici yok. Tek, küçük (~200 KB) bir EXE.
- **Kabuk yok:** `cmd.exe` veya PowerShell çağrılmaz. Yalnızca Windows'un kendi imzalı araçları (`netsh`, `ipconfig`, `nbtstat`) `System32` içindeki **tam yoluyla** çalıştırılır; teşhis Windows API'leriyle yapılır.
- **İndirip çalıştırma yok:** Uygulama hiçbir zaman kod indirmez veya güncellemeyi kendisi kurmaz; yalnızca Release sayfasını açar.
- **Sürüm bilgisi ve tekrarlanabilir derleme:** EXE'de yayıncı/sürüm bilgisi vardır; her sürüm GitHub Actions'ta derlenir ve `Netzy.exe.sha256` ile yayınlanır.

**SmartScreen hakkında:** İmzasız ve yeni bir EXE indirildiğinde Windows SmartScreen "Windows bilgisayarınızı korudu" uyarısını hangi dilde yazılmış olursa olsun gösterir. Bu uyarıyı kaldırmanın tek yolu kod imzalamadır (aşağıya bakın). İmza gelene kadar: **Ek bilgi → Yine de çalıştır**. İndirdiğiniz dosyayı doğrulamak için:

```bat
certutil -hashfile Netzy.exe SHA256
```

Çıkan değer, Release sayfasındaki `Netzy.exe.sha256` ile aynı olmalıdır.

## 🚀 Gereksinimler ve Kurulum

- Windows 10 (1903 veya üstü) ya da Windows 11. Gerekli .NET Framework 4.8 bu sürümlerde **hazır gelir**; ek kurulum yok.
- [Releases](https://github.com/furkanyasarr0/Netzy/releases/latest) sayfasından `Netzy.exe`'yi indirip çalıştırın. Kurulum gerekmez (taşınabilir).

## 🔧 Geliştirme

Gereken: [.NET SDK](https://dotnet.microsoft.com/download) (8 veya üstü).

```bat
build.bat
```

Testleri çalıştırır ve `dist\Netzy.exe` + `dist\Netzy.exe.sha256` üretir. Elle:

```bat
dotnet test Netzy.sln
dotnet build src\Netzy\Netzy.csproj -c Release
```

Proje yapısı:

```
src/Netzy/            WPF uygulaması (.NET Framework 4.8)
  Core/               Arayüzden bağımsız mantık: komut çalıştırma, teşhis, onarım adımları, yedek, DNS, CLI, metinler
  Ui/                 İletişim pencereleri
  MainWindow.xaml     Ana pencere
tests/Netzy.Tests/    xUnit testleri
.github/workflows/    Derleme, test, imzalama ve Release
```

### Sürüm yayınlama

```bat
git tag v2.0.0
git push origin v2.0.0
```

GitHub Actions testleri çalıştırır, sürümü etiketten alarak derler, (yapılandırıldıysa) imzalar ve `Netzy.exe` ile `Netzy.exe.sha256`'yı Release'e yükler. Uygulamadaki güncelleme bildirimi de bu etiketi kullanır.

### Kod imzalama

SmartScreen uyarısını kaldırmak için EXE'nin imzalanması gerekir. Açık kaynak projeler için ücretsiz seçenek: [SignPath Foundation](https://signpath.org) (başvuru gerekir). Onaylandıktan sonra depo ayarlarına şunları ekleyin; iş akışı imzalamayı kendiliğinden yapar:

- **Secrets:** `SIGNPATH_API_TOKEN`
- **Variables:** `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_POLICY_SLUG`

Ücretli alternatif: Azure Artifact Signing (eski adıyla Trusted Signing). İmzalı EXE'ler de SmartScreen itibarını indirme sayısıyla kazanır; ilk günlerde uyarı yine görülebilir.

## 📄 Lisans

[MIT Lisansı](LICENSE) — GitHub: [furkanyasarr0/Netzy](https://github.com/furkanyasarr0/Netzy)

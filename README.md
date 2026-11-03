# Netzy

Windows 10 ve 11 için geliştirilmiş modern, hafif ve açık kaynaklı ağ onarım ve sistem optimizasyon aracı.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Privacy: Zero Data Collection](https://img.shields.io/badge/Privacy-Zero%20Data%20Collection-success.svg)](#-gizlilik-ve-veri-politikası)
[![Platform: Windows 10/11](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6.svg)](https://microsoft.com)

---

## ⚡ Temiz ve Sade Kullanıcı Deneyimi

Netzy, ana sayfasında kalabalık listeler veya karmaşık menüler barındırmaz:
- **Ana Sayfa:** Sadece şık ve odaklanmış **⚡ BAŞLAT** butonu ve canlı ilerleme çubuğu yer alır.
- **İşlemler Butonu (`📋 İşlemler`):** Yürütülecek 9 temel sistem ve ağ işlemini kategorize edilmiş halde ayrı bir pencerede listeler.
- **Canlı Ağ Takibi:** Gecikme süresini (ping ms) ve internet durumunu arka planda donma yapmadan gösterir.
- **Modern "N" Logosu:** Sade, teknolojik ve şık Windows arayüz simgesi.

---

## 📋 Kategorilere Göre Yürütülen İşlemler

### 1. 🌐 Ağ & Protokol Sıfırlama
- **Winsock Sıfırlama (`netsh winsock reset`):** Ağ soket kütüphanesini fabrika ayarlarına döndürür.
- **TCP/IP Stack Sıfırlama (`netsh int ip reset`):** IPv4 ve IPv6 protokol yığınını sıfırlar ve yönlendirmeleri düzeltir.
- **Ağ Adaptörlerini Yeniden Başlatma (`Restart-NetAdapter`):** Aktif bağdaştırıcıları temiz bir şekilde devre dışı bırakıp tekrar başlatır.

### 2. 🛡️ Güvenlik & Sistem Servisleri
- **Windows Güvenlik Duvarını Sıfırla (`netsh advfirewall reset`):** Güvenlik duvarı kurallarını varsayılan ayarlara döndürür.
- **DNS İstemci Servisini Yenile (`ipconfig /flushdns`):** DNS önbelleğini temizler ve kayıtları yeniler.
- **DHCP İstemci Servisini Yeniden Başlat (`Restart-Service Dhcp`):** Otomatik IP alma servisini sıfırlar.

### 3. 🧹 Önbellek & Sistem Temizliği
- **ARP Cache Temizliği (`netsh interface ip delete arpcache`):** ARP adres eşleme tablosunu temizler.
- **NetBIOS Önbelleğini Temizle (`nbtstat -R` & `-RR`):** Ad önbelleğini tazeleyip LMHOSTS'u yeniden yükler.
- **Geçici Dosya Temizliği (%TEMP%, Temp, Prefetch):** Kilitli olmayan gereksiz sistem ve uygulama geçici dosyalarını güvenle silerek depolama alanı açar.

---

## 🔒 Gizlilik ve Veri Politikası (Sıfır Veri)

Netzy, kullanıcı gizliliğine tam saygı duyar:
- **Sıfır Veri Toplama:** Uygulama hiçbir kişisel bilgi, IP adresi, donanım kimliği veya kullanım istatistiği **TOPLAMAZ**, **KAYDETMEZ** ve **İŞLEMEZ**.
- **Tamamen Yerel (Offline):** Tüm onarım ve sıfırlama adımları tamamen kendi bilgisayarınızda çalışır. Harici hiçbir sunucuya veri aktarılmaz.
- **Şeffaf & Açık Kaynak:** %100 açık kaynak kodludur, herkes tarafından GitHub üzerinde incelenebilir.

---

## 🚀 Başlatma

1. **Yönetici Olarak Çalıştırma (Önerilen):**
   `Netzy.bat` dosyasına çift tıklayarak doğrudan yönetici yetkisiyle açabilirsiniz.

2. **Komut Satırından:**
   ```bash
   pip install -r requirements.txt
   python main.py
   ```

---

## 📄 Lisans

Bu proje [MIT Lisansı](LICENSE) ile lisanslanmıştır.
GitHub Repository: [https://github.com/furkanyasarr0/Netzy](https://github.com/furkanyasarr0/Netzy)

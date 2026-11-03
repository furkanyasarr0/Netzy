# Netzy

Windows 10 ve 11 için geliştirilmiş modern, hızlı ve açık kaynaklı ağ sıfırlama, bağlantı onarım ve sistem optimizasyon aracı.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Platform: Windows 10/11](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6.svg)](https://microsoft.com)

---

## ⚡ Özellikler

- **Winsock Sıfırlama:** Ağ soket kütüphanesini (`netsh winsock reset`) fabrika ayarlarına döndürür.
- **TCP/IP Protokol Yığını Sıfırlama:** Çekirdek IPv4 ve IPv6 yığınlarını ve yönlendirme tablolarını (`netsh int ip reset`) sıfırlar.
- **Windows Güvenlik Duvarı Sıfırlama:** Güvenlik duvarı kurallarını varsayılan ayarlara (`netsh advfirewall reset`) döndürür.
- **ARP Cache Temizliği:** Fiziksel adres eşleme ve ARP tablosunu (`netsh interface ip delete arpcache`) temizler.
- **DNS İstemci Servisi ve Önbelleği:** DNS önbelleğini temizler (`ipconfig /flushdns`) ve DNS kayıtlarını yeniler.
- **DHCP İstemci Servisi:** IP adresi alma servisini yeniden başlatır.
- **Ağ Adaptörlerini Yeniden Başlatma:** Aktif tüm ağ adaptörlerini hızlıca devre dışı bırakıp yeniden etkinleştirir.
- **NetBIOS Önbellek Temizliği:** NetBIOS ad önbelleğini temizler (`nbtstat -R` & `nbtstat -RR`).
- **IP Yenileme (Release / Renew):** DHCP üzerinden IP bırakıp yeniden talep eder.
- **WinHTTP Proxy Sıfırlama:** Proxy ayarlarını doğrudan bağlantıya sıfırlar.
- **Geçici Dosya Temizliği (Temp Cleanup):** `%TEMP%`, `C:\Windows\Temp` ve `Prefetch` dizinlerindeki gereksiz dosyaları temizleyerek depolama alanı açar.
- **Canlı Ağ Takibi:** Gecikme süresini (ping ms) ve internet bağlantısını anlık olarak gösterir.
- **GitHub Sürüm Takipçisi:** Yeni bir sürüm çıktığında kullanıcıyı otomatik bilgilendirir.
- **Açık Kaynak & MIT Lisansı:** Tamamen şeffaf ve güvenli.

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

Bu proje [MIT Lisansı](LICENSE) ile lisanslanmıştır. Detaylar için `LICENSE` dosyasına göz atabilirsiniz.
Repository: [https://github.com/furkanyasarr0/Netzy](https://github.com/furkanyasarr0/Netzy)

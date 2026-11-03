"""
Netzy - Windows Ağ Sıfırlama ve Sistem Onarım Aracı
Ana sayfada sadece Başlat butonu ve ilerleme çubuğu yer alır.
İşlemler ayrı bir butonla açılan sade kategori görünümündedir.
Yeni "N" logolu, sıfır veri toplamalı ve açık kaynak lisanslıdır.
"""

import os
import sys
import json
import time
import socket
import ctypes
import webbrowser
import threading
import subprocess
import urllib.request
import customtkinter as ctk
from PIL import Image
from tkinter import messagebox

# Çalışma dizinini sabitle
APP_DIR = os.path.dirname(os.path.abspath(__file__))
os.chdir(APP_DIR)

CURRENT_VERSION = "v1.0.0"
GITHUB_REPO_URL = "https://github.com/furkanyasarr0/Netzy"
GITHUB_API_URL = "https://api.github.com/repos/furkanyasarr0/Netzy/releases/latest"
CONFIG_FILE = os.path.join(APP_DIR, "config.json")
LOGO_PATH = os.path.join(APP_DIR, "logo.png")
ICO_PATH = os.path.join(APP_DIR, "logo.ico")

# Windows konsol OEM kod sayfası (Türkçe için cp857)
OEM_CODEPAGE = f"cp{ctypes.windll.kernel32.GetOEMCP()}" if hasattr(ctypes, "windll") else "utf-8"

# Kategorize edilmiş 9 işlem
CATEGORIZED_OPERATIONS = [
    {
        "category": "🌐 Ağ & Protokol Sıfırlama",
        "items": [
            {
                "id": "winsock",
                "name": "Winsock Sıfırlama",
                "cmd_label": "netsh winsock reset",
                "desc": "Ağ soket kütüphanesini fabrika ayarlarına döndürür.",
                "type": "cmd",
                "cmd": "netsh winsock reset",
            },
            {
                "id": "tcpip",
                "name": "TCP/IP Stack Sıfırlama",
                "cmd_label": "netsh int ip reset",
                "desc": "TCP/IP (IPv4 ve IPv6) protokol yığınını sıfırlar ve yönlendirmeleri düzeltir.",
                "type": "cmd",
                "cmd": "netsh int ip reset",
                "extra_cmds": ["netsh int ipv4 reset", "netsh int ipv6 reset"],
            },
            {
                "id": "adapter",
                "name": "Ağ Adaptörlerini Yeniden Başlatma",
                "cmd_label": "Restart-NetAdapter",
                "desc": "Aktif ağ bağdaştırıcılarını devre dışı bırakıp temiz bir şekilde yeniden başlatır.",
                "type": "cmd",
                "cmd": 'powershell -Command "Get-NetAdapter | Where-Object Status -eq \'Up\' | Restart-NetAdapter -Confirm:$false"',
            },
        ],
    },
    {
        "category": "🛡️ Güvenlik & Sistem Servisleri",
        "items": [
            {
                "id": "firewall",
                "name": "Windows Güvenlik Duvarını Sıfırla",
                "cmd_label": "netsh advfirewall reset",
                "desc": "Güvenlik duvarı kurallarını varsayılan ayarlara döndürür.",
                "type": "cmd",
                "cmd": "netsh advfirewall reset",
            },
            {
                "id": "dns",
                "name": "DNS İstemci Servisini Yenile",
                "cmd_label": "ipconfig /flushdns",
                "desc": "DNS çözümleme önbelleğini temizler ve kayıtları yeniler.",
                "type": "cmd",
                "cmd": "ipconfig /flushdns",
                "extra_cmds": ['powershell -Command "Clear-DnsClientCache"', "ipconfig /registerdns"],
            },
            {
                "id": "dhcp",
                "name": "DHCP İstemci Servisini Yeniden Başlat",
                "cmd_label": "Restart-Service Dhcp",
                "desc": "Otomatik IP adresi alma servisini (DHCP) yeniden başlatır.",
                "type": "cmd",
                "cmd": 'powershell -Command "Restart-Service -Name Dhcp -Force -ErrorAction SilentlyContinue"',
            },
        ],
    },
    {
        "category": "🧹 Önbellek & Sistem Temizliği",
        "items": [
            {
                "id": "arp",
                "name": "ARP Cache Temizliği",
                "cmd_label": "netsh interface ip delete arpcache",
                "desc": "ARP tablosunu ve IP-fiziksel adres eşleme önbelleğini temizler.",
                "type": "cmd",
                "cmd": "netsh interface ip delete arpcache",
            },
            {
                "id": "netbios",
                "name": "NetBIOS Önbelleğini Temizle",
                "cmd_label": "nbtstat -R & -RR",
                "desc": "NetBIOS ad önbelleğini temizler ve LMHOSTS dosyasını yeniden yükler.",
                "type": "cmd",
                "cmd": "nbtstat -R",
                "extra_cmds": ["nbtstat -RR"],
            },
            {
                "id": "temp",
                "name": "Geçici Dosyaları Temizle (Temp Cleanup)",
                "cmd_label": "%TEMP%, Temp, Prefetch",
                "desc": "%TEMP%, Windows Temp ve Prefetch geçici sistem dosyalarını temizler.",
                "type": "python",
                "handler": "clean_temp_files",
            },
        ],
    },
]

# Düz sıralı işlem listesi
FLAT_OPERATIONS = [item for cat in CATEGORIZED_OPERATIONS for item in cat["items"]]


def is_admin() -> bool:
    """Yönetici haklarını denetler."""
    try:
        return ctypes.windll.shell32.IsUserAnAdmin() != 0
    except Exception:
        return False


def request_admin_elevation():
    """Uygulamayı UAC penceresiyle yönetici olarak yeniden başlatır."""
    if not is_admin():
        try:
            script_path = os.path.abspath(sys.argv[0] if sys.argv else __file__)
            work_dir = os.path.dirname(script_path)
            ctypes.windll.shell32.ShellExecuteW(
                None, "runas", sys.executable, f'"{script_path}"', work_dir, 1
            )
            sys.exit(0)
        except Exception as e:
            messagebox.showerror("Hata", f"Yönetici olarak başlatılamadı:\n{e}")


def load_config() -> dict:
    if os.path.exists(CONFIG_FILE):
        try:
            with open(CONFIG_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            pass
    return {"terms_accepted": False, "version": CURRENT_VERSION}


def save_config(cfg: dict):
    try:
        with open(CONFIG_FILE, "w", encoding="utf-8") as f:
            json.dump(cfg, f, indent=2, ensure_ascii=False)
    except Exception:
        pass


def clean_temp_files():
    """Geçici dosyaları siler ve serbest bırakılan boyutu hesaplar."""
    targets = [
        os.environ.get("TEMP"),
        os.path.join(os.environ.get("SystemRoot", "C:\\Windows"), "Temp"),
        os.path.join(os.environ.get("SystemRoot", "C:\\Windows"), "Prefetch"),
    ]
    deleted_files = 0
    deleted_bytes = 0

    for folder in targets:
        if not folder or not os.path.exists(folder):
            continue
        for root, dirs, files in os.walk(folder, topdown=False):
            for f in files:
                filepath = os.path.join(root, f)
                try:
                    size = os.path.getsize(filepath)
                    os.remove(filepath)
                    deleted_files += 1
                    deleted_bytes += size
                except Exception:
                    pass
            for d in dirs:
                try:
                    os.rmdir(os.path.join(root, d))
                except Exception:
                    pass

    mb = deleted_bytes / (1024 * 1024)
    return deleted_files, mb


class PrivacyTermsModal(ctk.CTkToplevel):
    """Kullanım Koşulları ve Gizlilik Politikası Penceresi (Sıfır Veri & Open Source Vurgulu)."""

    def __init__(self, parent, is_first_run=False, on_accept=None):
        super().__init__(parent)
        self.on_accept = on_accept
        self.is_first_run = is_first_run
        self.title("Netzy — Gizlilik Politikası ve Kullanım Koşulları")
        self.geometry("580x520")
        self.minsize(540, 480)
        self.configure(fg_color="#0D1525")

        self.transient(parent)
        self.grab_set()

        # Header
        self.header_frame = ctk.CTkFrame(self, fg_color="transparent")
        self.header_frame.pack(fill="x", padx=24, pady=(20, 10))

        if os.path.exists(LOGO_PATH):
            try:
                logo_pil = Image.open(LOGO_PATH)
                self.logo_img = ctk.CTkImage(light_image=logo_pil, dark_image=logo_pil, size=(40, 40))
                self.logo_lbl = ctk.CTkLabel(self.header_frame, image=self.logo_img, text="")
                self.logo_lbl.pack(side="left", padx=(0, 12))
            except Exception:
                pass

        title_text = "Netzy'ye Hoş Geldiniz" if is_first_run else "Gizlilik Politikası & Koşullar"
        self.title_lbl = ctk.CTkLabel(
            self.header_frame,
            text=title_text,
            font=ctk.CTkFont(family="Segoe UI", size=20, weight="bold"),
            text_color="#F8FAFC",
        )
        self.title_lbl.pack(side="left")

        privacy_text = (
            "🔒 GİZLİLİK POLİTİKASI (SIFIR VERİ TOPLAMA)\n"
            "--------------------------------------------------\n"
            "• Netzy, kullanıcı gizliliğine tam saygı duyar. Uygulamamız kesinlikle hiçbir kişisel veri, "
            "IP adresi, donanım kimliği, ağ trafiği veya kullanım istatistiği TOPLAMAZ, SAKLAMAZ ve İŞLEMEZ.\n"
            "• Harici hiçbir sunucuya veri aktarımı yapılmaz. Uygulama tamamen yerel (local) olarak sizin "
            "bilgisayarınızda çalışır.\n"
            "• İnternet bağlantısı sadece sol üstteki anlık ping gecikme göstergesi ve GitHub üzerindeki "
            "yeni sürüm kontrolü için kullanılır; hiçbir veri dışarı sızdırılmaz.\n\n"
            "🌐 %100 AÇIK KAYNAK (OPEN SOURCE & MIT LİSANSI)\n"
            "--------------------------------------------------\n"
            "• Netzy tamamen şeffaf ve açık kaynaklı bir projedir.\n"
            "• Kaynak kodlarının tamamını dilediğiniz gibi GitHub üzerinden inceleyebilir, denetleyebilir "
            "veya katkıda bulunabilirsiniz:\n"
            "  https://github.com/furkanyasarr0/Netzy\n\n"
            "⚙️ KULLANIM KOŞULLARI VE HATIRLATMALAR\n"
            "--------------------------------------------------\n"
            "1. Yönetici Yetkisi: TCP/IP yığını, Winsock ve Güvenlik Duvarı gibi çekirdek düzeyindeki "
            "ayarların sıfırlanabilmesi için Yönetici (Administrator) izni gereklidir.\n"
            "2. Geçici Bağlantı Yenilenmesi: Ağ adaptörlerinin ve IP adresinin yenilenmesi esnasında "
            "internetiniz birkaç saniyeliğine kesilip otomatik olarak tekrar bağlanır.\n"
            "3. Sistem Yeniden Başlatma: Protokol sıfırlamalarının tam olarak devreye girmesi için "
            "işlemler bittiğinde bilgisayarınızı bir kez yeniden başlatmanız tavsiye edilir."
        )

        self.textbox = ctk.CTkTextbox(
            self,
            font=ctk.CTkFont(family="Segoe UI", size=12),
            fg_color="#142136",
            text_color="#CBD5E1",
            border_width=1,
            border_color="#1E2C44",
            corner_radius=8,
        )
        self.textbox.pack(fill="both", expand=True, padx=24, pady=(0, 14))
        self.textbox.insert("1.0", privacy_text)
        self.textbox.configure(state="disabled")

        self.btn_frame = ctk.CTkFrame(self, fg_color="transparent")
        self.btn_frame.pack(fill="x", padx=24, pady=(0, 20))

        btn_text = "Kabul Ediyorum ve Başla" if is_first_run else "Kapat"
        self.action_btn = ctk.CTkButton(
            self.btn_frame,
            text=btn_text,
            font=ctk.CTkFont(family="Segoe UI", size=14, weight="bold"),
            height=44,
            corner_radius=8,
            fg_color="#2563EB",
            hover_color="#1D4ED8",
            text_color="#FFFFFF",
            command=self._on_click,
        )
        self.action_btn.pack(fill="x")

    def _on_click(self):
        if self.is_first_run and self.on_accept:
            self.on_accept()
        self.destroy()


class OperationsWindow(ctk.CTkToplevel):
    """İşlemleri sade kategori grupları şeklinde gösteren pencere."""

    def __init__(self, parent):
        super().__init__(parent)
        self.title("Netzy — İşlem Listesi (Kategoriler)")
        self.geometry("560x540")
        self.minsize(520, 480)
        self.configure(fg_color="#0D1525")

        self.transient(parent)

        # Header
        header = ctk.CTkFrame(self, fg_color="transparent")
        header.pack(fill="x", padx=20, pady=(18, 10))

        title = ctk.CTkLabel(
            header,
            text="Yürütülecek 9 İşlem",
            font=ctk.CTkFont(family="Segoe UI", size=18, weight="bold"),
            text_color="#F8FAFC",
        )
        title.pack(side="left")

        sub = ctk.CTkLabel(
            header,
            text="(Başlatıldığında sırayla uygulanır)",
            font=ctk.CTkFont(family="Segoe UI", size=11),
            text_color="#64748B",
        )
        sub.pack(side="left", padx=(10, 0))

        # Kategorili Scrollable Frame
        scroll = ctk.CTkScrollableFrame(
            self,
            corner_radius=10,
            fg_color="#10192A",
            border_width=1,
            border_color="#1E2C44",
        )
        scroll.pack(fill="both", expand=True, padx=20, pady=(0, 16))

        for cat in CATEGORIZED_OPERATIONS:
            # Kategori Başlığı
            cat_header = ctk.CTkFrame(scroll, fg_color="transparent")
            cat_header.pack(fill="x", padx=4, pady=(12, 6))

            cat_title = ctk.CTkLabel(
                cat_header,
                text=cat["category"],
                font=ctk.CTkFont(family="Segoe UI", size=13, weight="bold"),
                text_color="#38BDF8",
            )
            cat_title.pack(side="left")

            # Kategori Kartları
            for item in cat["items"]:
                row = ctk.CTkFrame(
                    scroll,
                    corner_radius=6,
                    fg_color="#142136",
                    border_width=1,
                    border_color="#1E2C44",
                )
                row.pack(fill="x", padx=4, pady=3)

                row_inner = ctk.CTkFrame(row, fg_color="transparent")
                row_inner.pack(fill="x", padx=12, pady=8)

                top_line = ctk.CTkFrame(row_inner, fg_color="transparent")
                top_line.pack(fill="x")

                item_name = ctk.CTkLabel(
                    top_line,
                    text=item["name"],
                    font=ctk.CTkFont(family="Segoe UI", size=12, weight="bold"),
                    text_color="#F1F5F9",
                )
                item_name.pack(side="left")

                item_cmd = ctk.CTkLabel(
                    top_line,
                    text=item["cmd_label"],
                    font=ctk.CTkFont(family="Segoe UI", size=10),
                    text_color="#FDBA74",
                    fg_color="#7A2D0E",
                    corner_radius=4,
                    padx=6,
                    pady=1,
                )
                item_cmd.pack(side="right")

                item_desc = ctk.CTkLabel(
                    row_inner,
                    text=item["desc"],
                    font=ctk.CTkFont(family="Segoe UI", size=11),
                    text_color="#8295AD",
                    anchor="w",
                )
                item_desc.pack(anchor="w", pady=(2, 0))

        # Kapat Butonu
        close_btn = ctk.CTkButton(
            self,
            text="Kapat",
            font=ctk.CTkFont(family="Segoe UI", size=12, weight="bold"),
            height=36,
            corner_radius=6,
            fg_color="#1E293B",
            hover_color="#334155",
            text_color="#F8FAFC",
            command=self.destroy,
        )
        close_btn.pack(fill="x", padx=20, pady=(0, 16))


class NetzyApp(ctk.CTk):
    def __init__(self):
        super().__init__()

        # Temel Yapılandırma
        ctk.set_appearance_mode("dark")
        ctk.set_default_color_theme("blue")

        # Sade başlık: Netzy
        self.title("Netzy")
        self.geometry("540x380")
        self.minsize(500, 340)
        self.configure(fg_color="#0D1525")

        # Uygulama Simgesi (logo.ico)
        if os.path.exists(ICO_PATH):
            try:
                self.iconbitmap(ICO_PATH)
            except Exception:
                pass

        self.is_running = False
        self._stop_net_thread = False

        self._build_ui()
        self._start_network_monitor()
        self._check_version_background()
        self._check_first_launch()

    def _check_first_launch(self):
        cfg = load_config()
        if not cfg.get("terms_accepted", False):
            self.after(200, lambda: PrivacyTermsModal(self, is_first_run=True, on_accept=self._on_terms_accepted))

    def _on_terms_accepted(self):
        cfg = load_config()
        cfg["terms_accepted"] = True
        save_config(cfg)

    def _build_ui(self):
        self.main_container = ctk.CTkFrame(self, fg_color="transparent")
        self.main_container.pack(fill="both", expand=True, padx=24, pady=20)

        # ==========================================
        # 1. ÜST BAR (Header)
        # ==========================================
        self.header_frame = ctk.CTkFrame(self.main_container, fg_color="transparent")
        self.header_frame.pack(fill="x", pady=(0, 14))

        # Sol: Yeni N Logosu ve Netzy İsmi
        self.brand_frame = ctk.CTkFrame(self.header_frame, fg_color="transparent")
        self.brand_frame.pack(side="left")

        if os.path.exists(LOGO_PATH):
            try:
                logo_pil = Image.open(LOGO_PATH)
                self.logo_img = ctk.CTkImage(light_image=logo_pil, dark_image=logo_pil, size=(32, 32))
                self.logo_lbl = ctk.CTkLabel(self.brand_frame, image=self.logo_img, text="")
                self.logo_lbl.pack(side="left", padx=(0, 8))
            except Exception:
                pass

        self.app_title_lbl = ctk.CTkLabel(
            self.brand_frame,
            text="Netzy",
            font=ctk.CTkFont(family="Segoe UI", size=20, weight="bold"),
            text_color="#F8FAFC",
        )
        self.app_title_lbl.pack(side="left")

        # Sağ: Ağ Durumu & Butonlar
        self.top_right = ctk.CTkFrame(self.header_frame, fg_color="transparent")
        self.top_right.pack(side="right")

        # Canlı Ping Göstergesi
        self.net_badge = ctk.CTkFrame(
            self.top_right,
            height=30,
            corner_radius=6,
            fg_color="#142136",
            border_width=1,
            border_color="#1E2C44",
        )
        self.net_badge.pack(side="left", padx=(0, 6))

        self.net_dot = ctk.CTkLabel(self.net_badge, text="●", font=ctk.CTkFont(size=12), text_color="#F59E0B")
        self.net_dot.pack(side="left", padx=(8, 2))

        self.net_lbl = ctk.CTkLabel(
            self.net_badge,
            text="Kontrol...",
            font=ctk.CTkFont(family="Segoe UI", size=11, weight="bold"),
            text_color="#CBD5E1",
        )
        self.net_lbl.pack(side="left", padx=(0, 8))

        # İşlemler Listesi Butonu
        self.ops_btn = ctk.CTkButton(
            self.top_right,
            text="📋 İşlemler",
            width=75,
            height=30,
            corner_radius=6,
            fg_color="#142136",
            border_width=1,
            border_color="#1E2C44",
            hover_color="#1E2C44",
            text_color="#38BDF8",
            font=ctk.CTkFont(family="Segoe UI", size=11, weight="bold"),
            command=self._open_operations_window,
        )
        self.ops_btn.pack(side="left", padx=(0, 6))

        # Gizlilik & Koşullar Butonu
        self.privacy_btn = ctk.CTkButton(
            self.top_right,
            text="Gizlilik",
            width=60,
            height=30,
            corner_radius=6,
            fg_color="#142136",
            border_width=1,
            border_color="#1E2C44",
            hover_color="#1E2C44",
            text_color="#94A3B8",
            font=ctk.CTkFont(family="Segoe UI", size=11),
            command=lambda: PrivacyTermsModal(self, is_first_run=False),
        )
        self.privacy_btn.pack(side="left", padx=(0, 6))

        # GitHub Butonu
        self.github_btn = ctk.CTkButton(
            self.top_right,
            text="GitHub",
            width=55,
            height=30,
            corner_radius=6,
            fg_color="#142136",
            border_width=1,
            border_color="#1E2C44",
            hover_color="#1E2C44",
            text_color="#F8FAFC",
            font=ctk.CTkFont(family="Segoe UI", size=11),
            command=lambda: webbrowser.open(GITHUB_REPO_URL),
        )
        self.github_btn.pack(side="left")

        # ==========================================
        # 2. GÜNCELLEME DUYURUSU (Varsa açılır)
        # ==========================================
        self.update_banner = ctk.CTkFrame(self.main_container, fg_color="#064E3B", corner_radius=6, height=32)
        self.update_banner_lbl = ctk.CTkLabel(
            self.update_banner,
            text="",
            font=ctk.CTkFont(family="Segoe UI", size=11, weight="bold"),
            text_color="#34D399",
        )
        self.update_banner_lbl.pack(side="left", padx=12, pady=4)

        self.update_banner_btn = ctk.CTkButton(
            self.update_banner,
            text="İndir",
            width=50,
            height=22,
            corner_radius=4,
            fg_color="#059669",
            hover_color="#047857",
            font=ctk.CTkFont(family="Segoe UI", size=10, weight="bold"),
            command=lambda: webbrowser.open(f"{GITHUB_REPO_URL}/releases"),
        )
        self.update_banner_btn.pack(side="right", padx=8, pady=4)

        # ==========================================
        # 3. ANA SAYFA GÖVDESİ (SADECE BAŞLAT BUTONU & PROGRESS)
        # ==========================================
        self.center_card = ctk.CTkFrame(
            self.main_container,
            corner_radius=12,
            fg_color="#142136",
            border_width=1,
            border_color="#1E2C44",
        )
        self.center_card.pack(fill="both", expand=True, pady=(10, 0))

        self.center_inner = ctk.CTkFrame(self.center_card, fg_color="transparent")
        self.center_inner.pack(fill="both", expand=True, padx=30, pady=24)

        # Başlık ve Kısa Bilgi
        self.card_title = ctk.CTkLabel(
            self.center_inner,
            text="Ağ Onarım & Sistem Temizliği",
            font=ctk.CTkFont(family="Segoe UI", size=16, weight="bold"),
            text_color="#F8FAFC",
        )
        self.card_title.pack(pady=(4, 4))

        self.card_desc = ctk.CTkLabel(
            self.center_inner,
            text="9 temel ağ protokolü sıfırlanır, önbellekler yenilenir ve geçici dosyalar temizlenir.",
            font=ctk.CTkFont(family="Segoe UI", size=11),
            text_color="#8295AD",
        )
        self.card_desc.pack(pady=(0, 20))

        # Ana Başlat Butonu
        self.start_btn = ctk.CTkButton(
            self.center_inner,
            text="⚡ BAŞLAT",
            height=54,
            width=280,
            corner_radius=10,
            fg_color="#2563EB",
            hover_color="#1D4ED8",
            text_color="#FFFFFF",
            font=ctk.CTkFont(family="Segoe UI", size=18, weight="bold"),
            command=self.start_process,
        )
        self.start_btn.pack(pady=(0, 18))

        # Progress bar ve durum yazısı
        self.progress_meta = ctk.CTkFrame(self.center_inner, fg_color="transparent")
        self.progress_meta.pack(fill="x", padx=10, pady=(0, 4))

        self.status_lbl = ctk.CTkLabel(
            self.progress_meta,
            text="Hazır. Başlatmak için butona tıklayın.",
            font=ctk.CTkFont(family="Segoe UI", size=11),
            text_color="#94A3B8",
            anchor="w",
        )
        self.status_lbl.pack(side="left")

        self.percent_lbl = ctk.CTkLabel(
            self.progress_meta,
            text="%0",
            font=ctk.CTkFont(family="Segoe UI", size=11, weight="bold"),
            text_color="#38BDF8",
            anchor="e",
        )
        self.percent_lbl.pack(side="right")

        self.progress_bar = ctk.CTkProgressBar(
            self.center_inner,
            height=8,
            corner_radius=4,
            fg_color="#0F172A",
            progress_color="#38BDF8",
        )
        self.progress_bar.pack(fill="x", padx=10, pady=(0, 16))
        self.progress_bar.set(0.0)

        # Alt Hızlı Kategori Bağlantısı
        self.view_ops_link = ctk.CTkButton(
            self.center_inner,
            text="📋 Yürütülecek 9 İşlemi Kategori Halinde Gör",
            font=ctk.CTkFont(family="Segoe UI", size=11, underline=True),
            fg_color="transparent",
            hover_color="#1A283E",
            text_color="#38BDF8",
            height=28,
            command=self._open_operations_window,
        )
        self.view_ops_link.pack()

    def _open_operations_window(self):
        OperationsWindow(self)

    def _start_network_monitor(self):
        def loop():
            while not self._stop_net_thread:
                connected = False
                ping_ms = 0
                for host in ("8.8.8.8", "1.1.1.1"):
                    try:
                        t0 = time.perf_counter()
                        s = socket.create_connection((host, 53), timeout=1.5)
                        s.close()
                        ping_ms = int((time.perf_counter() - t0) * 1000)
                        connected = True
                        break
                    except Exception:
                        continue

                if not self._stop_net_thread:
                    try:
                        self.after(0, self._update_net_badge, connected, ping_ms)
                    except Exception:
                        pass

                for _ in range(12):
                    if self._stop_net_thread:
                        break
                    time.sleep(0.25)

        threading.Thread(target=loop, daemon=True).start()

    def _update_net_badge(self, connected: bool, ping_ms: int):
        if not self.winfo_exists():
            return
        if connected:
            col = "#10B981" if ping_ms < 60 else "#F59E0B"
            self.net_dot.configure(text_color=col)
            self.net_lbl.configure(text=f"Bağlı • {ping_ms} ms", text_color="#F8FAFC")
        else:
            self.net_dot.configure(text_color="#EF4444")
            self.net_lbl.configure(text="Bağlantı Yok", text_color="#EF4444")

    def _check_version_background(self):
        def worker():
            try:
                req = urllib.request.Request(GITHUB_API_URL, headers={"User-Agent": "Netzy-Client"})
                with urllib.request.urlopen(req, timeout=4) as res:
                    data = json.loads(res.read().decode())
                    latest = data.get("tag_name", "").strip()
                    if latest and latest != CURRENT_VERSION:
                        self.after(0, self._show_update_banner, latest)
            except Exception:
                pass

        threading.Thread(target=worker, daemon=True).start()

    def _show_update_banner(self, latest: str):
        if not self.winfo_exists():
            return
        self.update_banner_lbl.configure(text=f"🎉 Yeni Netzy sürümü mevcut ({latest})!")
        self.update_banner.pack(fill="x", pady=(0, 8), before=self.center_card)

    def start_process(self):
        """Ana sayfadan tüm 9 işlemi sırayla çalıştırır."""
        if self.is_running:
            return

        # Yönetici Kontrolü
        if not is_admin():
            ans = messagebox.askyesno(
                "Yönetici İzni Zorunludur",
                "Ağ sıfırlama komutları doğrudan Windows çekirdeğini yapılandırdığı için "
                "Yönetici yetkisi ZORUNLUDUR.\n\n"
                "Uygulama şimdi Yönetici olarak yeniden başlatılsın mı?",
            )
            if ans:
                request_admin_elevation()
            return

        self.is_running = True
        self.start_btn.configure(state="disabled", text="⏳ İŞLEMLER YÜRÜTÜLÜYOR...", fg_color="#334155")
        self.progress_bar.set(0.0)

        threading.Thread(target=self._run_worker, daemon=True).start()

    def _run_worker(self):
        total = len(FLAT_OPERATIONS)
        success_count = 0
        creation_flags = getattr(subprocess, "CREATE_NO_WINDOW", 0x08000000)

        for i, op in enumerate(FLAT_OPERATIONS):
            pct_before = i / total

            # UI Güncelleme: Çalışıyor
            self.after(0, self._update_progress_ui, pct_before, f"[{i + 1}/{total}] {op['name']} çalıştırılıyor...")

            is_ok = True
            extra_msg = ""

            try:
                if op["type"] == "cmd":
                    proc = subprocess.run(op["cmd"], shell=True, capture_output=True, creationflags=creation_flags)
                    stdout = proc.stdout.decode(OEM_CODEPAGE, errors="replace").strip()
                    stderr = proc.stderr.decode(OEM_CODEPAGE, errors="replace").strip()
                    combined = (stdout + "\n" + stderr).lower()

                    if "netsh int ip reset" in op["cmd"]:
                        if "requires elevation" in combined or "yükseltme gerekiyor" in combined:
                            is_ok = False
                        elif "restart the computer" in combined or "yeniden başlat" in combined or "ok!" in combined:
                            is_ok = True
                        else:
                            is_ok = (proc.returncode == 0)
                    else:
                        is_ok = (proc.returncode == 0 or "ok!" in combined or "successfully" in combined or "başarıyla" in combined)

                    for extra in op.get("extra_cmds", []):
                        try:
                            subprocess.run(extra, shell=True, capture_output=True, creationflags=creation_flags)
                        except Exception:
                            pass

                elif op["type"] == "python" and op.get("handler") == "clean_temp_files":
                    count, mb = clean_temp_files()
                    extra_msg = f"{mb:.1f} MB temizlendi"
                    is_ok = True

            except Exception:
                is_ok = False

            if is_ok:
                success_count += 1

            pct_after = (i + 1) / total
            status_text = extra_msg if extra_msg else ("Tamamlandı" if is_ok else "Hata")
            self.after(0, self._update_progress_ui, pct_after, f"[{i + 1}/{total}] {op['name']} {status_text}.")

            time.sleep(0.35)

        self.after(0, self._on_all_finished, success_count, total)

    def _update_progress_ui(self, progress_val: float, status_msg: str):
        if not self.winfo_exists():
            return
        self.progress_bar.set(progress_val)
        self.percent_lbl.configure(text=f"%{int(progress_val * 100)}")
        self.status_lbl.configure(text=status_msg)

    def _on_all_finished(self, success_count: int, total: int):
        if not self.winfo_exists():
            return

        self.is_running = False
        self.progress_bar.set(1.0)
        self.percent_lbl.configure(text="%100")
        self.status_lbl.configure(text=f"✅ Tüm adımlar tamamlandı! ({success_count}/{total} başarılı)")
        self.start_btn.configure(state="normal", text="⚡ BAŞLAT", fg_color="#2563EB")

        messagebox.showinfo(
            "İşlemler Tamamlandı",
            f"Tüm ağ ve sistem onarım adımları yürütüldü ({success_count}/{total} başarılı).\n\n"
            "Ayarların tam olarak devreye girmesi için bilgisayarınızı yeniden başlatmanız önerilir.",
        )

    def destroy(self):
        self._stop_net_thread = True
        super().destroy()


if __name__ == "__main__":
    app = NetzyApp()
    app.mainloop()

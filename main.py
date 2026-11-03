"""
Netzy - Windows Ağ Sıfırlama ve Sistem Onarım Aracı
Kullanıcı referans görseline birebir sadık kart tasarımı.
Seçimsiz, doğrudan gösterim ve sıralı çalıştırma.
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
LOGO_PATH = os.path.join(APP_DIR, "logo.jpg")
ICO_PATH = os.path.join(APP_DIR, "logo.ico")

# Windows konsol OEM kod sayfası (Türkçe için cp857)
OEM_CODEPAGE = f"cp{ctypes.windll.kernel32.GetOEMCP()}" if hasattr(ctypes, "windll") else "utf-8"

# Referans görseldeki liste ve eklenen gereksinimler
ACTIONS = [
    {
        "id": "winsock",
        "title": "Winsock Sıfırla (netsh winsock reset)",
        "desc": "Ağ soket kütüphanesini fabrika ayarlarına döndürür",
        "type": "cmd",
        "cmd": "netsh winsock reset",
    },
    {
        "id": "tcpip",
        "title": "TCP/IP Stack Sıfırla",
        "desc": "TCP/IP protokol yığınını sıfırlar, bağlantı sorunlarını giderir",
        "type": "cmd",
        "cmd": "netsh int ip reset",
        "extra_cmds": ["netsh int ipv4 reset", "netsh int ipv6 reset"],
    },
    {
        "id": "firewall",
        "title": "Windows Güvenlik Duvarını Sıfırla",
        "desc": "Güvenlik duvarı kurallarını varsayılan ayarlara döndürür",
        "type": "cmd",
        "cmd": "netsh advfirewall reset",
    },
    {
        "id": "dns_service",
        "title": "DNS İstemci Servisini Yeniden Başlat",
        "desc": "DNS çözümleme servisini ve önbelleğini yeniler",
        "type": "cmd",
        "cmd": "ipconfig /flushdns",
        "extra_cmds": ['powershell -Command "Clear-DnsClientCache"', "ipconfig /registerdns"],
    },
    {
        "id": "dhcp_service",
        "title": "DHCP İstemci Servisini Yeniden Başlat",
        "desc": "IP adresi otomatik alma servisini yeniden başlatır",
        "type": "cmd",
        "cmd": 'powershell -Command "Restart-Service -Name Dhcp -Force -ErrorAction SilentlyContinue"',
    },
    {
        "id": "adapter_restart",
        "title": "Ağ Adaptörlerini Yeniden Başlat",
        "desc": "Aktif ağ adaptörlerini devre dışı bırakıp yeniden etkinleştirir",
        "type": "cmd",
        "cmd": 'powershell -Command "Get-NetAdapter | Where-Object Status -eq \'Up\' | Restart-NetAdapter -Confirm:$false"',
    },
    {
        "id": "netbios",
        "title": "NetBIOS Önbelleğini Temizle",
        "desc": "NetBIOS ad önbelleğini temizler ve LMHOSTS dosyasını yeniden yükler",
        "type": "cmd",
        "cmd": "nbtstat -R",
        "extra_cmds": ["nbtstat -RR"],
    },
    {
        "id": "arp",
        "title": "ARP Cache Temizliği",
        "desc": "ARP tablosunu ve IP-fiziksel adres eşleme önbelleğini temizler",
        "type": "cmd",
        "cmd": "netsh interface ip delete arpcache",
    },
    {
        "id": "temp_cleanup",
        "title": "Geçici Dosyaları Temizle (Temp Cleanup)",
        "desc": "%TEMP%, Windows Temp ve Prefetch geçici sistem dosyalarını temizler",
        "type": "python",
        "handler": "clean_temp_files",
    },
]


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


class TermsModal(ctk.CTkToplevel):
    """İlk açılışta gösterilen Kullanım Koşulları ve Sorumluluk Reddi Penceresi."""

    def __init__(self, parent, on_accept):
        super().__init__(parent)
        self.on_accept = on_accept
        self.title("Netzy — Kullanım Koşulları")
        self.geometry("560x470")
        self.resizable(False, False)
        self.configure(fg_color="#0D1525")

        self.transient(parent)
        self.grab_set()

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

        self.title_lbl = ctk.CTkLabel(
            self.header_frame,
            text="Netzy'ye Hoş Geldiniz",
            font=ctk.CTkFont(family="Segoe UI", size=20, weight="bold"),
            text_color="#F8FAFC",
        )
        self.title_lbl.pack(side="left")

        terms_text = (
            "Netzy; Windows 10 ve 11 işletim sistemlerinde ağ yığınını sıfırlamak, "
            "bağlantı sorunlarını gidermek, DNS ve ARP önbelleğini tazelemek ve "
            "sistem geçici dosyalarını temizlemek amacıyla geliştirilmiş açık kaynaklı bir araçtır.\n\n"
            "Önemli Hatırlatmalar ve Koşullar:\n"
            "1. Yönetici Yetkisi: Winsock, TCP/IP ve Güvenlik Duvarı gibi çekirdek düzeyindeki "
            "işlemlerin uygulanabilmesi için Yönetici hakları gereklidir.\n"
            "2. Geçici Bağlantı Kesintisi: Adaptör yeniden başlatma adımları esnasında internet "
            "bağlantınız birkaç saniye kesilip otomatik olarak tekrar bağlanacaktır.\n"
            "3. Açık Kaynak & Lisans: Bu yazılım MIT Lisansı altında 'OLDUĞU GİBİ' (AS IS) sunulmaktadır.\n"
            "4. Sistem Yeniden Başlatma: Protokol sıfırlamalarının tam geçerlilik kazanması için "
            "işlemler bittiğinde bilgisayarınızı yeniden başlatmanız önerilir."
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
        self.textbox.pack(fill="both", expand=True, padx=24, pady=(0, 15))
        self.textbox.insert("1.0", terms_text)
        self.textbox.configure(state="disabled")

        self.btn_frame = ctk.CTkFrame(self, fg_color="transparent")
        self.btn_frame.pack(fill="x", padx=24, pady=(0, 20))

        self.accept_btn = ctk.CTkButton(
            self.btn_frame,
            text="Kabul Ediyorum ve Başla",
            font=ctk.CTkFont(family="Segoe UI", size=14, weight="bold"),
            height=44,
            corner_radius=8,
            fg_color="#2563EB",
            hover_color="#1D4ED8",
            text_color="#FFFFFF",
            command=self._accept,
        )
        self.accept_btn.pack(fill="x")

    def _accept(self):
        self.on_accept()
        self.destroy()


class NetzyApp(ctk.CTk):
    def __init__(self):
        super().__init__()

        # Temel Yapılandırma
        ctk.set_appearance_mode("dark")
        ctk.set_default_color_theme("blue")

        # Kısa ve sade başlık: Netzy
        self.title("Netzy")
        self.geometry("620x720")
        self.minsize(580, 640)
        self.configure(fg_color="#0D1525")

        # Uygulama Simgesi
        if os.path.exists(ICO_PATH):
            try:
                self.iconbitmap(ICO_PATH)
            except Exception:
                pass

        self.is_running = False
        self._stop_net_thread = False
        self.card_widgets = []

        self._build_ui()
        self._start_network_monitor()
        self._check_version_background()
        self._check_first_launch()

    def _check_first_launch(self):
        cfg = load_config()
        if not cfg.get("terms_accepted", False):
            self.after(200, lambda: TermsModal(self, self._on_terms_accepted))

    def _on_terms_accepted(self):
        cfg = load_config()
        cfg["terms_accepted"] = True
        save_config(cfg)

    def _build_ui(self):
        self.main_container = ctk.CTkFrame(self, fg_color="transparent")
        self.main_container.pack(fill="both", expand=True, padx=20, pady=16)

        # ==========================================
        # 1. ÜST BAR (Header)
        # ==========================================
        self.header_frame = ctk.CTkFrame(self.main_container, fg_color="transparent")
        self.header_frame.pack(fill="x", pady=(0, 12))

        # Sol: Logo ve İsim
        self.brand_frame = ctk.CTkFrame(self.header_frame, fg_color="transparent")
        self.brand_frame.pack(side="left")

        if os.path.exists(LOGO_PATH):
            try:
                logo_pil = Image.open(LOGO_PATH)
                self.logo_img = ctk.CTkImage(light_image=logo_pil, dark_image=logo_pil, size=(34, 34))
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

        # Sağ: Ağ Durumu & GitHub
        self.top_right = ctk.CTkFrame(self.header_frame, fg_color="transparent")
        self.top_right.pack(side="right")

        self.net_badge = ctk.CTkFrame(
            self.top_right,
            height=30,
            corner_radius=6,
            fg_color="#142136",
            border_width=1,
            border_color="#1E2C44",
        )
        self.net_badge.pack(side="left", padx=(0, 8))

        self.net_dot = ctk.CTkLabel(self.net_badge, text="●", font=ctk.CTkFont(size=12), text_color="#F59E0B")
        self.net_dot.pack(side="left", padx=(8, 2))

        self.net_lbl = ctk.CTkLabel(
            self.net_badge,
            text="Kontrol...",
            font=ctk.CTkFont(family="Segoe UI", size=11, weight="bold"),
            text_color="#CBD5E1",
        )
        self.net_lbl.pack(side="left", padx=(0, 8))

        self.github_btn = ctk.CTkButton(
            self.top_right,
            text="GitHub",
            width=65,
            height=30,
            corner_radius=6,
            fg_color="#142136",
            border_width=1,
            border_color="#1E2C44",
            hover_color="#1E2C44",
            text_color="#F8FAFC",
            font=ctk.CTkFont(family="Segoe UI", size=11, weight="bold"),
            command=lambda: webbrowser.open(GITHUB_REPO_URL),
        )
        self.github_btn.pack(side="left")

        # ==========================================
        # 2. GÜNCELLEME DUYURUSU (Varsa)
        # ==========================================
        self.update_banner = ctk.CTkFrame(self.main_container, fg_color="#064E3B", corner_radius=6, height=34)
        self.update_banner_lbl = ctk.CTkLabel(
            self.update_banner,
            text="",
            font=ctk.CTkFont(family="Segoe UI", size=11, weight="bold"),
            text_color="#34D399",
        )
        self.update_banner_lbl.pack(side="left", padx=12, pady=5)

        self.update_banner_btn = ctk.CTkButton(
            self.update_banner,
            text="İndir",
            width=55,
            height=24,
            corner_radius=4,
            fg_color="#059669",
            hover_color="#047857",
            font=ctk.CTkFont(family="Segoe UI", size=10, weight="bold"),
            command=lambda: webbrowser.open(f"{GITHUB_REPO_URL}/releases"),
        )
        self.update_banner_btn.pack(side="right", padx=10, pady=5)

        # ==========================================
        # 3. BAŞLAT BUTONU & PROGRESS BAR
        # ==========================================
        self.action_panel = ctk.CTkFrame(self.main_container, fg_color="transparent")
        self.action_panel.pack(fill="x", pady=(0, 10))

        self.start_btn = ctk.CTkButton(
            self.action_panel,
            text="⚡ BAŞLAT",
            height=46,
            corner_radius=8,
            fg_color="#2563EB",
            hover_color="#1D4ED8",
            text_color="#FFFFFF",
            font=ctk.CTkFont(family="Segoe UI", size=16, weight="bold"),
            command=self.start_process,
        )
        self.start_btn.pack(fill="x", pady=(0, 8))

        # Progress bar ve durum yazısı
        self.progress_meta = ctk.CTkFrame(self.action_panel, fg_color="transparent")
        self.progress_meta.pack(fill="x", pady=(0, 4))

        self.status_lbl = ctk.CTkLabel(
            self.progress_meta,
            text="Tüm işlemler sırayla yürütülmeye hazır.",
            font=ctk.CTkFont(family="Segoe UI", size=12),
            text_color="#94A3B8",
            anchor="w",
        )
        self.status_lbl.pack(side="left")

        self.percent_lbl = ctk.CTkLabel(
            self.progress_meta,
            text="%0",
            font=ctk.CTkFont(family="Segoe UI", size=12, weight="bold"),
            text_color="#38BDF8",
            anchor="e",
        )
        self.percent_lbl.pack(side="right")

        self.progress_bar = ctk.CTkProgressBar(
            self.action_panel,
            height=7,
            corner_radius=3,
            fg_color="#142136",
            progress_color="#38BDF8",
        )
        self.progress_bar.pack(fill="x")
        self.progress_bar.set(0.0)

        # ==========================================
        # 4. REFERANS GÖRSEL BİREBİR KART LİSTESİ
        # ==========================================
        self.scroll_frame = ctk.CTkScrollableFrame(
            self.main_container,
            corner_radius=8,
            fg_color="#090E1B",
            border_width=1,
            border_color="#162238",
        )
        self.scroll_frame.pack(fill="both", expand=True)

        for item in ACTIONS:
            self._create_reference_card(item)

    def _create_reference_card(self, item: dict):
        """Referans görseldeki kart tasarımını birebir üretir (Seçimsiz, salt gösterim)."""
        card = ctk.CTkFrame(
            self.scroll_frame,
            corner_radius=6,
            fg_color="#142136",
            border_width=1,
            border_color="#1E2C44",
            height=66,
        )
        card.pack(fill="x", padx=6, pady=4)
        card.pack_propagate(False)

        inner = ctk.CTkFrame(card, fg_color="transparent")
        inner.pack(fill="both", expand=True, padx=12, pady=8)

        # Sol: Görseldeki yuvarlatılmış kare kutucuk (Seçim değil, durum göstergesi)
        icon_box = ctk.CTkFrame(
            inner,
            width=24,
            height=24,
            corner_radius=4,
            fg_color="#1B283E",
            border_width=1,
            border_color="#2D405C",
        )
        icon_box.pack(side="left", padx=(0, 12))
        icon_box.pack_propagate(False)

        icon_lbl = ctk.CTkLabel(
            icon_box,
            text="",
            font=ctk.CTkFont(family="Segoe UI", size=11, weight="bold"),
            text_color="#64748B",
        )
        icon_lbl.pack(expand=True)

        # Orta: Başlık ve Açıklama Metni
        text_frame = ctk.CTkFrame(inner, fg_color="transparent")
        text_frame.pack(side="left", fill="both", expand=True)

        title_lbl = ctk.CTkLabel(
            text_frame,
            text=item["title"],
            font=ctk.CTkFont(family="Segoe UI", size=12, weight="bold"),
            text_color="#F1F5F9",
            anchor="w",
        )
        title_lbl.pack(anchor="w", pady=(1, 0))

        desc_lbl = ctk.CTkLabel(
            text_frame,
            text=item["desc"],
            font=ctk.CTkFont(family="Segoe UI", size=11),
            text_color="#8295AD",
            anchor="w",
        )
        desc_lbl.pack(anchor="w")

        # Sağ: Görseldeki Turuncu Admin Rozeti
        badge_frame = ctk.CTkFrame(inner, fg_color="transparent")
        badge_frame.pack(side="right")

        admin_badge = ctk.CTkFrame(
            badge_frame,
            corner_radius=5,
            fg_color="#7A2D0E",
            border_width=1,
            border_color="#B45309",
            height=24,
            width=68,
        )
        admin_badge.pack(side="right")
        admin_badge.pack_propagate(False)

        admin_badge_lbl = ctk.CTkLabel(
            admin_badge,
            text="🔑 Admin",
            font=ctk.CTkFont(family="Segoe UI", size=10, weight="bold"),
            text_color="#FDBA74",
        )
        admin_badge_lbl.pack(expand=True)

        self.card_widgets.append({
            "item": item,
            "card": card,
            "icon_box": icon_box,
            "icon_lbl": icon_lbl,
        })

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
        self.update_banner.pack(fill="x", pady=(0, 8), before=self.action_panel)

    def start_process(self):
        """Tüm işlemleri sırayla çalıştırır."""
        if self.is_running:
            return

        # Yönetici Kontrolü
        if not is_admin():
            ans = messagebox.askyesno(
                "Yönetici İzni Zorunludur",
                "Ağ sıfırlama komutları (TCP/IP, Winsock vb.) doğrudan Windows çekirdeğini "
                "yapılandırdığı için Yönetici yetkisi ZORUNLUDUR.\n\n"
                "Uygulama şimdi Yönetici olarak yeniden başlatılsın mı?",
            )
            if ans:
                request_admin_elevation()
            return

        self.is_running = True
        self.start_btn.configure(state="disabled", text="⏳ İŞLEMLER YÜRÜTÜLÜYOR...", fg_color="#334155")
        self.progress_bar.set(0.0)

        # Kart durumlarını sıfırla
        for w in self.card_widgets:
            w["card"].configure(border_color="#1E2C44")
            w["icon_box"].configure(fg_color="#1B283E", border_color="#2D405C")
            w["icon_lbl"].configure(text="")

        threading.Thread(target=self._run_worker, daemon=True).start()

    def _run_worker(self):
        total = len(self.card_widgets)
        success_count = 0
        creation_flags = getattr(subprocess, "CREATE_NO_WINDOW", 0x08000000)

        for i, w in enumerate(self.card_widgets):
            item = w["item"]
            pct_before = i / total

            # UI: Çalışıyor
            self.after(0, self._update_card_ui, w, "running", pct_before, f"[{i + 1}/{total}] {item['title']} çalıştırılıyor...")

            is_ok = True
            extra_msg = ""

            try:
                if item["type"] == "cmd":
                    proc = subprocess.run(item["cmd"], shell=True, capture_output=True, creationflags=creation_flags)
                    stdout = proc.stdout.decode(OEM_CODEPAGE, errors="replace").strip()
                    stderr = proc.stderr.decode(OEM_CODEPAGE, errors="replace").strip()
                    combined = (stdout + "\n" + stderr).lower()

                    if "netsh int ip reset" in item["cmd"]:
                        if "requires elevation" in combined or "yükseltme gerekiyor" in combined:
                            is_ok = False
                        elif "restart the computer" in combined or "yeniden başlat" in combined or "ok!" in combined:
                            is_ok = True
                        else:
                            is_ok = (proc.returncode == 0)
                    else:
                        is_ok = (proc.returncode == 0 or "ok!" in combined or "successfully" in combined or "başarıyla" in combined)

                    for extra in item.get("extra_cmds", []):
                        try:
                            subprocess.run(extra, shell=True, capture_output=True, creationflags=creation_flags)
                        except Exception:
                            pass

                elif item["type"] == "python" and item.get("handler") == "clean_temp_files":
                    count, mb = clean_temp_files()
                    extra_msg = f"{mb:.1f} MB temizlendi"
                    is_ok = True

            except Exception:
                is_ok = False

            if is_ok:
                success_count += 1

            pct_after = (i + 1) / total
            status_desc = extra_msg if extra_msg else ("Tamamlandı" if is_ok else "Hata")
            self.after(0, self._update_card_ui, w, "done" if is_ok else "error", pct_after, f"[{i + 1}/{total}] {item['title']} {status_desc}")

            time.sleep(0.35)

        self.after(0, self._on_all_finished, success_count, total)

    def _update_card_ui(self, w, state: str, progress_val: float, status_text: str):
        if not self.winfo_exists():
            return
        self.progress_bar.set(progress_val)
        self.percent_lbl.configure(text=f"%{int(progress_val * 100)}")
        self.status_lbl.configure(text=status_text)

        card = w["card"]
        box = w["icon_box"]
        lbl = w["icon_lbl"]

        if state == "running":
            card.configure(border_color="#3B82F6")
            box.configure(fg_color="#1E3A8A", border_color="#3B82F6")
            lbl.configure(text="●", text_color="#60A5FA")
        elif state == "done":
            card.configure(border_color="#059669")
            box.configure(fg_color="#064E3B", border_color="#059669")
            lbl.configure(text="✓", text_color="#34D399")
        elif state == "error":
            card.configure(border_color="#DC2626")
            box.configure(fg_color="#450A0A", border_color="#DC2626")
            lbl.configure(text="✕", text_color="#F87171")

    def _on_all_finished(self, success_count: int, total: int):
        if not self.winfo_exists():
            return

        self.is_running = False
        self.progress_bar.set(1.0)
        self.percent_lbl.configure(text="%100")
        self.status_lbl.configure(text=f"✅ Tüm işlemler tamamlandı! ({success_count}/{total} başarılı)")
        self.start_btn.configure(state="normal", text="⚡ BAŞLAT", fg_color="#2563EB")

        messagebox.showinfo(
            "İşlemler Tamamlandı",
            f"Tüm işlemler başarıyla yürütüldü ({success_count}/{total} başarılı).\n\n"
            "Ağ protokollerinin ve ayarların eksiksiz devreye girmesi için bilgisayarınızı yeniden başlatmanız önerilir.",
        )

    def destroy(self):
        self._stop_net_thread = True
        super().destroy()


if __name__ == "__main__":
    app = NetzyApp()
    app.mainloop()

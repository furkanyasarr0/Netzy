# Netzy

An open-source network diagnosis and repair tool for Windows 10 and 11. It finds the problem first, then fixes only what is needed.

[![Build](https://github.com/furkanyasarr0/Netzy/actions/workflows/build.yml/badge.svg)](https://github.com/furkanyasarr0/Netzy/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Privacy: Zero Data Collection](https://img.shields.io/badge/Privacy-Zero%20Data%20Collection-success.svg)](#-privacy)
[![Platform: Windows 10/11](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6.svg)](#-requirements-and-installation)

---

## ✨ Features

- **Connection diagnosis:** On startup, Netzy checks the network adapter, router, DNS and internet access, then tells you where the problem is and which repair to run. It also detects hotel/café Wi-Fi sign-in pages and active proxy settings.
- **⚡ Quick Repair:** Fixes most everyday problems without changing anything permanently. No restart needed.
- **Deep Repair:** Resets Winsock, TCP/IP and the firewall, and restarts the network adapters. Settings are backed up first.
- **↺ Undo:** Firewall rules, IP/DNS settings and proxy settings are backed up before they change. One click restores them to how they were before the last change.
- **Before/after results:** Measures latency and DNS response time before and after a repair (e.g. `Latency: 140 ms → 32 ms`).
- **DNS switch:** One click to switch between Automatic, Cloudflare, Google and Quad9.
- **Report:** A command-by-command log of every run, with a "Copy report" button for pasting into a GitHub issue.
- **Command-line mode:** For IT admins and scripts (`Netzy.exe --quick`).
- **Turkish / English** interface.

## 📋 Repair Steps

| Step | Quick | Deep |
|---|:---:|:---:|
| Back up settings (if the backup fails, nothing is changed) | ✓ | ✓ |
| Reset Winsock (`netsh winsock reset`) | | ✓ |
| Reset TCP/IP (`netsh int ip reset`, `netsh int ipv6 reset`) | | ✓ |
| Reset the firewall (`netsh advfirewall reset`) | | ✓ |
| Reset proxy settings (browser + `netsh winhttp reset proxy`) | ✓ | ✓ |
| Restart the DHCP client service | | ✓ |
| Restart network adapters | | ✓ |
| Clear the ARP cache (`netsh interface ip delete arpcache`) | ✓ | ✓ |
| Refresh the NetBIOS cache (`nbtstat -R`, `-RR`) | ✓ | ✓ |
| Renew the IP address (`ipconfig /release` + `/renew`, DHCP only) | ✓ | ✓ |
| Clear the DNS cache (`ipconfig /flushdns`, `/registerdns`) | ✓ | ✓ |
| Clean temporary files (older than 24 hours, never follows junctions) | | ✓ |

After a Deep Repair, restart your computer so the Winsock and TCP/IP resets fully take effect.

## 💻 Command Line

From a command prompt opened as administrator:

```bat
Netzy.exe --diagnose            :: check the connection (changes nothing)
Netzy.exe --quick               :: Quick Repair
Netzy.exe --deep                :: Deep Repair
Netzy.exe --dns cloudflare      :: auto | cloudflare | google | quad9
Netzy.exe --undo                :: restore the latest backups
Netzy.exe --help --lang en
```

Exit codes: `0` success, `1` error or problem found, `2` bad usage, `3` not administrator. To wait for the exit code in scripts, use `start /wait Netzy.exe --quick`.

## 🔒 Privacy

- Netzy **does not collect, store or send** any personal data, IP address, hardware ID or usage statistics.
- The internet is used only for: the connection badge (a short connection to 1.1.1.1 / 8.8.8.8), diagnosis (Microsoft's connection test `msftconnecttest.com` and one DNS query) and the new-version check on GitHub.
- Settings, reports and backups stay on your computer only: `%LOCALAPPDATA%\Netzy`.

## 🛡️ Security and Antivirus

Netzy is built to avoid antivirus false positives:

- **Native .NET app:** No packer that unpacks itself into `%TEMP%` (like Python/PyInstaller). Just one small (~200 KB) EXE.
- **No shell:** `cmd.exe` and PowerShell are never called. Only Windows' own signed tools (`netsh`, `ipconfig`, `nbtstat`) run, by their **full path** in `System32`. Diagnosis uses Windows APIs.
- **No download-and-run:** The app never downloads code or installs updates by itself; it only opens the Releases page.
- **Version info and checksum:** The EXE carries publisher and version info, and each release comes with a `Netzy.exe.sha256` checksum.

**About SmartScreen:** Windows SmartScreen may show "Windows protected your PC" because the EXE is not code-signed yet. Click **More info → Run anyway**. To verify the file you downloaded:

```bat
certutil -hashfile Netzy.exe SHA256
```

The result must match `Netzy.exe.sha256` on the Releases page.

## 🚀 Requirements and Installation

- Windows 10 (version 1903 or later) or Windows 11. The required .NET Framework 4.8 **comes with** these versions; nothing else to install.
- Download `Netzy.exe` from the [Releases](https://github.com/furkanyasarr0/Netzy/releases/latest) page and run it. No installation needed (portable).

## 🔧 Development

Requires the [.NET SDK](https://dotnet.microsoft.com/download) (8 or later).

```bat
build.bat
```

This runs the tests and produces `dist\Netzy.exe` + `dist\Netzy.exe.sha256`. Manually:

```bat
dotnet test Netzy.sln
dotnet build src\Netzy\Netzy.csproj -c Release
```

Project layout:

```
src/Netzy/            WPF app (.NET Framework 4.8)
  Core/               UI-independent logic: command runner, diagnosis, repair steps, backups, DNS, CLI, strings
  Ui/                 Dialog windows
  MainWindow.xaml     Main window
tests/Netzy.Tests/    xUnit tests
.github/workflows/    Continuous integration
```

## 📄 License

[MIT License](LICENSE) — GitHub: [furkanyasarr0/Netzy](https://github.com/furkanyasarr0/Netzy)

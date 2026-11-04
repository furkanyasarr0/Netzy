@echo off
title Netzy
cd /d "%~dp0"

:: Yonetici yetkisi kontrolu (UAC ile yetkili baslatma)
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [BILGI] Yonetici haklari talep ediliyor...
    powershell -Command "Start-Process '%~f0' -WorkingDirectory '%~dp0' -Verb RunAs"
    exit /b
)

:: Derlenmis EXE varsa doğrudan çalıştır, yoksa Python ile başlat
if exist "dist\Netzy.exe" (
    start "" "dist\Netzy.exe"
    exit /b
)

python main.py

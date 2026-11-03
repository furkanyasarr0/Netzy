@echo off
title Netzy - Ag Onarim ve Sifirlama
cd /d "%~dp0"

:: Yonetici yetkisi kontrolu (UAC ile yetkili baslatma)
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [BILGI] Yonetici haklari talep ediliyor...
    powershell -Command "Start-Process '%~f0' -WorkingDirectory '%~dp0' -Verb RunAs"
    exit /b
)

:: Uygulamayi baslat
python main.py

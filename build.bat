@echo off
title Netzy - Build Standalone EXE
cd /d "%~dp0"

echo [Netzy] EXE derleme baslatiliyor...
python -m PyInstaller --onefile --noconsole --name "Netzy" --icon "logo.ico" --add-data "logo.png;." --add-data "logo.ico;." --add-data "logo.jpg;." --collect-all customtkinter --uac-admin main.py

echo.
if %errorlevel% equ 0 (
    echo [BASARILI] Derleme tamamlandi: dist\Netzy.exe
) else (
    echo [HATA] Derleme sirasinda bir hata olustu.
)
pause

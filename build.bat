@echo off
title Netzy - Build
cd /d "%~dp0"

echo [Netzy] Testler calistiriliyor...
dotnet test Netzy.sln -c Release --nologo || goto :fail

echo [Netzy] EXE derleniyor...
dotnet build src\Netzy\Netzy.csproj -c Release --nologo || goto :fail

if not exist dist mkdir dist
copy /y src\Netzy\bin\Release\net48\Netzy.exe dist\Netzy.exe >nul || goto :fail
certutil -hashfile dist\Netzy.exe SHA256 | findstr /v ":" > dist\Netzy.exe.sha256

echo.
echo [BASARILI] dist\Netzy.exe hazir.
pause
exit /b 0

:fail
echo.
echo [HATA] Testler veya derleme basarisiz oldu.
pause
exit /b 1

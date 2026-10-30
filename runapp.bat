@echo off
setlocal
cd /d "%~dp0"

if "%1"=="installer" (
    echo building release...
    dotnet publish src\StreamMyScreen.App -c Release -o publish
    if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" (
        "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer\StreamMyScreen.iss
    ) else if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\StreamMyScreen.iss
    ) else (
        "C:\Program Files\Inno Setup 6\ISCC.exe" installer\StreamMyScreen.iss
    )
    echo [done] installer created in dist\
    exit /b 0
)

if not exist "engine\uxplay\uxplay-windows.exe" (
    echo downloading airplay engine...
    if not exist "engine\uxplay" mkdir "engine\uxplay"
    powershell -NoProfile -Command "curl.exe -L 'https://github.com/muse0x/uxplay-windows/releases/download/v2.0.0.1736/uxplay-windows.zip' -o 'uxplay.zip'; Expand-Archive 'uxplay.zip' -DestinationPath 'engine/uxplay' -Force; Remove-Item 'uxplay.zip'"
)

if exist "publish\StreamMyScreen.App.exe" (
    start "" "publish\StreamMyScreen.App.exe"
) else (
    echo building and launching...
    dotnet run --project src\StreamMyScreen.App
)
exit /b 0

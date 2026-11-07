@echo off
setlocal
set "ROOT=%~dp0"
set "OBJ=%ROOT%obj"
set "DIST=%ROOT%dist"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] .NET Framework csc.exe not found
    exit /b 1
)
if not exist "%OBJ%" mkdir "%OBJ%"
if not exist "%DIST%" mkdir "%DIST%"
if not exist "%ROOT%src\TinyPNGPro.App\Assets\icon.ico" (
    echo [ERROR] missing icon: src\TinyPNGPro.App\Assets\icon.ico
    exit /b 1
)

> "%OBJ%\sources.rsp" (
    for /r "%ROOT%src\TinyPNGPro.Core" %%f in (*.cs) do @echo "%%f"
    for /r "%ROOT%src\TinyPNGPro.App" %%f in (*.cs) do @echo "%%f"
)

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 ^
    /out:"%DIST%\TinyPNG Pro.exe" ^
    /win32icon:"%ROOT%src\TinyPNGPro.App\Assets\icon.ico" ^
    /win32manifest:"%ROOT%app.manifest" ^
    /resource:"%ROOT%src\TinyPNGPro.App\Assets\icon.png",IconPng ^
    /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
    /r:System.Net.Http.dll /r:System.Web.Extensions.dll ^
    @"%OBJ%\sources.rsp"

if errorlevel 1 (
    echo [ERROR] build failed
    exit /b 1
)

echo.
echo [OK] built: %DIST%\TinyPNG Pro.exe
exit /b 0

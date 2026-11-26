@echo off
rem Abre el Teleprompter. La primera vez lo compila con el .NET que ya trae Windows.
cd /d "%~dp0"
if exist "Teleprompter.exe" goto run

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
echo Preparando el teleprompter (solo la primera vez)...
"%CSC%" /nologo /optimize+ /target:winexe /out:Teleprompter.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll Teleprompter.cs
if errorlevel 1 (
  echo.
  echo No se pudo compilar. Revisa que Teleprompter.cs este en esta misma carpeta.
  echo Si el problema sigue, abri un "Issue" en el repositorio de GitHub.
  pause
  exit /b 1
)

:run
start "" "%~dp0Teleprompter.exe"

@echo off
rem Cria o instalador do MyGameLauncher.
rem Precisa de: .NET 10 SDK e Inno Setup 6 (https://jrsoftware.org/isdl.php).
cd /d "%~dp0"

echo.
echo [1/2] A preparar a app (com o .NET incluido)...
dotnet publish -c Release -o publish-instalador
if errorlevel 1 goto erro

set "ISCC="
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if exist "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
if not defined ISCC (
    echo.
    echo O Inno Setup 6 nao foi encontrado. Instala-o a partir de https://jrsoftware.org/isdl.php
    echo e volta a correr este ficheiro.
    pause
    exit /b 1
)

echo.
echo [2/2] A criar o instalador...
"%ISCC%" "installer\MyGameLauncher.iss"
if errorlevel 1 goto erro

echo.
echo Pronto! O instalador esta em: installer\Output
explorer "installer\Output"
pause
exit /b 0

:erro
echo.
echo Ocorreu um erro. Ve as mensagens acima.
pause
exit /b 1

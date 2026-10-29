@echo off
setlocal
set "taskApp=%~dp0src\PanificaB2B.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\PanificaB2B.App.exe"
if not exist "%taskApp%" (
    echo Compile o aplicativo para Windows no Visual Studio antes de iniciar.
    pause
    exit /b 1
)
echo A API deve estar aberta. Entre com o acesso de VENDEDOR criado no site.
echo No aplicativo, baixe a carteira antes de trabalhar offline.
"%taskApp%"
if errorlevel 1 (
    echo O aplicativo encerrou com erro. Copie a mensagem desta janela.
    pause
)

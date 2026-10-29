@echo off
setlocal
set "taskRoot=%~dp0"
if not exist "%taskRoot%src\PanificaB2B.Api\bin\Debug\net10.0\PanificaB2B.Api.dll" goto :compilar
if not exist "%taskRoot%src\PanificaB2B.Web\bin\Debug\net10.0\PanificaB2B.Web.dll" goto :compilar
where dotnet >nul 2>nul
if errorlevel 1 goto :runtime
start "Panifica - API" /D "%taskRoot%src\PanificaB2B.Api" dotnet .\bin\Debug\net10.0\PanificaB2B.Api.dll --environment Development --urls http://localhost:5187
start "Panifica - Site" /D "%taskRoot%src\PanificaB2B.Web" dotnet .\bin\Debug\net10.0\PanificaB2B.Web.dll --environment Development --urls http://localhost:5188
echo.
echo Aguarde as duas janelas mostrarem "Now listening on".
echo Depois abra http://localhost:5188 no navegador.
echo Para encerrar, pressione Ctrl+C nas duas janelas.
echo Este iniciador nao altera configuracoes do Windows.
pause
exit /b 0

:compilar
echo Compile a API e o site antes de iniciar.
pause
exit /b 1

:runtime
echo O comando dotnet nao foi encontrado. Verifique a instalacao existente do .NET 10.
pause
exit /b 1

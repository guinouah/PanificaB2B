# Runtime files stay under this checkout. This script does not change persistent environment settings.
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $taskRoot '.local\dotnet'
$env:NUGET_PACKAGES = Join-Path $taskRoot '.local\packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $taskRoot '.local\http-cache'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet restore (Join-Path $taskRoot 'PanificaB2B.sln') --configfile (Join-Path $taskRoot 'NuGet.Config')
if ($LASTEXITCODE -ne 0) { throw 'Falha na restauração.' }

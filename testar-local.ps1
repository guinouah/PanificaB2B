# Inicia somente os binários deste projeto; não instala nem altera configurações da máquina.
$ErrorActionPreference = 'Stop'
$processosTeste = @()
$taskDotnet = (Get-Command dotnet -ErrorAction Stop).Source
$taskLogs = Join-Path $PSScriptRoot 'App_Data\TesteLocal'

function Iniciar-ProjetoLocal([string] $nome, [string] $url, [string] $verificacao) {
    $pasta = Join-Path $PSScriptRoot "src\$nome"
    $binario = Join-Path $pasta "bin\Debug\net10.0\$nome.dll"
    if (-not (Test-Path -LiteralPath $binario)) {
        throw "Compile $nome no Visual Studio antes de iniciar o teste."
    }
    $processo = Start-Process -FilePath $taskDotnet -ArgumentList @(
        ('"{0}"' -f $binario), '--environment', 'Development', '--urls', $url
    ) -WorkingDirectory $pasta -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $taskLogs "$nome.log") `
        -RedirectStandardError (Join-Path $taskLogs "$nome.erros.log")
    $script:processosTeste += $processo
    $limite = (Get-Date).AddSeconds(20)
    do {
        $processo.Refresh()
        if ($processo.HasExited) { throw "$nome encerrou. Confira os registros em $taskLogs." }
        try {
            $resposta = Invoke-WebRequest -Uri $verificacao -UseBasicParsing -TimeoutSec 2
            if ($resposta.StatusCode -eq 200) { return }
        } catch { }
        Start-Sleep -Milliseconds 300
    } while ((Get-Date) -lt $limite)
    throw "$nome não respondeu. Confira os registros em $taskLogs."
}

try {
    New-Item -ItemType Directory -Path $taskLogs -Force | Out-Null
    Iniciar-ProjetoLocal 'PanificaB2B.Api' 'http://localhost:5187' 'http://localhost:5187/api/saude'
    Iniciar-ProjetoLocal 'PanificaB2B.Web' 'http://localhost:5188' 'http://localhost:5188/Conta/Entrar'
    Write-Host ''
    Write-Host 'Abra http://localhost:5188/Grupos no navegador.'
    Write-Host 'No primeiro acesso, crie o administrador e entre.'
    Read-Host 'Pressione Enter quando terminar para encerrar API e site' | Out-Null
} finally {
    foreach ($processoTeste in $processosTeste) {
        if (-not $processoTeste.HasExited) { Stop-Process -Id $processoTeste.Id -ErrorAction SilentlyContinue }
        $processoTeste.Dispose()
    }
}

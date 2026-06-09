$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$images = @(
    @{ Name = "Gateway.Yarp"; Image = "gatewayplayground-gateway-yarp:latest"; Dockerfile = "src/Gateway.Yarp/Dockerfile" },
    @{ Name = "Gateway.Ocelot"; Image = "gatewayplayground-gateway-ocelot:latest"; Dockerfile = "src/Gateway.Ocelot/Dockerfile" },
    @{ Name = "Monolith"; Image = "gatewayplayground-monolith:latest"; Dockerfile = "src/Monolith/Dockerfile" },
    @{ Name = "SearchService"; Image = "gatewayplayground-searchservice:latest"; Dockerfile = "src/SearchService/Dockerfile" },
    @{ Name = "MailService"; Image = "gatewayplayground-mailservice:latest"; Dockerfile = "src/MailService/Dockerfile" },
    @{ Name = "DeduplicationService"; Image = "gatewayplayground-deduplicationservice:latest"; Dockerfile = "src/DeduplicationService/Dockerfile" },
    @{ Name = "UserService"; Image = "gatewayplayground-userservice:latest"; Dockerfile = "src/UserService/Dockerfile" }
)

if ($null -eq (Get-Command "docker" -ErrorAction SilentlyContinue)) {
    throw "Docker не найден в PATH."
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Команда завершилась с ошибкой ${LASTEXITCODE}: $Command $($Arguments -join ' ')"
    }
}

Write-Host "Сборка Docker images для Kubernetes manifests..." -ForegroundColor Cyan

foreach ($item in $images) {
    if (-not (Test-Path $($item.Dockerfile))) {
        throw "Dockerfile не найден: $($item.Dockerfile)"
    }

    Write-Host ""
    Write-Host "Собираю $($item.Name): $($item.Image)" -ForegroundColor Cyan
    Invoke-NativeCommand "docker" @("build", "-f", $($item.Dockerfile), "-t", $($item.Image), ".")
}

Write-Host ""
Write-Host "Сборка завершена. Локальные images:" -ForegroundColor Green
foreach ($item in $images) {
    Invoke-NativeCommand "docker" @("image", "ls", $($item.Image))
}

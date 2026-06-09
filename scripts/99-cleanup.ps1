$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$overlay = "k8s/overlays/local"

if ($null -eq (Get-Command "kubectl" -ErrorAction SilentlyContinue)) {
    throw "kubectl не найден в PATH."
}

if (-not (Test-Path $overlay)) {
    throw "Kustomize overlay не найден: $overlay"
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

Write-Host "Удаляю Kubernetes resources: kubectl delete -k $overlay" -ForegroundColor Cyan
Invoke-NativeCommand "kubectl" @("delete", "-k", $overlay, "--ignore-not-found=true")

Write-Host ""
Write-Host "Ресурсы приложения удалены. ingress-nginx не удалялся, потому что он устанавливается отдельно через Helm." -ForegroundColor Green
Write-Host "Если нужно удалить ingress-nginx:"
Write-Host "helm uninstall ingress-nginx -n ingress-nginx" -ForegroundColor Yellow
Write-Host "kubectl delete namespace ingress-nginx" -ForegroundColor Yellow

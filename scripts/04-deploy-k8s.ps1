$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$namespace = "gateway-playground"
$overlay = "k8s/overlays/local"
$deployments = @(
    "keycloak",
    "search-service",
    "mail-service",
    "deduplication-service",
    "user-service",
    "gateway-yarp",
    "gateway-ocelot",
    "monolith"
)

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

Write-Host "Применяю Kubernetes manifests: kubectl apply -k $overlay" -ForegroundColor Cyan
Invoke-NativeCommand "kubectl" @("apply", "-k", $overlay)

Write-Host ""
Write-Host "Ожидаю готовность deployments в namespace $namespace..." -ForegroundColor Cyan
foreach ($deployment in $deployments) {
    Write-Host "Deployment/$deployment"
    Invoke-NativeCommand "kubectl" @("rollout", "status", "deployment/$deployment", "-n", $namespace, "--timeout=240s")
}

Write-Host ""
Write-Host "Pods:" -ForegroundColor Cyan
Invoke-NativeCommand "kubectl" @("get", "pods", "-n", $namespace, "-o", "wide")

Write-Host ""
Write-Host "Services:" -ForegroundColor Cyan
Invoke-NativeCommand "kubectl" @("get", "services", "-n", $namespace)

Write-Host ""
Write-Host "Ingress:" -ForegroundColor Cyan
Invoke-NativeCommand "kubectl" @("get", "ingress", "-n", $namespace)

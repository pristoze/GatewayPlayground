$ErrorActionPreference = "Stop"

$namespace = "ingress-nginx"
$releaseName = "ingress-nginx"
$repoName = "ingress-nginx"
$repoUrl = "https://kubernetes.github.io/ingress-nginx"

function Assert-CommandExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Команда '$Name' не найдена в PATH."
    }
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

Assert-CommandExists "kubectl"
Assert-CommandExists "helm"

Write-Host "Установка ingress-nginx через Helm..." -ForegroundColor Cyan

$repoExists = $false
$reposJson = helm repo list --output json 2>$null
if ($reposJson) {
    $repos = $reposJson | ConvertFrom-Json
    $repoExists = $null -ne ($repos | Where-Object { $_.name -eq $repoName })
}

if (-not $repoExists) {
    Write-Host "Добавляю Helm repository $repoName..."
    Invoke-NativeCommand "helm" @("repo", "add", $repoName, $repoUrl)
}
else {
    Write-Host "Helm repository $repoName уже добавлен."
}

Write-Host "Обновляю Helm repository $repoName..."
Invoke-NativeCommand "helm" @("repo", "update", $repoName)

Write-Host "Устанавливаю или обновляю release $releaseName в namespace $namespace..."
Invoke-NativeCommand "helm" @(
    "upgrade",
    "--install",
    $releaseName,
    "$repoName/$releaseName",
    "--namespace",
    $namespace,
    "--create-namespace",
    "--set",
    "controller.ingressClassResource.name=nginx",
    "--set",
    "controller.ingressClass=nginx",
    "--set",
    "controller.ingressClassResource.default=false"
)

Write-Host "Ожидаю готовность ingress-nginx controller..."
Invoke-NativeCommand "kubectl" @("rollout", "status", "deployment/ingress-nginx-controller", "-n", $namespace, "--timeout=180s")

Write-Host ""
Write-Host "Controller pods:" -ForegroundColor Cyan
Invoke-NativeCommand "kubectl" @("get", "pods", "-n", $namespace, "-l", "app.kubernetes.io/component=controller", "-o", "wide")

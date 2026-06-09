$ErrorActionPreference = "Stop"

$expectedContext = "docker-desktop"
$hasErrors = $false

function Test-CommandExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Write-Ok {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-Fail {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

Write-Host "Проверка локальных prerequisites для GatewayPlayground Kubernetes..." -ForegroundColor Cyan

if (Test-CommandExists "docker") {
    $dockerVersion = docker --version
    Write-Ok "Docker найден: $dockerVersion"

    docker info *> $null
    if ($LASTEXITCODE -eq 0) {
        Write-Ok "Docker Engine доступен."
    }
    else {
        Write-Fail "Docker установлен, но Docker Engine недоступен. Запустите Docker Desktop."
        $hasErrors = $true
    }
}
else {
    Write-Fail "Docker не найден в PATH. Установите Docker Desktop."
    $hasErrors = $true
}

if (Test-CommandExists "kubectl") {
    $kubectlVersionJson = kubectl version --client=true --output=json 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($kubectlVersionJson)) {
        $kubectlVersion = $kubectlVersionJson | ConvertFrom-Json
        Write-Ok "kubectl найден: $($kubectlVersion.clientVersion.gitVersion)"
    }
    else {
        Write-Fail "kubectl найден, но команда 'kubectl version --client' завершилась с ошибкой."
        $hasErrors = $true
    }

    $currentContext = kubectl config current-context 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($currentContext)) {
        Write-Ok "Текущий kubectl context: $currentContext"

        if ($currentContext -ne $expectedContext) {
            Write-Warn "Ожидаемый context для Docker Desktop Kubernetes: $expectedContext. Переключение: kubectl config use-context $expectedContext"
        }
    }
    else {
        Write-Fail "Не удалось получить текущий kubectl context. Проверьте, что Kubernetes включен в Docker Desktop."
        $hasErrors = $true
    }
}
else {
    Write-Fail "kubectl не найден в PATH. Установите kubectl или включите интеграцию Docker Desktop."
    $hasErrors = $true
}

if (Test-CommandExists "helm") {
    $helmVersion = helm version --short 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($helmVersion)) {
        Write-Ok "helm найден: $helmVersion"
    }
    else {
        Write-Fail "helm найден, но команда 'helm version' завершилась с ошибкой."
        $hasErrors = $true
    }
}
else {
    Write-Fail "helm не найден в PATH. Установите Helm перед установкой ingress-nginx."
    $hasErrors = $true
}

if ($hasErrors) {
    Write-Host ""
    Write-Fail "Проверка завершилась с ошибками. Исправьте prerequisites и запустите скрипт повторно."
    exit 1
}

Write-Host ""
Write-Ok "Prerequisites готовы для локального запуска через Docker Desktop Kubernetes."

param(
    [string]$YarpBaseUrl = "http://yarp.gateway-playground.local",
    [string]$UserToken = $env:GATEWAY_PLAYGROUND_USER_TOKEN,
    [string]$AdminToken = $env:GATEWAY_PLAYGROUND_ADMIN_TOKEN
)

$ErrorActionPreference = "Stop"

function Invoke-HttpStatus {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        [string]$Token
    )

    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $headers["Authorization"] = "Bearer $Token"
    }

    try {
        $response = Invoke-WebRequest -Uri $Uri -Method Get -Headers $headers -UseBasicParsing
        return [int]$response.StatusCode
    }
    catch {
        $response = $_.Exception.Response
        if ($null -ne $response -and $null -ne $response.StatusCode) {
            return [int]$response.StatusCode
        }

        throw
    }
}

function Test-ExpectedStatus {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        [string]$Token,
        [Parameter(Mandatory = $true)]
        [int]$ExpectedStatus
    )

    $actualStatus = Invoke-HttpStatus -Uri $Uri -Token $Token
    if ($actualStatus -eq $ExpectedStatus) {
        Write-Host "[OK] $Name -> $actualStatus" -ForegroundColor Green
        return $true
    }

    Write-Host "[FAIL] $Name -> ожидался $ExpectedStatus, получен $actualStatus" -ForegroundColor Red
    return $false
}

if ([string]::IsNullOrWhiteSpace($UserToken) -or [string]::IsNullOrWhiteSpace($AdminToken)) {
    Write-Host "Не найдены env-переменные с токенами." -ForegroundColor Red
    Write-Host "Сначала выполните .\scripts\06-get-keycloak-token.ps1 и затем команды, которые он выведет." -ForegroundColor Yellow
    exit 1
}

$userEndpoint = "$YarpBaseUrl/api/search/info"
$adminEndpoint = "$YarpBaseUrl/api/search/admin"
$passed = $true

Write-Host "Проверяю YARP authorization flow: $YarpBaseUrl" -ForegroundColor Cyan

$passed = (Test-ExpectedStatus -Name "Без токена" -Uri $userEndpoint -ExpectedStatus 401) -and $passed
$passed = (Test-ExpectedStatus -Name "User token на user endpoint" -Uri $userEndpoint -Token $UserToken -ExpectedStatus 200) -and $passed
$passed = (Test-ExpectedStatus -Name "User token на admin endpoint" -Uri $adminEndpoint -Token $UserToken -ExpectedStatus 403) -and $passed
$passed = (Test-ExpectedStatus -Name "Admin token на admin endpoint" -Uri $adminEndpoint -Token $AdminToken -ExpectedStatus 200) -and $passed

if (-not $passed) {
    Write-Host ""
    Write-Host "Проверка YARP завершилась с ошибками." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Проверка YARP завершилась успешно." -ForegroundColor Green

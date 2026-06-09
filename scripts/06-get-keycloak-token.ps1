param(
    [string]$KeycloakBaseUrl = "http://keycloak.gateway-playground.local",
    [string]$Realm = "gateway-playground",
    [string]$ClientId = "gateway-playground-api"
)

$ErrorActionPreference = "Stop"

function Get-KeycloakAccessToken {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Username,
        [Parameter(Mandatory = $true)]
        [string]$Password
    )

    $tokenUrl = "$KeycloakBaseUrl/realms/$Realm/protocol/openid-connect/token"
    $body = @{
        grant_type = "password"
        client_id = $ClientId
        username = $Username
        password = $Password
    }

    Write-Host "Получаю access token для пользователя '$Username'..."
    try {
        $response = Invoke-RestMethod -Method Post -Uri $tokenUrl -ContentType "application/x-www-form-urlencoded" -Body $body
    }
    catch {
        Write-Host "Не удалось получить access token для пользователя '$Username'." -ForegroundColor Red

        if ($_.ErrorDetails -and -not [string]::IsNullOrWhiteSpace($_.ErrorDetails.Message)) {
            Write-Host $_.ErrorDetails.Message -ForegroundColor Yellow
        }

        Write-Host "Если Keycloak вернул invalid_grant / Account is not fully set up, realm уже мог быть импортирован до исправления realm-export.json." -ForegroundColor Yellow
        Write-Host "Для локального PoC пересоздайте namespace gateway-playground и разверните k8s manifests заново." -ForegroundColor Yellow
        throw
    }

    return $response.access_token
}

Write-Host "Запрос токенов Keycloak: $KeycloakBaseUrl/realms/$Realm" -ForegroundColor Cyan

$userToken = Get-KeycloakAccessToken -Username "testuser" -Password "testuser"
$adminToken = Get-KeycloakAccessToken -Username "admin" -Password "admin"

Write-Host ""
Write-Host "Выполните эти команды в текущем PowerShell окне, чтобы сохранить токены в env-переменные:" -ForegroundColor Green
Write-Host ""
Write-Host "`$env:GATEWAY_PLAYGROUND_USER_TOKEN = `"$userToken`"" -ForegroundColor Yellow
Write-Host "`$env:GATEWAY_PLAYGROUND_ADMIN_TOKEN = `"$adminToken`"" -ForegroundColor Yellow
Write-Host ""
Write-Host "После этого можно запускать: .\scripts\07-test-yarp.ps1"

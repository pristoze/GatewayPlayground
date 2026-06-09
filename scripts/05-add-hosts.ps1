$ErrorActionPreference = "Stop"

$namespace = "gateway-playground"
$ingressName = "gateway-playground"
$hostsPath = "C:\Windows\System32\drivers\etc\hosts"
$hosts = @(
    "yarp.gateway-playground.local",
    "ocelot.gateway-playground.local",
    "monolith.gateway-playground.local",
    "keycloak.gateway-playground.local"
)

$ingressAddress = "127.0.0.1"

if ($null -ne (Get-Command "kubectl" -ErrorAction SilentlyContinue)) {
    try {
        $ip = kubectl get ingress $ingressName -n $namespace -o jsonpath="{.status.loadBalancer.ingress[0].ip}" 2>$null
        $hostname = kubectl get ingress $ingressName -n $namespace -o jsonpath="{.status.loadBalancer.ingress[0].hostname}" 2>$null

        if (-not [string]::IsNullOrWhiteSpace($ip)) {
            $ingressAddress = $ip
        }
        elseif (-not [string]::IsNullOrWhiteSpace($hostname)) {
            $ingressAddress = $hostname
        }
    }
    catch {
        Write-Host "Не удалось получить адрес ingress через kubectl. Для Docker Desktop обычно используется 127.0.0.1." -ForegroundColor Yellow
    }
}

Write-Host "Добавьте следующую строку в ${hostsPath}:" -ForegroundColor Cyan
Write-Host ""
Write-Host "$ingressAddress $($hosts -join ' ')" -ForegroundColor Green
Write-Host ""
Write-Host "Скрипт не редактирует hosts автоматически."
Write-Host "Откройте файл от имени администратора, например:"
Write-Host 'Start-Process notepad "C:\Windows\System32\drivers\etc\hosts" -Verb RunAs' -ForegroundColor Yellow
Write-Host ""
Write-Host "После изменения проверьте разрешение имени:"
Write-Host "Resolve-DnsName yarp.gateway-playground.local" -ForegroundColor Yellow

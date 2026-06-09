# Локальный запуск Kubernetes на Windows 11

Документ описывает запуск `GatewayPlayground` через Docker Desktop Kubernetes.

Целевая схема:

```text
Windows 11
  |
Docker Desktop Kubernetes
  |
NGINX Ingress Controller
  |
Gateway.Yarp / Gateway.Ocelot / Monolith / Services / Keycloak
```

## Требования

- Windows 11.
- Docker Desktop с включенным Kubernetes.
- `kubectl` в `PATH`.
- `helm` в `PATH`.
- Активный `kubectl` context для Docker Desktop: `docker-desktop`.
- PowerShell 5.1+ или PowerShell 7+.

Проверка окружения:

```powershell
.\scripts\01-check-prerequisites.ps1
```

Если выполнение `.ps1` запрещено текущей политикой PowerShell, запустите для текущего окна:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
```

## Порядок запуска

Все команды выполняются из корня репозитория `GatewayPlayground`.

1. Проверьте prerequisites:

```powershell
.\scripts\01-check-prerequisites.ps1
```

2. Установите NGINX Ingress Controller:

```powershell
.\scripts\02-install-nginx-ingress.ps1
```

3. Соберите локальные Docker images:

```powershell
.\scripts\03-build-images.ps1
```

Скрипт собирает images с тегами, которые используются в `k8s/overlays/local/kustomization.yaml`:

```text
gatewayplayground-gateway-yarp:latest
gatewayplayground-gateway-ocelot:latest
gatewayplayground-monolith:latest
gatewayplayground-searchservice:latest
gatewayplayground-mailservice:latest
gatewayplayground-deduplicationservice:latest
gatewayplayground-userservice:latest
```

4. Разверните приложение в Kubernetes:

```powershell
.\scripts\04-deploy-k8s.ps1
```

Скрипт выполняет:

```powershell
kubectl apply -k k8s/overlays/local
```

и ожидает готовность deployment'ов в namespace `gateway-playground`.

5. Добавьте локальные hosts-записи:

```powershell
.\scripts\05-add-hosts.ps1
```

Скрипт только выводит строки для файла:

```text
C:\Windows\System32\drivers\etc\hosts
```

Откройте файл от имени администратора и добавьте выведенную строку. Для Docker Desktop обычно используется:

```text
127.0.0.1 yarp.gateway-playground.local ocelot.gateway-playground.local monolith.gateway-playground.local keycloak.gateway-playground.local
```

6. Получите Keycloak токены:

```powershell
.\scripts\06-get-keycloak-token.ps1
```

Скрипт выведет PowerShell-команды для сохранения токенов в переменные окружения текущего окна:

```powershell
$env:GATEWAY_PLAYGROUND_USER_TOKEN = "<access-token>"
$env:GATEWAY_PLAYGROUND_ADMIN_TOKEN = "<access-token>"
```

Выполните эти команды в том же PowerShell окне.

7. Проверьте YARP authorization flow:

```powershell
.\scripts\07-test-yarp.ps1
```

Скрипт проверяет сценарии:

```text
GET /api/search/info без токена -> 401
GET /api/search/info с User token -> 200
GET /api/search/admin с User token -> 403
GET /api/search/admin с Admin token -> 200
```

## Проверка Keycloak

Адрес Keycloak:

```text
http://keycloak.gateway-playground.local
```

Admin Console:

```text
username: admin
password: admin
realm: gateway-playground
```

Локальные пользователи realm:

```text
admin / admin       roles: Admin, User
testuser / testuser roles: User
```

OIDC token endpoint:

```text
http://keycloak.gateway-playground.local/realms/gateway-playground/protocol/openid-connect/token
```

Client:

```text
gateway-playground-api
```

## Проверка YARP

Swagger UI:

```text
http://yarp.gateway-playground.local/swagger
```

Проверка через PowerShell:

```powershell
Invoke-RestMethod `
  -Uri "http://yarp.gateway-playground.local/api/search/info" `
  -Headers @{ Authorization = "Bearer $env:GATEWAY_PLAYGROUND_USER_TOKEN" }
```

Admin endpoint:

```powershell
Invoke-RestMethod `
  -Uri "http://yarp.gateway-playground.local/api/search/admin" `
  -Headers @{ Authorization = "Bearer $env:GATEWAY_PLAYGROUND_ADMIN_TOKEN" }
```

## Проверка Ocelot

Swagger UI:

```text
http://ocelot.gateway-playground.local/swagger
```

Проверка маршрутизации:

```powershell
Invoke-RestMethod `
  -Uri "http://ocelot.gateway-playground.local/api/search/info" `
  -Headers @{ Authorization = "Bearer $env:GATEWAY_PLAYGROUND_USER_TOKEN" }
```

Важно: текущая security-модель переносит authentication/authorization в `Gateway.Yarp`. Для Ocelot gateway проверяется маршрутизация и swagger aggregation. Авторизацию через Ocelot отдельно не включали.

## Проверка ресурсов Kubernetes

Namespace приложения:

```text
gateway-playground
```

Команды диагностики:

```powershell
kubectl get pods -n gateway-playground -o wide
kubectl get services -n gateway-playground
kubectl get ingress -n gateway-playground
kubectl describe ingress gateway-playground -n gateway-playground
```

Логи:

```powershell
kubectl logs deployment/gateway-yarp -n gateway-playground
kubectl logs deployment/gateway-ocelot -n gateway-playground
kubectl logs deployment/keycloak -n gateway-playground
```

## Очистка окружения

Удалить ресурсы приложения:

```powershell
.\scripts\99-cleanup.ps1
```

Скрипт выполняет:

```powershell
kubectl delete -k k8s/overlays/local --ignore-not-found=true
```

NGINX Ingress Controller не удаляется автоматически. Если он больше не нужен:

```powershell
helm uninstall ingress-nginx -n ingress-nginx
kubectl delete namespace ingress-nginx
```

## Типовые ошибки

### Docker Engine недоступен

Симптом:

```text
Docker установлен, но Docker Engine недоступен
```

Решение: запустите Docker Desktop и дождитесь статуса `Running`.

### Kubernetes не включен в Docker Desktop

Симптомы:

```text
kubectl config current-context не возвращает docker-desktop
kubectl get nodes завершается ошибкой
```

Решение: включите Kubernetes в Docker Desktop settings и переключите context:

```powershell
kubectl config use-context docker-desktop
kubectl get nodes
```

### Helm не найден

Симптом:

```text
helm не найден в PATH
```

Решение: установите Helm, например через Winget:

```powershell
winget install Helm.Helm
```

После установки откройте новое PowerShell окно.

### Ingress host не открывается в браузере

Симптом:

```text
http://yarp.gateway-playground.local не открывается
```

Проверьте:

```powershell
kubectl get pods -n ingress-nginx
kubectl get ingress -n gateway-playground
Resolve-DnsName yarp.gateway-playground.local
```

Если DNS не резолвится, повторите `.\scripts\05-add-hosts.ps1` и добавьте hosts-запись от имени администратора.

### Pod в ImagePullBackOff или ErrImagePull

Симптом:

```text
ImagePullBackOff
ErrImagePull
```

Решение: пересоберите локальные images:

```powershell
.\scripts\03-build-images.ps1
kubectl rollout restart deployment -n gateway-playground
```

Для Docker Desktop Kubernetes локальные images Docker Engine доступны Kubernetes напрямую. Для `kind` или `minikube` этот сценарий не подходит без отдельной загрузки images.

### Keycloak токен не получается

Симптом:

```text
Invoke-RestMethod не может подключиться к keycloak.gateway-playground.local
```

Проверьте:

```powershell
kubectl get pods -n gateway-playground
kubectl logs deployment/keycloak -n gateway-playground
Resolve-DnsName keycloak.gateway-playground.local
```

Keycloak может стартовать дольше остальных компонентов. Дождитесь готовности pod'а и повторите:

```powershell
.\scripts\06-get-keycloak-token.ps1
```

### 401 с валидным токеном

Возможные причины:

- Токен получен до корректной настройки hosts и issuer отличается от ожидаемого.
- Переменные окружения не установлены в текущем PowerShell окне.
- Gateway.Yarp не может прочитать OIDC metadata от Keycloak внутри namespace.

Проверка:

```powershell
echo $env:GATEWAY_PLAYGROUND_USER_TOKEN
kubectl logs deployment/gateway-yarp -n gateway-playground
kubectl get configmap keycloak-auth-config -n gateway-playground -o yaml
```

После исправления получите токены заново:

```powershell
.\scripts\06-get-keycloak-token.ps1
```

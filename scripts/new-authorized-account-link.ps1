param(
    [Parameter(Mandatory=$true)][string] $Username,
    [Parameter(Mandatory=$true)][Guid] $CoachId,
    [Parameter(Mandatory=$true)][string] $Reason,
    [switch] $AuthorizedByOwner,
    [string] $ApiBase = 'http://127.0.0.1:5241'
)
$ErrorActionPreference = 'Stop'
if (-not $AuthorizedByOwner) { throw 'Este procedimento exige autorização explícita do proprietário: use -AuthorizedByOwner somente após comprovar a posse da vaga.' }
$apiUri = [Uri] $ApiBase
if (-not $apiUri.IsLoopback) { throw 'Procedimento exclusivo para a API local de teste.' }
$taskRoot = Split-Path -Parent $PSScriptRoot
$secretFile = Join-Path $taskRoot 'artifacts/account-login/migration-admin.secret'
if (-not (Test-Path -LiteralPath $secretFile)) { throw 'Inicie a prévia de contas para preparar o segredo administrativo local.' }
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$securePassword = Read-Host 'Senha da conta que receberá a vaga' -AsSecureString
$credential = New-Object System.Management.Automation.PSCredential($Username, $securePassword)
function Invoke-LinkCommand([string] $Path, [hashtable] $Body, [hashtable] $ExtraHeaders = @{}) {
    $csrf = Invoke-RestMethod "$ApiBase/api/auth/csrf" -WebSession $session
    $headers = @{ 'X-CSRF-TOKEN' = $csrf.csrfToken }
    foreach ($key in $ExtraHeaders.Keys) { $headers[$key] = $ExtraHeaders[$key] }
    Invoke-RestMethod "$ApiBase$Path" -Method Post -WebSession $session -Headers $headers -ContentType 'application/json' -Body ($Body | ConvertTo-Json)
}
try {
    $plainPassword = $credential.GetNetworkCredential().Password
    $null = Invoke-LinkCommand '/api/auth/login' @{ username=$Username; password=$plainPassword }
    $plainPassword = $null
    $grant = Invoke-LinkCommand '/api/accounts/legacy-link-grants' @{ username=$Username; coachId=$CoachId; reason=$Reason } @{ 'X-Account-Migration-Token'=[IO.File]::ReadAllText($secretFile) }
    Write-Output "Código para PERFIL E VÍNCULOS, válido por 30 minutos e uma única utilização: $($grant.code)"
} finally {
    $plainPassword = $null
    try { $null = Invoke-LinkCommand '/api/auth/logout' @{} } catch { }
    $securePassword.Dispose()
}

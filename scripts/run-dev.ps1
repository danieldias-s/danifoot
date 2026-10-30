param(
    [int]$ApiPort = 5080,
    [ValidateSet('PostgreSQL', 'Sqlite')][string]$DatabaseProvider = 'PostgreSQL',
    [string]$DatabasePath,
    [switch]$GenerateSeedOnly
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

Write-Host "=== ELIFOOT CLASSIC MULTIPLAYER - DEV LAUNCHER (WINDOWS) ===" -ForegroundColor Green

if ($GenerateSeedOnly) {
    dotnet run --project tools/seed-generator/Elifoot.SeedGenerator.csproj -c Release -- --clubs 128 --seed 199801 --output tools/seed-generator/seed-128-clubs.json
    exit 0
}

if (-not (Test-Path "tools/seed-generator/seed-128-clubs.json")) {
    Write-Host "Gerando pacote inicial de 128 clubes..." -ForegroundColor Yellow
    dotnet run --project tools/seed-generator/Elifoot.SeedGenerator.csproj -c Release -- --clubs 128 --seed 199801 --output tools/seed-generator/seed-128-clubs.json
}

$env:ASPNETCORE_ENVIRONMENT = "LocalDev"
$env:Elifoot__DatabaseProvider = $DatabaseProvider
if ($DatabaseProvider -eq 'Sqlite') {
    if (-not $DatabasePath) { $DatabasePath = Join-Path $RepoRoot 'elifoot-local.db' }
    $env:ConnectionStrings__DefaultConnection = "Data Source=$([IO.Path]::GetFullPath($DatabasePath))"
}
$env:ASPNETCORE_URLS = "http://127.0.0.1:$ApiPort"

Write-Host "Iniciando API autoritativa (.NET 10 + SignalR + $DatabaseProvider) em http://127.0.0.1:$ApiPort ..." -ForegroundColor Cyan
dotnet run --project src/Elifoot.Api/Elifoot.Api.csproj -c Release --no-launch-profile --urls "http://127.0.0.1:$ApiPort"

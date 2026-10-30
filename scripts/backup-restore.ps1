param(
    [ValidateSet("backup", "restore")]
    [string]$Action = "backup",
    [string]$ApiUrl = "http://localhost:5080",
    [string]$WorldId = "",
    [string]$FilePath = "backups/world-backup.json"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

if ($Action -eq "backup") {
    if ([string]::IsNullOrWhiteSpace($WorldId)) {
        $worlds = Invoke-RestMethod -Uri "$ApiUrl/api/worlds" -Method Get
        if ($worlds.Count -eq 0) { throw "Nenhum mundo encontrado na API." }
        $WorldId = $worlds[0].id
    }
    $dir = Split-Path -Parent $FilePath
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
    $pkg = Invoke-RestMethod -Uri "$ApiUrl/api/worlds/$WorldId/editor/export-json" -Method Get
    $pkg | ConvertTo-Json -Depth 20 | Set-Content -Path $FilePath -Encoding UTF8
    Write-Host "Backup salvo com sucesso em: $FilePath" -ForegroundColor Green
}
else {
    if (-not (Test-Path $FilePath)) { throw "Arquivo de backup não encontrado: $FilePath" }
    $json = Get-Content -Path $FilePath -Raw -Encoding UTF8
    $res = Invoke-RestMethod -Uri "$ApiUrl/api/worlds/editor/import-package?newWorldName=Mundo_Restaurado" -Method Post -ContentType "application/json" -Body $json
    Write-Host "Mundo restaurado com sucesso! Novo WorldId: $($res.worldId) | Código: $($res.inviteCode)" -ForegroundColor Green
}

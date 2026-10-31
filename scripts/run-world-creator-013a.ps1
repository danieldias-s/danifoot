param([int]$ApiPort = 5133, [switch]$Fixtures)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if ($taskRoot -ne 'D:\elifoot_98_013A') { throw 'Worktree isolado obrigatório.' }
& (Join-Path $PSScriptRoot 'test-world-creator-013a.ps1') -Action Start
if ($LASTEXITCODE -ne 0) { throw 'Falha no PostgreSQL isolado.' }
$env:ASPNETCORE_URLS = "http://127.0.0.1:$ApiPort"
$env:ASPNETCORE_ENVIRONMENT = 'LocalDev'
$env:Elifoot__DatabaseProvider = 'PostgreSQL'
$env:ConnectionStrings__DefaultConnection = 'Host=127.0.0.1;Port=55433;Database=elifoot_013a_worlds;Username=elifoot013a'
$env:ConnectionStrings__Catalog013A = 'Host=127.0.0.1;Port=55433;Database=elifoot_013a_catalog;Username=elifoot013a'
$env:Catalog013A__Enabled = 'true'
$env:Catalog013A__LoadFixtures = $Fixtures.IsPresent.ToString()
$env:Elifoot__AutoSeedDefaultWorld = 'false'
$env:Elifoot__AuthMode = 'LocalDev'
dotnet run --no-launch-profile --project (Join-Path $taskRoot 'src\Elifoot.Api') -c Release

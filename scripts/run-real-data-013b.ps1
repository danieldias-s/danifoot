param([int]$ApiPort = 5134, [switch]$Fixtures, [switch]$WikidataPilot, [string]$ProviderManifest)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if ($taskRoot -ne 'D:\elifoot_98_013A') { throw 'Worktree isolado obrigatório.' }
& (Join-Path $PSScriptRoot 'test-real-data-013b.ps1') -Action Start
if ($LASTEXITCODE -ne 0) { throw 'Falha no PostgreSQL isolado.' }
$env:ASPNETCORE_URLS = "http://127.0.0.1:$ApiPort"
$env:ASPNETCORE_ENVIRONMENT = 'LocalDev'
$env:Elifoot__DatabaseProvider = 'PostgreSQL'
$env:ConnectionStrings__DefaultConnection = 'Host=127.0.0.1;Port=55434;Database=elifoot_013b_worlds;Username=elifoot013b'
$env:ConnectionStrings__Catalog013A = 'Host=127.0.0.1;Port=55434;Database=elifoot_013b_catalog;Username=elifoot013b'
$env:Catalog013B__Enabled = 'true'
$env:Catalog013A__Enabled = 'true'
$env:Catalog013A__LoadFixtures = $Fixtures.IsPresent.ToString()
$env:Elifoot__AutoSeedDefaultWorld = 'false'
$env:Elifoot__AuthMode = 'LocalDev'
if ($WikidataPilot -and $ProviderManifest) { throw "Escolha WikidataPilot ou ProviderManifest." }
if ($WikidataPilot) { $env:Catalog013B__ProviderManifest = Join-Path $taskRoot "tools\catalog-013b\wikidata-pilot.json" }
elseif ($ProviderManifest) { $env:Catalog013B__ProviderManifest = [IO.Path]::GetFullPath($ProviderManifest) }
else { Remove-Item Env:\Catalog013B__ProviderManifest -ErrorAction SilentlyContinue }
dotnet run --no-launch-profile --project (Join-Path $taskRoot 'src\Elifoot.Api') -c Release

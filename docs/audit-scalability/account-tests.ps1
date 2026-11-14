param([Parameter(Mandatory)][string]$OutputDirectory, [string]$NuGetSource)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = New-Item -ItemType Directory -Path $OutputDirectory
$keys = New-Item -ItemType Directory -Path (Join-Path $output.FullName 'keys')
$env:AuditKeyDirectory = $keys.FullName
$env:ASPNETCORE_TEST_CONTENTROOT_ELIFOOT_API = Join-Path $repo 'src/Elifoot.Api'
$env:Logging__EventLog__LogLevel__Default = 'None'
$env:TEMP = $output.FullName
$env:TMP = $output.FullName

# Use the repository's tests with a key store owned by this fixture.
$source = Get-Content -Raw (Join-Path $repo 'tests/Elifoot.Integration.Tests/AccountLoginTests.cs')
$anchor = 'b.UseSetting("Accounts:MigrationAdminToken", AdminToken);'
if (!$source.Contains($anchor)) { throw 'Fixture de autenticação mudou.' }
$keysSetup = 'b.ConfigureServices(services => services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Environment.GetEnvironmentVariable("AuditKeyDirectory")!)));'
$source = "using Microsoft.AspNetCore.DataProtection;`n" + $source.Replace($anchor, "$anchor`n            $keysSetup")
Set-Content -LiteralPath (Join-Path $output.FullName 'AccountLoginTests.cs') -Value $source
$api = Join-Path $repo 'src/Elifoot.Api/Elifoot.Api.csproj'
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Using Include="Xunit" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.*" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
    <ProjectReference Include="$api" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $output.FullName 'AuditAccount.csproj')
$build = Join-Path $output.FullName 'build'
$dotnetArgs = @('test', (Join-Path $output.FullName 'AuditAccount.csproj'), '-c', 'Release', '--artifacts-path', $build,
    '-p:NuGetAudit=false', '--logger', 'trx;LogFileName=accounts.trx', '--results-directory', $output.FullName)
if ($NuGetSource) { $dotnetArgs += "-p:RestoreSources=$NuGetSource" }
dotnet @dotnetArgs
exit $LASTEXITCODE

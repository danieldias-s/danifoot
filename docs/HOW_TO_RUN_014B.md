# Execução local — 014B

Abra PowerShell em `D:\elifoot_98_014B`:

```powershell
.\scripts\run-local-014b.ps1 -OpenChrome
```

API: [127.0.0.1:5145](http://127.0.0.1:5145/api/health); jogo: [127.0.0.1:5146](http://127.0.0.1:5146). Modo final usa relógio automático. Entre em LOCAL DEV, crie campeonato pelo criador ou entre numa sala; escolha clube elegível/desempregado. Ao enviar tática, aguarde todos. Montagens ficam na criação de campeonato.

Requisitos usados: .NET 10, Flutter C:\flutter, Node, PostgreSQL 18 C:\Program Files\PostgreSQL\18\bin. PG loopback 55437 e role exclusiva; senha local artifacts/014b/pg-password.txt, fora do Git. Bancos distintos elifoot_014b_013b_catalog_real e elifoot_014b_013b_worlds_real; fixtures em bancos próprios. Não configure bancos/portas de sessões anteriores.

`-NoBuild` reutiliza binários atuais. `-ManualClock` serve aos testes dirigidos; API ativa com modo diferente é recusada. Para trocar, encerre somente os serviços próprios:

```powershell
.\scripts\stop-final-worlds-014b.ps1
# -IncludePostgres encerra também a instância própria.
.\scripts\run-local-014b.ps1 -NoBuild -OpenChrome
```

Regressão isolada:

```powershell
. .\scripts\test-final-worlds-014b.ps1 -Action Start
$env:ELIFOOT_AUDIT_OUTPUT='D:\elifoot_98_014B\docs\audit-014b\nova-regressao'
dotnet build Elifoot.slnx -c Release
dotnet test Elifoot.slnx -c Release --no-build
```

Catálogo importado para cópia própria; partidas sem chamadas externas. Bundle, inputs e backups em artifacts/014b são dados locais preservados. Runners recusam raiz/porta/PID incompatíveis e não param processos de outros projetos.

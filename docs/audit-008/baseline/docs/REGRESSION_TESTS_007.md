# Regressão007

Data30/10/2026. Build Release PASS,0 avisos/erros. Baseline27/27 .NET PASS. Final52 casos únicos PASS: Domain34, Application4, Integration11, Simulation3. Flutter8/8 PASS e analyze sem problemas. Os27 casos anteriores permanecem; foram acrescentados25 casos (a teoria de11 atributos conta como11).

## Evidências

- Baseline: audit-007/baseline-tests-retry.log e baseline-tests/*.trx.
- Antes da correção: audit-007/reproductions.log e reproductions/reproductions.trx:12 falhas esperadas,11 respostas táticas inadequadas e1 reload de liquidação.
- SQLite antes: audit-007/ledger-first-save-reproduction.log:256 esperados,200 gravados na primeira operação. O teste inicial que verificava só depois de duas gravações não detectava isso; foi fortalecido antes da correção.
- Execução completa pós-correção tática: audit-007/final-tests.log e final-tests/*.trx,50 casos naquela revisão, incluindo os3 estatísticos.
- Fechamento após a correção SQL: audit-007/closure-tests.log e closure-tests/*.trx,49 casos não estatísticos. A união com os3 estatísticos resulta52; não é uma única chamada com52.
- Flutter: audit-007/flutter-analyze.log e flutter-tests.log.
- Verificação: audit-007/verification.json contém cada nome e arquivo TRX, hashes e escopo.

## Comandos executados

PowerShell na raiz D:\elifoot_98:

```powershell
git status --short
git log -3 --oneline
$env:ELIFOOT_AUDIT_OUTPUT='D:\elifoot_98\docs\audit-007\baseline-run'
dotnet test Elifoot.slnx -c Release --logger 'trx;LogFilePrefix=baseline' --results-directory docs/audit-007/baseline-tests
dotnet test tests/Elifoot.Domain.Tests -c Release --no-restore --filter FullyQualifiedName~MetricAiAudit007Tests
dotnet test tests/Elifoot.Simulation.Tests -c Release --no-restore --filter FullyQualifiedName~Audit007_Reconstruct
$env:ELIFOOT_AUDIT_OUTPUT='D:\elifoot_98\docs\audit-007\final-run'
dotnet build Elifoot.slnx -c Release
dotnet test Elifoot.slnx -c Release --no-build --logger 'trx;LogFilePrefix=final' --results-directory docs/audit-007/final-tests
dotnet test tests/Elifoot.Integration.Tests -c Release --no-restore --filter FullyQualifiedName~FinancialPersistence007Tests
$env:ELIFOOT_AUDIT_OUTPUT='D:\elifoot_98\docs\audit-007\closure'
dotnet build Elifoot.slnx -c Release
dotnet test Elifoot.slnx -c Release --no-build --filter 'FullyQualifiedName!~MultiSeasonAndBenchmarkTests' --logger 'trx;LogFilePrefix=closure' --results-directory docs/audit-007/closure-tests
Push-Location client/elifoot_flutter
C:\flutter\bin\flutter.bat analyze
C:\flutter\bin\flutter.bat test
Pop-Location
node scripts/report-metric-ai-007.mjs
node scripts/finish-metric-ai-007.mjs
```

Os comandos de reprodução anterior documentam a versão de antes do fix; executar hoje seus testes espera sucesso. Use nova pasta ELIFOOT_AUDIT_OUTPUT para evitar substituir capturas e relatórios históricos. O script de fechamento apenas verifica/evidencia os resultados existentes; ele espera as pastas atuais. Para repetir somente a captura real100 temporadas em pasta nova, execute o teste Audit007_Reconstruct com o ambiente apontando para ela e configure ELIFOOT_007_CANDIDATE no gerador de relatórios.

## Cobertura A–N

| Pedido | Evidência executada |
|---|---|
| A reconciliação | ledger integral baseline/candidato; unicidade, pares e soma por manager |
| B/C responsável e troca de clube | Transfers_ReconcileOnce_AndRetainResponsibleManagerAfterClubChangeAndReload; rotação real anterior mantida |
| D lances versus compras | AuctionBids_AreNotPurchases; fluxo real IA→oferta→humano→aceite |
| E/F adaptabilidade/extremo | teoria0,20,40,50,60,64,65,70,80,90,100; matriz33 estados;100 temporadas reais |
| G personalidade | teste de rotação existente; preferências e identidade conservadas; nenhuma alteração nos atributos |
| H/I/J disciplina/segundo amarelo/suspensão | testes006 preservados;2400 controles homogêneos pelo MatchEngine; exposições de35100 partidas |
| K recuperação |21 clubes baseline com27 episódios;19 na nova trajetória; controle de receita identificada |
| L idempotência | repetição por2200 rodadas; reload JSON completo e compatibilidade006 |
| M conservação | centavos inteiros; débitos/créditos e ambos os guards; SQLite real em memória |
| N estádio em crise | expansão sem caixa rejeitada; decisão sazonal; fluxos de obras registrados |

## Escala das execuções

Antes do fix: suíte006 (100 temporadas principais+25 agentes humanos+25 estresse+4000 matriz+2 replays de1 temporada) e captura independente de100 temporadas. Depois do fix: a mesma suíte006 mais captura007 de100 temporadas e benchmark. O cenário principal do relatório compara apenas as100 temporadas seed199801 de cada lado; não soma esses períodos repetidos como se fossem200 temporadas distintas de uma carreira. Os controles homogêneos têm2400 partidas adicionais, fora da população principal.

Replay determinístico: duas execuções de1 temporada,351 partidas cada, com assinatura de gameplay, resultados, gols, disciplina e caixa iguais. Não se afirma igualdade literal de snapshots que contêm GUIDs e timestamps. A fórmula defensiva e todo MatchEngine.cs preservam SHA256 C4A053C673F0044D89C035DC323103F6CB760DC6594C74FF7902549FCD07DF9B.

Multiplayer E2E usa HTTP+SignalR via ASP.NET TestServer/InMemory e os testes anteriores de2/4/6/8 clientes e100 rodadas. Persistência financeira usa SQLite em memória. Nenhum PostgreSQL externo nem runtime Android foi certificado em007. Não houve teste visual de interface novo, pois não houve alteração de UI.


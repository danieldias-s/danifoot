# Regressão completa — 015A

Nenhum teste de base foi excluído. Base aprovada: 628 .NET, 134 Flutter e 2 Node. Resultado final: **640 .NET, 158 Flutter e 2 Node**, todos aprovados, zero ignorados.

| Suíte | Aprovados |
|---|---:|
| Application | 7/7 |
| Domain | 409/409 |
| Integration | 198/198 |
| Simulation | 26/26 |
| Flutter | 158/158 |
| Node | 2/2 |

`dotnet build Elifoot.slnx -c Release`: zero erros/avisos. `flutter analyze`: zero issues. `flutter build web`: aprovado na build final de revisão com API_BASE_URL=5165. Os 12 testes .NET adicionais verificam compra concorrente/idempotência/persistência e proteções; os 24 Flutter adicionais cobrem barra, formulários, confirmações, filtros, navegação e metadados da contraproposta.

Uma primeira tentativa revelou 14 falhas ao impedir a troca já permitida de preço fixo para leilão. Essa restrição foi removida, restaurando a operação atômica anterior. A execução completa posterior passou, sem filtro nem supressão. Ajustes dos testes de interface acompanham botões e etapas novas, preservando as verificações anteriores. Depois dos últimos ajustes visuais, analyze, os 158 testes e a build Web foram executados novamente.

## Simulações efetivamente executadas

Os 26 testes de Simulation incluem novamente os cenários longos de economia, força, disciplina e mundos reais, com 100 temporadas nos cenários abaixo. Cada cenário econômico completou 35.100 partidas pelo motor real.

| Cenário | Caixa final | Dívida final | Pares de transferências | Transações |
|---|---:|---:|---:|---:|
| Balanço 012D FIX | 137.353.919,79 | 3.270.104,68 | 8.888 | 556.238 |
| Catálogo real masculino | 86.753.694,92 | 0 | 8.841 | 553.560 |
| Catálogo real feminino | 103.405.567,05 | 0 | 8.825 | 553.828 |

Caixa inicial dos três: 138.800.000. As variações refletem o sistema econômico preservado; não foi introduzido índice de inflação. Os cenários reais usam REAL_CLUBS_SIMULATED e FixtureWorld=false. O campo SourceCheckpoint dos relatórios é metadado histórico do cenário, não o commit da execução 015A.

Economia 012D: 5.059.273 verificações de preservação de contratos, juros cobrados 6.200.000, pagos 3.429.895,32 e principal pago 1.000.000. A dívida remanescente não foi apagada. Força 012C: 100 temporadas, 35.100 partidas; débitos bilaterais e créditos de venda ambos 904.333.396. Zero violações de escalação, limites de elenco e dupla liquidação nos relatórios econômicos. Disciplina, evolução, lesões, empréstimos antigos, crédito individual e insolvência permanecem na regressão.

Evidências versionadas: [contadores e nomes de todos os testes .NET](evidence-015a/regression-counts.json), resumos `*-100-summary.json`, finais dos logs `*.log.tail.txt`, dados financeiros/reinício e capturas. Relatórios completos e TRX permanecem em `artifacts/015a/regression` e `artifacts/015a/regression-results`.

## Reprodução neste computador

```powershell
Set-Location D:\elifoot_98_015A
. .\scripts\test-match-market-015a.ps1
dotnet build Elifoot.slnx -c Release
dotnet test Elifoot.slnx -c Release --no-build --logger trx --results-directory artifacts/015a/regression-results
Set-Location client/elifoot_flutter
flutter analyze
flutter test
flutter build web --dart-define=API_BASE_URL=http://127.0.0.1:5165 --pwa-strategy=none --output=D:/elifoot_98_015A/artifacts/015a/web
Set-Location D:\elifoot_98_015A
node --test tools/catalog-013b/openfootball-directory.test.mjs
```

O ambiente preparado contém uma cópia somente de leitura do catálogo real 014C, restaurada em banco exclusivo 015A; os testes não dependem de rede nem alteram o catálogo original. O runner reutiliza inputs previamente autorizados, copiados para artifacts/015a. Ele não provisiona sozinho um catálogo real novo em uma máquina sem esses dados.

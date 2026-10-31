# Cinco séries de 100 temporadas — 014B

STATUS: PASS para execução/reconciliação; não significa ausência de insolvência ou validação humana.

Cada caso disputou 35.100 jogos pelo motor existente e pelo menos 3.159.000 minutos. Regressão completa antes e depois da UX, sem interromper simulações. Os cinco resumos pós-UX repetiram o checkpoint anterior.

| Caso (100 temporadas) | Caixa final R$ | Dívida final R$ | Mediana R$ | Insolventes finais | Média anual das últimas 25 R$ |
|---|---:|---:|---:|---:|---:|
| Legacy012D | 137353919.79 | 3270104.68 | 749694.905 | 8 | -135053.8116 |
| Baseline014AMale | 79157786.17 | 4366066.93 | 425999.40 | 10 | -4909762.7868 |
| Baseline014AFemale | 79157786.17 | 4366066.93 | 425999.40 | 10 | -4909762.7868 |
| RealMale | 86753694.92 | 0 | 575955.88 | 5 | -380895.0604 |
| RealFemale | 103405567.05 | 0 | 521992.105 | 5 | 200411.696 |

Legacy012D reproduz seed clássica aprovada. Baseline014AMale/Female são fixtures anteriores e por isso têm números iguais; não são prova do catálogo real. RealMale/Female usam inputs congelados da 013C, REAL_CLUBS_SIMULATED, FixtureWorld false, elencos gerados autorizados e vínculo feminino real preservado.

Reconciliação independente Python/Decimal: caixa por lançamento, identidade anual de caixa/dívida, transferências com soma zero, 128 clubes/ano, continuidade de saldo, elencos 11..24, resumos e hashes. Testes do motor também verificam escalações e liquidação única. 2.771.498 lançamentos e 64.000 identidades clube/ano por passagem.

Novos casos: zero violações de escalação/elenco/dupla liquidação. M: 134 pedidos salariais, 65 leilões forçados, 9 sem venda, 80 crises/75 recuperações. F: 152 pedidos, 37 leilões, 6 sem venda, 63 crises/58 recuperações. Cinco clubes persistentes em crise não foram removidos; déficits continuam registrados.

Artefatos `verified-final` (pré-UX), `ux-verified-final` (final), `financial-reconciliation.json`, `ux-financial-reconciliation.json`. Resumos/séries anuais no checkpoint; ledgers e séries comprimidas integrais preservados localmente, SHA-256 nos relatórios. Não se promete estabilidade de todas as seeds ou medição de inflação de cesta fixa.

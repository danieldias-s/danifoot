# 100 temporadas antes e depois — 012D_FIX

STATUS: PASS. Mesmo motor minuto a minuto, 128 clubes, força clássica, seed 199801 e sondas iniciais idênticas às da 012D. Cada caso concluiu 35.100 partidas; placares agregados não foram utilizados. Ativos são proxies de modelo, separados de caixa e dívida. Divisões são as vigentes durante a temporada observada; acessos/rebaixamentos aparecem separadamente nos registros por clube.

| Marco | Caixa antes | Caixa depois | Mediana depois | Maior caixa depois | Dívida depois | Insolventes |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | R$ 317.015.213,09 | R$ 195.320.965,47 | R$ 653.068,61 | R$ 14.364.434,98 | R$ 1.000.000,00 | 1 |
| 5 | R$ 959.371.236,76 | R$ 230.229.068,68 | R$ 650.288,80 | R$ 21.923.272,82 | R$ 784.635,42 | 2 |
| 10 | R$ 1.755.873.482,05 | R$ 176.003.334,42 | R$ 615.720,58 | R$ 18.869.375,30 | R$ 967.338,23 | 2 |
| 25 | R$ 4.194.879.990,75 | R$ 149.730.087,38 | R$ 592.800,49 | R$ 17.241.252,90 | R$ 1.338.464,50 | 2 |
| 50 | R$ 8.647.776.299,75 | R$ 182.531.182,30 | R$ 586.353,16 | R$ 36.721.696,73 | R$ 2.032.287,37 | 5 |
| 75 | R$ 13.307.330.913,20 | R$ 140.730.265,08 | R$ 681.615,18 | R$ 24.085.968,77 | R$ 2.507.309,76 | 8 |
| 100 | R$ 18.107.128.451,84 | R$ 137.353.919,79 | R$ 749.694,90 | R$ 9.971.636,58 | R$ 3.270.104,68 | 8 |

## Hipóteses e seeds

| Caso / seed | Caixa T100 | Mediana | Dívida | Mudança anual média T76–T100 | Insolventes T100 |
|---|---:|---:|---:|---:|---:|
| baseline (0) / 199801 | R$ 18.107.128.451,84 | R$ 45.344.159,73 | R$ 0,00 | R$ 191.991.901,55 | 0 |
| final (2) / 199802 | R$ 404.287.586,57 | R$ 923.072,90 | R$ 3.232.017,87 | R$ 6.382.911,76 | 8 |
| final (2) / 199803 | R$ 231.901.943,09 | R$ 970.783,12 | R$ 2.998.386,55 | R$ 4.215.185,73 | 9 |
| h1-annual-sponsor (0) / 199801 | R$ 5.275.210.651,93 | R$ 2.271.174,16 | R$ 14.632.660,02 | R$ 72.810.102,41 | 2 |
| h2-quality-linear (1) / 199801 | R$ 1.307.032.286,58 | R$ 1.199.433,78 | R$ 14.759.959,25 | R$ 15.154.334,57 | 2 |
| h3-quality-square (2) / 199801 | R$ 110.950.244,98 | R$ 824.101,85 | R$ 112.202.916,97 | R$ -497.495,08 | 11 |
| h4-debt-reserve (2) / 199801 | R$ 201.599.037,31 | R$ 862.310,04 | R$ 81.570.730,07 | R$ -1.590.863,19 | 10 |
| h5-credit-surplus (2) / 199801 | R$ 208.174.554,87 | R$ 814.213,28 | R$ 3.102.782,52 | R$ -530.561,56 | 3 |
| h6-quality-2p2 (2.2) / 199801 | R$ 137.353.919,79 | R$ 749.694,90 | R$ 3.270.104,68 | R$ -135.053,81 | 8 |
| h6-quality-2p2 (2.2) / 199802 | R$ 120.794.271,78 | R$ 890.278,64 | R$ 2.755.147,34 | R$ 675.961,70 | 9 |
| h6-quality-2p2 (2.2) / 199803 | R$ 365.563.613,85 | R$ 776.191,73 | R$ 3.074.198,47 | R$ 1.840.035,04 | 7 |
| final (2) / 199801 | R$ 208.174.554,87 | R$ 814.213,28 | R$ 3.102.782,52 | R$ -530.561,56 | 3 |
| final (2.2) / 199801 | R$ 137.353.919,79 | R$ 749.694,90 | R$ 3.270.104,68 | R$ -135.053,81 | 8 |

H1 isolou o orçamento comercial anual; H2 acrescentou sensibilidade linear da demanda; H3 tornou-a quadrática; H4 reservou dívida no orçamento discricionário da IA; H5 exigiu superávit observado antes de novo crédito. H1/H2 não eliminaram superávit estrutural; H3/H4 diminuíram caixa mas deixaram endividamento crescente, motivo para rejeição. H5 estabilizou apenas a seed principal e foi mantida como comparação provisória V1. H6, expoente 2,2, obteve deriva tardia inferior a 1% do estoque por ano nas três seeds e foi incorporada como política V2. Resultados antigos não foram renomeados ou sobrescritos. A sequência e as decisões estão em `audit-012d-fix/HYPOTHESES.md`.

## Fluxos acumulados na comparação principal

| Categoria | Antes | Depois |
|---|---:|---:|
| AuctionPurchase | R$ -2.849.769.475,00 | R$ -2.512.572.146,00 |
| AuctionSale | R$ 60.000,00 | R$ 1.490.000,00 |
| ContractRenewal | R$ -56.819.877,00 | R$ -55.665.796,50 |
| FreeAgentSigning | R$ 0,00 | R$ 0,00 |
| LoanDisbursement | R$ 2.500.000,00 | R$ 1.500.000,00 |
| LoanInterestPayment | R$ -1.275.000,00 | R$ -3.429.895,32 |
| LoanPrincipalRepayment | R$ -2.500.000,00 | R$ -1.000.000,00 |
| PlayerSalary | R$ -1.173.597.390,00 | R$ -1.169.766.564,00 |
| PrizeMoney | R$ 1.590.000.000,00 | R$ 1.587.000.000,00 |
| Sponsorship | R$ 12.566.400.000,00 | R$ 571.200.000,00 |
| StadiumExpansion | R$ -108.800.000,00 | R$ -14.450.000,00 |
| StadiumFixedCost | R$ -784.612.258,68 | R$ -415.576.327,68 |
| StadiumMaintenance | R$ -473.527.557,00 | R$ -209.398.171,50 |
| StadiumRenovation | R$ -194.143.294,00 | R$ -108.187.701,00 |
| StadiumVariableCost | R$ -533.656.696,10 | R$ -132.470.922,10 |
| TicketRevenue | R$ 9.988.069.999,62 | R$ 2.459.881.443,89 |
| TransferPurchase | R$ -933.991.527,00 | R$ -883.362.471,00 |
| TransferSale | R$ 933.991.527,00 | R$ 883.362.471,00 |

No cenário final principal, T51–T100 ficou entre R$ 110.120.709,45 e R$ 186.224.946,03; a variação média em T76–T100 foi R$ -135.053,81/ano. A folha passou a representar 25.33% das receitas externas. A maior compra efetiva foi R$ 1.899.485,00. O cenário teve 111 entradas em crise, 103 recuperações, 162 leilões por dívida e 63 leilões novos sem comprador. Crises persistentes no fechamento: 6; elas permanecem explícitas, sem perdão de saldo ou liberação administrativa abaixo do mínimo.

A dívida da tabela é bancária. O JSON também separa caixa positivo e déficits de caixa negativos. Na principal, déficit de caixa não financiado de R$ 3.376.577,84, somado à dívida bancária, corresponde a R$ 6.646.682,52 de obrigações registradas; não foi escondido por compensação com o caixa dos clubes ricos.

Todas as seeds adicionais usam a mesma política e as mesmas sondas declaradas; muda a seed, portanto elencos, estádios, jogos e decisões podem divergir. O horizonte de 100 temporadas demonstra comportamento observado, não prova matemática para tempo infinito. As séries completas permitem distinguir saldo menor de sustentabilidade: caixa, dívida, fluxo, desigualdade, restrições de compra, crises e recuperação são analisados conjuntamente.

As seeds adicionais utilizam o rótulo experimental H6, que tem os mesmos parâmetros efetivos da política V2: anual, expoente 2,2, reserva de dívida e teste de superávit da IA. A seed principal foi repetida na regressão integral sob o rótulo de produto V2. O limiar de 1% é um diagnóstico quantitativo posterior à calibração inicial, explicitado para evitar selecionar apenas a seed favorável; ele não é limite imposto ao produto nem prova histórica. Existem crises persistentes, que não foram apagadas para cumprir esse diagnóstico macroeconômico.

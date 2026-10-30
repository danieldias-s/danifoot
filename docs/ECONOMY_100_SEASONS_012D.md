# Economia em 100 temporadas — 012D

STATUS: PARTIAL. A implementação e as invariantes contábeis passaram; o equilíbrio estratégico de longo prazo permanece uma limitação medida.

Seed 199801; 128 clubes; 100 temporadas; 35,100 partidas reais; 3,254,370 minutos. MatchEngine original preservado. O cenário é IA com sondas iniciais declaradas: dois empréstimos clássicos e despesa de capital de giro, um empréstimo legado e um clube em insolvência. Não representa um mundo sem intervenção inicial. As transações das sondas constam do livro-caixa.

## Resultados reconciliados

- 558,212 lançamentos únicos; 8,800 pares de transferência/venda por dívida, todos de soma zero.
- 5 empréstimos contratados; desembolso R$ 2.500.000,00; juros cobrados e pagos R$ 1.275.000,00; principal quitado R$ 2.500.000,00; dívida final R$ 0,00.
- 133 pedidos salariais: 133 aceitos e 0 recusados no cenário. 5,057,147 verificações de salário vigente preservado. Recusas e renovação obrigatória também foram validadas em testes controlados.
- Leilões: {'ContractExpiration': 23997, 'FinancialInsolvency': 1}. Sem comprador: 0 no cenário; caso sem lance e reoferta com intervalo validado separadamente. Não inferir cobertura de recusa/ausência de comprador a partir de contagens naturais iguais a zero.
- Clubes em crise no fechamento T100: 0; recuperações registradas: 1; caixa negativo T100: 0; clubes no elenco mínimo T100: 4.
- Zero violações de escalação, elenco e liquidação duplicada. Nenhum empréstimo é quitado pelo fim do contador. O teste legado específico conserva R$ 8.622,06 após 20 prestações e exige pagamento real do residual.

## Marcos da execução

| Temporada | Caixa total | Dívida | Empréstimos acumulados | Pedidos acumulados | Insolventes |
|---:|---:|---:|---:|---:|---:|
| 1 | R$ 317.015.213,09 | R$ 1.000.000,00 | 5 | 123 | 0 |
| 5 | R$ 959.371.236,76 | R$ 0,00 | 5 | 133 | 0 |
| 10 | R$ 1.755.873.482,05 | R$ 0,00 | 5 | 133 | 0 |
| 25 | R$ 4.194.879.990,75 | R$ 0,00 | 5 | 133 | 0 |
| 50 | R$ 8.647.776.299,75 | R$ 0,00 | 5 | 133 | 0 |
| 75 | R$ 13.307.330.913,20 | R$ 0,00 | 5 | 133 | 0 |
| 100 | R$ 18.107.128.451,84 | R$ 0,00 | 5 | 133 | 0 |

## Divisões após o fechamento da temporada 100

Os grupos são as divisões após acessos/rebaixamentos; históricos acumulados são atribuídos ao clube e agrupados por sua divisão nesse fechamento. O JSON inclui todas as temporadas, juros, quitações, pedidos, recusas, leilões, insolvência, elenco mínimo, salários e valores médios por divisão.

| Divisão | Caixa | Dívida | Salário médio por jogador/partida | Clubes com 11 | Distribuição dos limites |
|---|---:|---:|---:|---:|---|
| QuartaDivisao | R$ 2.392.185.043,68 | R$ 0,00 | R$ 511,82 | 0 | {'2': 7, '1': 1} |
| TerceiraDivisao | R$ 3.445.202.402,96 | R$ 0,00 | R$ 727,69 | 0 | {'3': 7, '2': 1} |
| PrimeiraDivisao | R$ 3.828.826.954,04 | R$ 0,00 | R$ 1.230,93 | 0 | {'5': 6, '2': 1, '4': 1} |
| SegundaDivisao | R$ 3.121.172.524,85 | R$ 0,00 | R$ 879,55 | 0 | {'4': 8} |
| Distrital | R$ 5.319.741.526,31 | R$ 0,00 | R$ 261,96 | 4 | {'1': 96} |

## Inflação e composição

Controle fixo: posição M, Cavalheiro, sem estrela e dez partidas contratuais. Nenhuma curva recebeu indexação temporal. Cada controle foi comparado nas 100 temporadas pelo auditor independente.

| Força | Valor justo fixo | Salário de referência fixo | Índice nominal T1 → T100 |
|---:|---:|---:|---:|
| 1 | R$ 15.000,00 | R$ 222,00 | 100 → 100 |
| 10 | R$ 48.245,00 | R$ 418,00 | 100 → 100 |
| 20 | R$ 236.334,00 | R$ 1.059,00 | 100 → 100 |
| 30 | R$ 667.800,00 | R$ 2.169,00 | 100 → 100 |
| 40 | R$ 1.416.906,00 | R$ 3.766,00 | 100 → 100 |
| 50 | R$ 2.549.325,00 | R$ 5.861,00 | 100 → 100 |

O salário contratado e o valor médio observado por força variam com posição, estrela, contrato e negociações. Faixas sem atletas têm média nula, não preço zero. Esses dados estão em `StrengthBins`, separadamente dos controles. Preços efetivos de cada transferência estão no livro-caixa comprimido.

## Acumulação monetária e comparação histórica

O caixa inicial foi R$ 138.800.000,00; chegou a R$ 18.107.128.451,84. O modelo permaneceu operacional, mas crédito deixou de ser necessário após os primeiros anos. Isso limita a validação do banco como ferramenta estratégica durante todo o horizonte.

Patrocínios somaram R$ 12.566.400.000,00; bilheterias R$ 9.988.069.999,62; salários R$ 1.173.597.390,00. A política preexistente paga patrocínio em 22 rodadas inclusive em folgas e eliminações, enquanto a folha depende de jogos. A auditoria 007 já descrevia essa assimetria e caixa final de R$ 5.664.382.164,15 no seu candidato; a 006 relatava recuperação de 21 clubes usando outro critério de crise. São contextos diferentes: força, disciplina, contratos, preços e decisões mudaram entre missões. A diferença não demonstra que os novos empréstimos causaram o crescimento.

A curva de valor justo da 005 permanece. A 012/012B/012C mudou a população de força; a 012D preserva o salário negociado durante a vigência e somente novos contratos podem reprificá-lo. Não há comparação causal controlada que isole todas essas mudanças. Não foi introduzido indexador anual nem alterado patrocínio/estádio para mascarar o resultado.

## Evidência

Dataset: `docs\audit-012d\solution-final\economy-012d`. Arquivos: `100-seasons.json`, `ledger.jsonl.gz`, `bank.jsonl.gz`, `contracts-and-crises.jsonl.gz`, `salary-requests-final.json`; reconciliação independente em `docs/audit-012d/independent-reconciliation.json`; marcos CSV em `docs/audit-012d/economy-checkpoints.csv`. O arquivo `economy-annual-flows.csv` separa novas operações e fluxos de cada uma das 100 temporadas dos contadores acumulados, mantendo caixa, dívida e crises como estoques de fechamento. O JSON `Status=PASS` refere-se às invariantes verificadas, não à validação do equilíbrio estratégico global.

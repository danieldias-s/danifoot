# Economia preservada — 014B

Motores econômicos 012D_FIX_V2 preservados por hash e regressão. Sem inflação arbitrária, teto de caixa, perdão de dívida ou alteração de taxa/limite. Salário negociado independente do valor justo; Flutter exibe SalaryPerRound do servidor.

Defeitos/correções já aprovados: IsPaidOff não elimina saldo pelo prazo; força não reajusta contrato vigente; principal/juros/pagamentos separados; empréstimo legado conserva modalidade; comandos financeiros compartilham sincronização/recibos; leilões salariais e de insolvência distintos, protegendo elenco mínimo. Consulte `CLASSIC_BANK_RESEARCH_012D.md` e `audit-012d/RESEARCH_ONLY_REPORT.md`.

Classificação histórica preservada: limites de elenco têm documentação histórica; juros por partida, aumentos e leilões têm relatos. R$ 500.000/R$ 25.000 e fórmula patrimonial são CLASSIC_ADAPTED_DESIGN autorizados, sem alegar fórmula original autenticada.

Banco: principal R$ 500.000 e juros R$ 25.000 por jogo oficial efetivamente disputado. Crédito por patrimônio individual. Consulta aos oito clubes da quarta em cada mundo retornou limites `[1,1,1,1,1,2,2,1]`; evidência `individual-credit-http.json`.

HTTP final M/F: compra/venda com dinheiro simétrico, renovação +5 uma vez mantendo salário, principal R$ 500.000 quitado uma vez, juros R$ 25.000 cobrados, leilão por vencimento fechado com lance e livros de comprador/vendedor reconciliados. Contrato da sonda foi definido como 2 antes da confirmação; venceu por jogos reais. `actual-financial-ux.json`; ensaio anterior preservado em `actual-financial-flow.json`.

Cinco séries de 100 temporadas reconciliadas antes e depois da UX: 2.771.498 lançamentos por passagem, caixa/livro por transação, 12.800 identidades clube/ano por caso, dívida anual e transferências com soma zero. Resumos econômicos finais repetiram exatamente o checkpoint pré-UX.

| Caso (100 temporadas) | Caixa final R$ | Dívida final R$ | Mediana R$ | Insolventes finais | Média anual das últimas 25 R$ |
|---|---:|---:|---:|---:|---:|
| Legacy012D | 137353919.79 | 3270104.68 | 749694.905 | 8 | -135053.8116 |
| Baseline014AMale | 79157786.17 | 4366066.93 | 425999.40 | 10 | -4909762.7868 |
| Baseline014AFemale | 79157786.17 | 4366066.93 | 425999.40 | 10 | -4909762.7868 |
| RealMale | 86753694.92 | 0 | 575955.88 | 5 | -380895.0604 |
| RealFemale | 103405567.05 | 0 | 521992.105 | 5 | 200411.696 |

Saldo agregado não elimina risco: cinco clubes insolventes persistentes nos novos mundos, déficits mantidos. M: juros acumulados R$ 575.000 pagos integralmente; F: R$ 1.100.000. Principal pago R$ 1.500.000 em cada série, dívida final zero. Legados conservam dívida positiva.

Salários, mercado e inflação são medidos separadamente nos fluxos anuais: PlayerSalary, AssetsRoster, receitas/custos, crédito, juros e insolvência. Patrimônio nominal dos elencos caiu de R$ 1.154.481.662 para R$ 141.611.396 (M) e de R$ 1.116.291.276 para R$ 170.468.224 (F), junto da evolução de força/carreiras. Isso não é índice de cesta constante. Não se adicionou indexação arbitrária.

# Integração 014A

```text
TASK: ELIFOOT_INTEGRATION_012D_013AB_014A
STATUS: PASS
INTEGRATION_WORKTREE: D:\elifoot_98_integration
INTEGRATION_BRANCH: feature/integration-012d-013ab
BASE_012D_COMMIT: 7a00bdd2dcf2f721ac32d14b2d195e3ea2aebf48
SOURCE_013B_COMMIT: 47608e9a674c0978c4be4b244de88d961876e197
COMMON_ANCESTOR: 50e9c1215ed6d1256cc0593f84ab8aa54629b314
INTEGRATION_COMMIT: 228c3bfe3e4627aa5288a6424519f3af1752879e

012C_PRESERVED: motores protegidos idênticos à base; replay e regressão executados.
012D_PRESERVED: regras financeiras, sala e contabilidade preservadas; comparação exata abaixo.
013A_PRESERVED: criador e snapshots integrados.
013B_PRESERVED: infraestrutura preservada; cobertura real continua PARTIAL.
GENDER_SELECTION: masculino/feminino testados.
COUNTRY_SELECTION: oito países em ambos.
AUTOMATIC_DIVISIONS: 4x8 por ranking.
MANUAL_DIVISIONS: personalização testada.
DISTRICTS: 96, aleatórios e manuais.
CATALOG_DATABASE: PostgreSQL exclusivo 014A, loopback 55435, bancos de mundos/catálogo separados.
CACHE: persistido e recuperado após reinício real.
FALLBACK: falha externa controlada preserva último snapshot válido.
WORLD_SNAPSHOT: independente do catálogo, persistido.
CONTRACTS: salário, prazo e proteção contratual preservados; pacote corrigido.
BANK: R$ 500.000, R$ 25.000 por partida elegível, crédito individual e recibos persistentes.
SALARIES: SalaryPerRound do servidor; força não sobrescreve salário vigente.
AUCTIONS: motivos, prioridade, contabilidade e mínimo de elenco preservados/testados.
FINANCIAL_INTEGRITY: audit-014a/financial-reconciliation.json.
OLD_WORLDS: saves históricos 009/012D carregados em bancos próprios sem alterar originais.
NEW_WORLDS: quatro mundos HTTP de 128 clubes válidos, além das simulações.
MATCH_ENGINE: código e fórmula defensiva intocados.
MULTIPLAYER: 2/4/6/8 clientes, táticas, pronto, partidas, resultados e reconexão; banco idempotente.
DOTNET_TESTS: 533 nos TRX finais disponíveis; exit integral 0.
FLUTTER_TESTS: 70; analyze limpo; Web compilado.
NODE_TESTS: 2.
100_SEASONS: ver tabela.
FINANCIAL_RECONCILIATION: PASS.
CONFLICTS_RESOLVED: zero conflitos textuais; incompatibilidades semânticas corrigidas.
KNOWN_ISSUES: INTEGRATION_KNOWN_ISSUES_014A.md.
```

| Mundo | Caixa T100 | Dívida T100 | Mediana | Jogos reais | Minutos reais | Variação anual T76–100 |
|---|---:|---:|---:|---:|---:|---:|
| Seed012D | R$ 137.353.919,79 | R$ 3.270.104,68 | R$ 749.694,90 | 35100 | 3264000 | R$ -135.053,81 |
| Male | R$ 79.157.786,17 | R$ 4.366.066,93 | R$ 425.999,40 | 35100 | 3249330 | R$ -4.909.762,79 |
| Female | R$ 79.157.786,17 | R$ 4.366.066,93 | R$ 425.999,40 | 35100 | 3249330 | R$ -4.909.762,79 |

Escopo DEV. Não houve alteração de branches existentes, acesso aos bancos originais, merge no principal, push, produção ou desenvolvimento 013C. Estado original conferido em audit-014a/verification.json. O PASS da integração não amplia a cobertura real parcial da 013B.

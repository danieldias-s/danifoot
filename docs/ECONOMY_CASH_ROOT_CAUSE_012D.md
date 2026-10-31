# Causa do acúmulo de caixa — 012D_FIX

Base `7529e69`, reproduzida integralmente: R$ 18.107.128.451,84 em T100. Essa métrica soma **somente o caixa dos 128 clubes**. Não soma patrimônio ou empréstimos à parte; os desembolsos bancários já aparecem no caixa e têm dívida correspondente. Em T100 a dívida da baseline é zero. Elencos valem R$ 130.336.414,00 pelo modelo de mercado e estádios R$ 106.066.500,00 pela proxy do crédito, ambos separados.

A mediana é R$ 45.344.159,73; mínimo R$ 43.205.838,15; máximo R$ 926.200.113,37. Nenhum clube ficou com caixa baixo. Os dez maiores detêm 38.03% do total. A série cresce aproximadamente de forma linear: R² 0,99923, contra 0,84958 para o logaritmo do caixa; a expansão tardia média foi R$ 191.991.901,55/ano. Não se trata apenas de poucos clubes com crescimento exponencial.

## Causas demonstradas

1. O apoio financeiro introduzido na QA002/005/006 repete R$ 185/125/92/72/20 mil a cada rodada global. Os 96 distritais, mesmo eliminados, recebem as 22 parcelas integrais. A emissão anual recorrente é R$ 125.664.000 e soma R$ 12.566.400.000 em 100 temporadas. Isso era uma política intencional documentada, não uma transação duplicada. O problema é a dimensão econômica permanente desse apoio.
2. A bilheteria depende de divisão, reputação, campanha, capacidade e condição, mas ignorava a qualidade nominal dos elencos. A força e os salários de novos contratos caem com a dinâmica 012C, enquanto a receita de público permanece elevada. Bilheteria baseline: R$ 9.988.069.999,62. A folha representou apenas 4.86% das entradas externas legítimas.
3. A IA considerava o caixa emprestado no orçamento para compras e obras e tomava empréstimos para cobrir folha sem verificar capacidade operacional de suportar juros. Quando as receitas são recalibradas, esse uso transforma o crédito em financiamento de déficit recorrente. H3/H4 demonstraram crescimento de dívida mesmo com caixa agregado menor; foram rejeitadas.

Não houve evidência de duplicação de receitas, crédito ao vendedor sem débito do comprador, perdão de dívida ou dinheiro bancário tratado como receita definitiva. Os pares internos e as identidades anuais foram conferidos independentemente. Empréstimos/principal baseline somam zero em caixa; juros são despesa externa. Arremate de livre sai do universo de clubes, sem crédito fictício ao ex-clube. Prêmios e acesso são receita externa explícita, com chaves únicas, não transferência interna. A baseline não é economicamente neutra apenas porque é contabilmente correta.

## Dez maiores acumulações na baseline

| Clube | Caixa T100 | Aumento desde a abertura |
|---|---:|---:|
| Leão da Ilha | R$ 926.200.113,37 | R$ 923.600.113,37 |
| Palestra Verde | R$ 824.006.571,11 | R$ 819.806.571,11 |
| Atlético Paulistano | R$ 767.284.248,46 | R$ 763.084.248,46 |
| Grêmio Guanabara | R$ 720.452.405,13 | R$ 716.252.405,13 |
| Real Pampulha | R$ 677.119.704,07 | R$ 672.919.704,07 |
| Guarani da Serra | R$ 665.525.082,68 | R$ 662.925.082,68 |
| Estrela Solitária | R$ 618.463.284,50 | R$ 614.263.284,50 |
| Bahia de Todos | R$ 615.764.013,64 | R$ 613.164.013,64 |
| Esporte Clube Litoral | R$ 549.758.649,12 | R$ 545.558.649,12 |
| Internacional do Sul | R$ 521.748.666,79 | R$ 517.548.666,79 |

Salários são contratados **por partida oficial**, conforme unidade já implementada e exibida. Não surgiu uma obrigação mensal em folgas. Jogador renovado continua na folha a cada partida; força não sobrescreve seu salário. Cobrar salário em toda rodada apenas para consumir dinheiro mudaria a unidade contratual, portanto não foi usado. Custos operacionais, manutenção e obras permanecem lançamentos reais; não foi criado imposto ou despesa de expurgo.

O JSON `ECONOMY_CASH_FLOW_012D.json` conserva todas as temporadas, divisões, extremos, mediana, patrimônio separado, fluxos e dez maiores. As referências anteriores consultadas incluem 005/006, `METRIC_AI_AUDIT_007.md`, `FINANCIAL_RECOVERY_007.json`, `ECONOMIA_ESTADIOS.md` e os relatórios integrais 012D. A assimetria de patrocínio e salários já era reconhecida pela 007; a presente missão autoriza sua recalibração.

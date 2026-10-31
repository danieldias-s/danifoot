# Hipóteses registradas antes da calibração

Base preservada: `7529e69`. A 013B está em `D:/elifoot_98_013A`, branch `feature/real-football-data-013b`; nenhum arquivo desse worktree será alterado.

## Medida e diagnóstico inicial

Os R$ 18.107.128.451,84 representam caixa agregado dos 128 clubes, não patrimônio ou caixa somado a dívida. A evidência anterior reconcilia 558.212 lançamentos sem emissão por transferências internas. A reprodução nova manterá seed 199801, força clássica, 128 clubes, as mesmas sondas bancárias e 100 temporadas completas.

As séries distinguem caixa, dívida, valor de mercado do elenco e indicador patrimonial do estádio. O indicador de estádio (capacidade utilizável × R$ 50) é uma proxy do modelo de crédito, não avaliação imobiliária comprovada. A abertura de caixa não é contada como receita anual.

## Sequência experimental

1. **Baseline:** nenhuma alteração de fórmula. Reproduzir exatamente o caixa final anterior e medir mediana, extremos, dez maiores clubes, concentração, contas por divisão e identidades anuais.
2. **H1 — periodicidade comercial:** a quantia de patrocínio é emitida integralmente em cada uma das 22 rodadas, inclusive sem jogo. Testar as mesmas quantias como orçamento contratual de uma temporada, parcelado em centavos nas 22 rodadas. Folga não elimina o contrato comercial; ela deixa de multiplicar um orçamento anual. Não reduzir caixa existente nem inventar despesa. A escolha será explicitamente uma adaptação econômica, não regra histórica comprovada.
3. **H2 — demanda de bilheteria:** depois de H1, testar se a ausência de qualidade nominal dos elencos na demanda mantém receita elevada mesmo quando a massa salarial e a força caem. Primeiro testar um fator linear com a qualidade média das duas equipes, normalizada pelas médias iniciais de suas divisões. Manter preços, custos e os demais fatores existentes.
4. **H3 — sensibilidade da demanda:** somente se H2 ainda deixar superávit estrutural, comparar uma sensibilidade maior com os mesmos dados e a mesma seed. Uma redução de T100 isolada não aprova a regra. Registrar cada rejeição e os efeitos em clubes pobres, crises, recuperação e restrições financeiras.

Banco, limites, juros, contratos negociados, solicitações salariais e modalidades de leilão não serão reconstruídos ou usados como variável de calibração. Não será alterado o MatchEngine nem qualquer regra de força, lesões, cartões ou suspensões.

## Avaliação

Reconciliar caixa e dívida a cada temporada e em cada observação. Verificar repetição de transações e conservação de todos os pares internos. Avaliar tendência de caixa e de liquidez nos últimos 25 anos, distribuição e concentração, persistência de insolvência, recuperações, crédito efetivamente utilizado, folha e custos relativos à receita. Rejeitar tanto enriquecimento sistemático sem restrição quanto colapso persistente generalizado. Não impor teto ao caixa, taxa patrimonial, inflação anual ou despesa fictícia.

As decisões de aceitar/reverter serão preenchidas após os experimentos, mantendo os dados de todas as variantes separadamente.

## Resultados intermediários

- Baseline: reprodução exata de R$ 18.107.128.451,84. Mediana R$ 45.344.159,73; maior clube R$ 926.200.113,37; menor R$ 43.205.838,15. Regressão linear do estoque tem R² 0,99923; a regressão do logaritmo tem R² 0,84958. Não é apenas crescimento exponencial de alguns poucos clubes.
- H1: R$ 5.275.210.651,93 em T100, mediana R$ 2.271.174,155 e R$ 72.810.102,41 de acréscimo anual médio em T76–T100. A contabilidade reconciliou, mas a variante é insuficiente como solução completa. A emissão de patrocínio passou de R$ 125.664.000 para R$ 5.712.000 por temporada, um orçamento anual explícito em vez de 22 repetições integrais.
- H2: R$ 1.307.032.286,58 em T100; mediana R$ 1.199.433,775; maior caixa R$ 135.353.107,76. Mesmo assim, o estoque ainda aumenta R$ 15.154.334,57 por ano nos últimos 25 anos, com zero novos empréstimos nesse intervalo. Rejeitada como ajuste final. Duas crises persistentes também precisam ser acompanhadas, não declaradas recuperadas por redução do caixa agregado.
- H3: teste iniciado com elasticidade quadrática da qualidade, mantendo todas as demais condições de H2. A comparação será julgada por tendência, restrições, crises e recuperação, não apenas por T100 menor.

H3 concluída: caixa R$ 110.950.244,98, dívida R$ 112.202.916,97 e 11 clubes insolventes há cinco ou mais temporadas. O estoque de caixa ficou contido, mas a dívida cresceu R$ 1.545.454,82/ano nos últimos 25 anos. Rejeitada como solução final. A auditoria de decisões detectou 169 compras/obras/manutenções com principal ainda em aberto; R$ 7,58 milhões de despesas discricionárias atingiram o montante necessário à quitação. H4 mantém demanda quadrática e testa apenas reservar, no orçamento discricionário da IA, o saldo bancário a quitar. O dinheiro continua no caixa real; não é taxa, amortização fictícia ou perdão. Humanos conservam sua liberdade de decisão. Valores, cobrança e quitação do banco são inalterados.

H5, registrada antes de executar: se a reserva de capital não impedir endividamento operacional irrecuperável, condicionar a decisão automática de tomar novo crédito à capacidade observada de quitá-lo. Avaliar os últimos 20 jogos (mínimo quatro), incluindo patrocínio, bilheteria, folha, operação, manutenção e renovação; excluir empréstimos, vendas e prêmios extraordinários. O superávit operacional projetado para 20 jogos precisa cobrir principal total mais juros. Não concede saldo nem restringe a autoridade de um treinador humano; impede a IA de usar crédito como receita recorrente para cobrir déficit estrutural.

H5 principal: caixa R$ 208.174.554,87, dívida R$ 3.102.782,52, mediana R$ 814.213,285 e 60 recuperações. Fluxo tardio de caixa −R$ 530.561,56/ano. Porém as seeds 199802/199803 terminaram em R$ 404,3/R$ 231,9 milhões e mantiveram crescimento tardio de R$ 6,38/R$ 4,22 milhões por ano. Não basta validar apenas a seed favorável. H6 testa exclusivamente a sensibilidade da demanda 2,2 em vez de 2,0; orçamento, IA financeira, banco, contratos, prêmios e custos são mantidos. A candidata 2,0 permanece identificada nos dados como `final` provisória; a aceitação final depende da comparação completa, sem renomear resultados antigos.

H6 concluída: caixa T100 de R$ 137.353.919,79 / R$ 120.794.271,78 / R$ 365.563.613,85 nas seeds 199801/2/3. Variação anual média em T76–T100: −R$ 135.053,81 / +R$ 675.961,70 / +R$ 1.840.035,04, equivalentes a −0,10% / +0,56% / +0,50% do estoque T100. Na principal, máximo R$ 9.971.636,58 e mediana R$ 749.694,905; a dívida continua registrada e há oito insolventes. Não houve colapso generalizado, mas persistem crises individuais. H6 é a candidata selecionada para a regressão final, política V2. Os critérios numéricos de diagnóstico foram explicitados após a calibração inicial; não representam prova matemática de equilíbrio infinito ou regra histórica. Nenhum teto de caixa ou expurgo foi aplicado.

## Sondas de entrada preservadas

Como exigido para reproduzir os R$ 18,1 bilhões, todas as variantes mantêm exatamente as intervenções iniciais da 012D: dois empréstimos clássicos com despesas de capital de giro que deixam R$ 120 mil em cada clube, um empréstimo legado de R$ 500 mil e uma despesa extraordinária que leva um clube a −R$ 1,2 milhão. Os lançamentos sintéticos estão identificados por chaves `012d-*` e pelas descrições `Controlled...`; a 012D os classificava em `StadiumRenovation`. Essa classificação foi mantida para comparabilidade, mas essas sondas não correspondem a obra física nem são despesas recorrentes do produto. Ambas as trajetórias incluem as mesmas sondas uma única vez. Elas não são o mecanismo usado para retirar caixa da candidata; todos os ajustes econômicos estão nos termos comerciais, demanda e decisões financeiras explicitados acima. O caixa de abertura de R$ 138,8 milhões é medido antes dessas intervenções, e os fluxos iniciais entram na reconciliação da primeira temporada.

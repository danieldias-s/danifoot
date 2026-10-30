# Economia dos Estádios, Bilheteria Automática e Desgaste Estrutural

A gestão de estádios e finanças em `src/Elifoot.Domain/MarketAndEconomyEngine.cs` implementa precificação automática de ingressos, custos operacionais fixos/variáveis, desgaste estrutural por temporada e cálculo de ROI para reformas e ampliações.

## 1. Precificação Automática e Público (`CalculateMatchGateEconomics`)

O preço do ingresso é determinado automaticamente a cada partida com base na divisão, campanha do mandante, reputação do adversário e condição estrutural do estádio:

- **Ingresso Base por Divisão**:
  - `1ª Divisão`: `R$ 24,00`
  - `2ª Divisão`: `R$ 18,00`
  - `3ª Divisão`: `R$ 14,00`
  - `4ª Divisão`: `R$ 11,00`
  - `Distrital`: `R$ 8,00`
  - Jogos avançados da Taça (`Oitavas` até `Final`): multiplicador $\times 1.25$.
- **Elasticidade Preço-Demanda**:
  - O público pagante é limitado ao intervalo $[5\% \cdot \text{UsableCapacity},\ 100\% \cdot \text{UsableCapacity}]$.
  - Receita bruta: $\text{Revenue} = \text{Attendance} \times \text{TicketPrice}$.

## 2. Custos Operacionais da Partida

Conforme os parâmetros de referência calibrados:
- **Custo Fixo Operacional**:
  $$\text{FixedCost} = \text{BuiltCapacity} \times 0.70 \times \text{DeteriorationPenalty}$$
  onde $\text{DeteriorationPenalty} = 1 + 0.008 \cdot (80 - \text{ConditionPercent})$ quando a condição estrutural cai abaixo de $80\%$.
- **Custo Variável por Torcedor**:
  $$\text{VariableCost} = \text{Attendance} \times 1.10$$

## 3. Desgaste Estrutural Anual (1% a 6%), Manutenção e Reforma

Ao final de cada temporada (`ProcessSeasonalStadiumWearAndAiProjects`):
- **Sem manutenção preventiva**: o estádio sofre desgaste estrutural determinístico entre **1% e 6%** (sorteado via seed).
- **Com manutenção preventiva paga**: o desgaste é reduzido para **0% a 1%**.
- **Impacto na Capacidade**:
  - O desgaste **nunca apaga fisicamente a capacidade construída (`BuiltCapacity`)**; ele reduz apenas a **capacidade utilizável (`UsableCapacity`)**:
    $$\text{UsableCapacity} = \text{Round}\left(\text{BuiltCapacity} \times \frac{\text{ConditionPercent}}{100}\right)$$
- **Reformas (`StartStadiumRenovation`)**: recuperam `+15%` de condição estrutural e restauram os assentos utilizáveis após 3 rodadas de obra, com custo proporcional ao dano e ao porte do estádio.
- **Ampliações (`StartStadiumExpansion`)**: a IA só amplia o estádio quando a ocupação supera `88%` da capacidade utilizável e o retorno líquido estimado em 4 temporadas supera o custo da obra (ROI positivo).
# Atualização de QA 002

A auditoria de 100 temporadas identificou déficit estrutural sem receitas recorrentes. Foi adicionada a categoria auditável e idempotente `Sponsorship`. Os valores atuais, conferidos no código de 006/007, são R$ 185.000 / 125.000 / 92.000 / 72.000 por rodada nas quatro divisões e R$ 20.000 para distritais, incluindo as 22 rodadas mesmo sem jogo. Os valores menores anteriormente descritos aqui pertenciam a QA002. São parâmetros novos deste projeto, sem pretensão de reproduzir a economia original do Elifoot 98.

Salários, contagem de contratos, empréstimos e avanço de obras são processados somente quando o clube participa da rodada. Assim, patrocínio continua entrando após eliminação, enquanto salários por rodada de jogo não são cobrados. Essa assimetria explica parte importante da recuperação e do acúmulo de caixa; foi preservada. Os 21 recuperados do cenário 006 incluem 5 clubes que tiveram recaídas. Evidências por clube, fluxos e limitações de sustentabilidade estão em `FINANCIAL_RECOVERY_007.json`.


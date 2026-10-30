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

A auditoria de 100 temporadas identificou déficit estrutural sem receitas recorrentes. Foi adicionada a categoria auditável e idempotente `Sponsorship`: R$ 140.000 / 100.000 / 75.000 / 60.000 por rodada nas quatro divisões e R$ 20.000 para distritais, incluindo rodadas sem jogo. São parâmetros novos deste projeto, sem pretensão de reproduzir a economia original do Elifoot 98. Valores e limitações estão em [PLAYABLE_QA_002.md](PLAYABLE_QA_002.md) e [AI_100_SEASONS_QA002.json](AI_100_SEASONS_QA002.json).


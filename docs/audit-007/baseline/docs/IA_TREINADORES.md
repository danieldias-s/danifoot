# Inteligência Artificial dos Treinadores (`AiManagerBrain`)

A IA tática em `src/Elifoot.Domain/AiManagerBrain.cs` modela cada treinador (`AiManager`) como uma entidade persistente e independente do clube (`Club`). Quando um técnico é demitido e contratado por outro clube, ele leva sua identidade e suas formações preferidas consigo.

## 1. Perfil Tático Persistente

Cada `AiManager` possui:
- **1 a 3 formações preferidas com pesos normalizados somando 100%** (ex.: `4-3-3: 60%`, `3-4-3: 25%`, `4-4-2: 15%`).
- **Identidade Tática**: `Ofensivo`, `Equilibrado` ou `Defensivo`.
- **Parâmetros Comportamentais (`0..100`)**:
  - `Pragmatism`: disposição para abandonar a formação favorita quando faltam peças ou o orçamento está curto.
  - `Adaptability`: velocidade de reação a sequências sem vitória (`ConsecutiveWinlessRounds`).
  - `FinancialAggressiveness`: fração do caixa excedente alocada para contratações no mercado.
  - `YouthPatience`: tolerância na rotação do elenco.

## 2. Pontuação de Escolha Tática por Partida

A cada rodada, a escolha da formação entre os esquemas candidatos é calculada por:

$$\text{Score}(F) = \text{PreferencePrior} + \text{FitToSquad} + \text{OpponentMatchup} + \text{HomeAway} + \text{CurrentForm} + \text{InjuriesSuspensions} + \text{FinancialConstraints} + \text{StrategicVariation}$$

- **Adaptação Realista**: se o técnico prefere `4-3-3` (exige 3 atacantes titulares), mas só possui 2 atacantes aptos (ou está com caixa curto e zero reservas no setor), o termo `FitToSquad + FinancialConstraints` penaliza `4-3-3` e faz o treinador escolher `4-4-2` temporariamente, registrando no log explicável:
  > `Formação 4-4-2 escolhida: faltam atacantes aptos para 4-3-3 (2/3), jogo fora de casa, orçamento curto.`

## 3. Planejamento de Elenco (`ManagerSquadPlan`) e Mercado a Serviço da Tática

Semanalmente, `PlanAndSelectLineup` gera um `ManagerSquadPlan` contendo:
- `TargetStarters` e `TargetReserves` por setor (`G`, `D`, `M`, `A`) baseados na tática favorita nº 1 do treinador.
- `PriorityGapPositions` e `PriorityGapDescriptions`: identifica urgências de titulares, falta de reservas imediatos (ex.: buscar o 4º atacante para um esquema `4-3-3` quando há caixa disponível) ou necessidade de elevar a força média de um setor, **mesmo que o clube já tenha 18 a 20 atletas** (respeitando o teto de 24).
- `SurplusPlayerIds`: jogadores excedentes fora do plano tático que podem ser listados para venda para financiar reforços.

## 4. Reação Tática Durante a Partida (`EvaluateAiInMatchReaction`)

Durante a partida, a IA avalia o estado nos intervalos gratuitos (`45'`, `90'`, `105'`) e em checkpoints do 2º tempo (`62'`, `74'`, `83'`, `112'`):
- Substitui imediatamente titulares lesionados em campo se houver quota disponível.
- Se estiver perdendo após os `60'`, muda para formação mais ofensiva (`3-4-3` ou `4-3-3`) e coloca atacantes descansados.
- Se estiver vencendo após os `74'`, reforça a defesa (`5-4-1` ou `5-3-2`) e substitui atletas amarelados de comportamento agressivo (`Caneleiro`, `Caceteiro`, `Sarrafeiro`).
- Respeita estritamente os limites de **5 substituições / 3 paradas** no tempo normal (**6 substituições / 4 paradas** na prorrogação) e **nunca abre janelas de pausa global desnecessárias** para os humanos.

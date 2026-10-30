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
- Ao perder, considera uma formação própria mais ofensiva a partir de `clamp(84 - Adaptability*0.28 - max(0,desvantagem-1)*8,55,84)` minutos. Ao vencer, considera proteger o resultado a partir de `clamp(88 - Adaptability*0.14 - max(0,vantagem-1)*3,70,88)`.
- A avaliação continua restrita aos checkpoints e intervalos; essas fórmulas não criam decisões a cada minuto. As opções são somente suas formações preferidas, ponderadas pelo peso e pelo encaixe real. Banco e quotas devem permitir suprir os setores necessários. Uma formação que reduza improvisações também pode ser considerada.
- Há cooldown de 10 minutos a partir da última troca ou mudança de formação; nova lesão permite avaliação imediata. Empate não provoca mudanças aleatórias de formação. Jogadores amarelados ou mais fracos podem ser substituídos independentemente de Adaptability.
- Respeita estritamente os limites de **5 substituições / 3 paradas** no tempo normal (**6 substituições / 4 paradas** na prorrogação) e **nunca abre janelas de pausa global desnecessárias** para os humanos.

O corte rígido `Adaptability >= 65` foi introduzido intencionalmente em 005 para preservar personalidade. Em 007, controles reproduziram sua incapacidade de reagir a desvantagens extremas e a escolha inadequada da segunda preferência. A correção usa progressão de tempo e todas as preferências próprias, preservando a seleção pré-jogo e a personalidade persistente. Uma mudança somente de formação pode ocorrer em intervalo gratuito mesmo com a quota de substituições esgotada.

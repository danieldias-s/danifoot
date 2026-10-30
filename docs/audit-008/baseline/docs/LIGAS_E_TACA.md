# Competições, Divisões, Clubes Distritais e Taça de 64 (`CompetitionAndSeasonEngine`)

O sistema de competições em `src/Elifoot.Domain/CompetitionAndSeasonEngine.cs` organiza temporadas consecutivas completas para **128 clubes** (baseline) e escala para **256, 512 e 1.024 clubes**.

## 1. Estrutura das 4 Divisões Nacionais + Distritais

- **32 Clubes de Divisão**:
  - `1ª Divisão`: 8 clubes
  - `2ª Divisão`: 8 clubes
  - `3ª Divisão`: 8 clubes
  - `4ª Divisão`: 8 clubes
- **96 Clubes Distritais (`Distrital`)** no mundo padrão de 128 clubes (`224` em 256, `480` em 512, `992` em 1.024):
  - Possuem elenco completo (`18` jogadores válidos), estádio, caixa e treinador de IA próprio.
- **Campeonato Nacional (Pontos Corridos Ida e Volta)**:
  - 14 rodadas de liga por temporada (7 no turno + 7 no returno com mando invertido pelo algoritmo de Berger).
  - Critérios de desempate: 1) Pontos (`LeaguePoints`), 2) Vitórias (`LeagueWins`), 3) Saldo de Gols (`LeagueGoalDifference`), 4) Gols Pró (`LeagueGoalsFor`), 5) Índice determinístico da seed (`DeterministicIndex`).
- **Promoção e Rebaixamento**:
  - Os 2 primeiros de cada divisão (`2ª`, `3ª`, `4ª`) sobem; os 2 últimos de cada divisão (`1ª`, `2ª`, `3ª`, `4ª`) caem.
  - Os 2 clubes distritais de melhor campanha na Taça (e maior reputação) sobem para a `4ª Divisão`.

## 2. Regra Estrita: Humano Nunca Controla Clube Distrital

- Nenhum humano pode selecionar um clube distrital ao entrar na sala ou por convite.
- **Demissão por queda à Distrital**: se o clube controlado por um treinador humano terminar entre os 2 últimos da `4ª Divisão` e cair para a `Divisão Distrital`, o treinador humano **perde o cargo automaticamente (`DismissHumanCoach`)**, fica **Desempregado**, **mantém seu histórico e pontos no Ranking de Treinadores**, continua avançando rodadas pelo botão `PRONTO PARA ACOMPANHAR` e recebe convites (`JobInvitations`) de clubes das divisões principais.

## 3. Taça Nacional (Preliminares Distritais + Chave Principal de 64)

1. **Preliminar 1 (`CupPreliminary1`)**:
   - Dos 96 clubes distritais, **64 duelam em mata-mata (32 confrontos)** e **32 recebem bye determinístico**.
   - Saem 32 vencedores + 32 byes = **64 clubes distritais**.
2. **Preliminar 2 (`CupPreliminary2`)**:
   - Os 64 clubes distritais disputam 32 confrontos eliminatórios, classificando **32 clubes distritais**.
3. **Taça Principal de 64 Clubes (`CupRoundOf64` até `CupFinal`)**:
   - Os **32 distritais classificados** juntam-se aos **32 clubes das 4 divisões** = **64 clubes**.
   - Fases: `32-avos (64)` $\to$ `16-avos (32)` $\to$ `Oitavas (16)` $\to$ `Quartas (8)` $\to$ `Semifinal (4)` $\to$ `Grande Final (2)`.
   - Em caso de empate aos `90'`, a partida entra em **Prorrogação (`91'..120'`)** com `+1` substituição (`6` no total) e `+1` oportunidade de parada (`4` no total). Persistindo o empate aos `120'`, decide-se nos **Pênaltis (`ResolvePenaltyShootout`)**.

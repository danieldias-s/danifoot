# ELIFOOT 98 — REGISTRO DE PROBLEMAS CORRIGIDOS, ESCOLHAS DE DESIGN E LIMITAÇÕES CONHECIDAS (006)

**TASK:** `ELIFOOT_DISCIPLINE_AND_GAMEPLAY_AUDIT_006`  
**PROJECT:** `D:\elifoot_98`  
**DATA:** 2026-10-30 UTC

---

## 1. Bugs e Problemas de Contabilização/Balanceamento Resolvidos na Auditoria 006

Todos os problemas identificados durante a inspeção de código e auditoria estatística foram corrigidos e cobertos por testes automatizados de regressão (`27/27 PASS`):

| ID | Classificação | Descrição do Problema (Baseline `005`) | Solução Implementada em `006` | Status |
|---|---|---|---|---|
| `BUG-006-01` | **BUG** | Em `MatchEngine.ProcessTeamDisciplineTick`, quando um jogador recebia o **2º amarelo na mesma partida** (`SecondYellowRedCard`), `YellowCardsAccumulated` era incrementado uma segunda vez na mesma partida além da suspensão automática por expulsão (`SuspensionRoundsRemaining = 1`), podendo gerar dupla suspensão (vermelho + 3º amarelo acumulado). | No ramo de 2º amarelo (`hasYellow`), o incremento de `YellowCardsAccumulated` do 1º amarelo da partida é revertido (se não tiver acionado suspensão por 3º amarelo no 1º cartão) e apenas a suspensão de 1 rodada pela expulsão é aplicada. | **RESOLVIDO E TESTADO** |
| `BUG-006-02` | **BUG** | Em `MarketAndEconomyEngine.SettleMatchAndRoundFinancials`, o Passo 1 possuía trava `!m.FinancialsSettled`, mas o Passo 2 (decremento de `ContractRoundsRemaining`, `InjuryRoundsRemaining`, `SuspensionRoundsRemaining`, `BankLoans` e obras de estádio) não possuía trava de idempotência por rodada. Invocar a liquidação duas vezes na mesma rodada reduzia contratos em dobro e disparava renovações duplicadas. | Adicionada chave idempotente `round_settled:S{SeasonNumber}:R{RoundNumber}` em `world.AppliedTransactionKeys` antes do Passo 2. Verificado em 100 temporadas com chamada dupla forçada (`doubleSettledMatchFinancialViolations = 0`). | **RESOLVIDO E TESTADO** |
| `BALANCEAMENTO-006-01` | **BALANCEAMENTO** | Taxa elevada de expulsões no baseline `005` (`16.808` vermelhos em `35.100` jogos = `0,479`/jogo) devido à ausência de amortecimento de cautela após o 1º amarelo e pesos agressivos de `Caceteiro`/`Sarrafeiro`. | Introduzido `GetEffectivePlayerFoulWeight` com amortecimento pós-1º amarelo (`0,48x` para `Caceteiro`/`Sarrafeiro`, `0,32x` para os demais) e recalibração das probabilidades de cartão e vermelho direto. Expulsões reduzidas para **`5.735` (`0,163`/jogo, `-65,88%`)**. | **RESOLVIDO E TESTADO** |
| `BALANCEAMENTO-006-02` | **BALANCEAMENTO** | Taxa elevada de pênaltis em bola rolando no baseline `005` (`12.199` cobranças = `0,3475`/jogo) devido a `penaltyChance = 0,052` fixa em qualquer ataque perigoso sem gol quando a defesa não era 100% `FairPlay`. | Probabilidade de falta na área ponderada pela média de `GetEffectivePlayerFoulWeight` dos defensores/goleiro (`0,031 * avgBoxFoulWeight`). Pênaltis em bola rolando reduzidos para **`7.018` (`0,1999`/jogo, `-42,47%`)**. | **RESOLVIDO E TESTADO** |
| `CONTABILIZAÇÃO-006-01` | **PROBLEMA DE CONTABILIZAÇÃO** | `MatchFixture` não contabilizava o total de faltas cometidas por equipe (`HomeFouls` / `AwayFouls`), registrando apenas os cartões resultantes. | Adicionados `HomeFouls` e `AwayFouls` em `MatchFixture`, garantindo `Cartões <= Faltas` (`633.847` faltas / `18,06` por jogo em 100 temporadas). | **RESOLVIDO E TESTADO** |
| `CONTABILIZAÇÃO-006-02` | **PROBLEMA DE CONTABILIZAÇÃO** | Pênaltis perdidos (`PenaltyMissed`) não separavam defesas do goleiro de chutes para fora, e `MatchFixture` não registrava o total de cobranças de disputas pós-120' (`HomeShootoutAttempts` / `AwayShootoutAttempts`). | `PenaltyMissed` agora distingue `"PÊNALTI DEFENDIDO!"` (`SecondaryPlayerId = defendingGk.Id`, `906` ocorrências) de `"PÊNALTI PARA FORA!"` (`864` ocorrências), e `ResolvePenaltyShootout` grava `HomeShootoutAttempts` / `AwayShootoutAttempts` (`18.802` cobranças em `1.800` disputas). | **RESOLVIDO E TESTADO** |

---

## 2. Escolhas de Design Documentadas

- **`DESIGN-006-01` — Simplicidade Clássica Elifoot 98:**
  - O jogo preserva intencionalmente a estética e o fluxo direto do Elifoot 98: atributos enxutos (Força `1..50`, Estrela `*`, Comportamento disciplinar, Posição `G/D/M/A`), tela de rodada simultânea focada em placar e eventos-chave, e intervenção rápida durante a pausa global.
- **`DESIGN-006-02` — Formação `5-0-5` como Tática Situacional de Alto Risco:**
  - A formação `5-0-5` aplica o multiplicador estrutural de `0,82x` pela ausência de meio-campistas, resultando em menor posse (`44,30%`) e menor taxa de vitórias em 90 minutos (`29,44%` no experimento controlado), funcionando como opção extrema de tudo-ou-nada nos minutos finais.

---

## 3. Limitações Conhecidas de Ambiente e Escopo

1. **Emulador Android Fora de Escopo (Bloqueio Ambiental Documentado):**
   - Conforme determinado nas diretrizes da missão (`Seção 1`), o ambiente Android permanece fora do escopo desta auditoria devido à instabilidade comprovada do emulador local na máquina de desenvolvimento. O código Flutter (`src/elifoot_client`) permanece 100% compatível e o **Flutter Web** é o ambiente principal validado.
2. **Playtest Humano Subjetivo vs Automação Multi-Cliente:**
   - Os testes de `2`, `4`, `6` e `8` treinadores em `Elifoot.Integration.Tests` utilizam clientes HTTP e SignalR reais e independentes contra o backend e banco PostgreSQL reais, comprovando a corretude técnica e sincronização em tempo real, mas a avaliação subjetiva de diversão entre amigos depende de sessão presencial/LAN seguindo o guia [`docs/HUMAN_PLAYTEST_GUIDE_006.md`](file:///d:/elifoot_98/docs/HUMAN_PLAYTEST_GUIDE_006.md).

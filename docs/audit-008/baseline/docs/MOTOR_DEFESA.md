# Motor de Partidas, Defesa e Disciplina (`MatchSimulationEngine`)

O motor de simulação em `src/Elifoot.Domain/MatchEngine.cs` é 100% original, auditável, determinístico por seed (`SplitMix64`) e opera **minuto a minuto (`SimulateSingleMinute`)**, sem pré-calcular o resultado final da partida antes de possíveis solicitações de pausa.

## 1. Cálculo de Força por Setores (`ComputeSectorBreakdown`)

Para cada equipe ativa no minuto $m$:

1. **Goleiro (`G`)**:
   $$P_G = \text{Strength}_G \times \text{StarMod}_G \times \text{PositionMod}_G \times (0.93)^{\max(0, 11 - N_{\text{ativos}})}$$
   - Onde $\text{StarMod}_G = 1.10$ se possuir `*`, e $\text{PositionMod}_G = 1.0$ para goleiro de origem (`0.35` se jogador de linha improvisado no gol).

2. **Setores de Linha (`D`, `M`, `A`)**:
   - Jogadores escalados em seu setor primário recebem $100\%$ da sua força; jogadores improvisados fora de posição de linha recebem $65\%$.
   - Bônus de Craque `*` (`IsStar`):
     - Atacantes (`A`): $\times 1.18$
     - Meio-campistas (`M`): $\times 1.15$
     - Defensores (`D`): $\times 1.10$

3. **Sinergia Defesa-Meio e Ataque-Meio**:
   $$\text{EffectiveDefense} = (P_D + 0.45 \cdot P_M) \times \text{HomeMod} \times \text{IdentityDefMod} \times (0.93)^{\text{desfalques}}$$
   $$\text{EffectiveAttack} = (P_A + 0.50 \cdot P_M) \times \text{HomeMod} \times \text{IdentityAttMod} \times (0.93)^{\text{desfalques}} \times \text{TransitionMod}$$
   - $\text{HomeMod} = 1.06$ para o mandante (`1.00` para o visitante).
   - $\text{TransitionMod} = 0.82$ se a formação for `5-0-5` (ausência de meio-campo), `1.00` para as demais formações.

## 2. Resolução Minuto a Minuto: Ataque vs. Defesa e Finalização vs. Goleiro

1. **Probabilidade de Criação de Chance no Minuto $m$**:
   $$R_{\text{setor}} = \frac{\text{EffectiveAttack}_{\text{atacante}}}{\max(10, \text{EffectiveDefense}_{\text{defensor}})}$$
   $$P(\text{Chance}) = \text{Clamp}\left(0.044 \cdot R_{\text{setor}}^{0.85},\ 0.008,\ 0.14\right)$$
   - Quando $\text{Ataque} = \text{Defesa}$ ($R_{\text{setor}} = 1.0$), há $4.4\%$ de chance por minuto (~4 finalizações claras por equipe em 90 minutos).
   - Há $9\%$ de chance da jogada resultar em impedimento (`Offside`).

2. **Confronto Finalizador vs. Goleiro (`Shot vs Goalkeeper`)**:
   - O finalizador é sorteado com pesos proporcionais à posição (`A=5.5`, `M=2.8`, `D=0.7`), bônus `*` ($\times 1.35$) e força.
   - Poder do chute:
     $$S_{\text{shot}} = \text{Finishing}_{\text{atleta}} + 0.08 \cdot \text{EffectiveAttack}$$
   - Resistência do goleiro e cobertura defensiva:
     $$R_{GK} = 1.18 \cdot P_G + 0.06 \cdot \text{EffectiveDefense}$$
   - Probabilidade de Gol:
     $$P(\text{Gol} \mid \text{Chute}) = \text{Clamp}\left(0.34 \cdot \left(\frac{S_{\text{shot}}}{\max(8, R_{GK})}\right)^{0.90},\ 0.06,\ 0.72\right)$$

## 3. Disciplina e Comportamentos (`Fair Play` a `Sarrafeiro`)

Pesos de propensão a faltas (`GetBehaviorFoulWeight`):
- `Fair Play`: **0.00** (sem faltas intencionais e sem cartões nos mecanismos implementados)
- `Cordeirinho`: **0.35** (vermelho direto em uma decisão de cartão: 0.5%)
- `Cavalheiro`: **0.75** (vermelho direto em uma decisão de cartão: 1.2%)
- `Caneleiro`: **1.45** (vermelho direto em uma decisão de cartão: 2.5%)
- `Caceteiro`: **2.35** (vermelho direto em uma decisão de cartão: 4.8%)
- `Sarrafeiro`: **3.60** (vermelho direto em uma decisão de cartão: 8.5%)

Valores conferidos em 007 contra o código efetivamente executado de 006. A probabilidade de falta por equipe/minuto é `clamp(0.095 * peso médio efetivo, 0, 0.35)`; a região do mesmo sorteio que gera cartão é `0.0128 * peso médio efetivo`. Não são dois sorteios independentes. Após um amarelo, o peso individual cai para `0.48x` em Caceteiro/Sarrafeiro e `0.32x` nos demais. Pênaltis usam a média de peso dos defensores/goleiro, com `clamp(0.031 * média, 0, 0.085)`.

Fair Play continua sujeito a gols, lesões, impedimentos e substituições. Não existe no projeto outro mecanismo de cartões por reclamação ou conduta não relacionada a essas faltas. Isso descreve a escolha documentada deste projeto; não comprova regras do executável histórico do Elifoot 98. Faltas sem cartão e faltas de pênalti não registram infrator individual: sua atribuição exata por comportamento só é possível em equipes homogêneas. Consulte `PLAYER_BEHAVIOR_DISCIPLINE_007.json`.

O 2º cartão amarelo na mesma partida gera automaticamente `SecondYellowRedCard` e remove o atleta de `ActiveStarterIds`, reduzindo o poder do setor a partir do minuto seguinte.

## 4. Exemplo Numérico Reprodutível

- **Equipe A (Casa, `4-3-3`, 11 titulares F=40, 2 atacantes `*`)**:
  - $P_D = 160$, $P_M = 120$, $P_A = 40 \times 1.18 + 40 \times 1.18 + 40 = 134.4$.
  - $\text{EffectiveAttack}_A = (134.4 + 0.50 \times 120) \times 1.06 = 206.06$.
- **Equipe B (Fora, `4-4-2`, 11 titulares F=25, Goleiro F=25)**:
  - $P_D = 100$, $P_M = 100 \implies \text{EffectiveDefense}_B = 145.0$.
  - Razão $R_{\text{setor}} = 206.06 / 145.0 = 1.421 \implies P(\text{Chance}_A) = 5.94\%/\text{min}$.
  - Caso Equipe B tente defender com atletas F=45 ($\text{EffectiveDefense}_B = 261.0$), $R_{\text{setor}}$ cai para $0.789$ e $P(\text{Chance}_A)$ cai para $3.60\%/\text{min}$, comprovando monotonicidade estrita.

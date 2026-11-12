# Elifoot 98 Multiplayer — Regras de força e integração com o catálogo offline V1.1

**Documento de referência — 12/11/2026**  
**Base:** auditoria somente leitura da implementação existente fornecida pelo Codex, após o commit local `aa5130b`.  
**Objetivo:** registrar **as regras efetivamente encontradas no código** e a integração mínima necessária com o catálogo V1.1.  
**Situação na auditoria:** `PARTIAL`: o catálogo V1.1 foi implementado, mas a criação de mundos V1.1 estava bloqueada. Este documento não comprova que a integração já foi realizada.

> Não substituir nem recalibrar as fórmulas de força existentes. Manter os saves e o formato 1.0, e validar o fluxo completo antes de retirar o bloqueio V1.1.

## 1. Três conceitos distintos

| Conceito | Intervalo | Uso confirmado |
|---|---:|---|
| `teamStrength` / `ClubInitialPower` | 1–20 | Classificar os clubes para a distribuição inicial e as posições nas divisões. |
| Ranking histórico (`historicalRanking`, prioridade manual) | Métrica comparável / prioridade | Desempatar clubes com igual força coletiva; não é bônus de partida. |
| `Player.Strength` | 1–50 | Atributo dos jogadores, armazenado no **mundo** e usado pelo motor, mercado e outras regras. Não aparece no JSON de jogador V1.1. |

**Importante:** na implementação auditada, `teamStrength` **não se converte diretamente** em força individual. A ordenação dos clubes define a divisão/posição inicial, e as regras 017B utilizam essa divisão/posição para distribuir a força dos jogadores.

## 2. Regra existente 016B — força coletiva inicial

Arquivo de referência: `src/Elifoot.Domain/Catalog/WorldCatalogRules016B.cs`.

Para ranking geral zero-based `r`:

```text
ClubInitialPower(r) = max(1, 20 - floor(max(0, r) / 2))
```

Pares sucessivos de clubes começam em força 20, 19, 18 ... até 1. Overrides manuais de força 1–20 já existem na regra 016B. No catálogo V1.1, o proprietário preenche `teamStrength` explicitamente. A distribuição inicial prioriza:

1. `teamStrength` decrescente;
2. `historicalRanking.rating` decrescente, **somente quando comparável**;
3. `tieBreakPriority` crescente para empates não resolvidos.

A identidade estável do clube não deve decidir arbitrariamente uma vaga em fronteiras entre divisões quando faltar desempate válido.

### Tamanho dos elencos 016B

```text
SquadSizeAt(r) = 23 - floor((max(0, r) mod 14) / 2)
```

O padrão cíclico identificado é `[23, 23, 22, 22, 21, 21, 20, 20, 19, 19, 18, 18, 17, 17]`. Clubes no topo, com força coletiva 20 ou 19, têm regra de mínimo de 22 jogadores no preparo 016B. O schema V1.1 `READY` aceita de **11 a 23 jogadores** (com pelo menos um goleiro e dez jogadores de linha). **Não confundir capacidade aceita pelo catálogo com criação automática de jogadores.**

## 3. Regra existente 017B — força individual inicial

Arquivos de referência:

- `src/Elifoot.Domain/Catalog/InitialSquadStrength017B.cs`
- `src/Elifoot.Domain/ChampionshipStrengthLimits.cs`
- `src/Elifoot.Domain/Catalog/WorldCreatorEngine.cs`
- `src/Elifoot.Domain/WorldSeedFactory.cs`

A regra 017B define faixas de força dos jogadores segundo a **divisão** e a **posição inicial (1–8)** do clube. Aplica-se às **32 equipes das quatro divisões principais**.

| Divisão | Referências de força conforme posição 1ª a 8ª | Faixa geral |
|---|---|---|
| 1ª | 50, 50, 50, 46, 44, 42, 40, 39 | 39–50 |
| 2ª | 38, 37, 36, 34, 32, 30, 28, 27 | 26–38 |
| 3ª | 25, 24, 23, 21, 19, 17, 15, 14 | 13–25 |
| 4ª | 12, 11, 10, 8, 6, 4, 2, 1 | 1–12 |

### Faixas por posição inicial

| Divisão | P1 | P2 | P3 | P4 | P5 | P6 | P7 | P8 |
|---|---|---|---|---|---|---|---|---|
| 1ª | 47–50 | 47–50 | 47–50 | 45–47 | 43–45 | 41–43 | 39–41 | 39–40 |
| 2ª | 37–38 | 36–38 | 35–37 | 33–35 | 31–33 | 29–31 | 27–29 | 26–28 |
| 3ª | 24–25 | 23–25 | 22–24 | 20–22 | 18–20 | 16–18 | 14–16 | 13–15 |
| 4ª | 11–12 | 10–12 | 9–11 | 7–9 | 5–7 | 3–5 | 1–3 | 1–2 |

O sorteio é **determinístico** por `seed`, identificador do clube e identificador do jogador, com SHA-256. Nos três primeiros clubes da 1ª divisão, existe uma distribuição especial que garante a presença dos valores 47, 48, 49 e 50 no elenco (desde que as pré-condições do algoritmo sejam atendidas). Não depender de `GetHashCode` instável.

**Posição, idade, titularidade, asterisco, comportamento e nacionalidade não influenciam a atribuição 017B.** Os titulares são selecionados depois com base nas forças já criadas.

### Distritais — pendência da integração

A regra `InitialSquadStrength017B.Apply` **não sobrescreve** os jogadores dos 96 clubes distritais (divisão 5). A auditoria encontrou mecanismos pré-existentes de força para distritais em `WorldSeedFactory`, `InitialPlayerStrength` e `PlayableWorldPolicy`. É necessário **verificar qual caminho é efetivamente usado e aprovado**, reaproveitá-lo e testá-lo; não inventar curva nova.

Como referência, a fábrica rápida legada sem 017B usa sorteios por divisão, inclusive distrital 10–21, mas isso **não autoriza assumir** que seja o caminho definitivo do criador V1.1. Se a decisão sobre os distritais permanecer ambígua, manter o bloqueio somente no fluxo não comprovado.

### Exemplo importante para múltiplos países

Se nove times têm `teamStrength = 20`, apenas oito ocupam a 1ª divisão inicial. O nono entra na 2ª segundo ranking comparável ou desempate manual, **mantendo força coletiva 20**, mas recebendo a **faixa de força individual da 2ª divisão** no mecanismo 017B. Não elevar automaticamente seus atletas à banda da 1ª divisão, nem reduzir a `teamStrength` para 19. Registrar essa distinção na prévia do criador.

## 4. Evolução de força — regra existente `RESERVE_PROGRESS`

Referência: `src/Elifoot.Domain/ClassicForceEvolutionEngine.cs`, método `ApplyFinishedRound`, chamado pela atualização de resultados da liga.

Ao final de **cada rodada oficial da Liga**:

- Jogador que **não entrou em campo** recebe **+1 de força**, desde que sua força atual seja menor que o teto de sua divisão e **posição atual do clube na tabela**.
- Inclui reservas não utilizados, atletas fora da relação, suspensos e lesionados que não atuaram.
- É determinístico; não é uma chance aleatória.
- Não há ganho por reserva em rodadas da Taça.

Tetos de posição (`ChampionshipStrengthLimits.For`) são as mesmas referências listadas na seção 3. Para distritais ou atletas sem clube, o teto documentado é 1, portanto não há crescimento dessa regra.

## 5. Regressão de força — regra existente `PLAYED_MATCH_DECLINE`

Em uma rodada oficial da Liga, quem **entrou em campo** (titular ou reserva utilizado), desde que **não tenha sofrido nova lesão na mesma partida**, sofre sorteio determinístico:

- **50%**: perde **1 de força**;
- **50%**: mantém a força;
- força mínima sempre **1**.

A fonte pseudoaleatória decorre da seed do mundo, temporada, rodada e identidade do jogador. Na Taça, não há perda por **participação**.

## 6. Regressão por lesão — `INJURY_DAILY_LOSS`

Na política `ForceEvolutionMode == ClassicAdapted`:

- A lesão dura de **2 a 15 dias virtuais**, com distribuição ponderada já codificada.
- `MaximumLoss(force,duration) = min(force - 1, floor(force * duration / 30))`.
- `TotalLoss` é sorteado de zero até `MaximumLoss`, inclusive.
- Essa perda é descontada progressivamente durante os dias da lesão, respeitando força mínima 1.
- O calendário avança **3 dias virtuais por rodada**.
- Ao recuperar-se, o atleta **não recupera automaticamente** a força perdida.

O processamento da virada de temporada avança 21 dias virtuais para liquidar lesões remanescentes. O motor sazonal legado de evolução não deve ser acionado em duplicidade quando `ClassicForceEvolutionEngine` estiver ativo.

## 7. Integração necessária no catálogo V1.1

O catálogo offline V1.1 já registra em cada jogador apenas:

```json
{
  "id": "jogador-exemplo-001",
  "name": "Atleta Exemplo",
  "position": "M",
  "nationality": "BR",
  "behavior": 2,
  "isStar": true
}
```

**Não há** `players[].strength` no JSON de cadastro V1.1. A força pertence ao jogador persistido no **mundo criado**.

A auditoria encontrou os seguintes impedimentos em `src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs`:

1. O caminho V1.1 ainda não mapeava adequadamente `OfflinePlayerV11` para as entidades usadas na criação do mundo.
2. `OfflineCatalogRanking.ForceBlock` impedia `Preview` e `Create` no fluxo V1.1.
3. Os 96 distritais precisam receber força válida por um mecanismo **pré-existente e comprovado**, pois 017B só cobre os 32 clubes das divisões 1–4.

### Critérios para retirar o bloqueio V1.1

- [ ] Exatamente 128 clubes selecionados: 8 + 8 + 8 + 8 + 96.
- [ ] 128 elencos válidos, sem `strength` no JSON V1.1, sem completar artificialmente rascunhos.
- [ ] Mapeamento de todos os atletas/treinadores ao mundo sem perder identidade, posição, nacionalidade, comportamento, estrela e preferências táticas.
- [ ] Força final 1–50 para **todos** os jogadores, inclusive distritais, antes de salvar ou calcular salários.
- [ ] 017B inalterada nas quatro divisões principais.
- [ ] Determinismo de prévia/criação e mesma seed.
- [ ] `teamStrength` 1–20 e ranking mantidos como atributos distintos da força individual.
- [ ] Formato 1.0 e saves existentes sem mudança de resultado.
- [ ] Testes de regressão .NET, Flutter, compilação e análise aprovados.

Só então retirar o bloqueio, com testes que comprovem que os pré-requisitos são satisfeitos.

## 8. Segurança, Git e documentação

- Trabalhar **na branch local `main`**, verificando antes `git status`, `git branch -vv`, `git worktree list` e eventuais commits em outras branches e alterações não commitadas.
- **Não** executar `git reset --hard`, `git clean`, sobrescrever alterações, apagar branches, reescrever histórico ou fazer force push.
- Não incorporar automaticamente trabalho não validado de outras branches. Inventariar, testar, integrar na `main` o que é pertinente e seguro, ou documentar pendências e conflitos.
- Não fazer push. Evitar reiniciar os serviços ativos e nunca alterar bancos/saves existentes.
- Guardar estes dois documentos, quando disponibilizados ao ambiente Windows, em `D:\elifoot_98\docs\catalogos\`:
  - `ELIFOOT98_REGRAS_CADASTRO_CATALOGOS_V11.md` — cadastro de clubes, identidades, arquivos, jogadores, técnicos e critérios do catálogo.
  - `ELIFOOT98_REGRAS_FORCAS_E_INTEGRACAO_V11.md` — geração 016B/017B, evolução/regressão e integração V1.1.
- Caso o primeiro arquivo não esteja disponível no workspace Windows, **não alegar que foi copiado**. Usar o conteúdo fornecido pelo proprietário ou recriá-lo de modo fiel a partir das regras confirmadas, assinalando diferença de proveniência.

## 9. Testes essenciais

1. Forças iniciais conforme as 32 posições das quatro divisões e a regra 017B; garantia das forças 47, 48, 49 e 50 nas três primeiras equipes da 1ª divisão.
2. Forças válidas e determinísticas para os 96 clubes distritais, usando o caminho comprovado e já existente.
3. Repetição com mesma seed/IDs produz forças iguais; seed diferente pode mudar sorteios sem extrapolar faixas.
4. Ranking resolve empate de clubes com `teamStrength` igual; prioridade manual usada quando necessário; dados sem comparabilidade não são misturados.
5. Comportamento 0–5, estrela apenas para `M` e `A`, jogador sem força no arquivo 1.1.
6. Crescimento +1 para não utilizados nas rodadas da Liga até o teto da posição atual, nunca na Taça.
7. Participantes da Liga têm chance determinística de -1, salvo exceção de lesão na partida; sem queda por participação na Taça.
8. Lesões descontam força gradualmente e não devolvem automaticamente na recuperação; processamento único na virada de temporada.
9. Mercado, contratos, economia, IA, táticas, Taça, classificação, multiplayer e saves não sofrem regressão.
10. Nenhuma criação de mundo incompleto, e nenhum desbloqueio V1.1 sem prova por testes.

## 10. Arquivos técnicos para revisão

```text
src/Elifoot.Domain/Catalog/WorldCatalogRules016B.cs
src/Elifoot.Domain/Catalog/InitialSquadStrength017B.cs
src/Elifoot.Domain/ChampionshipStrengthLimits.cs
src/Elifoot.Domain/ClassicForceEvolutionEngine.cs
src/Elifoot.Domain/CompetitionAndSeasonEngine.cs
src/Elifoot.Domain/WorldSeedFactory.cs
src/Elifoot.Domain/Catalog/OfflineWorldFactory.cs
src/Elifoot.Domain/Catalog/OfflineCatalogRanking.cs
src/Elifoot.Domain/Catalog/InitialPlayerStrength.cs
src/Elifoot.Domain/Catalog/PlayableWorldPolicy.cs
docs/CLUB_INITIAL_POWER_016B.md
docs/INITIAL_STRENGTH_RULES_017B.md
docs/CLASSIC_FORCE_EVOLUTION_012C.md
docs/catalogos/ELIFOOT98_AUDITORIA_FORCAS_EXISTENTES.md
```

**Nota de proveniência:** os nomes, faixas e fórmulas acima foram extraídos da auditoria que o usuário compartilhou, não reexecutados neste ambiente contra o repositório local Windows. A implementação deve ser validada pelo Codex no código e testes reais antes de declarar `PASS`.

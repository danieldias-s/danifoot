# ELIFOOT 98 MULTIPLAYER — V3 ETAPA 02D: RESULTADO TÉCNICO CONSOLIDADO
## Carreira dos Treinadores, Demissões, Contratações e Trocas de Clubes

**Projeto:** Elifoot 98 Multiplayer (`D:\elifoot_98`)  
**Branch:** `main`  
**Etapa:** V3 — Etapa 02D (`Carreira dos Treinadores, Demissões, Contratações e Trocas de Clubes`)  
**Data:** 2026-11-11  
**Status Final:** **PASS**

---

## 1. Resumo executivo

A **Etapa 02D** consolidou integralmente o sistema de carreira dos treinadores humanos (`HumanCoach`) e dos treinadores controlados pela IA (`AiManager`) no **Elifoot 98 Multiplayer**, preservando a experiência clássica do **Elifoot 98** original (`ELIFOOT.EXE`) e todas as regras já aprovadas nas Etapas **01**, **02A**, **02B** e **02C**.

Principais entregas técnicas desta etapa:
1. **Resolução da Inconsistência Técnica I12 / Módulo M07 (`ELIFOOT98_V3_ETAPA02_PROPOSTA.md`):**
   - Criação de contratos tipados em `Elifoot.Contracts` (`CoachCareerSnapshotDto`, `CoachEmploymentRecordDto`, `CoachMoraleEntryDto`, `CoachMoraleRulesDto`, `CoachTitleRecordDto`) substituindo o retorno anônimo `object?` em `ClubAndMarketApplicationService.GetCareerAsync`, mantendo 100% de compatibilidade JSON camelCase com o cliente Flutter.
   - Implementação de idempotência baseada em recibo (`world.Economy.Commands[$"INV:{coachId}:{idempotencyKey}"]`) e validação de versão otimista (`expectedRoundVersion`) em `RespondJobInvitationAsync` e `AcceptJobInvitationAsync`, com suporte a replay idempotente seguro em caso de falha transitória de persistência ou queda de conexão HTTP.
2. **Blindagem de Concorrência, Transição de Temporada e Permissões:**
   - Guard de idempotência em `CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance` impedindo que chamadas repetidas na mesma temporada dupliquem promoções, rebaixamentos, demissões ou incrementos de `SeasonsManaged`.
   - Limpeza determinística de escalação e formação submetidas (`SubmittedStarterIds`, `SubmittedBenchIds`, `SubmittedFormation`, `IsReadyForRound = false`) em `CoachCareerEngine.Hire` e nos fluxos de entrada/atribuição de clube (`MultiplayerRoundOrchestrator` e `WorldLobbyOrchestrator016B`).
   - Bloqueio de bypass de propostas (`JobInvitation`) via `/join` com `preferredClubId` para treinadores que já iniciaram carreira no mundo (`EmploymentHistory.Count > 0`).
   - Resolução automática imediata de pedidos salariais pendentes (`ClassicEconomyEngine.ResolvePendingAiSalaryRequests`) quando um clube deixa de ser controlado por humano (por demissão pós-rodada ou troca de clube via convite) e passa ao comando provisório da IA, evitando bloqueio de transição de rodada/temporada (`SALARY_PENDING_TRANSITION`).
   - Proteção por lock de sala (`MultiplayerRoundOrchestrator.GetLock(worldId)`) em `SetCoachAutoModeByHostAsync`.
3. **Validação Completa e Simulações de Longo Prazo:**
   - **50 itens obrigatórios de teste** (`01..50`) e **cenários de concorrência/idempotência `A..H`** cobertos e aprovados.
   - Simulações reprodutíveis de **1, 3, 5, 10 e 20 temporadas** com **128 clubes IA** e **8 humanos + 120 clubes IA**, nos modos de Economia Clássica (`CLASSICAL_ELIFOOT98_V3_02C`) e Economia Calibrada (`MULTIPLAYER_CALIBRATED_012D`), totalizando **0 violações** de posse de clube, exclusividade de treinador, histórico de vínculos ou divisão distrital.

---

## 2. Checkpoints verificados

Todos os checkpoints históricos exigidos foram verificados e permanecem intactos no histórico Git da branch `main`:

| Etapa | Commits Obrigatórios | Status no Git |
|---|---|---|
| **Etapa 01** (Escalação de Jogadores) | `d0489d3`, `2818cb3` | **PRESERVADOS (PASS)** |
| **Etapa 02A** (Compra e Venda de Jogadores) | `2402e99`, `ced4e43` | **PRESERVADOS (PASS)** |
| **Etapa 02B** (Salários e Contratos) | `8df9b0c`, `b3fc9e3` | **PRESERVADOS (PASS)** |
| **Etapa 02C** (Economia Clássica do Elifoot 98) | `82bdadb`, `f678e85`, `2492e95` | **PRESERVADOS (PASS)** |
| **HEAD inicial da Etapa 02D** | `2492e95fbcd849e97c8a4b851da2079596e0bee2` (`2492e95`) | **VERIFICADO** |

---

## 3. Auditoria completa da carreira atual

A auditoria cobriu os **20 subsistemas de carreira** especificados na missão:

| # | Subsistema | Regra Aprovada | Implementação Atual / Arquivos | Comportamento Humano | Comportamento IA | Necessidade de Alteração |
|---|---|---|---|---|---|---|
| 1 | **Entidade treinador** | `HumanCoach` e `AiManager` com adaptador unificado `CoachCareerEngine.Coach` | `Entities.cs`, `CoachCareerEngine.cs` | Persistido em `world.HumanCoaches` | Persistido em `world.AiManagers` | Nenhuma estrutural; mantido |
| 2 | **Associação treinador/clube** | Vínculo bidirecional exclusivo (`Club.HumanCoachId` XOR `Club.AiManagerId` <-> `CurrentClubId`) | `Entities.cs`, `CoachCareerEngine.cs`, `MarketValidationEngine.cs` | no máx. 1 humano por clube e 1 clube por humano | 1 `AiManager` por clube sem humano | Limpeza de táticas pendentes em `Hire` e atribuição inicial |
| 3 | **Identidade persistente `accountId`** | `coachId` canônico da conta (`AccountRecord.CoachId`), nunca `deviceId` como identidade primária | `AccountLogin.cs`, `AccountSessionService.cs`, `Program.cs`, `MultiplayerRoundOrchestrator.cs` | Reconecta em qualquer dispositivo preservando carreira | N/A | Mantido e validado por testes de integração multi-dispositivo |
| 4 | **Treinadores humanos** | Até 8 humanos por mundo; estados ativo, ausente (`AutoMode`) e desempregado (`CurrentClubId == null`) | `Entities.cs`, `MultiplayerRoundOrchestrator.cs`, `WorldLobbyOrchestrator016B.cs` | Toma decisões táticas, financeiras e de carreira | Substitui apenas taticamente quando `AutoMode == true` | Lock de sala adicionado em `SetCoachAutoModeByHostAsync` |
| 5 | **Treinadores controlados pela IA** | 5 identidades táticas, moral `0..100`, histórico, títulos, pontos e contratação automática em vagas | `AiManagerBrain.cs`, `CoachCareerEngine.cs` | N/A | Avalia convites deterministicamente e assume vagas/interinos | Nenhuma estrutural; mantido |
| 6 | **Entrada de treinador em um mundo** | `/join` associa ou recupera `HumanCoach` existente por `coachId` sem duplicar | `MultiplayerRoundOrchestrator.cs`, `WorldLobbyOrchestrator016B.cs` | Idempotente para mesmo `coachId` | Cede vaga na 3ª/4ª divisão na entrada inicial | Bloqueado bypass de `preferredClubId` após carreira iniciada (`EmploymentHistory.Count > 0`) |
| 7 | **Escolha inicial do clube** | Entrada inicial restrita à 3ª ou 4ª Divisão (`TerceiraDivisao` / `QuartaDivisao`) ou sem clube (`joinUnemployed`) | `MultiplayerRoundOrchestrator.cs`, `WorldLobbyOrchestrator016B.cs` | Seleciona clube elegível da 3ª/4ª divisão ou sorteio determinístico | IA titular fica desempregada quando humano assume clube inicial | Garantido `PreferredStarterIds`/`PreferredBenchIds` válidos |
| 8 | **Troca de clube** | Exclusivamente via aceite de `JobInvitation` válido entre partidas (`IsSafe(world)`) | `CoachCareerEngine.cs`, `ClubAndMarketApplicationService.cs` | Encerra vínculo anterior (`CLUB_PROJECT_UPGRADE`) e abre novo vínculo | Clube anterior recebe interino (`EnsureProvisional`) e abre vaga | Adicionado recibo idempotente `INV:{coachId}:{key}` e resolução salarial IA no clube liberado |
| 9 | **Demissão** | Moral vermelha (`< 30`) antes de derrota oficial (`RED_MORALE_LOSS`), queda à Distrital para humano (`Rebaixamento para a Divisão Distrital`) ou rebaixamento com moral `< 45` para IA (`AI_RELEGATION`) | `CoachCareerEngine.cs`, `CompetitionAndSeasonEngine.cs` | Fica `CurrentClubId = null`, perde autoridade sobre o clube, preserva carreira | Fica `CurrentClubId = null`, entra em cooldown de 2 rodadas | Resolução automática de pedidos salariais órfãos após demissão pós-rodada |
| 10 | **Pedidos de emprego** | Modelo passivo clássico: não há candidatura livre (`apply`); vagas geram convites automáticos | `CoachCareerEngine.cs`, `COACH_INVITATION_AI_008.md` | Recebe convites quando moral está `VERDE` (`>= 60`) | Avaliada na mesma fila por ranking/score | Mantido sem inventar endpoint não aprovado |
| 11 | **Propostas de contratação (`JobInvitation`)** | Até 3 convites ativos por humano, expiração em 3 rodadas (`Ordinal + 3`), validação de vaga (`VacancyId`) | `CoachCareerEngine.cs`, `ClubAndMarketApplicationService.cs` | Aceita ou recusa explicitamente na aba Carreira | Decide imediatamente em `ProcessVacancies` | Tipagem forte `CoachCareerSnapshotDto` (**I12/M07**) + idempotência HTTP/App |
| 12 | **Histórico da carreira** | `EmploymentHistory`, `MoraleHistory`, `CoachTitles` e `AuditLogs` | `Entities.cs`, `CoachCareerEngine.cs`, `HistoryQueries.cs` | Preservado ao trocar de clube, ser demitido ou reconectar | Preservado ao longo de todas as temporadas | Mantido e validado |
| 13 | **Títulos** | `LeagueTitles` e `CupTitles` atribuídos ao técnico no cargo no encerramento da temporada | `CompetitionAndSeasonEngine.cs`, `Entities.cs` | Soma no ranking e histórico pessoal | Soma no ranking e histórico pessoal | Guard de idempotência na transição de temporada |
| 14 | **Classificação dos campeonatos** | Pontos (V=3, E=1), saldo de gols, gols pró, vitórias e `DeterministicIndex` | `CompetitionAndSeasonEngine.cs` | Afeta moral por objetivo de divisão e posição na tabela | Afeta moral e elegibilidade de convites | Mantido |
| 15 | **Promoção e rebaixamento** | 4 promovidos e 4 rebaixados por divisão ao fim da temporada (rodada 22) | `CompetitionAndSeasonEngine.cs` | Rebaixamento à Distrital demite humano; promoção dá bônus de moral (`+8`) | Rebaixamento pode demitir IA com moral `< 45` | Idempotência garantida em `FinalizeSeasonAndAdvance` |
| 16 | **Temporadas** | 22 rodadas (18 Liga + 4 Taça intercaladas) + transição determinística para `SeasonNumber + 1` | `CompetitionAndSeasonEngine.cs`, `MultiplayerRoundOrchestrator.cs` | `SeasonsManaged++` para quem termina temporada empregado | `SeasonsManaged++` para IA empregada | Guard de idempotência por `SeasonHistories` |
| 17 | **Reputação / Moral / Pontos** | Moral `0..100` (`VERDE >= 60`, `AMARELA 30..59`, `VERMELHA < 30`) + `CoachPoints` (`V*1 + E*0.5 + Div1*20 + Div2..4*10 + Cup*12.5`) | `CoachCareerEngine.cs`, `CompetitionAndSeasonEngine.cs` | Governa demissão, convites e posição no Ranking de Treinadores | Mesma fórmula exata para IA | Mantido sem criar fórmula paralela |
| 18 | **Treinadores sem clube** | `CurrentClubId == null`, confirma presença via `SubmitUnemployedReadyAsync`, recupera `+6` moral/rodada até `60` | `CoachCareerEngine.cs`, `MultiplayerRoundOrchestrator.cs` | Continua no mundo acompanhando rodadas e propostas | Recupera moral e volta a receber convites após cooldown de 2 rodadas | Mantido e validado |
| 19 | **Clubes sem treinador** | Nenhum clube das 4 divisões ativas fica sem comando: `EnsureProvisional` nomeia `AiManager` interino | `CoachCareerEngine.cs` | Quando humano sai, clube recebe IA interina imediatamente | Interino disputa partidas até contratação efetiva | Mantido e validado |
| 20 | **Reconexão e recuperação de sessão** | Sessão recuperada via `/api/auth/session` + `/api/worlds/{worldId}/join` + snapshot | `AccountLogin.cs`, `main.dart`, `api_client.dart` | Restaura clube atual (ou estado sem clube), histórico e convites | N/A | `_postFinance` integrado a `acceptInvitation`/`rejectInvitation` |

---

## 4. Identidade persistente `accountId`

A regra de identidade aprovada no projeto foi integralmente preservada:
- A identidade principal do treinador humano é o `CoachId` persistente vinculado à conta (`AccountRecord.CoachId` em `AccountLogin.cs` e `AccountSessionService.cs`), resolvido no servidor por `ResolveCoachIdentity` em `Program.cs`.
- O `deviceId` é apenas metadado informativo de sessão/dispositivo (`existingCoach.DeviceId = deviceId`), nunca chave primária de criação de treinador.
- Reconectar na mesma conta a partir de outro dispositivo (`dev-mobile-A` -> `dev-desktop-B`), após troca de clube ou após demissão:
  - **Não cria** novo `HumanCoach` no mundo (`world.HumanCoaches.Count` permanece inalterado).
  - **Não duplica** registros no ranking de treinadores.
  - **Preserva** `CurrentClubId`, `EmploymentHistory`, `LeagueTitles`, `CupTitles`, `CoachPoints`, `Dismissals` e `Career.Morale`.

---

## 5. Entidade treinador e vínculo com clube

O modelo de domínio mantém invariantes estritas entre `Club`, `HumanCoach` e `AiManager`:
1. **Exclusividade de Comando no Clube:** Para todo clube `c` das divisões ativas (`1ª` à `4ª`), vale `c.HumanCoachId != null` **XOR** `c.AiManagerId != null`. Nunca ambos simultaneamente e nunca ambos nulos após `CoachCareerEngine.EnsureMigrated` / `ProcessVacancies`.
2. **Exclusividade de Clube no Treinador:** Cada `HumanCoach` ou `AiManager` possui no máximo um `CurrentClubId`, e `world.Clubs.Count(c => c.HumanCoachId == h.Id) <= 1`.
3. **Integridade de `EmploymentHistory`:** Todo treinador possui no máximo **1** registro aberto (`EndSeason == null`) em `EmploymentHistory`, cujo `ClubId` coincide exatamente com `CurrentClubId`. Quando `CurrentClubId == null`, todos os registros em `EmploymentHistory` possuem `EndSeason != null`, `EndRound != null` e `ReasonForLeaving` preenchido.

---

## 6. Regras do treinador humano

- **Entrada:** Conta autenticada entra no mundo e assume clube elegível (3ª ou 4ª divisão na entrada inicial) ou entra sem clube (`joinUnemployed: true`).
- **Durante a carreira:** Administra escalação (`SelectLineupAsync` / `SubmitTacticsAsync`), mercado (`BuyListedPlayerAsync`, `ListPlayerForTransferAsync`, `CreateTransferOfferAsync`, leilões), contratos (`RenewPlayerContractAsync`, `RespondSalaryAsync`), banco e estádio apenas sobre seu `CurrentClubId`.
- **Modo Automático (`AutoMode`):** Quando ativado pelo host para um treinador ausente, a IA gera exclusivamente a escalação da rodada (`EnsureAutoLineupForCoach`) para não travar a sala. A IA **nunca** executa compras, vendas, leilões, empréstimos, obras de estádio ou decisões de carreira em nome do humano (`MarketValidationEngine.CanActAsAi` exige `club.HumanCoachId == null`).
- **Troca de clube e Demissão:** Atualizam atomicamente o vínculo, revogam permissões sobre o clube anterior, limpam escalações/formações pendentes e preservam toda a carreira do treinador.

---

## 7. Regras do treinador da IA

- Clubes sem treinador humano são comandados por `AiManager` com personalidade tática determinística (`Ofensivo`, `Equilibrado`, `Defensivo`, `ContraAtaque`, `PosseDeBola`), `Ambition` e `Pragmatism`.
- A IA participa do mesmo sistema de moral (`0..100`), pontos de carreira (`CoachPoints`), títulos (`LeagueTitles`, `CupTitles`), demissões (`RED_MORALE_LOSS` e `AI_RELEGATION`) e convites (`JobInvitation`) que os treinadores humanos.
- Quando um clube fica vago (por demissão ou saída do técnico para outro clube), `CoachCareerEngine.EnsureProvisional` aloca imediatamente um `AiManager` interino (`club.HasProvisionalManager = true`) para garantir que nenhuma partida ocorra sem técnico, enquanto `ProcessVacancies` busca um treinador definitivo.

---

## 8. Entrada no mundo e escolha inicial

- Na primeira entrada no mundo (`EmploymentHistory.Count == 0` e `CurrentClubId == null`), o treinador humano recebe o clube solicitado (desde que pertença à `TerceiraDivisao` ou `QuartaDivisao` e não pertença a outro humano) ou um clube elegível sorteado deterministicamente, ou permanece sem clube se `joinUnemployed == true`.
- **Correção aplicada na Etapa 02D:** Caso um treinador já tenha iniciado carreira no mundo (`EmploymentHistory.Count > 0`) e esteja desempregado (por exemplo, após demissão), chamadas a `JoinWorldAndAssignClubAsync` com `preferredClubId` **não** atribuem o clube diretamente; o treinador reconecta em seu estado real (`IsUnemployed = true`) e só pode assumir novo clube por meio de `JobInvitation` válido.

---

## 9. Troca de clube

A troca de clube ocorre exclusivamente por `AcceptJobInvitationAsync` / `RespondJobInvitationAsync(..., accept: true)` quando `CoachCareerEngine.IsSafe(world)` (`PREPARING`, `RESULTS` ou `FINISHED`):
1. Valida-se que o convite existe, pertence ao `coachId`, não expirou (`ExpiresAtOrdinal > Ordinal(world)`), não foi aceito/recusado anteriormente, a vaga (`club.CareerVacancyId == invitation.VacancyId`) segue aberta, o clube de destino não possui humano e `EvaluateOpportunity(world, coach, targetClub).Eligible` é verdadeiro (`Morale >= 60` VERDE, cooldown de 3 rodadas desde a última troca respeitado e melhoria de projeto válida).
2. No clube anterior (`previous`):
   - Fecha o registro aberto em `EmploymentHistory` com `EndSeason`, `EndRound` e `ReasonForLeaving = "CLUB_PROJECT_UPGRADE"`.
   - Limpa `previous.HumanCoachId = null`.
   - Abre vaga (`OpenVacancy(world, previous)`) e nomeia interino (`EnsureProvisional(world, previous)`).
   - Resolve imediatamente eventuais pedidos salariais pendentes deixados no clube anterior (`ClassicEconomyEngine.ResolvePendingAiSalaryRequests(world)`).
3. No novo clube (`target`):
   - Libera o `AiManager` anterior/interino (`NEW_COACH_APPOINTED`).
   - Define `target.HumanCoachId = coach.Id`, `target.AiManagerId = null`, `coach.CurrentClubId = target.Id`.
   - Abre novo `CoachEmploymentRecord` com `StartSeason` e `StartRound`.
   - Limpa `IsReadyForRound = false`, `SubmittedStarterIds`, `SubmittedBenchIds` e `SubmittedFormation`, garantindo `PreferredStarterIds` (11 titulares com 1 goleiro) e `PreferredBenchIds` válidos no novo clube.
   - Expira todos os outros convites pendentes da mesma vaga (`VacancyId`) e do mesmo treinador (`CoachId`).

---

## 10. Demissão

Os gatilhos de demissão aprovados e preservados são:
1. **Derrota com Moral Vermelha (`RED_MORALE_LOSS`):** Em `CoachCareerEngine.ApplyRound`, se o treinador entra na partida com moral na faixa `VERMELHA` (`before < 30`) e sofre derrota (`result < 0`), é demitido após a atualização da moral da partida.
2. **Rebaixamento à Divisão Distrital (Humano):** Em `CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance`, se o clube controlado por humano é rebaixado para a `DivisionTier.Distrital`, o treinador humano é demitido (`"Rebaixamento para a Divisão Distrital"`), pois a Divisão Distrital é exclusiva da IA.
3. **Rebaixamento com Moral Baixa (IA — `AI_RELEGATION`):** Em `CoachCareerEngine.EndSeason`, treinadores da IA rebaixados com `Morale < 45` são demitidos.

Consequências imediatas da demissão:
- `Dismissals++`, fechamento do `CoachEmploymentRecord` aberto com o motivo da saída, `CurrentClubId = null`, `UnemployedSinceOrdinal = Ordinal(world)`.
- Limpeza de `IsReadyForRound`, `SubmittedStarterIds`, `SubmittedBenchIds` e `SubmittedFormation`.
- O clube demissor abre vaga (`OpenVacancy`) e recebe `AiManager` interino (`EnsureProvisional`), resolvendo pedidos salariais pendentes se houver.
- **Nenhum comando sobre o clube anterior** (escalação, venda, compra, renovação, banco, estádio) é permitido ao treinador demitido.

---

## 11. Treinador sem clube

- O treinador humano sem clube (`CurrentClubId == null`, `IsUnemployed == true`) permanece ativo no mundo, visualiza classificação, resultados, ranking e aba Carreira, e confirma prontidão na rodada por `SubmitUnemployedReadyAsync` (`POST /api/worlds/{worldId}/unemployed-ready`).
- A cada rodada avançada após a demissão (`ordinal > UnemployedSinceOrdinal`), `CoachCareerEngine.RecoverUnemployed` recupera `+6` pontos de moral (`UnemployedRecoveryPerRound = 6`) até o piso da faixa `VERDE` (`GreenMinimum = 60`), registrando `UNEMPLOYED_RECOVERY` em `MoraleHistory`.
- Respeitado o cooldown mínimo de 2 rodadas após demissão (`Ordinal(w) - UnemployedSinceOrdinal >= 2`) e atingida a faixa `VERDE` (`>= 60`), o treinador volta a receber convites de clubes compatíveis com sua reputação/experiência (`established` para 1ª/2ª divisão; qualquer técnico em moral verde para 3ª/4ª divisão).

---

## 12. Clubes sem treinador humano

- Todo clube sem treinador humano nas divisões ativas (`1ª` à `4ª`) é administrado integralmente pela IA (`AiManager`), que escala a equipe, resolve pedidos salariais, gerencia caixa/empréstimos e atua no mercado e leilões segundo as regras das Etapas 01, 02A, 02B e 02C.
- Quando um humano assume o clube, a IA deixa o comando imediatamente (`club.AiManagerId = null`, `club.HumanCoachId = human.Id`). Quando o humano sai (troca ou demissão), a IA reassume imediatamente (`EnsureProvisional`).

---

## 13. Propostas de emprego

- As propostas (`JobInvitation`) são geradas deterministicamente em `CoachCareerEngine.ProcessVacancies` para clubes com `CareerVacancyId != null` e `HumanCoachId == null`.
- Ordenação de candidatos: combina posição no Ranking Clássico de Treinadores, moral atual, títulos de liga, pontos de carreira e proximidade de divisão (`OrderByDescending(x => x.Score).ThenBy(x => x.Coach.Id)`).
- Cada humano pode ter até **3 convites pendentes simultâneos**, com validade de **3 rodadas** (`ExpiresAtOrdinal = Ordinal(w) + 3`).
- Ao recusar (`RespondJobInvitationAsync(..., accept: false)`), o convite é marcado como `IsRejected = true`, o treinador mantém seu estado atual (empregado ou sem clube) e o clube busca imediatamente o próximo candidato elegível na fila.

---

## 14. Pedidos de emprego

Conforme auditado em `docs/COACH_INVITATION_AI_008.md` e `ELIFOOT98_V3_ETAPA02_PROPOSTA.md`, o projeto adota o modelo clássico de convites passivos (`JobInvitation`) gerados pelos clubes vagos com base no ranking e moral do treinador. Não há endpoint de candidatura manual avulsa (`apply`), preservando-se a regra aprovada sem inventar mecânicas fora do escopo.

---

## 15. Concorrência e idempotência

Foram implementados e validados os cenários **A a H** da seção 9 da missão:
- **Cenário A (Dois treinadores tentam assumir o mesmo clube):** Serializado pelo lock de sala (`MultiplayerRoundOrchestrator.GetLock(worldId)`) e validado por `club.CareerVacancyId == invitation.VacancyId && !club.HumanCoachId.HasValue`. Apenas o primeiro vence; o segundo recebe erro controlado (`"Clube indisponível."` ou `"Vaga encerrada."`).
- **Cenário B (Mesmo treinador envia duas vezes o mesmo aceite):**
  - Com a mesma `idempotencyKey`: retorna o recibo `INV:{coachId}:{idempotencyKey}` com `(true, null)` sem duplicar vínculo nem incrementar `RoundVersion`.
  - Sem `idempotencyKey` (clique duplo não chaveado): o segundo clique encontra `invitation.IsAccepted == true` e retorna `(false, "Convite encerrado ou inválido.")`.
- **Cenário C (Aceita proposta após já ter trocado de clube):** Os demais convites do treinador são expirados no primeiro aceite (`other.IsExpired = true`), bloqueando o segundo aceite.
- **Cenário D (Demissão concorrente com ação do treinador):** Como a demissão ocorre sob o lock da sala na finalização da rodada e remove `club.HumanCoachId`, qualquer comando subsequente do treinador sobre o ex-clube é negado por validação de autoridade.
- **Cenário E (Tentativa de escalar time após perder vínculo):** `SelectLineupAsync` e `SubmitTacticsAsync` verificam `coach.CurrentClubId` e `club.HumanCoachId == coach.Id`, recusando imediatamente.
- **Cenário F (Tentativa de comprar/vender jogador após perder vínculo):** `MarketValidationEngine.ValidateCoachAndClub` e `FinancialCommandsApplicationService.Finance` rejeitam qualquer operação financeira ou de mercado sem clube ativo ou para clube alheio.
- **Cenário G (Tentativa de renovar contrato após trocar de clube):** Negada para jogadores do clube antigo (`p.ClubId != c.Id`), permitida apenas para jogadores do novo clube.
- **Cenário H (Reenvio de requisição após falha de rede):** O middleware de erro de persistência em `Program.cs` agora cobre `/invitations/` retornando HTTP `500` em caso de `IOException`/`DbUpdateException`, e `ElifootApiClient._postFinance` preserva a `idempotencyKey` em respostas `>= 500`. Na repetição, `RespondJobInvitationAsync` reexecuta `SaveWorldAsync` quando o recibo já está na memória ou retorna o recibo persistido sem duplicar histórico.

---

## 16. Separação entre clube e treinador

Testada e comprovada a separação absoluta entre patrimônio do clube e carreira do treinador:
- **Pertence ao Clube (`Club`):** Elenco (`Player.ClubId`), caixa (`CashBalance`), livro-razão (`FinancialTransactions`), dívidas bancárias (`BankLoans`), estádio (`Stadium`), contratos de jogadores (`ContractRoundsRemaining`, `RenewedForSeason`, `NegotiatedContractProtected`), propostas de transferência (`TransferOffers`), pedidos salariais (`SalaryRequests`) e posição/estatísticas na divisão (`LeaguePoints`, `DivisionTier`, etc.).
- **Pertence ao Treinador (`HumanCoach` / `AiManager`):** Identidade (`Id` / `accountId`), nome (`Nickname` / `Name`), moral (`Career.Morale`, `MoraleHistory`), histórico de clubes (`EmploymentHistory`), títulos (`LeagueTitles`, `CupTitles`, `CoachTitles`), estatísticas acumuladas (`TotalWins`, `TotalDraws`, `TotalLosses`, `SeasonsManaged`, `Dismissals`) e pontuação no ranking (`CoachPoints`).

---

## 17. Histórico da carreira

- Cada passagem por um clube gera exatamente um `CoachEmploymentRecord` (`ClubId`, `ClubName`, `StartSeason`, `StartRound`, `EndSeason`, `EndRound`, `ReasonForLeaving`).
- O histórico é consultável via `GET /api/worlds/{worldId}/invitations/{coachId}` (`CoachCareerSnapshotDto.EmploymentHistory`) e `GET /api/worlds/{worldId}/coaches/{coachId}/history` (`HistoryQueries.GetCoachHistory`), mantendo a ordem cronológica exata e sem duplicar registros em replays idempotentes.

---

## 18. Títulos e conquistas

- No encerramento da temporada (`CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance`), os campeões das 4 divisões (`PrimeiraDivisao` a `QuartaDivisao`) recebem `LeagueTitles++` e registro `CoachTitleRecord(..., "LEAGUE")`, e o campeão da Taça recebe `CupTitles++` e registro `CoachTitleRecord(..., "CUP")`.
- O título é creditado ao treinador (humano ou IA) que está no comando do clube campeão no fechamento da temporada, refletindo imediatamente em `ComputePoints`:
  $$\text{CoachPoints} = \text{TotalWins} \times 1.0 + \text{TotalDraws} \times 0.5 + \text{Title}_{1\text{ªDiv}} \times 20.0 + \text{Title}_{2\text{ª..4ªDiv}} \times 10.0 + \text{CupTitles} \times 12.5$$
- A ordenação do Ranking Clássico de Treinadores (`GetClassicCoachRanking`) respeita estritamente a hierarquia clássica:
  1. `LeagueTitles` (decrescente)
  2. `CupTitles` (decrescente)
  3. `CoachPoints` (decrescente)
  4. `TotalWins` (decrescente)

---

## 19. Reputação

- Conforme diretriz da missão ("Se reputação ainda não existir, documentar claramente e não inventar fórmula complexa sem evidência"), o sistema de avaliação do treinador utiliza as métricas já aprovadas no domínio (`CoachCareerState.Morale` em `[0, 100]` com faixas `VERDE >= 60`, `AMARELA 30..59`, `VERMELHA < 30`, combinada com `CoachPoints`, `LeagueTitles` e `TotalWins`), enquanto cada `Club` possui seu atributo próprio `Club.Reputation` (`1..100`) usado no cálculo de `ProjectScore`. Nenhuma fórmula paralela foi inventada.

---

## 20. Transição de temporada

- Na transição de temporada (`CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance`), foi adicionado o guard idempotente:
  ```csharp
  if (world.SeasonHistories.FirstOrDefault(h => h.SeasonNumber == world.SeasonNumber) is SeasonHistoryEntry existingSeasonHistory)
      return existingSeasonHistory;
  ```
- Isso garante que:
  - Promoções e rebaixamentos não sejam aplicados duas vezes.
  - Treinadores humanos e da IA preservem seus vínculos ao avançar para a nova temporada (exceto nos casos legítimos de demissão por rebaixamento à Distrital ou `AI_RELEGATION`).
  - `SeasonsManaged` seja incrementado exatamente uma vez por temporada concluída.

---

## 21. Compatibilidade com saves existentes

- `CoachCareerEngine.EnsureMigrated` preserva integralmente saves de versões anteriores (`CareerRulesVersion < 8`):
  - Clampa `Morale` em `[0, 100]`.
  - Preenche o `CoachEmploymentRecord` inicial caso o treinador tenha `CurrentClubId != null` e `EmploymentHistory` vazio.
  - Corrige eventual duplicidade legada `HumanCoachId != null && AiManagerId != null`.
  - Migra convites legados sem `VacancyId` abrindo a vaga correspondente.
  - Mantém compatibilidade total com `SQLiteWorldRepository` e `PostgresWorldRepository`.

---

## 22. Integração com Etapas 01, 02A, 02B e 02C

Validada por regressão automatizada (`T33_To_T50_SeasonTransitionSavesAndStages01To02CRegression` e suítes completas):
- **Etapa 01 (Escalação):** `SelectLineupAsync` valida 11 titulares, exatamente 1 goleiro, atletas sem suspensão/lesão/leilão e limpa escalações pendentes ao trocar de clube.
- **Etapa 02A (Mercado e Leilões):** Compra direta (`BuyListedPlayerAsync`), ofertas e leilões voluntários (`CreateVoluntaryAuctionAsync`, `PlaceAuctionBidAsync`) respeitam o vínculo atual do treinador e a reserva mínima de elenco por posição.
- **Etapa 02B (Salários e Contratos):** Renovação voluntária (`RenewPlayerContractAsync`), bloqueio de transferência na mesma temporada (`RenewedForSeason`) e resolução automática de pedidos salariais pendentes quando um clube passa ao controle da IA após demissão ou troca de clube.
- **Etapa 02C (Economia Clássica):** Fórmulas clássicas de salário-base (`CalculateClassicalBaseSalary`), valor de mercado (`CalculateClassicalMarketValue`), ágio por nacionalidade, interesse da IA e evolução sazonal da inflação operam sem conflito com trocas de clube e demissões ao longo de 20 temporadas.

---

## 23. Ajustes de interface Flutter

- `client/elifoot_flutter/lib/api_client.dart`:
  - `getInvitations` agora sincroniza `_financeVersions[worldId]` a partir de `roundVersion` do `CoachCareerSnapshotDto`.
  - `acceptInvitation` e `rejectInvitation` passaram a utilizar `_postFinance(uri)`, anexando automaticamente `idempotencyKey` e `expectedRoundVersion` nas chamadas HTTP e preservando a mesma `idempotencyKey` caso ocorra erro `>= 500` para retry idempotente seguro, sem alterar a assinatura pública dos métodos.
- `client/elifoot_flutter/lib/coach_finance_screens.dart` e `lib/main.dart`:
  - A aba **Carreira / Ranking** (`ClassicCoachScreen`), o indicador de moral (`MoraleBar`), a listagem de propostas (`invitations`), o histórico de vínculos (`employmentHistory`) e o botão `AGUARDAR PROPOSTAS (PRONTO)` para treinador sem clube foram auditados e validados em layouts mobile e desktop sem overflow.

---

## 24. Arquivos alterados

### Código de Produção (`.NET` e `Flutter`)
1. `src/Elifoot.Contracts/Dtos.cs` — Adicionados DTOs tipados `CoachCareerSnapshotDto`, `CoachEmploymentRecordDto`, `CoachMoraleEntryDto`, `CoachMoraleRulesDto` e `CoachTitleRecordDto` (**I12 / M07**).
2. `src/Elifoot.Domain/CoachCareerEngine.cs` — Limpeza de `SubmittedStarterIds`, `SubmittedBenchIds` e `SubmittedFormation` em `Hire` mesmo quando o treinador estava desempregado.
3. `src/Elifoot.Domain/CompetitionAndSeasonEngine.cs` — Guard de idempotência em `FinalizeSeasonAndAdvance` por `SeasonHistories`.
4. `src/Elifoot.Application/ClubAndMarketApplicationService.cs` — Retorno tipado `Task<CoachCareerSnapshotDto?>` em `GetCareerAsync`; recibos de idempotência `INV:{coachId}:{idempotencyKey}` e validação de `expectedRoundVersion` em `RespondJobInvitationAsync`/`AcceptJobInvitationAsync`; resolução salarial da IA (`ResolvePendingAiSalaryRequests`) após troca de clube; lock de sala em `SetCoachAutoModeByHostAsync`.
5. `src/Elifoot.Application/MultiplayerRoundOrchestrator.cs` — Bloqueio de bypass de `preferredClubId` via `/join` para treinador com carreira já iniciada; limpeza de escalação submetida ao atribuir clube inicial; chamada a `ClassicEconomyEngine.ResolvePendingAiSalaryRequests(world)` antes do início da rodada e após `ApplyFinishedRoundResultsToStandingsAndCareers`.
6. `src/Elifoot.Application/WorldLobbyOrchestrator016B.cs` — Bloqueio de bypass de `preferredClubId` para treinador com carreira em andamento e limpeza de escalação submetida na atribuição inicial.
7. `src/Elifoot.Api/Program.cs` — Inclusão de `/invitations/` no middleware de falha de persistência (`500` em `IOException`/`DbUpdateException`) e encaminhamento de `idempotencyKey` e `expectedRoundVersion` nas rotas `POST /api/worlds/{worldId}/invitations/{invitationId}/accept` e `/reject`.
8. `client/elifoot_flutter/lib/api_client.dart` — Integração de `getInvitations`, `acceptInvitation` e `rejectInvitation` com controle de versão (`_financeVersions`) e envio idempotente (`_postFinance`).

### Testes Criados e Artefatos
9. `tests/Elifoot.Domain.Tests/ManagerCareerV3Stage02DTests.cs`
10. `tests/Elifoot.Application.Tests/ManagerCareerV3Stage02DApplicationTests.cs`
11. `tests/Elifoot.Integration.Tests/ManagerCareerV3Stage02DIntegrationTests.cs`
12. `tests/Elifoot.Simulation.Tests/ManagerCareerV3Stage02DSimulationTests.cs`
13. `client/elifoot_flutter/test/manager_career_v3_stage02d_test.dart`
14. `artifacts/v3-stage02d/manager-career-*.json` (relatórios de simulação de 1, 3, 5, 10 e 20 temporadas)

---

## 25. Testes criados e executados

Todos os **50 itens de teste obrigatórios** da Seção 16 foram implementados e executados com aprovação integral:

| Grupo | Itens da Missão (`01..50`) | Suíte / Método de Teste | Resultado |
|---|---|---|---|
| **Identidade** | `01..05` (Persistência `accountId`, reconexão, troca de dispositivo, não duplicação, preservação em troca/demissão) | `ManagerCareerV3Stage02DApplicationTests.T01_T02_T03_T04_T05_AccountIdIdentity_PreservedAcrossReconnectDeviceSwitchClubSwitchAndDismissal` + `ManagerCareerV3Stage02DIntegrationTests.AccountIdIdentity_PreservedAcrossDevicesClubSwitchAndDismissal_AndPreventsDuplicateCoachInSameWorld` | **PASS** |
| **Vínculo Clube/Treinador** | `06..11` (Associação inicial, exclusividade humano/clube, bloqueio de 2 clubes, permissões sobre próprio clube e bloqueio sobre clube alheio) | `ManagerCareerV3Stage02DApplicationTests.T06_T07_T08_T09_T10_T11_ClubCoachBinding_ExclusivityAndPermissions` | **PASS** |
| **Troca de Clube e Separação** | `12..18` (Troca válida, revogação de permissões antigas, novas permissões, elenco/caixa/contratos permanecem no clube antigo, histórico atualizado) | `ManagerCareerV3Stage02DApplicationTests.T12_To_T18_ClubSwitch_UpdatesOwnershipRevokesOldPermissionsAndLeavesSquadCashContractsWithOldClub` + `ManagerCareerV3Stage02DTests.Hire_ClearsPreviouslySubmittedLineupAndFormation_EvenWhenCoachWasUnemployed` | **PASS** |
| **Demissão e Sem Clube** | `19..23` (Demissão por gatilho válido, estado sem clube, bloqueio de ações sobre ex-clube, IA assume clube, histórico preservado) | `ManagerCareerV3Stage02DApplicationTests.T19_To_T23_Dismissal_ValidTriggerUnemployedStateBlockedClubCommandsAiTakeoverAndHistoryPreserved` | **PASS** |
| **Propostas e Concorrência** | `24..28` + Cenários `A..H` (Elegibilidade, aceite, recusa, expiração, concorrência entre 2 treinadores, replay idempotente com e sem chave) | `ManagerCareerV3Stage02DApplicationTests.T24_To_T28_Proposals_AcceptRejectExpiredConcurrencyAndIdempotentReplay` + `ManagerCareerV3Stage02DIntegrationTests.HttpInvitationIdempotencyAndSqliteRestart_PreservesClubSwitchAndEmploymentHistory` | **PASS** |
| **IA e Modo Automático** | `29..32` (IA administra clube sem humano, IA contrata treinador, IA demite treinador, transição IA <-> humano e isolamento financeiro de `AutoMode`) | `ManagerCareerV3Stage02DApplicationTests.T29_To_T32_AiCareer_ManagesClubHiresDismissesAndHandsOffToHuman` + `T32B_AutoMode_GeneratesLineupOnly_AndNeverExecutesFinancialOrCareerMutationsForHuman` | **PASS** |
| **Histórico e Títulos** | `33..37` (Registro de temporadas, títulos de liga e taça, promoções, rebaixamentos, pontuação e ordenação do ranking) | `ManagerCareerV3Stage02DApplicationTests.T33_To_T37_HistoryTitlesPromotionsRelegationsAndRankingOrder` | **PASS** |
| **Transição de Temporada** | `38..41` (Continuidade do vínculo, atualização de divisões, ausência de duplicação, idempotência) | `ManagerCareerV3Stage02DTests.FinalizeSeasonAndAdvance_IsIdempotent_WhenCalledRepeatedlyForSameSeason` + `T33_To_T50` | **PASS** |
| **Saves e Regressão 01..02C** | `42..50` (Save/load de mundo com carreiras, compatibilidade legada, escalação 01, força, salários 02C, compra 02A, leilões 02A/02B, renovação 02B, simulação multi-temporadas) | `ManagerCareerV3Stage02DApplicationTests.T33_To_T50_SeasonTransitionSavesAndStages01To02CRegression` + `ManagerCareerV3Stage02DSimulationTests` + `manager_career_v3_stage02d_test.dart` | **PASS** |

### Resumo Global das Suítes Executadas

| Suíte | Total de Testes | Aprovados | Falhas | Status |
|---|---:|---:|---:|---|
| `Elifoot.Domain.Tests` | 627 | 627 | 0 | **PASS** |
| `Elifoot.Application.Tests` | 63 | 63 | 0 | **PASS** |
| `Elifoot.Integration.Tests` (SQLite + PostgreSQL 18 real) | 298 | 298 | 0 | **PASS** |
| `Elifoot.Simulation.Tests` (Etapa 02D: 7 cenários + Etapa 008: 2 cenários) | 9 | 9 | 0 | **PASS** |
| **Total .NET Executado** | **997** (1.030 incluindo todas as simulações) | **100%** | **0** | **PASS** |
| `flutter test test/manager_career_v3_stage02d_test.dart` | 3 | 3 | 0 | **PASS** |
| `flutter test` (Suíte completa Flutter) | 508 | 508 | 0 | **PASS** |
| `flutter analyze` | — | 0 avisos/erros | 0 | **PASS** |
| `dotnet build Elifoot.slnx -c Release --no-restore` | — | 0 avisos/erros | 0 | **PASS** |

---

## 26. Resultados das simulações de 1, 3, 5, 10 e 20 temporadas

As simulações multi-temporadas (`Stage02D_ManagerCareerMultiSeasonSimulations_ZeroCareerOrOwnershipViolations`) foram executadas com `seed = 199801` e verificação determinística de replay:

| Cenário | Economia | Temp. | Partidas | Minutos | Convites Humanos Aceitos / Recusados | Convites IA Aceitos / Recusados | Demissões (Moral Vermelha / Distrital Hum. / Reb. IA / Total) | Recuperações Sem Clube | Títulos (Liga / Taça) | Violações (Posse / Exclusividade / Histórico / Distrital) | Assinatura Determinística (SHA-256 Prefixo) |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| **8 Humanos + 120 IA** | Clássica 02C | **1** | 351 | 32.640 | 1 / 0 | 19 / 683 | 5 / 0 / 6 / **11** | 23 | 4 / 1 | **0 / 0 / 0 / 0** | `E8C4FC2544FE7D5C...` |
| **8 Humanos + 120 IA** | Clássica 02C | **3** | 1.053 | 97.650 | 6 / 0 | 65 / 1.986 | 17 / 0 / 20 / **37** | 169 | 12 / 3 | **0 / 0 / 0 / 0** | `8FD327DCDB92AB75...` |
| **8 Humanos + 120 IA** | Clássica 02C | **5** | 1.755 | 162.510 | 10 / 0 | 110 / 2.717 | 36 / 0 / 36 / **72** | 354 | 20 / 5 | **0 / 0 / 0 / 0** | `D5353EF76A65E75F...` |
| **8 Humanos + 120 IA** | Clássica 02C | **10** | 3.510 | 325.050 | 16 / 1 | 327 / 6.717 | 121 / 0 / 76 / **197** | 1.196 | 40 / 10 | **0 / 0 / 0 / 0** | `41C814A974ECDDB0...` |
| **8 Humanos + 120 IA** | Clássica 02C | **20** | 7.020 | 648.870 | 38 / 7 | 765 / 12.821 | 333 / 0 / 148 / **481** | 3.247 | 80 / 20 | **0 / 0 / 0 / 0** | `F7CE6273F7F9D9B2...` |
| **128 Clubes IA** | Clássica 02C | **20** | 7.020 | 649.440 | 0 / 0 | 920 / 16.950 | 332 / 0 / 160 / **492** | 3.191 | 80 / 20 | **0 / 0 / 0 / 0** | `3696112B22B62B36...` |
| **8 Humanos + 120 IA** | Calibrada 012D | **20** | 7.020 | 648.990 | 51 / 19 | 704 / 12.566 | 283 / 5 / 141 / **429** | 2.687 | 80 / 20 | **0 / 0 / 0 / 0** | `CACBB2D919676698...` |

Além disso, a simulação legada de **50 temporadas** (`CoachCareer008SimulationTests`) manteve exatamente a mesma assinatura determinística histórica (`474E4A3579EFDC4D0441464EA37ABE4C07173172636110AB8DE6160A58...`).

---

## 27. Problemas encontrados e correções

1. **Inconsistência I12 / Módulo M07 — Retorno não tipado em `GetCareerAsync` e ausência de recibo idempotente em respostas a convites:**
   - **Problema:** `GetCareerAsync` retornava `Task<object?>` anônimo e `RespondJobInvitationAsync` não registrava recibo idempotente para retries com `idempotencyKey`.
   - **Correção:** Criado `CoachCareerSnapshotDto` (e sub-DTOs) em `Elifoot.Contracts/Dtos.cs` e implementada persistência de recibo `INV:{coachId}:{idempotencyKey}` em `world.Economy.Commands`, com suporte a `expectedRoundVersion` e recuperação de falha transitória de persistência.
2. **Pedidos salariais pendentes órfãos quando humano deixa um clube (demissão pós-rodada ou troca de clube):**
   - **Problema:** Na finalização da rodada, `SettleMatchAndRoundFinancials` roda antes de `ApplyFinishedRoundResultsToStandingsAndCareers`. Se um clube com técnico humano recebesse um pedido salarial espontâneo na rodada e, segundos depois, esse técnico humano fosse demitido por derrota com moral vermelha (ou trocasse de clube via convite), o clube passava ao comando provisório da IA com um `SalaryRaiseRequest` `PENDING`, o que bloquearia `SalarySeasonTransitionEngine.RequireResolved` na rodada seguinte.
   - **Correção:** Adicionada chamada a `ClassicEconomyEngine.ResolvePendingAiSalaryRequests(world)` após `ApplyFinishedRoundResultsToStandingsAndCareers`, em `CheckAndStartRoundIfAllReadyInternal` e em `RespondJobInvitationAsync` após aceite de convite.
3. **Escalação submetida antiga após contratação de treinador desempregado:**
   - **Problema:** `CoachCareerEngine.Hire` limpava `SubmittedStarterIds`, `SubmittedBenchIds` e `SubmittedFormation` dentro de `Release` (quando havia clube anterior), mas apenas resetava `IsReadyForRound = false` quando o técnico contratado vinha do estado desempregado.
   - **Correção:** `CoachCareerEngine.Hire` agora limpa explicitamente `SubmittedStarterIds`, `SubmittedBenchIds` e `SubmittedFormation` para todo treinador humano contratado, e `AcceptJobInvitationAsync` garante `PreferredStarterIds` e `PreferredBenchIds` válidos no novo clube.
4. **Bypass de convite via `/join` com `preferredClubId` após demissão:**
   - **Problema:** Um treinador humano já existente no mundo (`EmploymentHistory.Count > 0`) mas atualmente desempregado (`CurrentClubId == null`) poderia acionar `/join` passando `preferredClubId` e cair na lógica de atribuição inicial de clube.
   - **Correção:** Em `MultiplayerRoundOrchestrator.JoinWorldAndAssignClubAsync` e `WorldLobbyOrchestrator016B.JoinAsync`, a atribuição inicial de clube é restrita a treinadores recém-ingressados que ainda não iniciaram carreira no mundo (`existingCoach == null || existingCoach.EmploymentHistory.Count == 0`).
5. **Idempotência de `FinalizeSeasonAndAdvance`:**
   - **Problema:** Chamadas diretas repetidas a `CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance` para o mesmo `SeasonNumber` poderiam reaplicar promoções/rebaixamentos e `SeasonsManaged++`.
   - **Correção:** Adicionado guard no início de `FinalizeSeasonAndAdvance` retornando o `SeasonHistoryEntry` já existente para `world.SeasonNumber`.

---

## 28. Decisões técnicas tomadas

- **Compatibilidade com cliques duplicados sem chave vs retries com chave:** Quando o cliente não informa `idempotencyKey`, o segundo clique sobre o mesmo convite continua retornando `400 BadRequest` (`"Convite encerrado ou inválido."`), preservando 100% dos testes legados (`CoachCareer008IntegrationTests`). Quando `idempotencyKey` é informada (padrão do `ElifootApiClient._postFinance`), a repetição com mesmos parâmetros retorna o recibo idempotente com sucesso, enquanto o reuso da mesma chave para parâmetros distintos (`accept: true` vs `accept: false` ou outro `invitationId`) é rejeitado com `"Chave de idempotência já utilizada em outra decisão de convite."`.
- **Preservação da assinatura pública em `ElifootApiClient`:** Em vez de alterar a assinatura posicional de `acceptInvitation(worldId, invitationId, coachId)` e `rejectInvitation(worldId, invitationId, coachId)` no Flutter, o envio de `idempotencyKey` e `expectedRoundVersion` foi integrado via `_postFinance(uri)` e `_financeVersions[worldId]`, mantendo compatibilidade total com todos os mocks e testes de widget existentes (`complete_gameplay_011_test.dart`, `career_api_test.dart`).

---

## 29. Limitações ou pontos que dependem de definição futura

- **Salário pessoal do treinador e multa rescisória de técnico:** Conforme regras da missão ("Não criar mecânicas modernas fora do escopo aprovado: multa rescisória de treinador, salário complexo de treinador sem base"), o contrato do treinador com o clube não debita folha salarial de comissão técnica nem multa rescisória do caixa do clube.
- **Candidatura ativa avulsa (`apply`):** O sistema permanece no modelo clássico de convites passivos gerados pelos clubes vagos conforme ranking e moral.

---

## 30. Hash do commit final

- **Commit de implementação (`feat(elifoot98): implement V3 stage 02D manager career`):** `ac4e867` (`ac4e8672609064b922d071c54d93877ceea5a71e`)
- **Commit de validação e testes (`test(elifoot98): validate V3 manager career`):** `5594e56` (`5594e56fdcb6dd048d83f737b438d3ae6c8053cf`)

---

## 31. Conclusão PASS ou FAIL

Todos os critérios de aceite da **Seção 19** da missão foram integralmente cumpridos:
- Identidade `accountId` preservada entre sessões, dispositivos, trocas de clube e demissões.
- Carreira humana e da IA funcionando sem conflitos e com separação estrita entre dados do clube e dados do treinador.
- Concorrência e idempotência validadas (Cenários `A..H`).
- 50 itens obrigatórios de teste (`01..50`) implementados e aprovados.
- Simulações de 1, 3, 5, 10 e 20 temporadas concluídas com **0 violações**.
- Todos os testes `.NET` (`Domain`, `Application`, `Integration`, `Simulation`) e `Flutter` (`508` testes + `flutter analyze`) aprovados e `dotnet build -c Release` sem avisos ou erros.

**CONCLUSÃO FINAL DA ETAPA 02D: PASS**

# Moral e demissão 008

30/10/2026. STATUS: PASS. Fonte: src/Elifoot.Domain/CoachCareerEngine.cs e CoachMoraleRules em Entities.cs.

## Faixas e configuração

Moral inteira limitada a 0–100, inicial 70. Verde: 60–100; amarela: 30–59; vermelha: 0–29. Comparação inclusiva no mínimo da faixa. O cabeçalho e a aba de carreira exibem número, texto e cor; o histórico persiste todos os eventos e mostra os cinco mais recentes na UI.

O anfitrião pode usar PUT /api/worlds/{worldId}/career/morale-rules?coachId={hostId}, com JSON {"greenMinimum":60,"yellowMinimum":30,"unemployedRecoveryPerRound":6}. Somente PREPARING, RESULTS ou FINISHED. Validação: 1 ≤ yellowMinimum < greenMinimum ≤ 100 e recuperação de 1–20. As regras pertencem à sala e persistem. Alterar faixas reclassifica a moral existente; não muda o valor numérico.

## Fórmula por partida

gap = média de força de todo o elenco do clube − média de força de todo o elenco adversário. A média não altera a força de jogador algum.

Favorito: gap ≥ 6 OU reputação ≥ reputação adversária +15. Azarão: gap ≤ −6 OU reputação +15 ≤ adversária. Se prestígio e elenco produzirem sinais opostos, prevalece o ramo favorito, explicitamente. Demais casos: equilibrado.

| Resultado | Favorito | Equilibrado | Azarão |
| --- | --- | --- | --- |
| Vitória | +6 | +9 | +13 |
| Empate | −3 | +1 | +4 |
| Derrota | −14 | −9 | −4 |

1. Vitória zera a sequência sem vencer; empate/derrota incrementa.
2. Sem vitória, subtrair min(4, max(0, sequênciaSemVitória−2)).
3. Derrota por três gols ou mais subtrai mais 2.
4. Com três ou mais jogos de liga: posição esperada ordena forçaMédia +0,15×reputação, desempate DeterministicIndex. Posição real usa a tabela com todos os resultados da rodada já aplicados. Se não venceu e está mais de duas posições abaixo do esperado, −2. Se não perdeu e está mais de duas posições acima do esperado, +1.
5. moralFinal = clamp(moralAnterior + delta, 0, 100).

Os últimos cinco resultados são registrados como +1/0/−1. A sequência sem vitória é o modificador recente da fórmula; o somatório recent5 serve ao histórico, sem um segundo peso oculto. Taça usa o vencedor incluindo prorrogação/pênaltis. Preliminares têm zero pontos de carreira, embora uma partida efetivamente disputada possa alterar moral e estatísticas da IA distrital.

O registro guarda temporada, rodada, MatchId, antes/depois e motivo com resultado, gap, sequência e pontos. A tabela é liquidada em duas passagens para não depender da ordem dos jogos. CareerResultsApplied impede reprocessamento após replay/reload.

## Demissão

CoachId e moral são capturados uma vez ANTES do início e persistidos no fixture. Se moralNoInício < yellowMinimum E o resultado é derrota, a demissão ocorre depois de encerrada a partida. Entrar no vermelho por essa primeira derrota não dispara a demissão se o início ainda era amarelo/verde. Vitória ou empate em vermelho não dispara esta regra.

| Caso determinístico | Resultado |
| --- | --- |
| 60, derrota 0–1 equilibrada | 51, amarela, continua. |
| 35, derrota 0–1 equilibrada | 26, vermelha, continua. |
| 20, derrota 0–1 equilibrada | 11, demitido uma vez. |
| 20, vitória 1–0 equilibrada | 29, continua mesmo ainda vermelha. |
| 55, vitória 1–0 equilibrada | 64, verde. |
| 20, empate 0–0 equilibrado | 21, continua. |

Na demissão, fecha emprego e limpa o vínculo, escalação enviada e pronto humano; preserva treinador, moral, personalidade IA, pontos, títulos e ranking. Cria vaga e interino. Repetir a liquidação/demissão não duplica o contador. Se um treinador já saiu por outra razão, o desligamento não remove o novo treinador do clube.

Humano rebaixado Quarta→Distrital é demitido independentemente da moral. A IA mantém o clube jogável. Promovido Distrital→Quarta volta a ser elegível; não reconecta automaticamente o antigo humano. IA rebaixada conserva a regra anterior de desligamento por rebaixamento, agora com vaga e substituição explicável em vez de rotação cega.

## Desemprego

Uma vez por rodada subsequente: moral = min(mínimoVerde, moral+6). Se já estava acima do mínimo verde, não reduzir. Sem recuperação na rodada da demissão; polling/reload não acelera. Exemplo: 20→26→32→38→44→50→56→60 em sete rodadas posteriores. Desemprego não remove conquistas.

Ainda verde, recém-demitido aguarda duas rodadas antes de novo convite ou nomeação interina. Sem experiência (nenhum campeonato, <60 pontos E <15 vitórias), desempregado só é elegível para Terceira/Quarta; IA pode também trabalhar em Distrital. Isso limita convite imediato de elite sem criar desemprego permanente. Humano desempregado continua participando do contador de preparação.

Provas: MoraleBandsAndPreMatchRedDismissal (seis casos), StrongFavoriteDemandsMoreThanUnderdog_AndRecentPoorFormMatters, MoraleUsesCompletedRoundTableRegardlessOfFixtureProcessingOrder, UnemployedRecoveryIsGradualOncePerRoundAndNeverDeletesCareer, DistrictRelegationDismissesRegardlessOfMorale_AndPromotedClubIsEligible e DismissedAiCannotImmediatelyReappointItselfAsInterim. [TRX final](audit-008/closure-domain-tests/career32-final.trx). [Auditoria](COACH_CAREER_AUDIT_008.md).

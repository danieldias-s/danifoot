# Histórico individual de força — 012C

> **REGISTRO HISTÓRICO DA IMPLEMENTAÇÃO 012C.** Os campos e números abaixo preservam a entrega original. A proteção esportiva atual de `ApplyFinishedRound` usa `GuardSeason`/`GuardRound` e chave por mundo/temporada/rodada/jogador/motivo; não depende dos conjuntos `ProcessedMatches`/`ProcessedRoundPlayers` descritos na versão antiga. A perda por lesão mantém episódio/data e `ProcessedDays`. Regras vigentes e fontes conferidas em `f1764fb`: [referência oficial](catalogos/ELIFOOT98_REGRAS_FORCAS_E_INTEGRACAO_V11.md). As medições históricas não foram reexecutadas nesta correção.

STATUS: PASS. O histórico 012 é estendido com campos opcionais, preservando os registros antigos e a rota GET /api/worlds/{worldId}/players/{playerId}/history.

| Campo | Conteúdo |
| --- | --- |
| PlayerId / WorldId | Identidade do jogador e mundo autoritativo |
| Season / Round / GameDate | Contexto esportivo e data virtual |
| ClubId / ClubName / Division | Clube no momento do fato; transferência não reescreve o passado |
| LeaguePosition / GrowthCeiling | Última posição oficial completa ou referência provisória 4 e teto aplicável |
| ForceBefore / ForceAfter | Aliases de StrengthBefore / StrengthAfter para compatibilidade |
| ChangeReason | RESERVE_PROGRESS, PLAYED_MATCH_DECLINE ou INJURY_DAILY_LOSS |
| InjuryId / InjuryDuration | Episódio original e duração para perdas diárias |
| DailyInjuryLoss / ReserveGrowth | Pontos efetivos, separados por motivo |
| Rule / IdempotencyKey | CLASSIC_ADAPTED_FORCE_EVOLUTION e identidade persistente do fato |

Só alterações efetivas criam registros; manter força não fabrica evento. OTHER_VERIFIED_REASON fica reservado a fatos futuros comprovados, sem uso artificial. A UI mostra motivo legível, data, posição, teto e forças antes/depois; registros 012 sem novos campos aparecem com traço nesses campos.

Idempotência esportiva: chave inclui WorldId, Season, Round, PlayerId, MatchId e motivo. ProcessedMatches é persistido inclusive para jogos sem alterações. ProcessedRoundPlayers impede ganho duplicado quando propriedade muda durante a rodada; participação efetiva tem prioridade sobre a relação de reserva. Seu conjunto é reciclado por rodada, mantendo ProcessedMatches e histórico. Lesão: InjuryId+data virtual, ProcessedDays e ActualLoss persistentes, inclusive em dias sem perda. Travas existentes por mundo abrangem escrita/persistência; consultas não sorteiam novamente. Troca de temporada não limpa histórico nem snapshots.

A auditoria validou 254.480 eventos, zero chaves repetidas, zero duplicações de InjuryId+data e continuidade antes/depois para cada jogador durante as 100 temporadas. SQLite foi fechado/reaberto entre parcelas e consultado via endpoint. Repetição do mesmo dia/rodada não altera estado. Migração de saves legados não é automática.

Snapshots de turno incluem forças inicial/atual e ganhos/perdas por clube durante a temporada. Para jogador transferido, os totais do snapshot são os eventos ocorridos por aquele clube; o histórico individual é a referência completa entre clubes. Data e teto de evento são preservados, sem reavaliação retroativa.

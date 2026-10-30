# Histórico de força — 012

**Implementado e persistido no snapshot JSON existente.** Consulta pela rota de histórico já disponível, sob o mesmo lock do mundo: `GET /api/worlds/{worldId}/players/{playerId}/history`.

## Dados e semântica

`GameWorld.PlayerStrengthHistory` contém `PlayerStrengthChangeDto` imutável:

| Campo | Conteúdo |
| --- | --- |
| PlayerId | Identidade permanente do atleta |
| Season / Round | Instante de processamento antes da troca de temporada; atualmente o fechamento sazonal |
| ClubId / ClubName | Clube e nome capturados no evento, ou ausência explícita de clube |
| Division | Divisão no instante do ajuste; no fechamento já inclui promoção/rebaixamento |
| StrengthBefore / StrengthAfter | Força nominal realmente alterada |
| ChangeReason | NORMAL_PROGRESS ou NORMAL_DECLINE nesta política |
| InjuryId | Nullable; sempre null nesta entrega, pois não existe perda nominal por lesão implementada |
| Rule | LEGACY_SEASONAL_005_SCALE_FIX_012 |
| Detail | Ramo local: gols, aparições, acima/abaixo do alvo ou poucas aparições |
| IdempotencyKey | `player_strength:S{temporada}:P{playerId}` |

INJURY_STRENGTH_LOSS, RECOVERY_PROGRESS e OTHER_VERIFIED_REASON ficam reservados para fatos futuros comprovados. Não se emitem eventos sem mudança, como 50→50. Ausência de evento não significa ausência de participação; os logs experimentais de rodada registram ambas separadamente.

## Continuidade e idempotência

`EvolvedPlayerStrengthSeasons` é persistido, não um cache ignorado pela serialização. A rotina roda uma vez por temporada; reconexão, reload ou repetição do fechamento não repete evolução. A chamada única em CompetitionAndSeasonEngine substitui a rotina antiga. O fluxo real é protegido pelo lock do orquestrador; o método de domínio deve continuar sendo chamado sob esse lock em futuras integrações.

Histórico não é removido nos cortes de duas temporadas de fixtures/ledger. Transferência muda apenas o clube atual e não reatribui eventos anteriores. Rename do clube não muda ClubName capturado. O DTO HTTP acrescenta `strengthHistory`, `strengthHistoryStartSeason` e `strengthHistoryStartRound`, preservando os campos anteriores de estatísticas e prêmios.

O armazenamento é no JSON do mundo; nenhuma tabela SQL nova nem migração destrutiva. Saves anteriores recebem listas vazias/contador ausente e não reconstruímos forças passadas. O início da cobertura é marcado no primeiro processamento novo, sem modificar força só para migrar. Um GET não inicializa a cobertura nem grava snapshot. A escala continua validada pelos caminhos existentes de editor/importação.

## Interface e verificação

O perfil existente do jogador exibe HISTÓRICO DE FORÇA, início da cobertura, temporada, rodada, clube histórico, divisão, antes/depois e motivo legível. Política antiga aparece como evolução/queda **sazonal**, sem atribuição fictícia a contusão. O perfil já pode ser aberto pelo plantel e pelo ranking. Cliente antigo pode ignorar os campos novos; o cliente novo tolera respostas antigas sem histórico.

Testes: SQLite real em memória, gravação/reload, transferência real, nome histórico, oito GETs concorrentes sem gravação, avanço sazonal concorrente, reconexão por snapshot e igualdade de replay determinístico. Flutter verifica vazio legado, nomes e larguras 320/390/1280. Não foi realizado novo playtest visual de navegador ou Android.

A retenção integral cresce proporcionalmente ao número de alterações. Não foram introduzidos cortes silenciosos para reduzir snapshot; isso preserva o requisito de carreira. Uma futura paginação pode reduzir a resposta do perfil sem eliminar eventos persistidos.

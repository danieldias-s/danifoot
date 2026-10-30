# Limitações e Questões Conhecidas — Tarefa 010 (`ELIFOOT_VISUAL_POLISH_AND_RESPONSIVE_010`)

## 1. Ferramenta de Browser Integrado Indisponível no Ambiente (`BROWSER_VALIDATION: BLOCKED`)
- Conforme já documentado nas tarefas 008 e 009 (`docs/PLAYER_RANKINGS_LOCAL_VALIDATION_009.md`), o ambiente de execução atual não disponibiliza uma ferramenta de automação de browser externo/integrado para abrir `flutter run -d web-server` em uma janela de navegador Chromium real.
- Seguindo estritamente as regras das Seções 20 e 23 da especificação da tarefa 010:
  - As capturas visuais de antes e depois em `docs/evidence/visual_010/` foram geradas pela renderização real dos widgets de produção do Flutter (`PlayerRankingsScreen`, `_PlayerHistoryDialog`, `SeasonTopScorersPanel`, `showSeasonTopScorersDialog`) alimentados pelos payloads reais capturados da API/SQLite (`docs/audit-009/live-ui-response-snapshot.json`) por meio do runner `client/elifoot_flutter/tool/qa/player_rankings_visual_010_test.dart`, e todas as 24 imagens PNG geradas foram inspecionadas visualmente.
  - O campo `BROWSER_VALIDATION` no relatório final é reportado honestamente como `BLOCKED` e o `STATUS` global como `PARTIAL` exclusivamente devido à indisponibilidade da ferramenta de browser integrado no ambiente.

## 2. Repositório Git Local
- O diretório `D:\elifoot_98` não possui diretório `.git` inicializado (`fatal: not a git repository`), característica preexistente documentada nas auditorias 007, 008 e 009.
- Para garantir rastreabilidade e preservação integral antes de qualquer edição da tarefa 010, todos os arquivos preexistentes e as capturas da tarefa 009 foram copiados com manifesto SHA-256 em `docs/evidence/visual_010/baseline/baseline-manifest.json` e `docs/evidence/visual_010/before/`.

## 3. Cobertura Histórica de Dados Legados
- Para mundos salvos antes da tarefa 010 em que um clube histórico tenha sido excluído sem registro em `Clubs`, `SeasonTopScorerAwards` ou `EmploymentHistory`, a interface não inventa nomes de clubes: exibe o texto amigável `Clube histórico indisponível` e emite log técnico (`[Elifoot010] Clube histórico indisponível para clubId=...`), preservando o `ClubId` interno no banco de dados.

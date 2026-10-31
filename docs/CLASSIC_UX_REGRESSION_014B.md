# Regressão da UX clássica e editor — 014B

TASK: ELIFOOT_CLASSIC_UX_AND_PRE_SAVE_EDITOR_014B

STATUS: PARTIAL — implementação e verificações automatizadas concluídas; auditoria nativa do Chrome e comparação com referências ausentes permanecem pendentes. Não se alega auditoria humana.

579 testes .NET: 6 Application, 391 Domain, 156 Integration e 26 Simulation; zero falhas e zero ignorados. 101 testes Flutter; analyze sem ocorrências e build Web concluído. 2 testes Node. Evidência consolidada em `audit-014b/test-results-final.json`, com logs e TRX.

Comandos executados: `dotnet build Elifoot.slnx -c Release`, `dotnet test Elifoot.slnx -c Release --no-build` sem filtro, `flutter test`, `flutter analyze`, `flutter build web --dart-define=API_BASE_URL=http://127.0.0.1:5145`, `node --test tools/catalog-013b/openfootball-directory.test.mjs`. Logs/códigos em `audit-014b/ux-*`. Testes anteriores mantidos; expectativas renumeradas e detalhes abertos pela linha compacta. As 26 simulações levaram 31m48s.

| Campo obrigatório | Resultado / evidência |
|---|---|
| AUTO_MATCH_NAVIGATION | PASS automatizado: snapshot, reentrada e versões |
| READY_WAITING | PASS: mensagem, contador, bloqueio e retirar pronto; cena real PREPARING 1/2 |
| MULTIPLAYER_2_4_6_8 | PASS: HTTP/SignalR e widgets |
| DIVISION_GROUPING | PASS: 16 confrontos oficiais, quatro por divisão |
| CUP_GROUPING | PASS: etapa oficial, distritais, grupo grande, expansão independente |
| CLASSIC_LAYOUT_DESKTOP | PASS no renderer de 1366 px; Chrome pendente |
| CLASSIC_LAYOUT_MOBILE | PASS no renderer de 390 px e widgets desde 320 px; Chrome pendente |
| LIVE_SCORE_UPDATES | PASS: snapshots reais inicial/final e atualização em teste |
| PRE_SAVE_EDITOR | PASS: edição privada, validação e guarda do save |
| CUSTOM_WORLD_PRESET | PASS: propriedade, versão, DB e confirmação concorrente |
| CUSTOM_WORLD_RESTORE | PASS: seleção, reuso e recuperação após reinício |
| OLD_SAVES_PRESERVED | PASS: duas cópias históricas com hash financeiro igual após reinício |
| 012C_PRESERVED | PASS: regressão e hashes protegidos |
| 012D_PRESERVED | PASS: regressão, finanças reais e cinco séries reconciliadas |
| 013ABC_PRESERVED | PASS: catálogo, modos, direitos, snapshot e confirmação |
| MATCH_ENGINE_UNCHANGED | PASS: SHA-256 idêntico; defesa e probabilidades preservadas |
| DOTNET_TESTS | 579 aprovados; zero falhas/ignorados |
| FLUTTER_TESTS | 101 aprovados; analyze e Web com código 0 |
| NODE_TESTS | 2 aprovados |
| VISUAL_CAPTURES | 20 PNGs automatizados; dois testes de captura aprovados; não são Chrome |
| CHECKPOINT_COMMIT | Commit local `feat(014b): classic match navigation and private pre-save assemblies`; consultar `git log -1` |
| KNOWN_ISSUES | Chrome/referências; riscos econômicos individuais documentados |

Defeitos encontrados/corrigidos: navegação dependente da ação individual; editor dentro do campeonato; falta de montagem privada reutilizável; expansão compartilhada entre grupo e confrontos da Taça; reentrada em sala iniciada; contador atrasado estrangeiro; rótulos lidos de eventType em vez de descrição; indicador de carregamento no diálogo; atletas livres indevidos no template. Falhas intermediárias foram corrigidas e a suíte completa executada novamente.

Checkpoint anterior 0327c548 preservou integração/resultados antes da UX. Nenhuma simulação longa interrompida. Worktrees de origem mantiveram commits e nenhum diff rastreado. Sem merge no principal, push ou produção.

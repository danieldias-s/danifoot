# ELIFOOT_DYNAMIC_CLUB_COLORS_014C

STATUS: PARTIAL. Funcionalidade implementada e validada automaticamente; cobertura do catálogo incompleta e auditoria nativa/comparação com a referência ainda pendentes.

WORKTREE: `D:\elifoot_98_014C`

BRANCH: `feature/club-colors-theme-014c`

BASE_COMMIT: `c0ff28ec510553385438f82dc652c23126bd7689`

CHECKPOINT_COMMIT: o commit que introduz este relatório (`git log -1 --format=%H`).

A 014B foi encerrada com build e testes aprovados e ressalva de auditoria nativa. Nenhuma implementação nova foi feita nos worktrees anteriores. Evidências: `audit-014c/baseline.json`, `previous-worktrees-final.json` e `protected-files-final.json`.

## Resultado

O catálogo guarda paletas por ID interno e identidade externa completa (país, categoria, provedor, ID). Snapshots novos recebem a paleta. Mundos existentes sem paleta ficam neutros e aceitam preferências privadas, sem recriar o save. A paleta possui duas cores opcionais, texto preferido, fonte, evidência, data, confiança, versão, precisão e indicação de personalização.

Flamengo masculino: `BR/Male/openfootball/qid-Q17479`, vermelho/preto `#C3281E/#000000`. O vermelho numérico representa o exemplo solicitado; a fonte do clube confirma as cores tradicionais, não certifica esse RGB como manual de marca. Não foi atribuída automaticamente a mesma paleta ao Flamengo feminino.

CLUB_COLOR_SEARCH: Wikidata com QID revisado e importação de cache aberto preservado; consulta em lote limitada por maxlag/HTTP 429.

CLUB_COLOR_COVERAGE: 17/1398 registros do catálogo (15 masculinos, 2 femininos); 1381 lacunas explícitas. O subconjunto elegível de clubes verificados é menor que o catálogo completo. Não confundir os dois denominadores.

OFFICIAL_COLOR_SOURCES: Wikidata P6364/P462 e P465; Museu Flamengo para vermelho/preto; W3C para representações RGB genéricas.

CACHE: PostgreSQL, último valor válido, prazos de nova tentativa e ausência de consultas externas na renderização.

FLAMENGO_RED_BLACK: verificado por HTTP e renderer Flutter, desktop/mobile.

OTHER_CLUB_COLORS: 16 paletas adicionais com proveniência, inclusive cores únicas.

DYNAMIC_THEME: ThemeExtension/ThemeData centralizados e telas existentes adaptadas.

DESKTOP / MOBILE: 1366/390 nas capturas; matriz responsiva anterior mantida nos testes.

MATCH_TEAM_COLORS: nomes com paletas próprias, quatro divisões agrupadas e placares centrais.

APPEARANCE_MENU / USER_CUSTOM_COLORS / RESTORE_DEFAULT: implementados, seletor HSV, hexadecimal e prévia imediata.

PRE_SAVE_EDITOR / CUSTOM_WORLD_PRESET: seletor visual, cores privadas e cópias independentes.

USER_PREFERENCES: por usuário/mundo/clube com versão otimista.

DATABASE_PERSISTENCE: tabelas aditivas no catálogo, snapshots públicos separados das preferências privadas.

OLD_SAVES: ausência de paleta aceita; personalização não escreve dados do jogo.

MULTIPLAYER_ISOLATION: dois treinadores por mundo validados pela API real; não há envio de preferências pelo snapshot/SignalR.

CONTRAST_VALIDATION: meta 4,5:1 nos pares de texto normais do tema e nas 17 paletas; tonalidade renderizada é ajustada, RGB registrado é preservado.

## Cobertura

| País | Categoria | Paletas | Registros |
| --- | --- | ---: | ---: |
| AR | Male | 1 | 67 |
| BR | Male | 1 | 82 |
| DE | Male | 1 | 258 |
| ES | Male | 3 | 128 |
| FR | Male | 2 | 87 |
| GB | Male | 4 | 208 |
| IT | Male | 3 | 100 |
| PT | Male | 0 | 61 |
| AR | Female | 1 | 43 |
| BR | Female | 0 | 33 |
| DE | Female | 0 | 69 |
| ES | Female | 0 | 82 |
| FR | Female | 0 | 38 |
| GB | Female | 1 | 55 |
| IT | Female | 0 | 69 |
| PT | Female | 0 | 18 |

## Validação

`dotnet build Elifoot.slnx -c Release`: aprovado, sem avisos/erros. Regressão da solução sem os testes longos de simulação: 6 aplicação, 391 domínio e 167 integração na primeira rodada (166 aprovados; um caso sem artefato). O caso foi repetido com o artefato real da 014B e passou. Mais cinco casos específicos foram acrescentados depois: a suíte de cores final passou 16/16. Total de casos distintos validados: 569. Os logs originais, inclusive a falha de preparação, foram preservados.

`flutter analyze --no-pub`: sem problemas. `flutter test --no-pub`: 114 aprovados. Renderer adicional: 2 testes e 18 PNGs. `flutter build web --release --dart-define=API_BASE_URL=http://127.0.0.1:5155`: aprovado. Node: 2 aprovados. Conversão CSV: 17 linhas e rejeição de identidade duplicada.

A 014B já validou 26 testes de simulação e cenários de 100 temporadas. Não foram repetidos, conforme o pedido desta missão de aparência. Doze arquivos protegidos de jogo, evolução, disciplina, economia, contratos, insolvência, seed e IA têm SHA-256 idêntico à base.

HTTP real: paletas propagadas, categorias separadas, dois treinadores, conflito em três de quatro gravações concorrentes, autenticação e reset; fingerprints completos de mundos antes/depois das preferências iguais. Três rodadas oficiais foram usadas somente para registrar partidas reais.

VISUAL_CAPTURES: `audit-014c/automated-renders/`, renderer de respostas HTTP reais; não são screenshots de Chrome. Ver `VISUAL_AUDIT_014C.md`.

MATCH_ENGINE_PRESERVED / ECONOMY_PRESERVED: hashes iguais, sem alterações de regras. Sem merge, push ou publicação.

## Execução local

`powershell -File scripts/run-local-014c.ps1 -NoBuild -ManualClock`

Web: `http://127.0.0.1:5156`; API: `http://127.0.0.1:5155`; PostgreSQL próprio: `127.0.0.1:55438`. O modo manual impede avanço enquanto o revisor inspeciona. Credenciais locais ficam em artifacts ignorados. O runner recusa portas/processos de outra sessão. Para parar somente estes serviços: `scripts/stop-club-colors-014c.ps1 -IncludePostgres`.

KNOWN_ISSUES: ver documento próprio. Este relatório não aprova cobertura integral nem auditoria nativa.

A reconciliação por identificador de teste está em `audit-014c/dotnet-reconciled-results.json`; nomes de alguns casos parametrizados são truncados pelo TRX e não devem ser usados para contagem. Reinício real de API/PostgreSQL confirmou quatro preferências, dois presets e quatro mundos independentes sem mudança dos fingerprints.

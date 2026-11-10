# Elifoot 98 Multiplayer V3 — Etapa 01

Implementação: 09/11/2026. Encerramento concluído em 10/11/2026, com validação iniciada em 09/11. Projeto: `D:\elifoot_98`.

**Status final: PASS. Etapa 01 concluída.**

A correção dos gols da Taça e a seleção A/M foram implementadas sobre `451a916033cb9a3adc945e6eaf3bb02c3b072434` e registradas na `main` local no commit `d0489d3a4f9618fb12d4ea1650ef597e835dd2fe`. Na validação inicial, o resultado geral foi PARTIAL pelo layout em 1024 px. Essa falha foi corrigida e as validações foram concluídas no encerramento descrito abaixo. Não foi iniciada outra etapa.

## Inspeção e limite de escopo

Foram consultados o código real, `docs/analises/ELIFOOT98_AUDITORIA_ESTADO_ATUAL.md`, a documentação consolidada e a proposta do módulo. A implementação segue a autorização explícita da Etapa 01; regras adicionais dos anexos não foram incorporadas.

Não houve alterações nas fórmulas de força efetiva, probabilidades de gols, evolução de força, políticas de tática, economia, mercado, estádio ou treinadores. A escolha de jogadores necessariamente pode produzir escalações e resultados esportivos diferentes. Os pontos de chamada em arquivos do motor e do mercado apenas passam o contexto da temporada à seleção existente.

Os arquivos e artefatos anteriores foram preservados. Nenhum save real foi substituído. Os testes usaram mundos em memória e bancos temporários. Foi criado o commit local da implementação, seguido do registro documental deste relatório com seu hash. Não foi feito push; o repositório não possui remoto configurado.

## Comportamento implementado

### Gols da Taça

`Player.SeasonGoals` já inclui Liga e Taça. `Player.CupGoals` é um subconjunto desse total. O painel do servidor e o fallback do painel clássico do Flutter agora usam o maior total comprovado entre contador sazonal e estatísticas, sem somar os dois contadores sobrepostos.

Um gol real da Taça mantém `SeasonGoals = 1`, `CupGoals = 1`, `CareerGoals = 1` e é exibido como **1** gol da temporada. Nenhum contador, evento antigo ou bucket histórico foi reescrito para fazer essa correção.

O endpoint legado `/top-scorers` tem outra semântica: seu DTO transmite gols da Liga e gols da Taça separadamente. A soma desses campos na tela de artilharia foi preservada. Rankings, histórico e premiação continuam usando seus registros por competição.

### Seleção centralizada

`LineupSelectionEngine` é a única implementação produtiva de seleção A/M. A UI deixou de ordenar atletas localmente e solicita a seleção ao servidor.

| Modo | Prioridade |
| --- | --- |
| M — Melhores | Força atual nominal |
| A — Automático | Força atual nominal + `min(10, mérito da temporada)` |

| Posição | Gol | Assistência |
| --- | ---: | ---: |
| Atacante | 1 | 0,5 |
| Meia | 0,5 | 1 |
| Defensor | 0,5 | 0,5 |
| Goleiro | 0 | 0 |

Liga e Taça entram uma única vez. As estatísticas são agrupadas por jogador na temporada atual, incluindo clubes anteriores após transferência. Para saves antigos, os contadores disponíveis de gols são preservados como evidência sobreposta; assistências não registradas não são inventadas.

A seleção respeita a formação informada, as posições e a disponibilidade. Um titular elegível vence o empate no score. O desempate restante usa o ID do jogador, e a ordenação de saída é estável em seleções repetidas. Lesionados e suspensos ficam fora de titulares e reservas. O banco mantém a política existente de goleiro reserva e limite de nove atletas; a política existente para elencos incompletos também foi mantida.

Estrela e comportamento não participam de A nem de M. A antiga política financeira de planejamento do elenco foi mantida separadamente, para não alterar mercado ou orçamento nesta etapa.

IA utiliza A, inclusive no fallback de clube sem treinador cadastrado. Humanos escolhem A ou M na UI, e o modo é persistido no clube. Preparação automática de humanos respeita essa preferência. A virada de temporada usa o reset sazonal existente e exclui os buckets de temporadas anteriores do mérito; a carreira permanece preservada.

### API, concorrência e saves

Novo comando: `POST /api/worlds/{worldId}/lineup-selection`.

Recebe treinador, formação, modo, titulares atuais, versão da rodada e chave de idempotência. Valida controle do clube, fase de preparação, estado de pronto, formação, jogadores e versão; executa sob o lock da sala. Salva modo, formação, titulares e reservas, incrementa a versão uma única vez e publica o contador atualizado.

Retries com a mesma chave e conteúdo devolvem a mesma seleção. Reutilização da chave com outro conteúdo ou em outra rodada é rejeitada. Um retry também conclui a gravação caso a tentativa anterior tenha falhado ao salvar. Campos novos do snapshot são aditivos: saves sem preferência carregam A e sem recibos carregam uma coleção vazia.

## Arquivos da implementação

| Arquivo | Alteração |
| --- | --- |
| `src/Elifoot.Contracts/LineupSelectionDtos.cs` | Novo enum, comando e resposta A/M |
| `src/Elifoot.Domain/LineupSelectionEngine.cs` | Novo algoritmo compartilhado |
| `src/Elifoot.Domain/LineupSelectionState.cs` | Recibos persistidos de idempotência |
| `src/Elifoot.Domain/Entities.cs` | Preferência A/M no clube |
| `src/Elifoot.Domain/PlayerStatisticsEngine.cs` | Total sazonal do painel sem duplicação |
| `src/Elifoot.Domain/AiManagerBrain.cs` | Seleção e prioridade de atletas pela rotina central; planejamento financeiro preservado |
| `src/Elifoot.Domain/MatchEngine.cs` | Contexto da temporada nos pontos de seleção/reação; fórmulas de partida intactas |
| `src/Elifoot.Domain/MarketAndEconomyEngine.cs` | Um argumento de contexto na chamada existente de preparação da IA |
| `src/Elifoot.Application/LineupSelectionCommands.cs` | Comando validado, persistente e idempotente |
| `src/Elifoot.Application/MultiplayerRoundOrchestrator.cs` | Integração na preparação de humanos e IA |
| `src/Elifoot.Application/MatchInteractionCommands.cs` | Contexto da temporada na seleção de atletas da IA |
| `src/Elifoot.Api/Program.cs` | Endpoint protegido pelo middleware de identidade existente |
| `client/elifoot_flutter/lib/api_client.dart` | Cliente do novo endpoint |
| `client/elifoot_flutter/lib/main.dart` | Aplicação da seleção retornada pelo servidor e versão da rodada |
| `client/elifoot_flutter/lib/shell_actions.dart` | A delegado ao servidor |
| `client/elifoot_flutter/lib/squad_classic.dart` | Menu explícito A/M |
| `client/elifoot_flutter/lib/classic98_shell.dart` | A/M na aparência clássica e correção do painel de gols |
| `client/elifoot_flutter/lib/tactical_formation.dart` | Remoção do seletor local substituído pelo servidor |

## Testes criados ou adaptados

| Arquivo | Cobertura |
| --- | --- |
| `tests/Elifoot.Domain.Tests/LineupSelectionV3Tests.cs` | Sete atacantes, pesos independentes de gols/assistências, teto, estrela/comportamento, formação, empate, lesões, suspensões, banco, transferência, reset, legado, gols reais, reload, rankings e premiação |
| `tests/Elifoot.Application.Tests/LineupSelectionV3Tests.cs` | A/M humano, paridade com IA, IA sem treinador, validações, concorrência, persistência, retry e falha de gravação |
| `tests/Elifoot.Integration.Tests/AccountLoginTests.cs` | Endpoint autenticado, rejeição de outro usuário, SQLite, reinício e replay |
| `tests/Elifoot.Domain.Tests/AssistOutcomeProbe.cs` | Probe com escalações fixas e sem decisões de treinador |
| `tests/Elifoot.Domain.Tests/AssistRankingsTests.cs` | Vinte referências de partidas com participantes fixos |
| `tests/Elifoot.Domain.Tests/Discipline012CTests.cs` | Fixture usa um defensor efetivamente titular, independente do novo desempate |
| `tests/Elifoot.Simulation.Tests/MultiSeasonAndBenchmarkTests.cs` | Simulação passa contexto sazonal à seleção, incluindo assistências |
| `tests/Elifoot.Simulation.Tests/PlayerForce012SimulationTests.cs` | Valida o total de observações pelo calendário oficial, preservando as verificações de força e integridade |
| `tests/Elifoot.Integration.Tests/PlayerRankings009IntegrationTests.cs` | Valida gols e vencedores pelos eventos oficiais do save, mantendo prêmio, ledger e idempotência |
| `client/elifoot_flutter/test/lineup_selection_v3_test.dart` | Payload A/M, IDs retornados pelo servidor, titulares atuais e formação preservada em celular/desktop |
| `client/elifoot_flutter/test/cup_goals_panel_v3_test.dart` | Um gol da Taça no painel, com e sem histórico, em celular/desktop |
| `client/elifoot_flutter/test/complete_gameplay_011_test.dart` | Fake do novo endpoint |
| `client/elifoot_flutter/test/squad_swap_season_test.dart` | Mantém trocas manuais; remove teste da antiga seleção local substituída |
| `client/elifoot_flutter/test/game_viewport_test.dart` | Mantém as verificações originais e acrescenta leitura, limites e abertura dos seis menus em tablet/desktop |

As referências antigas de partidas completas incluíam decisões de escalação da IA que foram expressamente alteradas nesta etapa. Para verificar a preservação da física, foram geradas **20 referências com escalações fixas a partir do código anterior da main**, extraído com `git archive HEAD` em `artifacts/v3-stage01/baseline-source`. As 20 coincidiram com a implementação atual. Não foram simplesmente substituídos hashes pelos novos resultados da IA.

## Resultados da implementação inicial

| Verificação | Resultado | Evidência em `artifacts/v3-stage01/` |
| --- | --- | --- |
| Build .NET Release da solução | PASS — zero avisos e erros | `build.log` |
| Domínio completo | PASS — 547 testes | `domain-final.log`, `tests/domain-final.trx` |
| Domínio após os ajustes finais | PASS — 66 testes relevantes, incluindo as 20 referências e pesos independentes | `domain-targeted-final.log`, `tests/domain-targeted-final.trx` |
| Aplicação completa | PASS — 37 testes | `application-final.log`, `tests/application-final.trx` |
| Integrações relevantes | PASS — 26 testes | `integration-final.log`, `tests/integration-final.trx` |
| Temporada real completa | PASS — 351 partidas, 32.610 minutos e 691 gols conciliados; prêmios e recarga validados | `simulation.log`, `simulation/REAL_SEASON_009.json`, `tests/simulation.trx` |
| Flutter analyze | PASS — sem problemas | `flutter-analyze-final.log` |
| Flutter direcionado após corrigir o painel | PASS — 19 testes | `flutter-targeted-final.log` |
| Flutter completo final | PARTIAL — 483 testes passaram; 1 falha de layout preexistente | `flutter-full-final.log` |
| Integridade do diff | PASS — `git diff --check` | Verificação local |

Na rodada inicial, o filtro de integrações foi `FullyQualifiedName~AccountLoginTests|FullyQualifiedName~PlayerRankings|FullyQualifiedName~WorldPersistence|FullyQualifiedName~Sqlite`. A fixture histórica necessária ao teste de exportação foi copiada para a pasta isolada de artefatos; o original não foi modificado. Naquela rodada não foi executada a suíte completa de simulações longas. O encerramento abaixo incluiu todas as quatro suítes .NET, sem filtro na execução integral.

Os logs iniciais também foram mantidos para rastreabilidade: falhas de fixtures dependentes da escalação anterior, ordenação de empates e ausência inicial da cópia de um artefato histórico foram resolvidas antes dos resultados finais acima.

## Encerramento — correção do layout em 1024 px

A falha em `game_viewport_test.dart`, teste `Game stays centered through desktop and phone sizes (classic=true)`, foi reproduzida isoladamente antes da correção: overflow horizontal de **48 px em 1024 px**. O diagnóstico completo identificou a `Row` da barra de menus em `classic98_shell.dart`: os seis menus precisavam de aproximadamente 1018 px, mas a área interna disponível era 970 px, após margens e bordas.

O problema era anterior à Etapa 01: `git blame` atribui a barra rígida ao commit `ac067947`, de 09/11/2026 às 11:37:04 (-03:00). A mesma `Row` está no commit-base `451a916`, e os logs `artifacts/main-sync-20261109-192940/flutter-tests.log` e `flutter-viewport-recheck.log` registram o mesmo overflow. A reprodução e o diagnóstico atuais estão em `artifacts/v3-stage01-close/flutter-1024-before.log` e `flutter-1024-diagnostic.log`. O teste diagnóstico temporário foi removido após identificar a causa.

A correção produtiva deste encerramento é a troca dessa `Row` por `Wrap`. Os menus passam à linha seguinte quando a largura não comporta todos eles. Não foram reduzidos textos, fontes ou áreas clicáveis, nem alterado o breakpoint móvel. O corpo da tela mantém sua área flexível e rolagem; o rodapé continua dentro do viewport.

O teste original permanece com suas verificações em **360, 390, 768, 1024, 1280, 1360, 1440, 1600 e 1920 px**, nas aparências atual e clássica. O teste adicional verifica os seis menus em **1024×640, 1072×768, 1280×720 e 1360×900**: limites horizontais/verticais, texto sem truncamento, hit testing, abertura e fechamento dos popups. A verificação de substituições mantém as resoluções **1920×500 e 360×640**.

Na conferência inicial, os 20 diffs previamente alterados, exceto a tela clássica, estavam idênticos à captura do início (`artifacts/v3-stage01-close/scope-verification.json`). Depois foram ajustadas somente as referências de testes descritas abaixo. A conferência de hashes confirmou que o código produtivo permaneceu congelado após a correção do layout. As regras A/M e as fórmulas de partidas não foram modificadas neste encerramento. Não houve migrations ou alterações em bancos existentes. As integrações usaram um novo cluster PostgreSQL de teste em loopback, porta 55442, dentro da pasta ignorada de artefatos.

### Validação do encerramento

Evidências em `artifacts/v3-stage01-close/`:

| Verificação | Resultado | Evidência |
| --- | --- | --- |
| Build .NET Release | PASS — zero avisos e erros | `dotnet-build-final.log` |
| Domínio completo | PASS — 547 testes | `Elifoot.Domain.Tests.log`, `tests/Elifoot.Domain.Tests.trx` |
| Aplicação completa | PASS — 37 testes | `Elifoot.Application.Tests.log`, `tests/Elifoot.Application.Tests.trx` |
| Simulações completas | PASS — 31 casos distintos: 29 aprovados na execução integral e dois corrigidos/revalidados | `tests/Elifoot.Simulation.Tests.trx`, `tests/simulation-audit-final.trx`, `tests/simulation-force-final.trx` |
| Integrações completas | PASS — 288 casos distintos: 286 aprovados inicialmente e dois corrigidos/revalidados; a reexecução incluiu também a variante SQLite | `tests/Elifoot.Integration.Tests.trx`, `tests/integration-corrected-final.trx` |
| Total .NET consolidado | PASS — 903 casos distintos, zero falhas ou skips pendentes | `validation-consolidated.json` |
| Calendário, A/M e física após manutenção dos testes | PASS — 39 testes de domínio e seis de aplicação | `tests/domain-final-related.trx`, `tests/application-final-related.trx` |
| Flutter analyze | PASS — sem problemas | `flutter-analyze.log` |
| Viewport, menus e substituições | PASS — 4 testes | `flutter-viewport-final.log` |
| Suíte completa Flutter | PASS — 485 testes, zero falhas | `flutter-full.log` |

Os testes de A/M para humanos e IA e da contagem dos gols da Taça estão incluídos nas suítes completas. O mérito continua restrito à prioridade de escalação, com teto +10 em A; M continua usando somente força atual. Não houve ampliação de escopo ou início da Etapa 02.

### Referências de validação reveladas pela execução integral

A execução integral das quatro suítes .NET terminou com 899 casos aprovados e quatro falhas. A investigação identificou referências de testes e infraestrutura, sem alteração das regras produtivas:

- Duas simulações ainda esperavam 2.200 rodadas em cem temporadas. O calendário oficial para 128 clubes tem 21 rodadas por temporada, explicitamente protegido por `CupCalendarTests` desde o commit `9af02bc` de 03/11/2026, anterior à V3 (`calendar-origin.txt`). As duas verificações passaram a exigir `100 × BuildSeasonRoundSchedule(128).Count`, mantendo as verificações de 100 temporadas, 35.100 partidas e todas as demais invariantes. O calendário e seu teste de domínio não foram alterados.
- A exportação de temporada esperava dois artilheiros com exatamente dez gols, números fixos de um artefato antigo. O save gerado nesta execução tem dois artilheiros com doze gols. O teste agora apura contagens e IDs dos vencedores diretamente dos eventos oficiais de gols, sem usar o ranking exportado como referência. Mantém a exigência de R$ 3.000.000 por vencedor, reconciliação do ledger, recarga e idempotência.
- O banco temporário preparado para importação não continha o identificador `013b`, exigido pelo guarda existente do catálogo. Foi criado outro banco no mesmo cluster exclusivo de testes, `elifoot_016b_v3close_013bimport16a`, e corrigida apenas a configuração do runner ignorado pelo Git. O guarda da aplicação foi preservado e nenhum banco existente foi modificado para contorná-lo.

Após essas correções, o build voltou a passar sem avisos ou erros, 39 testes de calendário/A/M/física e seis testes de A/M da aplicação passaram. As três integrações reexecutadas passaram. As duas simulações longas também passaram: a campanha de força levou 25 min 22 s e a campanha de auditoria/matriz tática levou 28 min 39 s. Os artefatos foram separados e os logs da execução integral original foram preservados. As demais verificações aprovadas da suíte completa continuam válidas: não houve mudança de código produtivo após aquela execução.

A consolidação usa o identificador de cada caso VSTest, preserva o resultado inicial e aponta o arquivo de evidência da revalidação. Nenhuma repetição foi somada ao total. Assim, a cobertura final é **547 domínio + 37 aplicação + 31 simulações + 288 integrações = 903 casos .NET distintos aprovados**. A execução integral inicial contém as falhas históricas descritas; as revalidações verdes são a evidência de sua resolução, sem ocultar os registros anteriores.

### Commit e pendências do encerramento

Todas as validações previstas estão aprovadas. A falha de layout inicial está resolvida e não há pendências funcionais ou de testes nesta etapa.

- Commit de código e testes: **`d0489d3a4f9618fb12d4ea1650ef597e835dd2fe`**.
- Mensagem exata: `feat(elifoot98): conclude V3 stage 01 and fix 1024px layout`.
- Branch: `main`, mantida localmente.
- Escopo: 32 arquivos de código/testes no commit principal e este relatório como complemento documental, totalizando 33 arquivos da etapa.
- Este relatório registra o hash do commit principal em um commit somente documental, sem alteração de código após os testes.
- Alterações preexistentes fora do escopo foram mantidas e excluídas dos commits.
- Push: não realizado.
- Etapa 02: não iniciada.
- Cluster PostgreSQL temporário: encerrado após as validações; dados e artefatos preservados para revisão.

**Pendências remanescentes da Etapa 01: nenhuma. Status final: PASS.**

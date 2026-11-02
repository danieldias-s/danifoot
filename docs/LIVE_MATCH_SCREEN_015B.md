# Nova tela de jogos ao vivo — desktop e mobile

STATUS: PARTIAL — implementação e validação automatizada concluídas; auditoria e capturas reais no Chrome pendentes porque o uso do navegador no Work continua desabilitado. Não se declara aprovação do teste manual do proprietário.

Worktree exclusivo: D:\elifoot_98_015B. Branch: feature/classic-desktop-ui-015b. Base: f1a5995. Checkpoint: commit que contém este relatório (`git log -1 --format=%H -- docs/LIVE_MATCH_SCREEN_015B.md`). Nenhum merge, push ou deploy.

## Funcionalidades

O jogo do clube vinculado ao treinador é selecionado automaticamente. Clicar em outro resultado atualiza placar, estado, minuto, estádio, público, renda, eventos e cartões. MEU JOGO retorna ao confronto do treinador. O painel Meu Clube continua vinculado ao clube do usuário enquanto ele observa outros jogos.

A seleção é local, identificada por sala, treinador, clube, temporada e rodada. Fica preservada durante snapshots e reconexões; o identificador é salvo no armazenamento de sessão já existente, independente por aba do navegador. Ao carregar novamente, só é reaproveitado no mesmo contexto e se o confronto ainda existir. Troca de sala, clube, treinador, temporada ou rodada descarta a seleção incompatível. As versões e a sala dos snapshots continuam validadas pelo shell antes da exibição.

Desktop: coluna de estádio e Meu Clube à esquerda, placar grande sobre a área principal, resultados compactos por divisão no centro e lances/estatísticas à direita. Eventos admitem rolagem independente. Cabeçalho ELIFOOT 98 ONLINE e navegação horizontal destacam Jogos ao Vivo.

Mobile: cabeçalho compacto, placar, resumo de estádio/bilheteria com acesso aos detalhes, MEU JOGO, abas Lances/Estatísticas e resultados por divisão expansível. Menu inferior: Plantel, Jogos, Divisões, Mercado e Mais. Nomes completos estão no placar e nos tooltips de nomes truncados na lista. Selecionar um resultado não desloca a lista. VER PLACAR SELECIONADO e VOLTAR À LISTA permitem alternar entre as duas posições.

Os filtros são criados pelas divisões/competições presentes, inclusive divisões numéricas adicionais. A Taça conserva sua fase real e confrontos entre clubes de divisões diferentes não são apresentados como uma partida de liga de uma divisão arbitrária.

O navegador de rodadas usa o calendário. Ver rodada anterior/seguinte não executa avanço do jogo. Para rodadas fora da atual, só aparecem os dados efetivamente retornados pelo calendário, sem copiar lances, público, renda ou minuto de outra rodada. Se o clube não jogar, o botão MEU JOGO fica desabilitado e a ausência é explicada.

O bloqueio 015A da partida continua autoritativo: menus externos, troca de sala e atalhos permanecem desabilitados durante o jogo. A navegação entre confrontos é somente leitura. Substituições e táticas em pausa continuam ligadas ao próprio clube; o painel de controle aparece ao observar o próprio jogo. Selecionar um adversário não concede controle sobre ele.

A aba Editor & Pacotes permanece indicativa na barra da partida, com tooltip para o criador de campeonato. O editor continua antes do save; o bloqueio de edição de saves confirmados foi preservado. Não foi reaberta uma rota para editar clubes/jogadores em jogo.

## Integrações e limites dos dados

- `GET /api/worlds/{worldId}/snapshot`: confrontos, ids de clubes, placares, minuto, fase, lineups, cores, eventos, público e valores de bilheteria.
- SignalR/WebSocket, polling, versões e reconexão existentes em ElifootApiClient: reutilizados, sem nova conexão ou endpoint.
- `GET /api/worlds/{worldId}/clubs`: nome, estádio cadastrado, capacidade e paleta. A capacidade é identificada como cadastro atual; não é apresentada como capacidade histórica de outra rodada. O diretório é atualizado nas consultas secundárias e na mudança de rodada, evitando o cache antigo após obras/renovações; o teste de integração verificou a atualização enquanto outro confronto permaneceu selecionado.
- `GET /api/worlds/{worldId}/calendar`: placares e confrontos de outras rodadas da temporada.
- `session_store`: sessão/seleção individual persistida. Selecionar partida não envia comando à sala nem altera preferências de outro treinador.

Os eventos oficiais são substituídos integralmente a cada snapshot, sem anexar novamente notificações repetidas. A ordenação usa minuto e ordem original dentro do mesmo minuto. O DTO não possui identificador individual de evento: registros oficiais idênticos não são apagados por uma deduplicação especulativa. O minuto repetido no início da descrição é removido apenas na apresentação. Substituições usam os campos oficiais de entrada/saída, mantendo o contrato do motor.

O snapshot não fornece posse, finalizações, chutes no gol, escanteios ou faltas. Esses números não foram simulados: a tela explica a indisponibilidade e apresenta os cartões contados dos eventos oficiais por clube, incluindo o segundo amarelo/expulsão. Cidade também não está disponível nessa consulta e aparece como Não informada.

Público, preço e renda são preenchidos juntos por SettleMatchAndRoundFinancials no encerramento da rodada. Durante o jogo, os zeros iniciais não representam uma bilheteria já apurada. A interface mostra Em apuração e traços nas linhas até haver valores/preço reportados. Zero legítimo continua exibido quando a bilheteria foi reportada. Não foram estimados público, renda ou receita do vendedor e nenhuma regra econômica mudou.

O backend não fornece os escudos oficiais da imagem. Foi reutilizado um escudo geométrico com a paleta registrada de cada clube; não se apresenta esse desenho como arte oficial. Nenhuma marca, resultado ou valor do exemplo Barcelona/Porto foi fixado na implementação.

## Arquivos

| Área | Arquivos |
|---|---|
| Painéis responsivos | client/elifoot_flutter/lib/live_match_view.dart |
| Seleção, calendário e eventos | client/elifoot_flutter/lib/live_match_data.dart |
| Disponibilidade da bilheteria | client/elifoot_flutter/lib/match_gate.dart |
| Lista clássica selecionável | client/elifoot_flutter/lib/classic_match_board.dart |
| Cabeçalho e navegação | client/elifoot_flutter/lib/classic_layout.dart, live_match_navigation.dart, mobile_classic.dart |
| Integração e persistência | client/elifoot_flutter/lib/main.dart, shell_actions.dart |
| Escudo reutilizável | client/elifoot_flutter/lib/club_crest.dart |
| Testes | client/elifoot_flutter/test/live_match_screen_test.dart, classic_ux_014b_test.dart, complete_gameplay_011_test.dart |
| Evidência | client/elifoot_flutter/tool/live_match_capture_015b_test.dart, scripts/prepare-live-review-015b.py, docs/evidence-live-matches-015b |

## Validação efetiva

196/196 testes Flutter, zero ignorados, incluindo os 180 existentes e 16 casos novos. Analyzer: zero issues. Build Web: concluída com API_BASE_URL=http://127.0.0.1:5167, sem service worker. O renderer de cenas oficiais passou em duas execuções (andamento e resultados), 1/1 em cada; ele utiliza transporte HTTP em memória e não modifica a sala.

A cobertura nova verifica seleção inicial, observação de outro confronto, MEU JOGO, estádio/público/renda/estatísticas do selecionado, seleção preservada nos ticks e no contexto recarregado, escopo por treinador/sala/clube/temporada/rodada, ordenação e substituição integral de eventos, calendário sem vazamento de dados da rodada atual, início/intervalo/fim, ausência de jogo, permissões de controle, filtros, expansão e rolagem mobile. Larguras 320/390/768/1024/1440/1920 passaram sem exceção. A regressão anterior de início 1/1, bloqueio multiplayer 2/4/6/8, 5-3-2, mercado, contratos e finanças também passou.

A sala QA exclusiva **015B QA - Jogos ao vivo / ELI0D648657** contém dois treinadores de teste (Flamengo RJ e FC Porto), clubes reais e jogadores simulados autorizados. Foi criada e avançada apenas essa sala. As cenas oficiais registraram R3 em andamento no minuto 31, 16 jogos/45 eventos, e encerrada no minuto 90, 16 jogos/165 eventos, com bilheteria apurada. Os resumos e hashes estão em official-api-scenes.json. Os saves individuais e a sala de mercado anterior não foram alvo dos comandos de teste.

Foram gerados e inspecionados 12 PNG em evidence-live-matches-015b/automated-renders: desktop e mobile com jogo do treinador, outro confronto e divisões expandidas, além da aba Estatísticas; os mesmos casos também foram registrados após a conclusão oficial. São **renderizações Flutter de dados reais gravados, não capturas de Chrome ou aparelho físico**. Os arquivos anteriores da 014C, 015B e da revisão mobile foram preservados.

Nenhum arquivo de src/.NET, MatchEngine, calendário, regras de progressão ou economia foi alterado. Os 640 testes .NET e dois Node documentados anteriormente não foram repetidos nesta alteração exclusiva do Flutter; não são apresentados como execução nova.

## Acesso e pendências

Jogo: http://127.0.0.1:5168/ — atualizar com Ctrl+F5 e abrir JOGOS AO VIVO no menu, ou usar #/partida. O início oficial continua abrindo a tela automaticamente. API: http://127.0.0.1:5167/api/health. PostgreSQL DEV exclusivo: 55440.

O Web próprio da 015B foi atualizado; API e PostgreSQL mantiveram seus processos. O hash do main.dart.js entregue por HTTP foi comparado com a build local (runtime-health.json).

Pendência concreta: validação/capturas reais no Chrome impedidas por “Local browser use is disabled in Work settings”. A aplicação está disponível para o teste manual do proprietário. As renderizações e os testes não foram apresentados como aprovação manual. Dados ausentes no contrato atual permanecem identificados como indisponíveis; nenhuma mudança de motor foi feita para decorar a tela.

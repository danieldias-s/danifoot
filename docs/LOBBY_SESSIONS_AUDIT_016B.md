# 016B — Auditoria de lobby e sessões

**STATUS: PARTIAL.** Funcionamento comprovado por HTTP/SignalR e testes Flutter/.NET; nova inspeção visual no Chrome ficou pendente porque a revisão automática não conseguiu autorizar o navegador após atingir o limite de uso da ferramenta. Não foram reutilizadas capturas da missão visual como prova desta auditoria.

## Causa comprovada

A sala **016B - Revisão visual**, código **ELI60A770AF**, foi criada intencionalmente com **MaxHumans = 2** por `scripts/prepare-visual-market-016b.py`, para testar comprador e vendedor. Existem exatamente dois participantes persistidos:

| Treinador | Clube | Presença no encerramento da auditoria |
|---|---|---|
| QA 016B Masculino | Flamengo RJ | Conectado |
| QA 016B Vendedor | FC Porto | Desconectado |

As contagens observadas ao fechar o relatório foram **1/2 conectados**, **2/2 vagas ocupadas**, **0/2 táticas enviadas** e **PREPARING, R1**. Presença é transitória: abrir ou fechar uma aba muda o número online, sem alterar as reservas.

Não há vagas duplicadas nem oito técnicos reduzidos globalmente a dois. O treinador vendedor foi cadastrado por HTTP durante a auditoria anterior e não tinha uma conexão de jogo ativa. O campo persistido `IsConnected` podia indicar conexão mesmo assim, porque a entrada HTTP o marcava como verdadeiro. A ocupação persistida da vaga, porém, é legítima: o vínculo e o clube continuam reservados para retorno. Uma terceira identidade é recusada corretamente nessa sala.

O problema de recuperação tinha duas causas adicionais: no Web, a identidade era guardada exclusivamente em `sessionStorage` (sobrevive à recarga, perde-se ao fechar a aba); e o login LocalDev deriva o CoachId de apelido + dispositivo. Assim, uma nova aba sem sessão ou uma edição de apelido podia gerar outra identidade e receber a mensagem genérica de lotação ao tentar retornar.

## Correções

- A sessão local continua específica de cada aba; uma cópia persistente e uma lista de perfis permitem retornar depois de fechar a aba/navegador. Um botão **NOVO TREINADOR** cria um dispositivo separado para quem quiser jogar com outro técnico. Perfis de oito abas independentes foram testados.
- O CoachId canônico retornado pela sala passa a ser usado para o SignalR e para o salvamento. O apelido não cria nova vaga ao retomar o mesmo dispositivo no **LocalDev**. Recuperação por dispositivo é restrita a esse modo; a autenticação de produção não foi ampliada.
- O lobby resolve a participação por sala, oferece **RETOMAR MINHA SESSÃO** e permite selecionar **RETOMAR TREINADOR SALVO**. Nomes iguais, isoladamente, não são suficientes para assumir uma identidade.
- Dispositivo genérico legado `dev` não é usado para assumir treinador. Se houver mais de uma identidade legada para um dispositivo, a reconexão exige a identidade original e não altera as vagas.
- Presença é calculada pelo registro de conexões ativas do servidor, separado de associação persistida. Duas conexões do mesmo técnico contam uma vez; fechar uma não desconecta a outra. Reiniciar o servidor não reaproveita sinalizadores antigos como prova de presença.
- Entrada, saída e leitura da sala compartilham a sincronização por sala. Uma conexão só pode sair do seu próprio vínculo; trocar de sala remove a presença anterior sem excluir nenhum técnico. Não houve alteração do denominador de prontidão, relógio, partidas ou progressão.
- O lobby mostra **CONECTADOS / CAPACIDADE**, **VAGAS OCUPADAS / CAPACIDADE**, **TÁTICAS ENVIADAS / NECESSÁRIAS**, **ESTADO**, lista de participantes e aviso de reserva dos desconectados.
- Sala cheia responde `ROOM_FULL` e explica a diferença entre nova entrada e retorno. Desconectar não libera automaticamente uma vaga de carreira.

## Preservação

O checkpoint visual **1e02ad2** permanece no Git e é o ancestral da branch `codex/lobby-sessions-016b`. Não houve reset, merge ou publicação. O banco 016B foi copiado antes da atualização; o dump privado fica em `artifacts/016b/lobby-audit/worlds-before.dump`. As builds anteriores também foram preservadas em `baseline-api` e `baseline-web`.

A comparação integral do mundo original, excluindo somente metadados voláteis (`updatedAtUtc`, `roundVersion`, `humanCoaches[].isConnected`), comprova que clubes, salários, jogadores, finanças, partidas, resultados, prontidão, limites e progressão continuam iguais. Capacidade 2, os mesmos dois CoachIds e os mesmos clubes foram mantidos. Não houve remoção de salas ou saves.

[Prova do estado preservado](lobby-sessions-016b/preserved-original-world.json).

## Validação efetiva

| Validação | Resultado |
|---|---|
| Flutter analyzer | Zero problemas |
| Flutter | 190/190 aprovados |
| .NET Application | 7/7 aprovados |
| .NET Domain | 425/425 aprovados |
| .NET Integration | 236/236 aprovados, incluindo 14 casos novos |
| Build Web Release | Aprovada; SHA256 `623b3f55ed981473f21e9dd31d9f243e1e952c2505bc0b739127b0a550b21513` |
| Capacidades | 1, 2, 3, 4, 5, 6, 7 e 8 por HTTP e SignalR em testes de integração |
| API local + WebSocket real | 13 verificações, sem avançar partida |
| Chrome / novas capturas | Pendente por bloqueio da revisão automática da ferramenta |

As 13 verificações no servidor local cobrem: entrar com vaga; contar presença só ao conectar; abrir duas conexões do mesmo treinador; fechar a primeira mantendo a segunda; desconectar todas sem perder vaga; retornar com apelido editado e o mesmo dispositivo; manter CoachId e clube; recusar nova identidade em sala lotada; isolar duas salas; impedir saída de sala incorreta; retornar à primeira sala; manter reservas e finalizar desconectado sem progressão.

A concorrência foi exercitada com 24 tentativas do mesmo dispositivo (um participante) e 16 dispositivos disputando duas vagas (dois admitidos). Os testes também conferem reinício do registro de presença, identidade legada por sala e rejeição de identidade não cadastrada no Hub.

Na regressão, duas fixtures antigas usavam um único DeviceId para vários clientes independentes. Foram corrigidas para fornecer dispositivos distintos; as assertivas de lesões, partidas, dívida e recuperação permaneceram. Uma execução teve timeout durante a limpeza de um banco temporário de teste; a suíte integral foi repetida e passou com **236/236**, sem ignorar casos. Os bancos de saves existentes não foram alvos da limpeza de testes.

As simulações de 100 temporadas não foram repetidas nesta auditoria: motor e economia não foram alterados. O resultado anterior pertence à missão visual e não é apresentado como nova execução.

[Resultados consolidados](lobby-sessions-016b/validation.json) · [Evidência HTTP/SignalR real](lobby-sessions-016b/live-session-evidence.json) · [Contagem da sala original](lobby-sessions-016b/original-room.json).

## Acesso para teste manual

Jogo: **http://127.0.0.1:5174/**. API: **http://127.0.0.1:5173/api/health**. O banco continua isolado em 55442. O runner da 016B continua sendo `scripts/run-local-016b.ps1 -NoBuild`.

Para retomar o treinador de testes já disponibilizado na sala **016B - Revisão visual**, usar apelido **QA 016B Masculino** e ID de dispositivo **016b-qa-male**, entrar no lobby e clicar **RETOMAR MINHA SESSÃO**. O clube é Flamengo RJ. Uma identidade nova não entra nessa sala cheia. Esses são identificadores de QA do ambiente LocalDev, não credenciais de produção.

Também foi criada a sala separada **016B - Auditoria de sessão (1 vaga)**, com o treinador **Auditoria 016B**, dispositivo **016b-lobby-browser**, Flamengo RJ, que terminou desconectado e pode retomar sua própria vaga. A sala original e o vendedor não foram liberados.

## Arquivos alterados

- Servidor: `src/Elifoot.Api/Program.cs`, `src/Elifoot.Api/Hubs/GameRoomHub.cs`, `src/Elifoot.Application/MultiplayerRoundOrchestrator.cs` e novo `RoomConnectionRegistry.cs`.
- Flutter: `lib/main.dart`, `lib/api_client.dart`, `lib/shell_actions.dart`, `lib/session_store_web.dart`, `lib/session_store_stub.dart`; novos `lib/session_storage_policy.dart` e `lib/lobby_room_status.dart`.
- Testes novos: `tests/Elifoot.Integration.Tests/LobbySessions016BTests.cs` e `client/elifoot_flutter/test/lobby_sessions_016b_test.dart`.
- Testes antigos de entrada: somente rolagem até os mesmos botões de entrada, após os novos detalhes de ocupação. Duas fixtures .NET receberam DeviceIds distintos para seus clientes independentes.
- Roteiro repetível: `scripts/audit-lobby-sessions-016b.ps1`; relatórios neste diretório. Artefatos privados, tokens, dump, builds e logs extensos permanecem ignorados no Git.

## Pendências

Repetir a inspeção visual e o fluxo manual no Chrome quando a ferramenta de navegador voltar a aceitar a revisão automática. A tentativa de abrir `http://127.0.0.1:5174/` foi recusada por limite de uso da ferramenta, não por uma conclusão de que a ação seria insegura. Nenhuma rota alternativa de automação de navegador foi utilizada. Esta limitação não impede os testes de HTTP/SignalR e de widgets executados e documentados, mas impede declarar conclusão da verificação manual.

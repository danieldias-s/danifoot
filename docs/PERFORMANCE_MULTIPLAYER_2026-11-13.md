# Auditoria de performance do Elifoot 98 Multiplayer

13 de novembro de 2026

## Escopo e método

A linha de base é o commit `2b79c36`. A comparação usou Flutter Web e API .NET em Release no mesmo Windows, Chrome 157 em modo headless e instâncias em loopback. Cada API recebeu um SQLite descartável. O ensaio HTTP executou seis salas com 128 clubes, seed 199801 e uma rodada por sala. As leituras foram repetidas 24 vezes. O navegador foi autenticado com contas descartáveis, em 1366 × 900 e 390 × 844.

O tempo de lote é o intervalo entre o início do primeiro GET da tela e o fim do último GET que completou durante a janela da amostra. Requisições de snapshot e autenticação ficaram fora dessa contagem. O tempo de lote mede trabalho de rede. O tempo de apresentação e os quadros da tela têm outros custos.

## Causas e mudanças

| Problema | Causa observada | Mudança |
|---|---|---|
| Troca de menu refazia dez leituras | Um único método aguardava classificação, taça, artilharia, mercado, ranking, calendário, clubes, plantel, finanças e convites em série | As telas pedem os dados que usam. Leituras independentes começam juntas. Cada sala e revisão reutiliza resultados e operações em andamento. A classificação usa uma entrada por divisão. |
| A classificação podia exibir linhas de outra divisão | A seleção mudava antes da resposta e a tabela usava uma lista compartilhada | A tabela exige a divisão e a revisão atuais. Respostas atrasadas têm escopo por sala, divisão e revisão. Um atraso de 180 ms ativa o indicador e a leitura expira em 15 s, com opção de tentar novamente. |
| Conteúdo piscava durante atualizações | Áreas assíncronas montavam telas vazias ou recriavam widgets ao recarregar | Mercado e finanças mantêm a superfície e bloqueiam ações sobre dados em atualização. O calendário atual usa construção sob demanda e preserva a posição por filtro. O indicador respeita a preferência de redução de movimento. |
| Rebuilds e tráfego do relógio | O HTTP reconciliava a cada 600 ms mesmo durante os ticks do SignalR | O fallback continua a 600 ms enquanto desconectado. Após atividade em tempo real, espera 3 s antes de reconciliar. O shell descarta snapshots equivalentes e separa mudanças de rodada de eventos de prontidão. |
| Decodificação repetida dos escudos | Cada build criava novos bytes para a mesma imagem base64 | Reutilização de até 128 provedores de imagem por conteúdo. Os builds Release já removiam fontes de ícones sem uso. |
| GET de carreira gravava e fazia o ranking geral mesmo sem mudanças | A leitura chamava a rotina de vagas e sempre salvava o mundo | Preserva o processamento de vagas, persiste quando a rotina altera o estado e mantém a pendência em memória se a gravação falhar. |
| Resultados podiam ficar visíveis antes da persistência e o replay regravava o snapshot | O mundo em cache era mutado antes da confirmação. O endpoint de avanço regravava quando a rodada esperada era antiga | Um checkpoint fecha o acesso a resultados pendentes. A recuperação consulta o identificador gravado para resolver um commit ambíguo ou persiste o mesmo estado em memória. A repetição de avanço confirmado é só leitura. |
| O relógio percorria salas ociosas e a preparação percorria jogadores por clube | A rotina de relógio usava a listagem geral e o avanço filtrava todo o elenco para cada clube | O relógio considera salas ativas e checkpoints pendentes. O avanço agrupa os elencos em uma passagem e preserva a ordem dos jogadores de cada clube. |

O estado oficial continua vindo do servidor. Durante a gravação do fim de partida, o SignalR sinaliza “Registrando resultado...” sem divulgar o placar final. A confirmação libera o resultado para todos. Se a gravação falha, o relógio espaça as tentativas e as leituras recebem HTTP 503 com orientação de recuperação. O avanço automático usa temporada e rodada esperadas e preserva o bloqueio existente para decisões salariais pendentes.

## Métricas

### API Release com SQLite descartável

| Operação | Amostras antes e depois | Antes p50 / p95 | Depois p50 / p95 |
|---|---:|---:|---:|
| Consultar classificação | 24 / 24 | 11,82 / 17,61 ms | 5,37 / 13,50 ms |
| Consultar plantel | 24 / 24 | 14,39 / 16,44 ms | 15,31 / 17,52 ms |
| Consultar mercado e leilões | 24 / 24 | 16,05 / 17,05 ms | 15,73 / 18,99 ms |
| Consultar calendário | 24 / 24 | 16,13 / 17,39 ms | 16,12 / 19,51 ms |
| Consultar finanças | 24 / 24 | 14,42 / 29,38 ms | 14,02 / 41,31 ms |
| Consultar carreira e convites | 24 / 24 | 117,46 / 191,79 ms | 13,42 / 16,15 ms |
| Dez leituras sequenciais | 24 / 24 | 226,98 / 331,84 ms | 125,53 / 170,18 ms |
| Dez leituras paralelas em laboratório | 24 / 24 | 108,06 / 164,97 ms | 9,57 / 31,52 ms |
| Iniciar partida pela tática | 6 / 6 | 142,76 / 230,76 ms | 155,21 / 341,41 ms |
| Tick sem checkpoint | 504 / 504 | 14,33 / 23,59 ms | 14,33 / 23,01 ms |
| Tick de checkpoint periódico | 30 / 30 | 146,29 / 203,58 ms | 158,28 / 274,80 ms |
| Encerrar e gravar resultado | 6 / 6 | 265,24 / 755,42 ms | 314,86 / 864,16 ms |
| Primeiro avanço de rodada | 6 / 6 | 129,56 / 252,04 ms | 146,37 / 257,29 ms |
| Repetir avanço confirmado | 6 / 6 | 117,46 / 203,82 ms | 6,57 / 16,90 ms |

Com seis observações, o p95 de início, fim e avanço corresponde ao máximo da amostra. O ensaio rodou no mesmo host das compilações e testes em momentos distintos. As amostras de início, fim e primeiro avanço não demonstram ganho e tiveram medianas maiores. Os principais ganhos medidos vieram da remoção de trabalho de leitura e de gravações redundantes. O snapshot inicial ainda tinha cerca de 184 kB de JSON e o final, 235 kB.

### Navegação no Chrome Release

| Operação | Requisições antes | Requisições depois | Intervalo do lote antes e depois |
|---|---:|---:|---|
| Atual, desktop, abrir classificação | 10 | 0 | 408–449 ms → 0 ms de lote |
| Atual, desktop, voltar ao plantel | 10 | 0 | 944 ms → 0 ms de lote |
| Atual, desktop, abrir mercado | 11 | 2 na primeira abertura, 1 ao voltar | 833–879 ms → 356 ms na primeira, 7 ms ao voltar |
| Atual, desktop, abrir finanças | 10 | 0 | 495–740 ms → 0 ms de lote |
| Atual, desktop, abrir calendário | 10 | 1 na primeira abertura, 0 ao voltar | 467–531 ms → 23 ms na primeira, 0 ao voltar |
| Atual, 390 px, abrir classificação | 10 | 0 | 316–417 ms → 0 ms de lote |
| Atual, 390 px, abrir mercado | 11 | 2 na primeira abertura, 1 ao voltar | 488–605 ms → 195 ms na primeira, 6 ms ao voltar |
| Atual, 390 px, voltar ao plantel | 10 | 0 | 568 ms → 0 ms de lote |
| Clássica, desktop, abrir classificação | 10 | 0 | 400–473 ms → 0 ms de lote |
| Clássica, desktop, voltar ao plantel | 10 | 0 | 590 ms → 0 ms de lote |
| Clássica, desktop, abrir mercado | 11 | 2 na primeira abertura, 1 ao voltar | 611–686 ms → 252 ms na primeira, 4 ms ao voltar |
| Clássica, desktop, abrir finanças | 10 | 0 | 436–562 ms → 0 ms de lote |
| Clássica, desktop, abrir calendário | 10 | 1 na primeira abertura, 0 ao voltar | 428–519 ms → 5 ms na primeira, 0 ao voltar |
| Clássica, 390 px, abrir mercado | 11 | 2 na primeira abertura, 1 ao voltar | 389–644 ms → 208 ms na primeira, 5 ms ao voltar |
| Clássica, 390 px, voltar ao plantel | 10 | 0 | 497 ms → 0 ms de lote |

As amostras de apresentação da classificação atual ficaram em aproximadamente 130–161 ms antes e 149–158 ms depois no desktop, e 100–225 ms antes e 133–163 ms depois a 390 px. A amostra é curta e a variação se sobrepõe. Na versão clássica, a primeira abertura do mercado levou 170 ms antes e 251 ms depois até o título aparecer. A versão anterior já desenhava a tela com dados anteriores enquanto o lote ainda estava em andamento. O GET restante ao revisitar o mercado pertence às observações de jogadores.

## Arquivos

- Flutter e apresentação — `api_client.dart`, `main.dart`, `room_data.dart`, `classic98_shell.dart`, `shell_actions.dart` e `club_crest.dart`.
- API e sincronização — `AccountLogin.cs`, `Program.cs`, `GameRoomHub.cs` e `RoomClockBackgroundService.cs`.
- Aplicação, domínio e persistência — `Interfaces.cs`, `MultiplayerRoundOrchestrator.cs`, `MatchInteractionCommands.cs`, `ClubAndMarketApplicationService.cs`, `CoachCareerEngine.cs`, `MatchInteractions.cs` e `EfWorldRepository.cs`.
- Regressões — `complete_gameplay_011_test.dart` e `MatchInteractionPersistenceTests.cs`.

## Validação e pendências

- 53 testes Flutter passaram, incluindo as apresentações atual e clássica, larguras de 320 a 1920 px, navegação, classificação, finanças, contratos, pausas e o fluxo automático da rodada. A nova regressão simula uma resposta fora de ordem e uma falha de leitura com tentativa de recuperação.
- 70 testes de aplicação passaram. A seleção de 19 testes de avanço e contratos também passou após o agrupamento dos elencos.
- O ciclo existente de API/SignalR com 2, 4, 6 e 8 clientes passou nos oito cenários executados. Os nove testes SQLite selecionados passaram. Os cenários novos usam dois clientes e falhas antes e depois do commit, verificam o bloqueio das leituras, a inclusão da sala no relógio enquanto há pendência e a ausência de nova escrita após o avanço confirmado.
- Builds Flutter Web Release e API Release foram compilados. A análise estática Dart dos arquivos modificados ficou sem diagnósticos.
- Faltam métricas de tempo de quadro, memória e rolagem em Android/iOS físicos. Os testes móveis usaram viewport de navegador e testes Flutter. Também faltam medição isolada de renovação contratual e ensaios longos de latência sob várias salas ativas.
- O checkpoint pendente vive em memória até gravar. Se o processo cair antes da gravação, o reinício recupera o último checkpoint persistido. O código não altera o esquema SQLite e mantém o intervalo de checkpoints existente. A causa física do erro histórico `SQLITE_IOERR` permanece indeterminada.
- Todos os ensaios usaram bancos descartáveis. A instância e os saves existentes ficaram fora do ambiente de testes.

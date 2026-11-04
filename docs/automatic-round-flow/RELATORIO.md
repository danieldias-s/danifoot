# Fluxo automático da rodada — duas aparências

Entrega local em 04/11/2026. Branch isolada `codex/automatic-round-flow`, no worktree `D:/elifoot_98/.worktrees/account-login-lobby`. Base preservada: `0001c41`. Checkpoint de implementação: `e22a9ea`. Sem merge ou publicação.

## Funcionamento

| Situação | Comportamento |
|---|---|
| Um humano envia a tática | O servidor inicia a rodada; a interface abre a partida automaticamente. |
| Parte dos humanos enviou | As duas aparências mostram a espera e o contador. Um contador isolado não antecipa o início. |
| Todos enviaram | O snapshot de partida iniciada abre a rodada em todos os clientes. |
| Partida em andamento ou pausada | Menus, navegação inferior, aparência e troca de sala ficam ocultos. Rotas internas e Voltar continuam bloqueados. Substituições, intervalo e escolha de cobrador usam os controles existentes. |
| Liga concluída | Abre a classificação da divisão do clube e atualiza os dados do servidor. |
| Taça concluída | Volta à tela inicial do treinador/plantel. |
| Reconexão | Restaura a partida bloqueada se ainda estiver em andamento; se concluída, abre o destino correspondente. |

A passagem para a próxima rodada continua pelo botão existente, após consultar a classificação ou voltar ao treinador. Não é criada outra rodada enquanto o usuário está vendo o encerramento. Snapshots repetidos não forçam navegação novamente.

## Causa e correções

O servidor já verificava todos os humanos e iniciava a rodada atomicamente. O problema de apresentação era manter menus visíveis, embora desativados, e direcionar RESULTS/FINISHED de volta ao placar. O coordenador agora distingue encerramento da liga e da taça; o listener usa o bloqueio já aceito pelo roteador durante a transição.

A validação encontrou ainda um menu móvel aberto que podia reaparecer após o jogo. O conteúdo fica oculto durante a partida, preservando o controlador necessário para fechar o drawer corretamente. Também foi removida da seleção local qualquer reserva que já não pertença ao elenco recebido do servidor; isso evita enviar um ID antigo na próxima rodada sem alterar jogadores ou transferências.

A mudança visual da tela de partidas clássica foi deixada para a etapa seguinte, conforme solicitado. Não foram alterados backend, MatchEngine, evolução, economia, contratos, regras ou sincronização multiplayer.

## Testes e evidências

| Verificação final | Resultado |
|---|---|
| [Flutter analyze](analyze.log) | Sem problemas |
| [Fluxo e navegação móvel](flow-tests.log) | 16 testes aprovados |
| [Regressão Flutter completa](regression.log) | 359 testes aprovados |
| [Build Web release](build.log) | Compilado com êxito |
| Navegador com API/PostgreSQL reais | Uma sala de um humano e outra de dois humanos, com aparências diferentes, foram verificadas |

A matriz automatizada cobre um/dois humanos, duas aparências, 390/1366 px, menus ocultos, URL/Voltar, pausa, destino da liga, destino da taça, snapshots repetidos e reserva que saiu do elenco. Todas as fases de taça e o estado FINISHED são cobertos no coordenador.

No navegador, a sala de dois humanos ficou PREPARING em 1/2 e abriu a partida nas duas telas em 2/2. Duas rodadas da liga abriram classificação; a rodada 3, CupRoundOf64, terminou aos 120 minutos e voltou ao treinador nas duas aparências. A reconexão restaurou uma decisão de pênalti pendente; após a taça, o reload preservou o destino do treinador. [evidence.json](evidence.json) registra estados, rotas e IDs das salas de QA, sem credenciais. Decisões de pênalti assistidas por API ocorreram somente nessas salas de teste.

Nenhum save do proprietário foi usado para jogar ou avançar rodadas. A validação móvel foi Web responsiva em Chromium, sem telefone físico.

## Capturas

- Um humano: [partida](um-humano-partida-atual.png), [classificação automática](um-humano-classificacao.png).
- Dois humanos: [aguardando 1/2](dois-humanos-aguardando.png), [partida atual](dois-humanos-partida-atual.png), [partida clássica mobile](dois-humanos-partida-classica-mobile.png).
- Fim da liga: [atual](dois-humanos-classificacao-atual.png), [clássica](dois-humanos-classificacao-classica.png).
- Fim da taça: [treinador atual](taca-retorno-treinador-atual.png), [treinador clássico](taca-retorno-treinador-classica.png).

## Como testar

Abra [a prévia local](http://127.0.0.1:5242/), atualize com F5 e retome sua sala. Envie a tática: com um humano, a partida abre assim que o servidor inicia; com mais humanos, aguarde todos enviarem. Ao terminar, confira o destino automático conforme a competição. A prévia já serve o build final.

# Classificação por 5 segundos e preparação automática

Entrega local em 04/11/2026. Branch `codex/post-round-auto-preparation`, worktree `D:/elifoot_98/.worktrees/account-login-lobby`. Base preservada: `cb505ce`. Checkpoint de implementação: `8ae9dd3`. Sem merge ou publicação.

## Resultado

- Na liga, o fim da rodada abre a classificação. Após **5 segundos**, volta à tela inicial do treinador.
- Na taça, volta diretamente ao treinador.
- A preparação da rodada seguinte é solicitada automaticamente. A escalação vem do servidor e o envio de tática fica disponível quando essa preparação termina.
- O botão **AVANÇAR PRÓXIMA RODADA** foi removido das duas aparências.
- A espera pelos demais humanos e o bloqueio da navegação durante as partidas continuam funcionando.

A classificação permanece pelo intervalo completo mesmo quando outro treinador recebe a preparação antes. Snapshots repetidos não reiniciam o temporizador. Sair da sala cancela o temporizador; uma falha de conexão tenta novamente sem introduzir um botão manual.

## Sala compartilhada

Foi reaproveitada a operação existente de preparar a próxima rodada. O cliente envia a temporada e a rodada que acabou de concluir. O servidor confere esses valores sob o lock já existente da sala: pedidos simultâneos preparam apenas uma rodada; um pedido atrasado não pode avançar uma rodada posterior que já tenha terminado. Chamadas legadas continuam compatíveis.

A API e a Web da prévia foram atualizadas. A API usa os mesmos bancos locais existentes. Não foram modificadas fórmulas do MatchEngine, evolução, economia ou contratos; a operação normal de passagem de rodada agora ocorre automaticamente.

## Validação final

| Verificação | Resultado | Evidência |
|---|---|---|
| Flutter analyze | Sem problemas | [analyze.log](analyze.log) |
| Fluxo, temporizador, falha de conexão e saída | 14 testes aprovados | [flutter-focused.log](flutter-focused.log) |
| Regressão Flutter completa | 364 testes aprovados | [flutter-regression.log](flutter-regression.log) |
| Regressão de aplicação .NET | 19 testes aprovados | [application-tests.log](application-tests.log) |
| Build Web e API | Compilados com êxito | [web-build.log](web-build.log), [api-build.log](api-build.log) |

Os testes de aplicação incluem oito pedidos simultâneos e um pedido atrasado para uma rodada anterior, além da separação entre temporadas. Os testes Flutter cobrem um/dois humanos, as duas aparências, 390/1366 px, repetição de snapshots, cinco segundos com relógio controlado e preparação por outro treinador.

No navegador, foram criadas duas contas e uma sala exclusiva de QA. Duas rodadas da liga e uma de taça foram jogadas. Foram observados seis pedidos automáticos, dois por rodada concluída, e a sala chegou à preparação da rodada 4 com os dois botões de envio habilitados. Nenhum avanço manual foi usado.

Tempos entre a navegação para classificação e o retorno ao treinador, medidos pelo Chromium: clássica 4.997 e 4.993 ms; atual 5.152 e 5.216 ms. São medições aproximadas de eventos do navegador; o temporizador usa exatamente 5 segundos. [evidence.json](evidence.json) registra esses eventos, os pedidos e o estado final, sem credenciais. Não houve erro JavaScript observado.

A validação móvel foi Web responsiva; nenhum telefone físico foi utilizado. Nenhum save do proprietário foi usado para jogar ou avançar partidas de QA.

## Capturas

- Rodada seguinte pronta antes da taça: [atual mobile](treinador-pronto-atual.png), [clássica desktop](treinador-pronto-classica.png).
- Após a taça, rodada 4 pronta para escalar/enviar: [atual mobile](apos-taca-pronto-atual.png), [clássica desktop](apos-taca-pronto-classica.png).

## Como testar

Atualize [a prévia](http://127.0.0.1:5242/) com F5 e retome sua sala. Envie a tática normalmente. No fim da liga, aguarde os cinco segundos; no fim da taça, o retorno é direto. A próxima escalação fica disponível sem clicar em avançar rodada.

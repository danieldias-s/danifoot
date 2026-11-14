# Multiplayer · Etapa 5.1 · Confiabilidade e recuperação

**PARTIAL.** O reinício forçado com a fila cheia passou em duas execuções isoladas. Os widgets iniciaram e concluíram uma rodada nos dois temas sobre PostgreSQL. Parte do roteiro de carreira e a medição prolongada com vários saves históricos ainda estão abertas. A verificação executada permitiu prosseguir para a Etapa 6.

## Fila cheia e reinício

PostgreSQL 18.4 descartável em loopback. Seis ligas de 64 clubes, um treinador sem clube por liga, um trabalhador e uma vaga de fila. O preparador gravou os seis comandos de prontidão com o relógio desabilitado. A API seguinte assumiu a autoridade. Uma transação de laboratório permitiu leituras e bloqueou uma gravação no PostgreSQL. O processo foi encerrado quando a API mostrava seis mundos ativos, um trabalho em execução, um na fila e um escritor aguardando lock no banco.

Na primeira execução a API voltou em 3,16 s. As seis ligas recuperaram suas partidas, terminaram de 50,9 a 54,3 s após o início da retomada e produziram 96 confrontos com IDs distintos. A versão mudou de época no SignalR e o cliente voltou à sua liga. Reenviar a prontidão com a mesma chave retornou `roundStarted=false`. Dois pedidos simultâneos de próxima rodada devolveram a mesma versão e avançaram uma única vez. No reteste instrumentado o driver também exigiu igualdade entre a versão exposta no resultado e a versão durável de cada sala. Os limites de fila e concorrência foram respeitados, com zero falhas do relógio após a retomada. [Primeira execução](../artifacts/stage6-run/recovery1/result.json) · [Reteste instrumentado](../artifacts/stage6-run/recovery2/result.json)

O canal em memória contém IDs e dados de agendamento. Após reiniciar, o relógio consulta os mundos em estados persistidos de jogo ou pausa, carrega o último checkpoint e agenda novo trabalho por mundo. Um tick cujo commit ficou pendente pode ser reexecutado a partir desse ponto. A confirmação ao treinador e o resultado definitivo dependem da gravação. Esta execução cobre reinício enquanto há trabalho aguardando e um checkpoint de tick em andamento. A injeção de falha imediatamente antes e depois do commit do resultado está na regressão PostgreSQL. [Driver da recuperação](audit-scalability/stage51-recovery.mjs) · [Integração PostgreSQL](../artifacts/stage6-run/integration-tests-postgres.log)

## Memória com histórico

Fixture de auditoria copiada, validada por SHA-256 e importada para outro banco descartável. O cenário principal tinha dez temporadas completas, 117.940.134 bytes de JSON após a descompressão na migração e iniciou na temporada 11. Foram exercitadas quatro rodadas, incluindo uma de 120 minutos virtuais, com navegação concorrente e 60 s em repouso. As quatro versões de resultado eram duráveis. As dez temporadas históricas continuavam no banco e o resultado da quarta rodada reabriu após a expulsão do cache. [Importação](../artifacts/stage6-run/aged-migration.log) · [Amostras](../artifacts/stage6-run/aged-result.json) · [Contagem histórica](../artifacts/stage6-run/aged-history-after.txt)

| Ponto da execução | RSS | Dados adicionais |
| --- | ---: | --- |
| Inicial, antes da primeira leitura | 141 MiB | Heap de 8 MiB na última coleta |
| Pico amostrado | 2.402 MiB | LOH de 1.489 MiB na última coleta |
| Fim da quarta rodada | 2.145 MiB | 25 coletas de geração 2 acumuladas |
| Após 60 s sem atividade, TTL de 25 s | 2.137 MiB | Zero mundos em cache forte, uma entrada de identidade fraca, mesmas 25 coletas de geração 2 |

A execução registrou aproximadamente 13,8 GiB de alocação gerenciada entre a primeira leitura e a amostra final em repouso. Cada rodada levou de 54,5 a 70,1 s. A expiração removeu a referência forte e o RSS recuou aproximadamente 9 MiB em repouso. Como a geração 2 permaneceu no mesmo contador durante o repouso, a amostra não distingue memória mantida pelo runtime, objetos ainda vivos ou retenção do provedor. Quatro rodadas de um mundo dão evidência sobre pressão de memória, com validação prolongada de múltiplos históricos e captura pós-GC ainda pendentes. Nenhuma retenção específica foi corrigida sem atribuição. [Driver do histórico](audit-scalability/stage51-aged.mjs)

## Latência e navegação

O p95 de 35,27 s da Etapa 5 foi medido em 100 ligas com janelas de dez partidas. Na repetição instrumentada com a mesma janela, o limite de RAM livre interrompeu o lote em 50 conclusões. O p95 dos 50 resultados foi 31,66 s e o HTTP p95 dos 260 pedidos que responderam foi 270,17 ms. Dez fluxos em andamento foram interrompidos pelo limite. No ensaio exploratório com 100 partidas iniciadas de uma vez, o driver atingiu o limite de 90 s antes de observar um resultado. A capacidade comercial para esse grau de simultaneidade permanece em aberto. [Carga anterior](../artifacts/stage5-run/clock-100-3/results.json) · [Repetição parcial](../artifacts/stage6-run/latency100b/results.json) · [100 partidas disparadas](../artifacts/stage6-run/latency100/results.json)

O controle de dez ligas concluiu 10/10 em p50/p95/p99 de 25,94/26,71/26,71 s, com três trabalhadores, pico de fila 8 e 900 ticks. Nos contadores instrumentados, por tick, houve 13,09 ms de espera após admissão, 2,71 ms de simulação, 10,26 ms de persistência e 1,44 ms para criar e publicar a projeção no servidor. A persistência respondeu por 68% do tempo medido de execução dos trabalhadores. No histórico, a persistência subiu para 324 ms/tick e a simulação para 6,62 ms/tick. A fase de persistência inclui a pequena notificação de checkpoint pendente no tick final. A sincronização medida no servidor termina ao publicar a projeção. No cliente, a janela entre o aviso de checkpoint e o resultado chegou a 293 ms no controle de dez, incluindo gravação e transporte. Os componentes são médias agregadas. O p95 ponta a ponta inclui cadência do relógio, espera antes da admissão, a barreira de pausa, leituras, rede e reconciliação. [Controle de dez](../artifacts/stage6-run/latency10b/results.json)

| HTTP durante a rodada | p95, controle de 10 | p95, janela de 10 no lote parcial de 100 |
| --- | ---: | ---: |
| Prontidão sem clube | 281 ms | 404 ms |
| Calendário | 93 ms | 31 ms |
| Clubes | 93 ms | 25 ms |
| Classificação | 27 ms | 16 ms |

No controle, clubes e classificação mediram 11/10 ms no p95 durante a fase sem partidas. Durante o jogo, apareceu uma cauda de até 93 ms nessas leituras, com amostras menores. A prontidão é a operação que melhor explica a cauda do agregado HTTP, inclusive no lote parcial. A amostra de navegação consulta a liga em jogo. Continua pendente uma comparação pareada com treinadores de mundos ociosos durante uma carga maior.

## Flutter e PostgreSQL

Dois clientes Flutter com sessões e preferências distintas receberam 91 minutos e a mesma versão durável 104. O cenário de transporte exercitou prontidão, intervalo, pênalti, reconexão, mercado, leilão, contrato, carreira, repetição de avanço e classificação. A suíte .NET filtrada aprovou 32 casos e ignorou um caso HTTPS comercial. Ela inclui persistência de substituição, pênalti e recuperação de checkpoint. [Flutter PostgreSQL](../artifacts/stage6-run/flutter-postgres-after.log) · [Integração PostgreSQL](../artifacts/stage6-run/integration-tests-postgres.log)

No Chrome Release, os dois temas retomaram a sessão, abriram a liga e enviaram a tática inicial. O treinador abriu a pausa e mudou de `3-5-2` para `4-4-2` pelo widget nos dois temas. O servidor confirmou cada mudança e gravou o resultado na versão 98. Os dois navegadores exibiram a classificação e o aviso de rodada concluída; em seguida chamaram `next-round` com HTTP 200. Uma leitura independente confirmou a rodada 2 em `PREPARING`, já na versão durável 99. As capturas da liga de 64 clubes registraram gols, cartões (incluindo expulsões), lesões e substituições no estado da rodada. Esses totais incluem partidas de IA e não comprovam a exibição de cada lance. As transições de promoção e demissão pelos widgets permanecem abertas. [Partida Clássico](../artifacts/stage6-run/widgets-final-CLASSIC_98.json) · [Partida Atual](../artifacts/stage6-run/widgets-final-CURRENT.json)

Num ensaio adicional, os dois navegadores enviaram pelo painel uma substituição do clube humano, com HTTP 200. Uma consulta independente ao PostgreSQL confirmou no histórico da rodada 1, pela equipe do treinador, a entrada de Caio Andrade no lugar de Lucas Carvalho no Atual (minuto 21) e no Clássico (minuto 23). A rodada seguinte permaneceu na versão durável 100 nos dois saves sintéticos. No reteste do Clássico, o driver também conferiu os dois IDs no próprio evento assim que a substituição foi aceita. [Captura Atual](../artifacts/stage6-run/widgets-sub-CURRENT.json) · [Reteste Clássico](../artifacts/stage6-run/widgets-proof-CLASSIC_98.json) · [Registros no PostgreSQL](../artifacts/stage6-run/widgets-sub-persisted.txt)

## Reprodução

Criar um banco vazio `audit_stage51_*` no cluster de auditoria de [REPRODUZIR.md](audit-scalability/REPRODUZIR.md), porta 54983, usuário `audit`. Executar `node docs/audit-scalability/stage51-recovery.mjs API.dll pasta-nova audit_stage51_nome caminho/psql.exe`. No histórico, usar a cópia fechada e o importador verificado da Etapa 4.1, iniciar uma API LocalDev separada na porta 54985, com autoridade exclusiva, métricas habilitadas e TTL de 25 s, e executar `node docs/audit-scalability/stage51-aged.mjs ID_DA_LIGA_SINTETICA arquivo.json`. Para carga, usar os parâmetros da Etapa 5, acrescentar `AUDIT_HUB_STANDINGS=1` e executar o `load.mjs` atualizado. O medidor da aplicação `Elifoot.Multiplayer.Clock` expõe as três fases do tick. O endpoint de auditoria conserva seu controle de acesso anterior.

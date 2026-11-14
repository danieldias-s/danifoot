# Elifoot 98 Multiplayer · Etapa 7

**NO-GO para implantação.** O staging local sustentou 500 conexões WSS ociosas por 15 s. Ao navegar com esse volume de usuários, o p95 chegou a 8,11 s. Os testes de partidas cobriram menos mundos ativos que as sessões de rede e a validação em hardware móvel continua pendente.

| Fase | Estado | Evidência e limite |
| --- | --- | --- |
| 7A · Persistência | PARTIAL | A conexão PostgreSQL é aberta após serializar nos checkpoints normais. Dez rodadas passaram em cada build. A mudança reduziu o tempo observado de transação, com p95 da rodada maior em uma amostra. |
| 7B · Carreira e partida | PARTIAL | Atual e Clássico confirmaram convite, troca de clube e estado sem clube em Chrome sobre PostgreSQL após reinício. Os estados de carreira foram produzidos por uma fixture controlada do motor. |
| 7C · Histórico e recuperação | PARTIAL | Dez rodadas em 15 minutos com dez temporadas. Fila cheia recuperada em seis mundos. Faltam históricos de idades diferentes em paralelo e atribuição da memória após a expiração. |
| 7D · Jogadores | PARTIAL | 100 e 500 conexões WSS em staging loopback com TLS validado. O tempo de navegação a 500 ultrapassou o limite de escalada. 1.000 ficaram fora do ensaio. |
| 7E · Mundos | PARTIAL | 10/10 rodadas por build em cinco partidas concorrentes. 100 mundos provisionados num lote, com dois mundos inscritos no hub. A Etapa 5 fechou 50 e 100 rodadas de mundos novos em janelas de dez. |
| 7F · GPU, mobile e rede | PARTIAL | Duas partidas recuperaram o snapshot após interrupção de 3 s e atraso injetado. GPU física, celular e perda de pacotes continuam pendentes. |
| 7G · Dimensionamento | PARTIAL | Orçamentos de homologação e limites iniciais ancorados nos testes. Faixas de 500 e 1.000 precisam de validação em hardware dedicado. |
| 7H · Operação | PARTIAL | TLS no ingresso, revogação, isolamento, limitação de cadastro, autoridade exclusiva e restauração de banco testados. O enlace ao banco, retenção, PITR e operação com réplicas precisam de validação. |

Os ensaios usaram um Windows com 20 CPUs lógicas, 15,9 GiB de RAM e PostgreSQL 18.4 no mesmo host. Dados de desempenho dependem da atividade concorrente no host. O checkpoint preserva 79 arquivos locais com SHA-256 conferidos. Os testes de escrita usaram bancos novos do cluster de auditoria e uma cópia verificada de fixture histórica. [Checkpoint](../artifacts/stage7-checkpoint/sha256.csv) · [Etapa 5](MULTIPLAYER_ETAPA_05.md) · [Etapa 5.1](MULTIPLAYER_ETAPA_051.md) · [Etapa 6](MULTIPLAYER_ETAPA_06.md)

## 7A · Persistência

O repositório serializava `GameWorld` dentro da transação de autoridade. Agora prepara o mesmo JSON antes de abrir a conexão nos checkpoints comuns em PostgreSQL. A operação continua dentro do semáforo do repositório. O controle de autoridade, a conferência de versão, o `SaveChanges` e o commit mantêm a ordem. Uma tentativa após commit ambíguo primeiro consulta o checkpoint anterior. SQLite conserva a seleção de formato existente. O meter `Elifoot.Multiplayer.Persistence` distingue serialização, hidratação, tempo com transação aberta, preparação de ledger/histórico, `SaveChanges` e commit. O tempo de transação inclui espera por conexão e fencing. O endpoint de auditoria só é registrado em `LocalDev` com opção explícita. [Repositório](../src/Elifoot.Infrastructure/EfWorldRepository.cs) · [Métricas](../src/Elifoot.Infrastructure/WorldPersistenceMetrics.cs)

Os builds de referência e final possuem a mesma instrumentação. Um lote por build, três trabalhadores, fila de 64, 10 mundos novos de 64 clubes, até cinco em partida e um observador V2 por mundo. `fsync` e `synchronous_commit` estavam ativos. Referência executada primeiro. CPU e RSS são da API. I/O é do volume D:, incluindo outros processos.

| Medida na fase de rodadas | Referência | Alterado |
| --- | ---: | ---: |
| Resultados duráveis | 10/10 | 10/10 |
| Rodada p50 / p95 / p99 | 28,10 / 28,85 / 28,85 s | 25,93 / 30,14 / 30,14 s |
| HTTP de prontidão p50 / p95 / p99 | 309 / 686 / 686 ms | 123 / 285 / 285 ms |
| CPU API / pico RSS | 17,42 CPU-s / 416 MiB | 18,42 CPU-s / 434 MiB |
| Disco médio / pico | 12,3 / 218 IOPS | 18,7 / 86 IOPS |
| Disco médio / pico | 2,49 / 25,26 MiB/s | 2,75 / 22,49 MiB/s |
| Espera após admissão / simulação por tick | 12,18 / 2,70 ms | 5,59 / 2,44 ms |
| Persistência / sincronização por tick | 34,05 / 0,97 ms | 24,42 / 0,86 ms |
| Serialização por gravação | 22,82 ms | 22,36 ms |
| Conexão, transação e fencing por commit contado | 539,0 ms | 360,7 ms |
| SaveChanges por gravação / commit por commit contado | 81,56 / 427,73 ms | 82,38 / 237,89 ms |
| Escritas de snapshot após o provisionamento | 90 | 90 |
| Bytes recebidos em mensagens SignalR | 12.090.485 | 12.090.485 |

Os contadores de fase são cumulativos, com sobreposição entre trabalhadores e entre transação e commit. Houve 88 commits na janela de referência e 90 na alterada. Duas gravações estavam em execução no corte da primeira amostra. A queda observada na fase PostgreSQL foi bem maior que os 23 ms de serialização deslocados. A diferença de commit, RSS e cauda de rodada pede repetição alternada em host dedicado para atribuição causal. Em uma tentativa separada com dez partidas, um trabalhador e três rodadas por mundo, o driver atingiu seu limite por rodada após receber resultados da primeira rodada. Cinco partidas em paralelo constituem o limite observado no lote comparável, com três trabalhadores. [Referência](../artifacts/stage7-run/pair-before/results.json) · [Alterado](../artifacts/stage7-run/pair-after/results.json) · [Um trabalhador](../artifacts/stage7-run/before/results.json) · [PostgreSQL](../artifacts/stage7-run/pg-counters.txt)

As 90 serializações somaram cerca de 269,2 milhões de caracteres em cada build. Os dez snapshots finais tinham 35,9 MB em ambos, com diferença de seis bytes. Cada checkpoint ainda atualiza `ef_worlds` e seu TOAST. As leituras do cabeçalho e da hidratação usam `AsNoTracking`. O EF anexa uma linha para atualizar o snapshot, e em `PREPARING` e `RESULTS` busca IDs do ledger e do histórico para deduplicação. Essa preparação ocorreu 30 vezes no lote, em média 4,11 → 3,25 ms. O tempo isolado de atualização de índices e contenção do pool ficou em aberto. A maior parcela medida no PostgreSQL foi o commit. Logs de conexão e `pg_stat_database` agregam outras fases e não separam o custo de cada índice ou de WAL por checkpoint. [Tamanho antes](../artifacts/stage7-run/storage-before.txt) · [Tamanho depois](../artifacts/stage7-run/storage-after.txt)

Ingresso e saída do SignalR gravam presença em certos estados mesmo quando a presença externa é definida pelo registro de conexões. Outros saves com a mesma versão ainda podem conter recibos, alterações de mercado ou preparação. Uma supressão genérica por versão pode perder alterações. Separar presença e histórico quente do JSON e medir reaproveitamento de seções fechadas são caminhos para reduzir regravações. Requerem teste de compatibilidade de snapshots e verificação de recuperação. A Etapa 3.1 detalha o perfil isolado de codificação, índices e Gzip opcional. [Etapa 3.1](MULTIPLAYER_ETAPA_031.md)

## 7B · Carreira e partida

O Chrome Release abriu o save sintético em ambos os temas após reiniciar a API `AccountTest`. A fixture preparou a vaga com as funções `OpenVacancy` e `ProcessVacancies` do motor e gravou o estado no PostgreSQL. Os dois treinadores viram o convite e clicaram em Aceitar. A API devolveu 200 e o snapshot confirmou Grêmio Guanabara como novo clube na versão durável 5. No teste separado de demissão, `CoachCareerEngine.Dismiss` gravou o desligamento. Após reiniciar a API, o Atual exibiu “TREINADOR SEM CLUBE” e o Clássico “Liga Clássica 98 — Treinador sem clube” na versão durável 4. A demissão foi preparada entre partidas. A apresentação do evento no momento exato de uma derrota e a classificação na transição de temporada ainda precisam de ensaio ponta a ponta. [Convite Atual](../artifacts/stage7-run/career-restart-invitation-CURRENT.json) · [Convite Clássico](../artifacts/stage7-run/career-verified-invitation-CLASSIC_98.json) · [Demissão Atual](../artifacts/stage7-run/career-restart-dismissal-CURRENT.json) · [Demissão Clássico](../artifacts/stage7-run/career-restart-dismissal-CLASSIC_98.json)

A Etapa 5.1 confirmou pelos widgets início de rodada, pausa, tática, substituição, classificação e avanço nos dois temas. O servidor registrou lesões, cartões e expulsões no estado da rodada. A visualização de cada lance do clube humano durante uma partida segue pendente. [Partidas no Flutter](MULTIPLAYER_ETAPA_051.md)

## 7C · Histórico e recuperação

A cópia SQLite sintética passou na conferência de SHA-256 e foi importada para PostgreSQL descartável. A liga principal, de 128 clubes, tinha dez temporadas fechadas e começou na temporada 11. Dez rodadas seguidas concluíram e foram verificadas pela versão durável. Ao longo de 15 minutos, houve 990 ticks instrumentados e zero falhas do relógio. A espera após admissão foi de 0,03 ms/tick em média com um mundo ativo. Simulação, persistência e sincronização mediram 5,30, 426,90 e 0,87 ms/tick. As rodadas mediram p50/p95/p99 de 68,16/115,77/115,77 s. Com dez observações, p95 e p99 recaem no maior valor. Outros ensaios compartilharam o host em parte da série.

| Medida no save de dez temporadas | Observado |
| --- | ---: |
| RSS inicial / pico / após 60 s em repouso | 109 / 2.391 / 1.962 MiB |
| Cache forte no encerramento / após repouso | 1 / 0 |
| Gen 2 no encerramento / após repouso | 71 / 71 |
| Alocação gerenciada acumulada ao fim do repouso | 37,34 GiB |
| Serializações / média por gravação / caracteres | 85 / 564 ms / 10,58 bilhões |
| Hidratação fria inicial | 4,41 s |
| `SaveChanges` / commit médios | 6.139 / 234 ms |
| Espaço físico da base antes / depois | 56,0 MiB / 2,26 GiB |
| Temporadas registradas no início / fim | 10 / 10 |

O aumento físico inclui páginas e versões MVCC do mesmo mundo durante a regravação e não mede consumo por temporada. O cache forte expirou e a leitura seguinte reabriu a última versão durável. O RSS permaneceu perto de 2 GiB após a expiração. A ausência de nova coleta gen 2 nesse intervalo deixa a retenção sem atribuição. Essa série mede operação sustentada curta de um save grande. Faltam um ensaio longo com vários saves de idades diferentes e correlação de heap, PostgreSQL e SO sob carga dedicada. [Importação verificada](../artifacts/stage7-run/history-migration.log) · [Série histórica](../artifacts/stage7-run/history-ten.json) · [Banco inicial](../artifacts/stage7-run/history-before.txt) · [Banco final](../artifacts/stage7-run/history-after.txt)

A amostra separada de uma temporada fechada tinha 24,0 MB de JSON. Quatro rodadas duráveis levaram p50/p95/p99 de 30,64/39,41/39,41 s. A hidratação fria levou 1,32 s. O RSS começou em 110 MiB, chegou a 935 MiB e ficou em 800 MiB após 60 s, com cache forte zero e reabertura confirmada pela versão. O banco ocupava 15,9 MiB antes e 149,3 MiB depois. A diferença de idade e o horário dos testes influenciam a comparação. [Série de uma temporada](../artifacts/stage7-run/history-one.json) · [Banco inicial](../artifacts/stage7-run/history-one-before.txt) · [Banco final](../artifacts/stage7-run/history-one-after.txt)

O reteste de recuperação encheu a fila de uma vaga com um trabalhador e seis mundos. O processo caiu após confirmar prontidão durável e com uma gravação aguardando o banco. A API voltou em 3,67 s e os seis mundos completaram entre 58,53 e 61,95 s da retomada, com 96 confrontos distintos, versão durável e avanço único após repetição de comandos. A primeira tentativa desta etapa parou no timeout de consulta de provisionamento. [Reteste](../artifacts/stage7-run/recovery-retry/result.json)

## 7D e 7E · Jogadores e mundos

Staging loopback com modo `Account`, proxy TLS de laboratório, cadeia do certificado verificada pelo cliente e WebSockets autenticados. Cinco contas por mundo. Cada navegador sintético fez um GET de navegação e um de mercado, com dispersão de chegada. O cronômetro da fase inclui filas do gerador. Percentis calculados sobre operações concluídas. Dez pessoas entraram em dois mundos. As demais mantiveram o socket autenticado aberto na fase ociosa. A reconexão teve rajada de até 20 em voo e fechou 100 sockets no maior teste.

| Métrica, p50 / p95 / p99 | 100 conectados e 20 mundos | 500 conectados e 100 mundos |
| --- | ---: | ---: |
| Conexão WSS | 91 / 122 / 215 ms | 81 / 114 / 153 ms |
| Navegação | 7 / 23 / 69 ms | 11 / 8.111 / 10.540 ms |
| Leitura de mercado | 5 / 8 / 10 ms | 7 / 1.388 / 1.951 ms |
| Pico de 100 reconexões | 46 / 2.582 / 3.019 ms | 54 / 926 / 1.109 ms |
| Navegações concluídas e taxa | 100/100 · 134,5/s | 500/500 · 21,0/s |
| Mercado, consultas concluídas e taxa | 100/100 · 89,7/s | 500/500 · 42,9/s |
| Payload HTTP de navegação e mercado | 4,45 MB | 22,30 MB |

As execuções TLS salvaram latências, bytes de aplicação e RAM livre inicial do host (4,24 GiB no lote de 100 e 2,80 GiB no reteste de 500). CPU, RSS e IOPS exclusivos por fase TLS ficaram sem coleta. CPU, RSS e disco comparáveis desta etapa vêm da carga de dez mundos da fase 7A.

O primeiro lote de 500 cadastrou 490 e recebeu dez HTTP 429 por reutilizar uma origem do lote anterior. O reteste distribuiu os cadastros por faixas de loopback separadas e manteve a limitação ativa. Os 100 mundos levaram 192 s para provisionar, com p95 de 10,32 s por grupo de cinco contas. O protocolo de sala confirmou mundo correto e revogação de sessão. O lote misto de 100 executou duas operações de anúncio e retirada e iniciou duas partidas com 2,3 s de diferença. Um fluxo completou e avançou, e o outro obteve resultado e recebeu HTTP 409 ao avançar. O código de avanço devolve 409 para transição salarial pendente. Os 500 ficaram restritos a navegação, consultas de mercado e ingresso em dois mundos. A cauda de 8,11 s na navegação ultrapassou o limite de 2 s adotado na auditoria anterior. A degradação passou a ser visível antes do teto de sockets. [100 conexões](../artifacts/stage7-run/players-good-100.json) · [500 conexões](../artifacts/stage7-run/players-retry-500.json) · [429](../artifacts/stage7-run/players-good-500.json) · [Lote misto com partidas](../artifacts/stage7-run/players-play-100.json)

Na Etapa 7 os grupos de carga usaram mundos novos. Os sinais de maior custo de histórico vêm do ensaio 7C. Os 50 e 100 mundos em rodadas da Etapa 5 completaram em janelas de dez, com três trabalhadores, p95 de 30,44 e 35,27 s. Os 500 mundos e os 1.000 usuários ficaram fora da margem deste host. [Etapa 5](MULTIPLAYER_ETAPA_05.md)

No lote de dez contas com mercado, partidas e tratamento das pendências salariais, as duas salas concluíram e avançaram com versão durável. O p50/p95/p99 da partida com avanço foi 36,89/38,11/38,11 s. A primeira tentativa desse lote, durante outros ensaios no mesmo host, registrou três timeouts de ingresso em sala antes de iniciar partidas. [Reteste de duas partidas](../artifacts/stage7-run/players-quiet-10.json) · [Timeout de ingresso](../artifacts/stage7-run/players-round-10.json)

## 7F · GPU, mobile e rede

O proxy de teste acrescentou 60 ms a cada bloco WebSocket em cada direção. Um treinador por sala ficou três segundos desconectado no primeiro tempo. Os dois WebSockets recuperaram snapshots da mesma rodada em 3,54–3,55 s, nas versões 38 e 39, superiores às anteriores ao corte (25 e 27). As duas partidas terminaram e a rodada seguinte foi confirmada no PostgreSQL. O p95 de partida com avanço foi 41,60 s nessa amostra de dois mundos. A injeção ocorre no fluxo WebSocket, pode serializar blocos e representa um atraso controlado no laboratório. Perda de pacotes WAN e atraso de pintura do Flutter ficam para um ensaio de rede e dispositivo próprio. [Atraso e interrupção](../artifacts/stage7-run/players-delay-10.json)

O Chrome desktop desta etapa confirmou as telas de carreira com renderização por software. As últimas cifras comparáveis de desempenho vêm da Etapa 6. No Clássico a cadência de frames foi 28,4 → 31,4 FPS. No Atual, o heap JavaScript foi 79,0 → 49,6 MiB e a cadência ficou perto de 23 FPS. A janela estreita mediu o layout responsivo no desktop. Flutter detectou Windows e Chrome. FPS, memória total de navegador e frames descartados por GPU permanecem abertos em desktop acelerado e celular físico. [Medições desktop](MULTIPLAYER_ETAPA_06.md) · [Dispositivos](../artifacts/stage7-run/flutter-devices.json)

## Regressão

O build Release passou. A seleção .NET somou 34 aprovações e uma falha de carreira, tanto com o repositório de referência quanto com o alterado. O teste de convite concorrente em SQLite esperava versão 4, recebeu 3 nas duas seleções e passou isoladamente na referência. Isso indica sensibilidade à execução em conjunto, com causa ainda em aberto. Os dois casos PostgreSQL de pênalti e substituição passaram após apontar também `ELIFOOT_015A_MARKET_PG` para o banco descartável. Os quatro casos de commit ambíguo e a verificação de cookie seguro passaram na seleção final. Os oito testes Flutter selecionados para carreira e SignalR passaram na Etapa 7. A suíte da Etapa 6 teve 576 aprovações e oito falhas já registradas na linha de base. [Atual](../artifacts/stage7-run/regression-retry.log) · [Referência completa](../artifacts/stage7-run/regression-baseline-full.log) · [Referência isolada](../artifacts/stage7-run/regression-baseline.log) · [Flutter Etapa 7](../artifacts/stage7-run/flutter-focus.log) · [Flutter Etapa 6](MULTIPLAYER_ETAPA_06.md)

## 7G · Dimensionamento para homologação

Faixas de reserva para investigar, com um processo autoritativo por banco. As alocações dependem da idade dos saves e de quantas salas jogam juntas. A projeção de rede usa 1,21 MB de mensagem por treinador inscrito por rodada de cerca de 30 s e 40% de usuários inscritos em jogo no pico. HTTP, retransmissão, TLS e ativos precisam de margem adicional. O disco inclui margem de WAL, TOAST e operações de manutenção. Os valores de crescimento histórico são de fixtures SQLite de 128 clubes, com a representação PostgreSQL ainda por medir ao longo de temporadas. [Auditoria inicial](audit-scalability/RELATORIO.md) · [Etapa 4](MULTIPLAYER_ETAPA_04.md)

Essa premissa fornece cerca de 13, 65 e 129 Mbit/s de payload SignalR a 100, 500 e 1.000 usuários. A referência SQLite mediu aproximadamente 140 MiB de páginas ocupadas em dez temporadas. Extrapolou 0,56–0,62 GiB para 50 e 1,10–1,22 GiB para 100, por mundo semelhante. O perfil posterior mediu 606 MiB alocados em SQLite aos 50 anos. A faixa é apenas referência de capacidade. O ensaio da Etapa 7 mostrou que múltiplas versões físicas de um mesmo snapshot PostgreSQL podem dominar o volume antes de completar outra temporada. [Auditoria de armazenamento](audit-scalability/RELATORIO.md) · [Perfil de 50 temporadas](MULTIPLAYER_ETAPA_031.md)

| Público | API | PostgreSQL | SSD NVMe e rede | Mundos e limites para começar o ensaio |
| --- | --- | --- | --- | --- |
| Piloto fechado | 4 vCPU, 8–16 GiB | 2–4 vCPU, 8 GiB | 100 GiB, 100 Mbit/s | Até 10 pessoas e dois mundos em partida. Um trabalhador, pool 8, 30 conexões comerciais como limite inicial de admissão. |
| 100 simultâneos | 8 vCPU, 16 GiB | 4 vCPU, 8–16 GiB | 150–250 GiB, 100–250 Mbit/s | 20 mundos, subir de duas a cinco partidas em ensaio. Um trabalhador inicial, medir três em staging. Pool 8–16. |
| 500 simultâneos | 12–16 vCPU, 32–48 GiB | 8 vCPU, 24–32 GiB | 400–600 GiB, 500 Mbit/s | 100 mundos como alvo de cadastro. Admitir partidas em lotes após medir cauda da navegação. Um trabalhador inicial, pool 16–32 para teste. |
| 1.000 simultâneos | 16–24 vCPU, 48–64 GiB | 12–16 vCPU, 32–64 GiB | 0,75–1 TiB, 1 Gbit/s | 200 mundos como alvo. Ensaiar primeiro 500 dentro dos limites. Particionamento de bancos depende de validação de contas, roteamento e autoridade. |

Usar uma conexão direta para autoridade, reservar conexões para manutenção e medir o pico no PostgreSQL antes de aumentar o pool. Os valores propostos de admissão são de usuários e partidas. O canal do processo limita trabalhos de relógio. O padrão de um trabalhador permanece configurado. Em testes neste host, parar abaixo de 2 GiB de RAM livre ou 12 GiB de disco, acima de 4 GiB de RSS nas experiências históricas, a partir de 5% de erros ou p95 HTTP de 2 s em navegação. A escala seguinte pede também taxa de reconexão, p95 de partidas e percentil de espera antes de entrar na fila. Medir por rota, fase e idade do mundo e observar LOH, coleções gen 2, cache, pool, espera em `pg_stat_activity`, idade de transações, WAL, `fsync`, espaço de disco e reconciliação SignalR. [Estatísticas PostgreSQL](https://www.postgresql.org/docs/18/monitoring-stats.html)

## 7H · Operação

O HTTPS em loopback devolveu 200 com autenticação comercial, e a requisição direta em HTTP devolveu 400. O laboratório teve `TLSv1.3` confirmado no cliente. O proxy sobrescreveu os cabeçalhos de encaminhamento e a API de retaguarda escutou apenas em `127.0.0.1`. Kestrel direto apresentou falha ao importar a chave privada no ambiente gerenciado. A configuração ampla de forwarded headers serviu só ao loopback descartável. Em staging externo, confiar explicitamente no proxy e restringir acesso ao backend. A conexão ao PostgreSQL usou loopback e autenticação descartável. Para operação, testar `VerifyFull` e certificados do banco em infraestrutura provisionada. [Validação TLS](../artifacts/stage7-run/tls-check.log) · [Configuração de proxy ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0)

O teste de contas confirmou 401 sem sessão, 403 para snapshot de outro mundo e encerramento do socket ao revogar a sessão. O segundo processo foi recusado enquanto havia lock exclusivo no banco. O primeiro staging reutilizou um diretório com uma chave anterior de `AccountTest`. Em diretório comercial novo, o reteste de dez contas passou e a única chave do key ring tinha envelope XML criptografado. Os logs amostrados do EF ocultaram parâmetros. Falta verificar retenção de logs do proxy e exposição de campos em operação. Mercado, criação de mundos e gravações exigem política de admissão revisada à luz da cauda de latência. Réplicas ativas simultâneas ficam fora do modelo validado. [Captura autenticada](../artifacts/stage7-run/players-retry-500.json) · [Recusa da segunda API](../artifacts/stage7-run/tls-api-final.log) · [Reteste do key ring](../artifacts/stage7-run/keyring-report.json)

O dump dos dez mundos levou 2,86 s e a restauração 1,53 s, com arquivo de 3,53 MB. A checagem comparou hashes de snapshots, versões e 2.240 IDs contábeis. O banco desse ensaio tinha zero temporadas encerradas. Para operação, definir RPO/RTO, guardar cópia externa com retenção, incluir catálogo e material protegido de identidade em processo próprio, e testar PITR e reabertura de contas e partidas após restaurar. Os tempos locais do dump pequeno não representam o RTO de produção. [Restauração](../artifacts/stage7-run/backup-restore.json) · [Documentação pg_dump](https://www.postgresql.org/docs/18/app-pgdump.html)

## Checklist de lançamento

- [ ] Validar piloto fechado com critérios de p95, erros, memória e fila, com partidas e navegação em simultâneo.
- [ ] Repetir o perfil de commit e histórico em host dedicado com telemetria PostgreSQL por fase.
- [ ] Exercitar demissão enquanto o treinador acompanha uma derrota e confirmar o estado de carreira em uma transição de temporada.
- [ ] Medir GPU, celular físico, latência, perda de pacotes e ressincronização durante uma partida.
- [ ] Validar TLS do banco, cofre de credenciais, restrição do proxy, política por endpoint e redigir procedimento de revogação e retenção.
- [ ] Restaurar backup completo com contas, histórico e partida ativa, respeitando RPO/RTO. Exercitar perda do executor único antes de avaliar réplicas.
- [ ] Corrigir cauda de navegação a 500 e repetir a carga antes de autorizar o nível de 1.000.

## Reprodução

Usar o cluster descartável do guia com bases `audit_*` novas e portas loopback. Para o pareado de persistência, compilar Release com as métricas e repetir com e sem antecipar `SerializeSnapshotAsync` no caminho PostgreSQL. Usar `AUDIT_CLOCK_WORKERS=3`, `AUDIT_ROUND_CONCURRENCY=5`, `AUDIT_CLOCK=1`, `AUDIT_ROUND_ONLY=1`, `AUDIT_AUTHORITY=1`, `AUDIT_CLOCK_METRICS=1`, `AUDIT_PERSISTENCE_METRICS=1`, `AUDIT_SIGNALR_V2=1` e `AUDIT_SAMPLE_DISK=1`. Executar `node docs/audit-scalability/load.mjs API.dll pasta-nova 10`. Para histórico, usar a importação por hash e `stage51-aged.mjs ID JSON RODADAS TEMPORADAS_MÍNIMAS`, com a API LocalDev descartável na porta 54985 e TTL de 25 s. A recuperação reaproveita o runner citado na Etapa 5.1. [Guia](audit-scalability/REPRODUZIR.md) · [Etapa 5.1](MULTIPLAYER_ETAPA_051.md)

Para jogadores, provisionar o esquema num PostgreSQL novo. Iniciar a API em modo `Account` na porta de retaguarda 54997 e preparar TLS com origens explícitas e diretório novo de chaves. No loopback do laboratório, `node docs/audit-scalability/stage7-tls-proxy.mjs certificado.pem chave.pem` publica 54996. Informar ao Node a raiz de confiança em `NODE_EXTRA_CA_CERTS`. Executar `node docs/audit-scalability/stage7-players.mjs captura.json 10`, depois `100` e `500` sob os limites medidos. `AUDIT_PLAY_ROUND=1` abre duas partidas. `AUDIT_INTERRUPT_ROUND=1`, com `AUDIT_TLS_TRANSIT_MS=60` no proxy, acrescenta interrupção e atraso. `AUDIT_LOOPBACK_SUBNET` permite uma faixa local independente no cadastro repetido. O proxy de auditoria é exclusivo do staging isolado.

Para carreira, o `stage6-chrome.mjs` aceita `AUDIT_CAREER_CASE=invitation` ou `dismissal` e `AUDIT_CAREER_TOOL` apontando para o `EngineAudit.dll`. O fixture só prepara mundo sintético em `audit_stage7_career`. Ao aparecer `captura.json.restart`, reiniciar a API `AccountTest` mantendo o mesmo banco e key ring. Criar `captura.json.ready` depois que a API voltar a responder. O Chrome retoma com a conta já provisionada e usa os menus de cada tema.

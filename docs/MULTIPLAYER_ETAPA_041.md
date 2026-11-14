# Multiplayer · Etapa 4.1 · Flutter com PostgreSQL

**PARTIAL.** A rodada com dois treinadores passou usando os transportes reais de HTTP e WebSocket do Flutter contra PostgreSQL 18.4 descartável. Os widgets dos temas Atual e Clássico renderizaram a sala através da mesma API. A cobertura visual dos fluxos de criação de catálogo, mercado e carreira segue incompleta.

## Execução controlada

A API usou modo `AccountTest`, key ring descartável, autoridade exclusiva e relógio automático. Dois clientes Flutter autenticaram contas distintas, verificaram visibilidade da sala, criaram uma liga de 64 clubes, fizeram ingresso por convite e assumiram clubes diferentes. O treinador Atual enviou escalação e tática e ficou aguardando o outro. O segundo treinador confirmou e ambos acompanharam a partida. O cenário publicou uma venda no mercado, verificou a invalidação no outro cliente, consultou a proposta de renovação e leu carreira. Também tratou pênalti e decisão salarial quando apareceram.

Os dois clientes registraram os mesmos cinco estados da rodada e 91 valores de minuto. Receberam 111 e 108 mensagens SignalR, respectivamente. O cliente Clássico ficou brevemente desconectado e se recuperou sem divergência. As projeções finais dos jogos e a classificação coincidiram. A versão do resultado já estava durável. As três chamadas concorrentes de avanço retornaram a mesma rodada e versão. O avanço terminou na versão 104 na amostra com leilão e lance.

O Flutter de teste abriu a tela real de login para cada conta, buscou a sala no PostgreSQL e conferiu o tema persistido em cada sessão. O teste de tema cobre renderização da lista de salas após login. A execução de partidas e comandos usa os dois clientes Dart reais fora dos widgets para comparar diretamente as projeções. Essa distinção limita a cobertura visual, especialmente no catálogo, no lobby inicial de montagem, nas negociações e nas transições de carreira.

Os primeiros ensaios encontraram as barreiras de pênalti, decisões salariais e validação de versão ou posse ao ofertar depois da rodada. O driver passou a atender as ações com os comandos públicos existentes e fez o leilão na preparação, usando versão atual. O treinador do outro clube fez o lance pela API Flutter, e o vendedor conferiu o comprador e o valor registrados. A execução final passou em três casos. [Log da execução](../artifacts/stage5-run/flutter-e2e-market-final.log) · [Driver Flutter](../client/elifoot_flutter/tool/qa/postgres_multiplayer_test.dart)

Ficam abertas a rodada completa manipulada pelos widgets dos dois temas, a compra direta pelo cliente Flutter, a aceitação de um convite de carreira com mudança de clube e os cenários de promoção e demissão em PostgreSQL. Permanecem também os ensaios de rede degradada, FPS e RAM do cliente.

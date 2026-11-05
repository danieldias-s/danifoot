# História do clube — missão 017C

Base: `c9a91a8` (pedido salarial concluído antes da 017C). A ancestralidade de
`8dda6d1` (017A) e `1ecfc6a` (017B) foi conferida antes de criar o worktree
`D:/elifoot_98_017C`, branch `feature/club-player-history-017c`.

A consulta `GET /api/worlds/{world}/clubs/{club}/history?season=N` atende clubes
humanos e de IA. Sem filtro, reúne a carreira disponível; com temporada, mostra
apenas os registros daquele ano. O número da temporada deve ser positivo e não
pode ultrapassar o ano corrente do mundo.

O histórico reúne a divisão disputada naquele ano, colocação final, promoção,
rebaixamento, campanha na Taça, jogos/vitórias/empates/derrotas, gols e saldo.
Há abas para campeões e vices das quatro divisões e da Taça, treinadores,
jogadores, transferências e recordes. Cada temporada abre detalhes de Liga,
Taça, artilheiros do clube e caixa/receitas/despesas registrados.

As classificações são congeladas antes de aplicar a divisão da temporada
seguinte. O clube promovido conserva seu resultado na divisão antiga. A
galeria não cria títulos para uma final que não aconteceu. Na Taça, o vencedor
registrado decide vitória/derrota; os gols da disputa de pênaltis não são
somados aos gols da partida.

O treinador de uma partida é o capturado no início daquele jogo, e não quem
está empregado no clube ao consultar a tela. Passagens e motivos de saída
aproveitam os vínculos existentes. A lista de atletas usa identidade estável,
vínculos e estatísticas que sobreviveram no save.

Transferências preservam operação, origem, destino, jogador, momento e valor
do evento financeiro existente. Compra e venda da mesma negociação não geram
duas transferências. Vendas antigas em leilão também aproveitam o registro
concluído do leilão. Não foi criado um mercado novo.

Maior vitória/derrota usam placares reais preservados. Sequências são omitidas
quando a cobertura está incompleta. Dados anteriores ausentes aparecem como
parciais ou não registrados. Um clube novo tem histórico vazio, sem conquistas
ou partidas inventadas.

Implementação: `HistoryState.cs`, `HistoryEngine.cs`, `HistoryQueries.cs` e
`HistoryDtos.cs`. A apresentação Flutter está em `history_views.dart`.

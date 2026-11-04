# Intervalo: somente humanos com partida em andamento aguardam decisões

O intervalo abria uma pausa compartilhada e exigia a confirmação de todos os
treinadores cadastrados, inclusive desempregados e clubes sem jogo na rodada.

Agora, os intervalos gratuitos consideram apenas treinadores humanos fora do
modo automático cujo clube tem partida não encerrada na rodada. Os demais são
confirmados automaticamente. Se não há decisão humana, o snapshot já chega como
segundo tempo, sem modal nem clique em CONTINUAR. O ajuste também contempla uma
janela já aberta: o relógio a libera sem aguardar o timeout quando só restam
observadores.

Se há um humano jogando, os espectadores continuam vendo a espera enquanto ele
altera sua equipe; somente sua confirmação é necessária. Dois humanos jogando
continuam precisando confirmar. A preparação da rodada, as paradas táticas
solicitadas, pênaltis, cotas de substituição, MatchEngine e economia foram
preservados. A avaliação dos clubes de IA ocorre no estado de intervalo antes
da possível retomada, mantendo seu caráter gratuito.

Validação:

- 10 casos novos: desempregados, ausentes da rodada, jogos encerrados, sala sem
  humanos, modo automático, observador com humano ativo, dois humanos ativos e
  recuperação de intervalo aberto sem esperar timeout.
- 29 testes da aplicação passaram, incluindo esses 10 casos.
- 3 testes de integração passaram: dois novos casos HTTP/SQLite com reinício
  e um cenário existente de API/SignalR com seis humanos.
- 33 testes Flutter de interações, rodada clássica e fluxo automático passaram.
- Análise Flutter sem problemas; builds release de API e Web concluídos.

Os logs correspondentes estão nesta pasta. Não houve migração de dados, alteração
automática de forças, merge ou publicação em produção. A prévia local será
atualizada junto ao ajuste subsequente da seleção por bolinhas.

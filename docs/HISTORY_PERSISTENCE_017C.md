# Persistência histórica — 017C

Base auditada: `c9a91a8`, descendente de `8dda6d1` (017A) e `1ecfc6a` (017B).
Worktree: `D:/elifoot_98_017C`; branch `feature/club-player-history-017c`.

O save já preserva estatísticas de jogadores por temporada, competição, divisão
e clube; títulos de treinadores; vínculos de emprego; artilharias gerais e
eventos de força. Partidas e transações transitórias são podadas após duas
temporadas. A 017C guarda resumos antes dessa poda, sem criar outro motor,
mercado ou sistema de premiação.

## Estrutura e integração

`GameWorld.History` é uma extensão aditiva, versão 1, do snapshot JSON já salvo
no PostgreSQL. Não há banco por aparência, segunda lógica de cálculo, nem
migração destrutiva de tabelas. Os DTOs anteriores conservam seus campos.

O arquivo registra partidas por GUID, temporadas por clube/ano, transferências
por ID de execução, títulos por ano/competição/divisão/clube/tipo, marcos de
força por jogador/ano e passagens pelos clubes. A coleção de anos fechados
impede novo fechamento do mesmo ano. A identificação de transferências de
leilão usa o mesmo GUID no extrato e no leilão concluído.

Os pontos de coleta são início da temporada, aplicação de resultados,
fechamento antes da poda/mudança de ano e salvamentos do repositório.
`HistoryEngine.Observe` não consome sorteios nem altera elenco, força, placar,
contratos ou valores financeiros. Os registros financeiros são observados.

Ao carregar um save sem a extensão, o coletor aproveita somente fatos
preservados: partidas concluídas, tabelas de temporadas, movimentações,
títulos confiáveis, artilharias, vínculos e leilões vendidos. Não se aceita o
antigo campeão substituto da Taça sem evidência da final. Campos desconhecidos
ficam nulos/parciais. A coleta posterior acompanha o mesmo save normalmente.

## Reinício e saves extensos

A simulação real de 100 temporadas produziu 28.700 resumos de partidas mesmo
após o descarte de jogos antigos (798 fixtures completas restantes). O
snapshot completo ficou com 625.466.017 bytes, incluindo o restante do estado
cumulativo que já existia. Ele não foi incluído no Git.

O teste PostgreSQL expôs uma leitura redundante desse JSON durante cada
salvamento, que podia esgotar o transporte ao repetir o save. O repositório
agora consulta somente os campos escalares necessários para atualizar a linha,
incluindo o token de concorrência RoundVersion; SaveChanges e a atomicidade
existente do snapshot/extrato foram preservados. Listar salas já em cache não
relê todos os payloads. Carregamentos frios concorrentes do mesmo mundo
compartilham o bloqueio existente, inclusive por código de convite.

O teste SQL confirma ausência de SELECT de SnapshotJson em save/listagem
quentes e uma única carga fria para quatro leitores. O teste PostgreSQL de
100 temporadas salvou, reiniciou a API, recuperou os números e salvou novamente.
A prévia de QA usa Command Timeout=180 segundos para esse estado extenso;
não se afirma que saves de 100 temporadas sejam pequenos ou instantâneos.

Somente bancos isolados `history017c_*` e `elifoot_013c_017c_*` receberam os
dados de QA. Saves do proprietário e os demais worktrees não foram modificados
pela missão 017C. No mundo de QA, dois treinadores com aparências distintas
consultaram o mesmo clube e receberam JSON histórico idêntico.

# Atualização semanal por país

A chave persistente é país|categoria|temporada. Um upsert impede criar duas filas para a mesma combinação. Tentativa e último sucesso são campos diferentes. Somente DONE atualiza LastSuccessfulUpdateAt e define NextEligibleUpdateAt para sucesso + 7 dias.

O worker busca também países SUCCEEDED vencidos e reinicia DISCOVER, sem precisar abrir o criador. Antes de sete dias, a seleção retorna CACHED e não faz chamadas novas. Estados/cursor de liga, clube e página sobrevivem à reinicialização. Um lease persistente de cinco minutos impede execução simultânea da mesma etapa por workers concorrentes.

Fases: DISCOVER, ligas/equipes, classificação, SQUADS por clube, estatísticas paginadas e publicação. Metadados/equipes válidos podem ser publicados por lote; falha posterior não apaga snapshots já ativos. Trabalho incompleto retoma cursor; não declara sucesso por ter só uma página.

Cache 016A até 168 horas quando armazenamento é autorizado; limite local diário até 100 e intervalo mínimo real de 6,1 segundos entre chamadas API-Football. Headers do fornecedor podem reduzir o saldo; 429/403 e indisponibilidade registram códigos e backoff. Corpo de erro e chave não vão para a interface.

Estados incluem NOT_REQUESTED, NOT_CONNECTED, QUEUED, UPDATING, REVIEW_REQUIRED, CURRENT_SEASON_UNAVAILABLE, PARTIAL, FAILED e SUCCEEDED. A interface separa cobertura salva do sucesso desta fila. Sem chave, NOT_CONNECTED faz zero chamadas.

Testes cobrem dias 1/6/7/8, renovação automática sem Queue após vencimento, 12 pedidos concorrentes, retomada por quota após reinício e preservação de cargas válidas após falha. Priorização refinada entre elencos selecionados dentro de um mesmo país permanece pendente; hoje a ordem é determinística por liga/clube, com retomada incremental.

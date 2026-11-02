# Lobby e sorteio de clubes

Estados: CONFIGURING, WAITING_FOR_PLAYERS, READY_FOR_DRAW, DRAW_COMPLETED, READY_TO_START e IN_GAME. Cada participante escolhe MANUAL, UNEMPLOYED ou FOURTH_DIVISION_DRAW; PENDING impede início.

Capacidade de 1..8 é configurada explicitamente; nenhuma entrada aumenta o limite automaticamente. O criador tem vaga reservada antes de entrar. Alterar capacidade exige host, não pode remover treinador e fica bloqueado depois do sorteio/início.

Sorteio exige todas as vagas preenchidas, todos conectados, escolhas concluídas, criador presente e clubes suficientes. Só considera quarta divisão livre. Shuffle criptográfico no servidor; atribuições, data, versão e recibos persistem no snapshot. Repetir comando, abrir nova aba ou reconectar retorna o mesmo sorteio; não sorteia de novo.

Escolha manual preserva clube e desempregado continua sem clube. Comandos novos compartilham o lock por sala e o repositório do orquestrador. São adequados à instância local atual; escala horizontal entre várias APIs continua fora desta missão, pois o lock por sala é em memória.

Evidências HTTP reais: sala c43d1dc4-46e1-4b2e-80ef-3a54ee361056, quatro conexões WebSocket SignalR; host manual, um desempregado, dois sorteados únicos da quarta divisão; oito pedidos concorrentes, recusa para não host, desconexão bloqueando sorteio, reconexão sem duplicidade, início idempotente, táticas e rodada real em RESULTS. Contexto/sorteio e script reproduzível ficam em artifacts/016b/world-catalog e scripts/audit-world-catalog-016b.mjs.

Reinício exclusivo da API 5175 preservou SHA256 7CC7C7752732A583750528826F06E337AD85992F175278F7FEC2787D11AF0984, versão 144, quatro registrados e sorteio. Conectados voltou a zero, como deve ocorrer sem transporte ativo.

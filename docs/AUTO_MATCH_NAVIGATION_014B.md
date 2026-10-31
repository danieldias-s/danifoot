# Navegação automática — 014B

`round_navigation.dart` decide a transição por snapshots autoritativos. `main.dart` centraliza HTTP, SignalR e reconexão e descarta versões anteriores e snapshots/contadores de outra sala.

Enviar tática apenas registra pronto. O cliente consulta o snapshot e aguarda; o booleano `roundStarted` e o contador completo não autorizam navegação. PREPARING (inclusive 0/0) e ALL_READY não abrem a partida. A mensagem TÁTICA ENVIADA. AGUARDANDO OUTROS TREINADORES. aparece junto do contador do servidor; desempregados seguem PRONTO PARA ACOMPANHAR. Retirar pronto permanece disponível antes do bloqueio. Escalação, formação e troca titular/reserva ficam bloqueadas enquanto pronto ou fora de PREPARING.

Estados aceitos: RUNNING_FIRST_HALF, HALFTIME_PAUSED, RUNNING_SECOND_HALF, GLOBAL_PAUSED, EXTRA_TIME_BREAK, RUNNING_EXTRA_TIME, EXTRA_TIME_HALFTIME, RESULTS e FINISHED. A identidade é mundo/temporada/rodada. Abre uma vez no início, para cada nova pausa e para resultados; ticks repetidos não sequestram a navegação manual. Próxima preparação retorna ao plantel. Troca de mundo/reentrada reinicia o coordenador; atualização e reconexão restauram pelo snapshot. Há retorno contextual à partida iniciada, sem recriar aba Jogos ao Vivo.

Testes: HTTP otimista durante PREPARING, espera individual, último pronto, 2/4/6/8 treinadores, mudança a partir de finanças/mercado, ticks, pausas, resultados, próxima rodada, desempregado, contador estrangeiro, troca/reentrada, refresh/reconexão de todos os estados. HTTP/SignalR com 2/4/6/8 continua na regressão completa; orquestrador e sincronização não foram modificados.

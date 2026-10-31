# Multiplayer em mundos criados

STATUS: PASS

Ciclo de 15 etapas ampliado preserva quatro casos antigos e acrescenta 2/4/6/8 clientes em mundos criados, masculinos/femininos. HTTP/SignalR independentes validam clube, tática, pronto 0/N→N/N, jogo por minuto, pausas, substituições, resultado, próxima rodada, desconexão e reconexão.

Banco concorrente com mesma chave gera só um principal/lançamento; repetir após reconectar o hub não duplica crédito. Comandos em clube alheio falham. Mesmo orquestrador e trava de sala 012D.

PostgreSQL testa compra/venda, renovação sem reprecificar salário, oito pedidos bancários concorrentes, jogo real, juros únicos, quitação e replay após recriar repositório. Reinício real PostgreSQL/APIs mantém snapshots e hashes financeiros dos quatro mundos. Confirmação da prévia retorna mesmo mundo. Comando financeiro entre salas é recusado mesmo quando os clubes têm IDs determinísticos iguais. Evidências: api-restart.json, restart-verification.json e TRX finais em audit-014a. Identidade LocalDev; nenhuma homologação de produção declarada.

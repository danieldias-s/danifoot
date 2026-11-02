# Modo clássico sincronizado

GameModeId=CLASSIC_SYNC persiste no mundo. A configuração inicial tem uma etapa anterior à preparação de táticas: primeiro todos entram e escolhem carreira; o criador sorteia quando necessário e inicia o campeonato. Esse início não simula partidas nem avança rodada.

Depois disso, todos os humanos necessários enviam tática ou pronto como desempregado; o último necessário inicia automaticamente a rodada no servidor. Seleção de clube, contador de conexão, contador de táticas e estado da sala são informações distintas.

O campo InitialLobby é null em saves antigos. Eles conservam o ciclo anterior, sem exigir novo sorteio ou início por host. Reconexões com identidade de dispositivo preservam o treinador canônico. Desconectar não apaga o vínculo nem libera automaticamente uma vaga ocupada.

Motor de jogo, resultados, calendário, pausas, pênaltis, salários negociados, juros, dívida e leilões não receberam nova fórmula nesta missão.

# Multiplayer — 014B

Regressão contém oito cenários anteriores e quatro novos 014B em mundos reais com 2/4/6/8 treinadores via HTTP e SignalR. O ciclo usa orquestrador original, snapshot, versões, pronto, travas, pausas, ajustes, substituições, continuação, resultados e próxima rodada. MatchEngine e unanimidade não foram alterados para adaptar a interface.

Flutter cobre espera pelo último treinador, resposta otimista/contador completo insuficientes, desemprego, restauração de partida iniciada, início a partir de outra aba e ausência de navegação repetida por tick. Troca/reentrada e contador estrangeiro também cobertos.

API executável gravou espera real 1/2 PREPARING, início após segundo treinador, Taça com distritais, liga 4×4 jogos, pausas/resultados por três rodadas. `ui-server-scenes.json` distingue esta evidência de sessão com múltiplos navegadores. Testes automatizados não são descritos como oito pessoas/janelas de Chrome.

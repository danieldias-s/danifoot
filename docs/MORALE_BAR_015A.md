# Barra de moral — 015A

A HUD e o painel de carreira exibiam MORAL, valor e faixa em texto. Foram substituídos por `MoraleBar`, uma barra horizontal de 104 × 16 que recebe o valor e a faixa reais do servidor. VERMELHA, AMARELA e VERDE seguem a faixa calculada pelo domínio; o Flutter não redefine seus limites.

Não há texto, número ou tooltip visível na barra. Leitores de tela recebem o nome e o valor pela árvore Semantics. A moral continua persistida e participa das mesmas regras do jogo.

Seis testes de widget cobrem três faixas, duas larguras, preenchimento real e ausência de Text. O navegador real desktop/mobile confirmou a barra na HUD; na revisão foi removido também o texto remanescente do painel do treinador e do estado sem clube. Os valores reais vistos incluíram 70, 79 e 61, todos na faixa verde do domínio; as faixas amarela e vermelha foram validadas por testes determinísticos, sem adulterar o banco da sala para gerar capturas.

Evidências: [desktop](evidence-015a/desktop-final-results.png), [mobile](evidence-015a/mobile-results-reconnected.png), testes `match_market_015a_test.dart` e `CoachCareer008Tests` na regressão completa.

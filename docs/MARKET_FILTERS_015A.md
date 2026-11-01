# Ordenação e posições — 015A

As duas abas têm preferências próprias: MENOR PREÇO, MAIOR PREÇO, MAIOR FORÇA, MENOR FORÇA e TODOS/GOLEIRO/DEFESA/MEIO-CAMPO/ATAQUE. Posições G/D/M/A e representação numérica do DTO são normalizadas. As comparações de preço e força são numéricas; empates usam o identificador estável do anúncio/jogador.

Compra direta usa AskingPrice. Leilão usa CurrentBid quando existe líder, senão MinimumBid; ambos aparecem separadamente na interface. A seleção permanece ao abrir/cancelar detalhes, trocar de aba e voltar de outro menu durante a sessão.

Os testes determinísticos cobrem todas as quatro ordens nas duas modalidades, filtro combinado e desempate. No navegador mobile, 25 anúncios reais da API foram ordenados nas quatro opções: preço de R$ 346.828 a R$ 1.200.000 e força de 22 a 37. Posições verificadas: G=1, D=2, M=4, A=18, TODOS=25. Detalhes cancelados mantiveram posição e ordem.

No desktop, os três leilões reais encerrados foram conferidos visualmente nas quatro ordens: preços 580.000, 595.000,25 e 750.000,75; forças 34, 35 e 36. GOLEIRO retornou zero nessa aba, apesar de existir goleiro na compra direta. A árvore Semantics do DataTable pode manter a ordem de criação dos nós após reordenar; as capturas confirmam a ordem visual das linhas. Asserções numéricas da lista mobile e testes de widget complementam essa inspeção.

Evidências: [ordens numéricas mobile](evidence-015a/numeric-sorts-real.json), [posições](evidence-015a/position-filters-real.json), [leilão menor preço](evidence-015a/desktop-auction-price-asc.png), [maior preço](evidence-015a/desktop-auction-price-desc.png), [menor força](evidence-015a/desktop-auction-strength-asc.png), [maior força](evidence-015a/desktop-auction-strength-desc.png), [mobile](evidence-015a/mobile-market-sorted.png).

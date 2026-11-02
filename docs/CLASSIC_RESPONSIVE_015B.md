# Responsividade — 015B

A matriz automatizada cobre 360, 390, 768, 1024, 1440 e 1920 px. A regressão anterior amplia a cobertura de 320 a 1920 px. Cada cenário navega pelas sete telas e verifica ausência de exceções de layout, tabela/filtros, barra de moral e dados recebidos. Testes adicionais exercitam bloqueio em 390 e 1440 px.

| Faixa | Comportamento |
|---|---|
| Abaixo de 768 px | Cartão do clube e quatro indicadores; listas claras; campo em Táticas; barra inferior fixa e Mais para drawer |
| 768 a 1023 px | Menu recolhido em drawer pelo cabeçalho |
| A partir de 1024 px | Menu fixo de 216 px; cabeçalho horizontal |
| Conteúdo com pelo menos 1000 px | Táticas e plantel lado a lado |
| Conteúdo menor | Painéis empilhados e rolagem vertical |
| Faixa do clube sem espaço (tablet) | Cartões em faixa horizontal rolável |
| Tabela extensa | Rolagem horizontal, sem remover atributos |

O campo mantém proporção esquemática e largura máxima de 480 px em painéis empilhados. Os nomes completos continuam na ficha e tooltip; no campo aparece o último componente do nome para caber sem sobreposição. A lista de reservas e os setores permanecem acessíveis por expansão.

No celular, o acompanhamento da partida continua como placares e substituições, sem exigir campo de futebol. O drawer não oferece saída durante o bloqueio autoritativo. Ao terminar a rodada, o menu volta a funcionar. Se o drawer já estiver aberto no instante do início, ele é fechado automaticamente antes de mostrar a partida; o caso tem teste específico.

As capturas reais em docs/evidence-015b documentam a inspeção visual; testes de widget não substituem essa etapa. Diferenças e limites estão em VISUAL_COMPARISON_015B.md e KNOWN_ISSUES_015B.md.

A revisão mobile posterior usa a referência individual enviada pelo proprietário e mantém a moral em barra. Sete testes adicionais passam nas larguras 320/360/390/430/600; a regressão completa totaliza 180 casos. O desktop mantém a composição anterior. Previews Flutter de dados reais foram inspecionados em 390 x 844, 390 x 1800 e 1440 x 1000; não são capturas de navegador. Ver MOBILE_REFERENCE_LAYOUT_015B.md e evidence-015b-mobile.

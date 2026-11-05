# ELIFOOT_CENTERED_RESPONSIVE_LAYOUT

Checkpoint local na branch `codex/centered-responsive-layout`, worktree
`D:/elifoot_98/.worktrees/centered-responsive-layout`. A comparação usa a
017C (`17e9770`) como base.

| Item | Resultado |
| --- | --- |
| MAX_WIDTH, CENTERING | Até 1.360 px lógicos, centralizados sobre fundo verde-escuro. |
| CURRENT_VERSION, CLASSIC_VERSION | Um contêiner compartilhado em torno do Navigator. Todas as rotas e seus diálogos usam a largura disponível dentro do contêiner. |
| DESKTOP, MOBILE | Chrome e testes Flutter nas nove larguras pedidas. No celular, o jogo usa toda a largura e conserva os layouts responsivos. |
| HEADER_ALIGNMENT, BOTTOM_BAR | Cabeçalhos, menus e barras pertencem ao mesmo contêiner. O rodapé clássico ficou dentro do quadro. No mercado atual em 390 px, a última ação ficou acima do rodapé após rolar. |
| SUBSTITUTION_MODAL | Até 720 px e altura ajustada ao espaço disponível. O cabeçalho permanece visível enquanto o conteúdo rola até o nono reserva e o botão Continuar. |
| VERTICAL_SCROLL | Confirmada até o último jogador nos dois plantéis, a última ação do mercado atual e a 4ª Divisão no placar clássico. |
| FLUTTER_TESTS | 398 testes passaram. Os três testes focados foram repetidos após a última asserção. A análise estática terminou sem problemas. |
| WEB_BUILD | Build release concluído com CanvasKit local. Prévia em `http://127.0.0.1:5252/`. |
| KNOWN_ISSUES | Nenhuma falha de layout nos fluxos verificados. O teste mobile usou viewport do Chrome e testes Flutter. |

O contêiner preserva a altura da janela e repassa sua largura real ao
`MediaQuery`. Isso mantém os pontos de quebra e as janelas de diálogo no
mesmo sistema de coordenadas. A verificação da partida e do intervalo usou
elencos da sala de QA e respostas temporárias apenas no navegador, com
placar, relógio e banco preparados para a inspeção visual. Os testes Flutter
existentes cobrem a interação e o bloqueio da navegação ao vivo.

| Janela | Largura do jogo | Margem por lado |
| ---: | ---: | ---: |
| 1.920 px | 1.360 px | 280 px |
| 1.600 px | 1.360 px | 120 px |
| 1.440 px | 1.360 px | 40 px |
| 1.360 px | 1.360 px | 0 px |
| 1.280 px | 1.280 px | 0 px |
| 1.024 px | 1.024 px | 0 px |
| 768 px | 768 px | 0 px |
| 390 px | 390 px | 0 px |
| 360 px | 360 px | 0 px |

As medidas, os limites do modal e a verificação de rolagem foram
registrados em [browser.json](centered-layout/browser.json).

| SCREENSHOTS_1920 | Antes | Depois |
| --- | --- | --- |
| Plantel atual | [Captura](centered-layout/before-squad-CURRENT-1920.png) | [Captura](centered-layout/after-squad-CURRENT-1920.png) |
| Plantel clássico | [Captura](centered-layout/before-squad-CLASSIC_98-1920.png) | [Captura](centered-layout/after-squad-CLASSIC_98-1920.png) |
| Divisões atual | [Captura](centered-layout/before-divisions-CURRENT-1920.png) | [Captura](centered-layout/after-divisions-CURRENT-1920.png) |
| Divisões clássico | [Captura](centered-layout/before-divisions-CLASSIC_98-1920.png) | [Captura](centered-layout/after-divisions-CLASSIC_98-1920.png) |
| Mercado atual | [Captura](centered-layout/before-market-CURRENT-1920.png) | [Captura](centered-layout/after-market-CURRENT-1920.png) |
| Mercado clássico | [Captura](centered-layout/before-market-CLASSIC_98-1920.png) | [Captura](centered-layout/after-market-CLASSIC_98-1920.png) |
| Lobby | [Captura](centered-layout/before-lobby-CURRENT-1920.png) | [Captura](centered-layout/after-lobby-CURRENT-1920.png) |
| Partida clássica | [Captura](centered-layout/before-match-CLASSIC_98-1920.png) | [Captura](centered-layout/after-match-CLASSIC_98-1920.png) |
| Substituições | [Captura](centered-layout/before-substitution-CLASSIC_98-1920.png) | [Captura](centered-layout/after-substitution-CLASSIC_98-1920x1000.png) |

| SCREENSHOTS_1360 | SCREENSHOTS_MOBILE |
| --- | --- |
| [Plantel atual](centered-layout/after-squad-CURRENT-1360.png) | [Plantel atual a 390](centered-layout/after-squad-CURRENT-390.png) · [a 360](centered-layout/after-squad-CURRENT-360.png) |
| [Plantel clássico](centered-layout/after-squad-CLASSIC_98-1360.png) | [Plantel clássico a 390](centered-layout/after-squad-CLASSIC_98-390.png) · [a 360](centered-layout/after-squad-CLASSIC_98-360.png) |
| [Lobby](centered-layout/after-lobby-CURRENT-1360.png) | [Lobby a 390](centered-layout/after-lobby-CURRENT-390.png) |
| [Partida clássica](centered-layout/after-match-CLASSIC_98-1360.png) | [Partida a 390](centered-layout/after-match-CLASSIC_98-390.png) · [rolagem à 4ª Divisão](centered-layout/after-match-scroll-CLASSIC_98-390.png) |

Capturas adicionais do [mercado a 390](centered-layout/after-market-CURRENT-390.png),
[último jogador no plantel clássico](centered-layout/after-roster-scroll-CLASSIC_98-1920.png)
e do intervalo com [500 px de altura](centered-layout/after-substitution-CLASSIC_98-1920x500.png)
ou [rolagem em 360×640](centered-layout/after-substitution-scroll-CLASSIC_98-360x640.png).

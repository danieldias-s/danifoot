# Evidências — Rankings de Jogadores 016B

**PARTIAL.** Estes PNGs são renderizações Flutter do aplicativo com respostas reais gravadas da API local, não screenshots Chrome. Capturas e inspeção em navegador continuam pendentes.

[Relatório completo](../PLAYER_RANKINGS_VISUAL_016B.md) · [Validação](validation.json) · [Hashes e dimensões](capture-manifest.json)

## Referências principais

[Desktop aprovado](references/reference-3.png) · [Mobile aprovado](references/reference-4.png)

## Composição final

| Largura | Captura | Rodapé após rolagem |
| --- | --- | --- |
| 360 px | [Rankings](flutter-renders/rankings-empty-360.png) | [Rodapé](flutter-renders/rankings-bottom-360.png) |
| 390 px | [Rankings](flutter-renders/rankings-empty-390.png) | [Rodapé](flutter-renders/rankings-bottom-390.png) |
| 430 px | [Rankings](flutter-renders/rankings-empty-430.png) | [Rodapé](flutter-renders/rankings-bottom-430.png) |
| 768 px | [Rankings](flutter-renders/rankings-empty-768.png) | [Rodapé](flutter-renders/rankings-bottom-768.png) |
| 1366 px | [Rankings](flutter-renders/rankings-empty-1366.png) | Visível na captura |
| 1920 px | [Rankings](flutter-renders/rankings-empty-1920.png) | Visível na captura |

## Interações com os filtros

| Estado | Desktop | Mobile |
| --- | --- | --- |
| Categoria aberta | [1366 px](flutter-renders/category-open-1366.png) | [390 px](flutter-renders/category-open-390.png) |
| Goleiros | [1366 px](flutter-renders/goalkeepers-1366.png) | [390 px](flutter-renders/goalkeepers-390.png) |
| Competição aberta | [1366 px](flutter-renders/scope-open-1366.png) | [390 px](flutter-renders/scope-open-390.png) |
| Período aberto | [1366 px](flutter-renders/period-open-1366.png) | [390 px](flutter-renders/period-open-390.png) |
| Temporada selecionada completa | [1366 px](flutter-renders/selected-season-1366.png) | [390 px](flutter-renders/selected-season-390.png) |
| Defensores | [1366 px](flutter-renders/defenders-1366.png) | [390 px](flutter-renders/defenders-390.png) |

## Leitura da comparação

Desktop: área útil inteira, filtros em uma faixa e painéis alinhados lado a lado, proporção 4:3. Mobile: fatos do clube em 2×2, filtros reordenados e painéis empilhados, com navegação fixa. Moral em barra. Cores derivadas do tema existente, áreas claras e ícones amarelos. Os textos integrais exigidos nesta missão ocupam mais linhas que os estados ilustrativos das referências.

Não existem estatísticas oficiais nas salas consultadas; as imagens mostram esse estado real. Nenhum resultado ou atleta foi acrescentado para produzir evidências. Fixtures preenchidos são usados somente pelos testes de widgets e histórico. A fonte e os ícones continuam os da 016B existente. A versão Flutter não comprova composição ou interação no Chrome.

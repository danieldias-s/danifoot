# Rankings de jogadores — padronização visual 016B

**STATUS: PARTIAL.** Implementação e validação técnica concluídas. A composição foi comparada visualmente às referências usando renderizações Flutter do aplicativo com respostas reais gravadas da API. A inspeção do aplicativo e novas capturas no Chrome continuam pendentes; as imagens aqui não são screenshots de navegador.

## Base e escopo

Worktree `D:\elifoot_98_016B`, branch `codex/player-rankings-visual-016b`, base `cee0058`. Os checkpoints `1e02ad2` e `cee0058` continuam na ancestralidade. A árvore estava limpa antes desta missão. Apenas Rankings de Jogadores e suas opções de apresentação no shell foram alterados. Backend, API client, sessões, banco, motor, classificação de treinadores, cálculos e progressão não receberam alterações. Sem merge ou publicação.

## O que mudou

- Removido o limite de largura que deixava o conteúdo centralizado em uma faixa estreita. No desktop, filtros em linha e ranking/premiações lado a lado, na proporção 4:3.
- Cabeçalho verde, descrição, rótulos amarelos, campos claros, botão Atualizar amarelo, painéis claros com cabeçalhos verdes, bordas e ícones do sistema existente.
- Mobile: clube no primeiro card, divisão/moral e caixa/próximo jogo em duas linhas; categoria inteira, competição/período lado a lado, Atualizar inteiro e temporada adicional quando selecionada. Moral permanece uma barra sem número sobreposto.
- Ranking compacto com nome completo e clube em linhas próprias; jogos, métrica da categoria e acesso ao histórico. Desktop mantém as colunas oficiais e rolagem interna quando necessária. Histórico continua disponibilizando as estatísticas completas.
- Premiações em painel separado, com dados e créditos oficiais existentes. Estados de carregamento, erro, filtros e atualização preservados.
- Menu inferior identifica a seção ativa como Rankings. As demais telas continuam com suas opções de navegação e disposição anteriores.
- Nomes podem quebrar linha. A captura revelou que o dropdown denso cortava a segunda linha de “Temporada selecionada”; o campo agora comporta o rótulo completo, com teste dos limites do texto. A fonte dos dropdowns foi explicitada para usar a fonte condensada existente.

## Arquivos e componentes

| Arquivo | Alteração |
| --- | --- |
| `client/elifoot_flutter/lib/player_rankings.dart` | Layout, filtros, painéis, estado vazio, lista compacta e estilo opcional das tabelas de rankings. |
| `client/elifoot_flutter/lib/main.dart` | Cards 2×2 e item inferior ativo somente na tela de jogadores. |
| `client/elifoot_flutter/lib/visual_components.dart` | Opções de cor do ícone do painel e item Rankings na navegação; comportamento padrão dos demais consumidores preservado. |
| `client/elifoot_flutter/test/player_rankings_visual_016b_test.dart` | Dez testes novos de larguras, filtros, nomes longos, histórico, tema e navegação. |
| `client/elifoot_flutter/test/player_rankings_widget_test.dart` | Expectativas adequadas à apresentação compacta; consultas, ordem e histórico continuam verificados. |
| `client/elifoot_flutter/test/visual_polish_010_test.dart` | Regressão mobile adaptada à lista compacta; histórico e tabelas internas continuam testados. |
| `client/elifoot_flutter/tool/player_rankings_capture_016b_test.dart` | Renderer de evidências com respostas gravadas reais; não dirige navegador e não escreve no servidor. |
| `docs/player-rankings-visual-016b/` | Referências intactas, 22 renderizações, hashes e evidências sanitizadas. |

Reutilizados `ClassicTheme`, `ClassicPaper`, `ClassicPaperPanel`, `ClassicSummaryCard`, `ClassicBottomNavigation`, `ClassicTable`, `SeasonTopScorersPanel` e o diálogo de histórico. As preferências salvas de tema continuam autoritativas; variações clássica, escura e alto contraste foram testadas.

## Comparação visual

Referências principais: [desktop](player-rankings-visual-016b/references/reference-3.png) e [mobile](player-rankings-visual-016b/references/reference-4.png). As demais imagens orientam a identidade do jogo, sem transportar dados ou funcionalidades de treinadores para jogadores.

| Elemento | Resultado observado nas renderizações |
| --- | --- |
| Largura desktop | Conteúdo ocupa a área disponível após a barra lateral; os dois painéis ficam alinhados no topo. |
| Filtros | Três campos e Atualizar na mesma faixa no desktop; organização vertical própria no mobile. Seleção de temporada aparece integralmente em duas linhas quando preciso. |
| Cards mobile | Clube inteiro, fatos em 2×2, moral em barra. |
| Painéis | Conteúdo claro, cabeçalhos verdes, premiações separadas e ícones amarelos do tema. |
| Estados vazios | Textos completos exigidos na missão, sem atletas inventados. |
| Navegação | Barra inferior fixa na área segura, Rankings amarelo e acesso às outras telas. Rodapé alcançável por rolagem. |
| Diferenças mantidas | Tipografia condensada, ícones Material, cabeçalho e menu existentes da 016B; não são cópia pixel a pixel dos arquivos ilustrativos. Textos vazios usam o conteúdo integral da missão, mais longo que nas imagens. |

A primeira revisão levou ao ajuste da proporção dos painéis e dos ícones. A revisão seguinte revelou o corte no período selecionado; novas imagens foram geradas após corrigir o campo. Os screenshots Chrome não foram realizados. [Galeria e larguras](player-rankings-visual-016b/README.md).

## Dados e validação

A cena veio da sala existente **016B - Revisão visual**, mundo `73e11073-a854-487e-bc59-efd9856cab91`, Flamengo RJ / QA 016B Masculino, temporada 1, rodada 1, `PREPARING`. A coleta contém os recortes de categoria, competição e período das APIs existentes. Não havia linhas oficiais nem premiações encerradas nas cinco salas consultadas. Nenhuma partida foi avançada para preencher os painéis.

Dados preenchidos, empates, elegibilidade, ordenação, erro e histórico são exercitados pelos fixtures dos testes; esses atletas não são conteúdo da aplicação ou da cena gravada. A cena bruta com contexto de autenticação permanece nos artefatos privados ignorados pelo Git. Apenas proveniência, hashes e resultados sanitizados constam neste relatório.

| Verificação executada nesta missão | Resultado final |
| --- | --- |
| `flutter analyze` | 0 issues. |
| `flutter test --reporter expanded` | 200/200, incluindo os 10 novos testes. |
| Testes novos + renderer gravado | 16/16: 10 testes repetidos e 6 casos de captura; não são 16 testes novos adicionais. |
| Larguras | 360, 390, 430, 768, 1366 e 1920 px, sem exceção de layout nos casos executados. |
| Filtros | Categoria, competição, período, temporada, menus abertos, atualização e consulta correspondente verificados. |
| Nomes e histórico | Nome e clube longos, lista compacta e histórico completo; cenários unitários. |
| Build Web | Concluída com API local 5173. |
| HTTP real | HTML e JavaScript acessíveis; hash servido idêntico ao da build final; três categorias e premiações respondem. |
| Preservação da sala | Temporada, rodada, estado, versão, minuto e partidas iguais à cena coletada. Somente GETs. |
| Chrome | Pendente por limite de uso da ferramenta. |

Os testes .NET não foram repetidos nesta missão exclusivamente Flutter. A regressão .NET do checkpoint anterior permanece documentada em `LOBBY_SESSIONS_AUDIT_016B.md`; ela não é apresentada como nova execução.

Logs completos em `artifacts/016b/player-rankings/`: `analyze-final.log`, `flutter-tests-final.log`, `render-capture.log` e `web-build.log`. [Resultados e hashes](player-rankings-visual-016b/validation.json), [proveniência](player-rankings-visual-016b/source-provenance.json) e [checagens HTTP](player-rankings-visual-016b/live-readonly-checks.json).

## Versão local para revisão

Jogo: **http://127.0.0.1:5174/**. API: **http://127.0.0.1:5173/api/health**. PostgreSQL da 016B permanece na porta **55442**. Somente o processo Web registrado da 016B foi reiniciado após verificar PID, horário de início, caminho e porta. API e banco não foram reiniciados. A build anterior foi copiada para `artifacts/016b/player-rankings/baseline-web-cee0058`.

Para retomar a vaga existente, usar **QA 016B Masculino**, dispositivo **016b-qa-male**, entrar no lobby e **RETOMAR MINHA SESSÃO** na sala **016B - Revisão visual**. Abrir **7. Rankings de Jogadores** no menu lateral ou Mais no mobile. A sala mantém capacidade 2 e suas duas vagas reservadas; não criar outra identidade para tentar ocupá-las.

Para subir o ambiente caso esteja parado: `D:\elifoot_98_016B\scripts\run-local-016b.ps1 -NoBuild` no PowerShell, dentro do worktree. Nenhuma publicação externa foi feita.

## Pendência para concluir

Abrir esta build no Chrome, conferir filtros, histórico quando houver estatísticas oficiais e navegação, registrar novas capturas desktop/mobile e comparar novamente com as referências. A revisão automática da ferramenta recusou a abertura do navegador por limite de uso, com liberação indicada para 16:15 local; não foi usada rota alternativa de automação. Renderizações Flutter e HTTP não substituem essa inspeção. **Não há aprovação do teste manual ou PASS visual nesta entrega.**

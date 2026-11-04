# Versão clássica (antiga) — implementação e validação

A VERSÃO CLÁSSICA (ANTIGA) está implementada como apresentação do mesmo jogo. A VERSÃO ATUAL mantém seus componentes e seu layout. A aprovação visual final permanece com o proprietário.

Prévia local: <http://127.0.0.1:5232/>. API: porta 5231. PostgreSQL de revisão: porta 55436. Esta prévia utiliza dados de QA isolados; não substitui o ambiente anterior nas portas 5221/5222.

Branch: `codex/presentation-versions-017a`.
Worktree: `D:/elifoot_98/.worktrees/presentation-versions-017a`.
Base preservada: `298252e`. Checkpoint da preferência individual: `422003e`.
Nenhum merge, push ou publicação em produção foi realizado.

## O que funciona

| Área | Integração |
|---|---|
| Lobby | Seleção individual antes de assumir clube, participar do sorteio da 4ª divisão ou começar desempregado. |
| Configurações | Aparência, troca de sala, retorno ao lobby e ajuda. A troca usa o fluxo de sessão existente. |
| Táticas | Formações realmente suportadas, Automática e Melhores; mesmos métodos de escalação e validação. Campo somente na página de táticas. |
| Equipa | Mercado & Leilão, Banco, Estádio e Finanças. |
| Jogador | Seleção visual independente da escalação; informações, estatísticas disponíveis, contrato, anúncio e procura no mercado. |
| Campeonato | Classificação das quatro divisões, tabela de jogos, calendário, resultados, taça e jogos ao vivo. |
| Rankings de jogadores | Artilheiros, goleiros e defensores abrem as categorias do componente oficial, com filtros e critérios existentes. |
| Treinador | Carreira, convites, perfil e ranking oficial. Humanos aparecem em ciano com identificação HUMANO. |
| Partida | Enviar tática, pronto para acompanhar, retirar pronto ou acompanhar a rodada, conforme o estado real. Continua sincronizada pelo servidor. |

O banco mostra somente os atletas relacionados, diferencia os demais atletas e usa a troca validada já existente. Selecionar uma linha não troca nem vende jogadores. Anúncios conservam a revisão e confirmação existentes. As operações financeiras na apresentação clássica também pedem confirmação antes de invocar o comando compartilhado.

A lista usa os nomes, forças, valores, condição, craques e relações de titulares/banco reais. Nenhum número das referências foi copiado para os modelos. A escala de força continua a do projeto.

## Estrutura e arquivos

Novos componentes visuais em `client/elifoot_flutter/lib/classic98_widgets.dart`: paleta fixa, tema, moldura com relevo, barra azul, botão retangular, seção compacta e tabela responsiva preta/amarela.

`client/elifoot_flutter/lib/classic98_shell.dart` adapta o estado e os callbacks do shell existente às telas clássicas: principal, menus, aparência, táticas, banco, jogador, classificação, calendário, resultados, taça, carreira e ranking de treinadores. Mantém a seleção visual do jogador no shell.

Arquivos de integração visual modificados:

- `main.dart`: seleção da apresentação, tema individual e integração com navegação e componentes compartilhados.
- `market_screen.dart`: apresentação compacta, tabela desktop, lista mobile e abas Leilão / Compra direta / Minhas ofertas, reutilizando os mesmos comandos e confirmações.
- `coach_finance_screens.dart`: painel compacto de estádio/finanças, com os mesmos dados e operações.
- `player_rankings.dart`: abertura de uma categoria solicitada pelo menu; nenhuma alteração nos critérios de ranking.
- `presentation_widgets.dart`: seletor comum e contêiner estável de navegação.
- `test/presentation_017a_test.dart`: preferência, sessão, navegação, reserva, confirmações, responsividade e regressão entre apresentações.

O checkpoint de preparação também criou `presentation_version.dart`, `ProfileAppearanceStore.cs`, `ProfileAppearanceEndpoints.cs` e `ProfileAppearance017ATests.cs`, e integrou a preferência em `api_client.dart`, `appearance_settings.dart`, `club_selection_dialog.dart`, `shell_actions.dart`, `world_initial_lobby_016b.dart` e `Program.cs`. Os testes de gameplay/lobby receberam o suporte correspondente ao endpoint de preferência.

A preferência fica em `ef_profile_appearance`, por identidade do treinador, fora de `ef_worlds.SnapshotJson`. O endpoint aceita CURRENT / CLASSIC_98 no mecanismo LocalDev existente. Nenhum campo visual é enviado nos comandos de sorteio, escalação, mercado ou partida.

O MatchEngine, Domain, regras de evolução, economia, salários, contratos, calendário e regras multiplayer não foram modificados por esta entrega. As telas usam a mesma API, PostgreSQL, save, identidade e dados compartilhados. Nenhum save existente teve jogadores recalculados.

## Validação

| Verificação | Resultado observado |
|---|---|
| Regressão Flutter completa | 320 testes aprovados. |
| Testes .NET pertinentes | 29 aprovados: preferência individual, cores e rebalanceamento visual. |
| Flutter analyze | Sem erros ou novos avisos. Permanece 1 info preexistente: unnecessary_const em live_match_view.dart:241. |
| Build Web | Release gerado para a API isolada. |
| Larguras | 360, 390, 430, 768, 1366 e 1920 px verificadas em widgets e navegador. |
| Rolagem horizontal da página | documentWidth igual à largura da janela em todas as seis medidas. |
| Dois treinadores | QA Atual e QA Clássica conectados à mesma sala; CURRENT e CLASSIC_98 independentes. |
| Reconexão | Recuperou CLASSIC_98 e a mesma identidade/sala. |
| Troca de aparência | Preservou estado do shell, formação, titulares, banco, clube, rodada e contagem de conexões nos testes. |
| Partida em andamento | Navegação e troca de aparência bloqueadas; visualização compartilhada de partida preservada. |
| Anúncio | Seleção e cancelamento inspecionados no navegador; nenhum comando de venda/anúncio executado. |
| Banco | Troca de goleiro reserva com titular e retirada do banco verificadas, conservando o elenco. |
| Finanças | Cancelar não executa a obra; confirmar invoca o comando existente uma única vez no teste. |

O snapshot completo da API manteve o mesmo SHA-256 antes/depois das trocas visuais. O conteúdo bruto do save no PostgreSQL manteve o MD5 `8d31941352a7a9bd18a1b89406660705` nas duas leituras. Ambos os treinadores estavam conectados; não houve requisição de escrita nos endpoints do mundo durante a troca. Os hashes, medidas e resultados estão em `evidence.json`.

Os caminhos de carreira MANUAL, FOURTH_DIVISION_DRAW e UNEMPLOYED foram exercitados nos testes do lobby com a preferência preservada. A verificação manual com PostgreSQL utilizou a sala de QA existente com dois treinadores. Os testes de persistência também verificam reinício da API e igualdade literal do save SQLite; a etapa de preparação já validou reinício do PostgreSQL isolado.

Logs finais disponíveis no diretório de evidências: `flutter-tests.log`, `dotnet-tests.log`, `analyze.log` e `web-build.log`. A execução .NET usa os binários já compilados para evitar sobrescrever DLLs abertas pela API local em execução.

## Comparação visual e capturas

As referências fornecidas em `467c794e-.../Texto colado.txt` e `9d263e28-.../Texto colado.txt` foram lidas; a segunda define os menus e telas adicionais. Foram comparadas com as capturas reais da aplicação, sem inserir imagens de referência como telas funcionais.

A versão implementada reproduz a barra azul, menu cinza, fundo amarelo/dourado, ambiente externo verde, tabelas pretas com texto amarelo, seleção destacada e controles retangulares. O celular usa menu próprio por toque e atalhos inferiores. A paleta é fixa e não altera as cores cadastradas dos clubes.

A inspeção real identificou e corrigiu: fonte sem texto desenhado na Web, glifo de titular ausente, sobreposição do campo tático, menu apertado no tablet, quebra do texto do rodapé e largura da coluna de força do banco. A fonte embutida no projeto é usada; o titular é desenhado com ícone, sem depender de um glifo externo.

As capturas `captures/final-desktop-*.png` incluem principal, seis menus, jogador selecionado, jogador, confirmação de anúncio, aparência, táticas, mercado, banco, estádio, classificação, calendário, taça, carreira e rankings. `captures/final-mobile-*.png` inclui principal, menu, seleção, aparência, táticas, mercado, banco, estádio, campeonato, jogador e treinador. `captures/final-width-*.png` documenta as seis larguras. `captures/final-current-preserved.png` mostra a versão atual.

## Limites explícitos

História completa do clube, histórico individual de jogador e gestão dedicada de treinadores não têm fluxo correspondente completo no jogo atual; as entradas relevantes ficam “Em breve” ou são omitidas. Nenhum histórico foi inventado. Cancelamento de anúncio e últimas transferências não ganharam regras ou endpoints novos.

Rankings de jogadores e jogos ao vivo continuam usando componentes funcionais compartilhados dentro da apresentação clássica. Os critérios oficiais, inclusive mínimos de partidas quando aplicáveis, são calculados pelo sistema existente. Não há cálculo de ranking na nova apresentação.

Teclas de função reservadas pelo navegador não foram interceptadas. As formações e ações estão disponíveis nos menus e controles por toque.

Não há bloqueio técnico restante para testar esta entrega. A aprovação estética final e ajustes de fidelidade adicionais ficam com o proprietário, sobre estas capturas e a prévia executável.

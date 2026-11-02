# 015B integrada à 016B — Ranking, Carreira e Finanças

Destino autorizado pelo usuário: `feature/world-catalog-lobby-016b`, no worktree D:\elifoot_98_016B. Base preservada: 008c756. Origem: feature/classic-desktop-ui-015b, 8ed83c3; quatro commits pendentes desde 456cd40. A master e os demais worktrees não foram modificados.

As branches codex/visual-web-mobile-016b e codex/player-rankings-visual-016b já eram ancestrais da 016B. Não existia uma branch separada com o visual solicitado para Ranking & Carreira e Finanças. O merge pendente trouxe formação 5-3-2 com reescalação, plantel mobile, partida ao vivo, substituições compartilhadas e escolha de cobrador em pênalti durante o jogo. Disputa de pênaltis permanece automática.

## Resolução do merge

Quatro arquivos tiveram conflitos: main.dart, classic_ux_014b_test.dart, MultiplayerRoundOrchestrator.cs e Dtos.cs. Foram combinados os campos HasInitialLobby/LobbyStarted/GameModeId com PendingPenalty; mantidos o bloqueio de navegação e o overlay global da partida; unificados os componentes de navegação mobile. Os módulos de catálogo mundial, reconexão e rankings de jogadores da 016B foram conservados.

O MatchEngine e a resolução de pênalti são exatamente os da branch 015B aprovada. Não se introduziu nova fórmula defensiva. Os motores econômicos e de leilão são idênticos ao checkpoint 008c756. Alterações de apresentação financeira não modificam lançamentos, principal, juros ou salário contratado.

## Telas solicitadas

- Ranking de treinadores: painel claro com cabeçalho verde, linhas alternadas, destaque turquesa para humanos e dados recebidos do servidor. Desktop mostra a tabela completa; mobile mostra cinco treinadores e um botão funcional para abrir o ranking completo. O item Ranking do menu inferior fica destacado nesta tela.
- Carreira: aba separada, mantendo moral em barra, histórico, convites, consequências da aceitação e premiações. O detalhe do treinador continua acessível pelo ranking.
- Finanças e estádio: oito indicadores, caixa destacado em amarelo, painel de estádio com ilustração vetorial, ações de reforma/ampliação/empréstimo, banco/contratos e livro-caixa. Totais rotulados como “nesta rodada” consideram apenas a temporada/rodada exibida; o saldo em caixa continua sendo o valor do servidor. Valores e percentuais incorretos da imagem de referência não foram copiados: o empréstimo continua em R$ 500.000 e R$ 25.000 de juros por partida.

## Validação

Flutter: 273 testes aprovados, análise sem apontamentos e build web aprovada. A matriz nova cobre 320, 360, 390, 430, 768, 1366 e 1920 px, ranking completo, carreira, comandos visuais e filtro financeiro por rodada. A regressão anterior de salários, empréstimos legados e proteção de renovação obrigatória permanece passando.

A primeira execução do merge encontrou falhas na conciliação dos menus mobile; foram corrigidas, sem remover os cenários. Um estouro de linha no livro-caixa em 320 px foi corrigido com quebra do bloco de data/valor. O teste que antes acessava ranking e carreira na mesma página passou a navegar explicitamente entre as abas.

Resultados .NET: 438 domínio, 17 aplicação e 258 integração, todos aprovados. Simulações: três cenários aprovados, incluindo duas execuções específicas de 100 temporadas (masculino e feminino), com 35.100 jogos reais de motor por categoria e zero violações de escalação, elenco ou liquidação financeira duplicada. Foram executados 716 casos .NET nesta integração; os demais cenários longos de simulação da regressão anterior não foram contados como uma nova execução. Detalhes estão em validation.json. Capturas em Flutter: oito arquivos, com fonte e instante gravados em scene-provenance.json. A cena foi obtida por GETs reais, com zero comandos de escrita. As imagens são renders Flutter, não screenshots de Chrome. A validação visual no navegador continua dependente da permissão efetiva do navegador local; não foi contornada por outro mecanismo. Nenhuma aprovação manual do usuário foi declarada.

## Imagens

[Ranking desktop](flutter-renders/coach-ranking-1366.png) · [Ranking mobile](flutter-renders/coach-ranking-390.png) · [Ranking completo mobile](flutter-renders/coach-ranking-complete-390.png)

[Carreira desktop](flutter-renders/coach-career-1366.png) · [Carreira mobile](flutter-renders/coach-career-390.png)

[Finanças desktop](flutter-renders/finances-top-1366.png) · [Finanças mobile](flutter-renders/finances-top-390.png) · [Livro-caixa mobile](flutter-renders/finances-ledger-390.png)

O runner agora aguarda o health check web antes de anunciar o endereço. O caminho ApiOnly informa somente a API. A correção foi conferida com reinício exclusivo do servidor web registrado e comparação do hash de main.dart.js.

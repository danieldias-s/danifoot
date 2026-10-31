# Auditoria visual — rebalanceamento 014C

Worktree: `D:\elifoot_98_014C`; branch: `feature/club-colors-theme-014c`; base: `9690d9578aba2ec6a35b344921d9429478bd54ab`.

## Método e proveniência

As duas imagens fornecidas pelo proprietário orientam as cores: identidade geral verde com destaque amarelo, confrontos identificados pela paleta de cada clube. Esta missão mantém os componentes e a navegação existentes; a imagem de referência não foi usada como alegação de uma interface já implementada.

Os 22 PNGs de `audit-014c-rebalance/automated-renders/` foram gerados pelo renderer Flutter real a partir de snapshots HTTP gravados da API própria (5155), em 390 × 1000 e 1366 × 1000. O harness usa `SceneApi`: login, clubes, plantel, snapshot e paletas vêm das cenas gravadas; dados auxiliares de calendário/mercado/classificação usam os adaptadores explícitos do teste anterior. As preferências são substituídas pelo modo legado `Club` para verificar a nova interpretação verde. Esses renders **não são capturas de navegador nem prova de funcionamento do SignalR no navegador**.

As cenas `live-league-grouped` e `live-cup-grouped` usam o mundo real de revisão `014C Rebalance Verde`, criado com clubes reais e elencos gerados autorizados, executando o motor existente. A liga está em R3 com 16 jogos (4 divisões); a taça está em R5 com 32 jogos. Os snapshots originais ficam em `artifacts/014c-rebalance/scenes/`; os hashes e dimensões de todas as imagens constam em `audit-014c-rebalance/visual-evidence-manifest.json`.

As evidências históricas em `docs/audit-014c/` são preservadas. Saídas de testes anteriores que já haviam alterado essa pasta ao retomar o trabalho foram arquivadas localmente e os arquivos históricos foram restaurados do Git. São 106 saídas preservadas em `artifacts/014c-rebalance/recovered-test-outputs/`; nenhum arquivo foi apagado. O manifesto está em `audit-014c-rebalance/historical-evidence-preservation.json`.

## Conferência dos renders

| Evidência (cada uma em 390 e 1366 px) | Observação |
|---|---|
| `general-red-club`, `general-blue-club` | Flamengo e Manchester City compartilham topo, painéis, botões e navegação verdes; destaque amarelo. |
| `matches-red-board`, `matches-blue-board` | Paletas independentes por equipe; placar centralizado; quatro divisões agrupadas. |
| `live-league-grouped` | Snapshot real durante a liga, 16 confrontos, quatro grupos e identificação do clube controlado. |
| `live-cup-grouped` | Snapshot real durante a taça, fase agrupada com 32 jogos; Flamengo vermelho/preto e Manchester City azul/branco. |
| `appearance-menu` | Variação verde, brilho, cores fortes/neutras, opções avançadas recolhidas e restauração do padrão. |
| `calendar-red-club`, `divisions-red-club`, `cup-grouped`, `market-red-club` | Estrutura geral verde; cores dos clubes apenas nos blocos aplicáveis. |

Mobile mantém a navegação horizontal e tabelas com rolagem já existentes. Nomes longos no quadro de partidas são abreviados visualmente para proteger o placar; não se alterou o nome salvo. Clubes sem paleta registrada permanecem neutros. O contraste dos tokens e blocos é verificado por testes; tons exibidos podem ser ajustados para leitura, sem alterar as cores do catálogo.

## Capturas reais no navegador

PASS. Foram capturadas **16 imagens originais** pela ferramenta de navegador suportada, em `audit-014c-rebalance/browser-captures/`, sem alterar bytes das imagens. A restrição inicial do Work foi removida pelo proprietário antes de continuar. A primeira carga ainda exibiu o build anterior em cache; uma recarga trouxe o build validado, e todas as evidências aceitas desta pasta usam a versão nova.

| Imagens reais | Validação |
|---|---|
| `general-red-desktop/mobile`, `general-blue-desktop/mobile` | Flamengo e Manchester City mantêm o tema geral verde. Desktop em 1280/1366 px e mobile em 390 px. |
| `cup-red-desktop/mobile`, `cup-blue-mobile` | Fase da taça com 32 jogos, cores independentes por equipe e placar central. |
| `league-red-desktop`, `league-blue-desktop/mobile` | Liga R6 com 16 jogos e os quatro grupos de divisão. |
| `appearance-mobile`, `appearance-advanced-desktop` | Variações verdes, brilho, modo neutro, opção antiga rebaixada para avançadas. |
| `live-update-blue-41-mobile` | As duas sessões mudaram do minuto 40 para o 41 sem recarga. |
| `calendar-blue-desktop`, `market-blue-desktop`, `finance-blue-desktop` | Estrutura geral verde; calendário usa blocos por equipe; operações financeiras não foram acionadas. |

O menu Aparência foi usado pela interface real: salvar `Escuro`, brilho `+1`, modo `Neutral`; recarregar a página; confirmar os valores; restaurar o padrão `Classic` com cores fortes. A sessão do outro treinador manteve suas preferências privadas. A atualização em tempo real foi provocada com um tick do motor existente na API local, e observada nas duas sessões sem recarga. Os overrides temporários de viewport foram removidos ao terminar.

O importador `scripts/capture-theme-rebalance-014c.mjs` preserva o original e registra SHA-256 em arquivo auxiliar. Os hashes constam também do manifesto visual. A aba do clube azul permanece disponível para revisão em `http://127.0.0.1:5156/`.

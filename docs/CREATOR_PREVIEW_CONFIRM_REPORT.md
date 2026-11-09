# Criador de Mundos — Prévia e Confirmar

## Escopo e estado de trabalho

Implementação das etapas 7/8 e 8/8 na worktree `D:/elifoot_98/.worktrees/local-historical-catalog`, branch `codex/creator-preview-confirm`. O ponto inicial inspecionado foi `96d5425`. O iniciador e sua correção de autenticação, anteriores a esta missão, foram preservados em um checkpoint separado: `4359435`.

A imagem da seleção de anos foi considerada como contexto do fluxo existente. A missão textual pede as duas últimas telas. A seleção dinâmica de países e anos continua usando o catálogo local; os exemplos de clubes, anos e jogadores das imagens não foram inseridos no catálogo.

## Componentes e arquivos

| Arquivo | Alteração |
| --- | --- |
| `client/elifoot_flutter/lib/creator_world_review.dart` | Apresentação distinta de Prévia e Confirmar, resumos, divisões, consulta de elencos, distritais paginados, início do mundo e avisos. |
| `client/elifoot_flutter/lib/creator_club_placement.dart` | Validação das 128 identidades e troca recíproca das posições, sem alterar dados de clubes ou atletas. |
| `client/elifoot_flutter/lib/world_creator.dart` | Integração com o estado compartilhado, revalidação do snapshot, trocas, navegação e confirmação existente. |
| `client/elifoot_flutter/lib/creator_division_picker.dart` | Extensão compatível do seletor existente para incluir os distritais e filtrar apenas clubes da montagem. |
| `client/elifoot_flutter/lib/creator_visual.dart` | Ação final persistente, botão com confirmação e progresso compacto nas duas etapas finais. |
| `src/Elifoot.Contracts/Catalog/WorldCreatorDtos.cs` | Comando de redistribuição do snapshot. |
| `src/Elifoot.Api/WorldCreatorEndpoints.cs` | Endpoint autenticado de redistribuição. |
| `src/Elifoot.Domain/Catalog/WorldCreatorEngine.cs` | Revalidação final e permutação do snapshot, reutilizando a ordem e o sorteio distrital existentes. |
| `src/Elifoot.Infrastructure/Catalog/WorldCreatorService.cs` | Rascunhos imutáveis, validação do proprietário e serialização da confirmação. |
| `src/Elifoot.Infrastructure/EfWorldRepository.cs` | Publicação no cache somente após a gravação durável do primeiro mundo. |

Foram reutilizados `CreatorFrame`, `CreatorBackdrop`, `CreatorCountryFlag`, `ClassicTheme`, os escudos de paleta existentes, os componentes de proveniência dos atletas e o processo original de criação e salvamento. As montagens privadas salvas conservam seu editor e seu fluxo próprios.

## Comportamento

- A Prévia mostra nome, anos, países, divisões, distritais, modo de elencos e contagem efetiva dos jogadores.
- Cada divisão apresenta os oito clubes na ordem da montagem. Os distritais podem ser consultados integralmente por páginas de dez clubes, pesquisa e filtro de país.
- A identificação do ano usa a variante completa; o nome recebe o sufixo quando há mais de um ano selecionado.
- A consulta ao elenco informa a origem dos atletas. Jogadores com origem desconhecida não são classificados como reais.
- A confirmação permite trocas entre quaisquer posições ocupadas nas quatro divisões e nos distritais. O seletor conserva filtros de país, ano e nome, além da divisão atual.
- Uma troca é recíproca: o clube substituído ocupa a posição anterior do clube escolhido. Permanecem 4 × 8 clubes principais e 96 distritais.
- O primeiro snapshot válido já é compartilhado entre as etapas. Editar divisões, voltar ou atualizar a prévia preserva as escolhas e os atletas gerados.
- Alterar explicitamente Ranking ou Aleatório reorganiza somente os distritais atuais pela rotina existente no servidor. Uma simples atualização não refaz o ranking sobre uma escolha manual.
- Mudanças em categoria, países, anos, seed ou origem dos elencos invalidam o snapshot incompatível. Nome e limite de treinadores podem ser atualizados sem recriar clubes ou atletas.
- As etapas finais usam a aparência moderna verde com painéis claros nas duas versões do jogo. A navegação inferior reserva espaço no layout e respeita a área segura.
- O início do mundo mostra os dados conhecidos da configuração. Datas, calendários e competições não fornecidos pela prévia não são inventados.
- Erros reais bloqueiam a continuação e a criação; avisos permanecem consultáveis e não são convertidos silenciosamente em bloqueios.

## Contrato e gravação

`POST /api/world-creator/preview/{draftId}/reassign` recebe as 128 atribuições ordenadas, nome e limite de treinadores opcionais. `districtMode` é enviado somente quando o jogador escolhe uma nova ordenação distrital.

O servidor aceita apenas uma permutação das mesmas variantes que já constam na prévia pertencente ao treinador. O resultado recebe outro `draftId` e mantém os elencos, forças e referências do snapshot. A confirmação continua usando `POST /api/world-creator/confirm/{draftId}`.

A confirmação revalida contagens, duplicações, países, anos, categoria, modo e elencos. Um recibo durável conserva o mesmo ID do mundo em uma repetição. Uma trava por draft e uma trava PostgreSQL entre processos impedem publicações concorrentes. O repositório salva a estrutura em uma operação transacional antes de torná-la visível no cache. A falha do primeiro salvamento foi testada com posterior repetição usando o mesmo recibo.

## Validação técnica

- Domínio completo: **537 testes aprovados**.
- Aplicação: **31 testes aprovados**.
- Integração do criador: **37 testes aprovados**.
- Regressões de lobby/multiplayer: **21 testes aprovados**.
- PostgreSQL: teste real em schema isolado para a trava e a publicação durável; testes de redistribuição, proprietário, expiração, falha e repetição.
- Flutter completo: **476 testes aprovados**; `flutter analyze --no-pub` sem apontamentos.
- Build API Release: aprovado, sem avisos e sem erros.
- Build Web Release: aprovado e disponibilizado na prévia local.
- Matriz de widgets: 360, 390, 430, 768, 1366 e 1920 px, nas duas aparências.

Os runners PostgreSQL legados 013A/013C que exigem configurações próprias não foram contados como aprovados nas tentativas sem tais configurações. A verificação PostgreSQL nova foi executada efetivamente.

Logs da missão em `artifacts/creator-review-qa`; logs .NET em `artifacts/local-historical-qa/backend-review-*.log`. A primeira compilação Web usou um caminho relativo que o compilador nativo de shaders do Windows não conseguiu gravar; a repetição utiliza o caminho absoluto da mesma pasta de QA. Nenhum arquivo do MatchEngine ou da evolução de forças foi alterado.

## Capturas reais

As capturas usam exclusivamente os arquivos e bancos sintéticos de QA já existentes, com Portugal/Espanha e variantes de 1985/2025. O catálogo canônico continua aguardando o cadastro dos dados definitivos. A inspeção visual humana permanece com o proprietário.

| Captura | Arquivo |
| --- | --- |
| Prévia desktop | [Abrir](../artifacts/creator-review-qa/screenshots/previa-desktop.jpg) |
| Prévia mobile | [Abrir](../artifacts/creator-review-qa/screenshots/previa-mobile.jpg) |
| Confirmar desktop | [Abrir](../artifacts/creator-review-qa/screenshots/confirmar-desktop.jpg) |
| Confirmar mobile | [Abrir](../artifacts/creator-review-qa/screenshots/confirmar-mobile.jpg) |
| Trocar desktop | [Abrir](../artifacts/creator-review-qa/screenshots/trocar-desktop.jpg) |
| Trocar mobile | [Abrir](../artifacts/creator-review-qa/screenshots/trocar-mobile.jpg) |
| Divisão expandida desktop | [Abrir](../artifacts/creator-review-qa/screenshots/divisao-desktop.jpg) |
| Divisão expandida mobile | [Abrir](../artifacts/creator-review-qa/screenshots/divisao-mobile.jpg) |
| Distritais | [Abrir](../artifacts/creator-review-qa/screenshots/distritais.jpg) |
| Avisos | [Abrir](../artifacts/creator-review-qa/screenshots/avisos.jpg) |

## Entrega

Validação de navegador, dez capturas reais e checkpoint final concluídos no ramo `codex/creator-preview-confirm`. A aplicação permanece disponível localmente em `http://127.0.0.1:5282/#/plantel`, com a API em `http://127.0.0.1:5281`. Não houve merge ou publicação em produção.

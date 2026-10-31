# ELIFOOT_VISUAL_THEME_REBALANCE_014C — Tema verde fixo + cores dos clubes nas partidas

Worktree `D:\elifoot_98_014C` • branch `feature/club-colors-theme-014c`.

## 1. Decisão

| Conceito | Antes (014C) | Agora (rebalanceamento) |
|---|---|---|
| **APP THEME** (cabeçalho, menu, painéis, tabelas, botões, filtros, calendário, mercado, finanças, rankings, carreira, divisões, taça, Aparência, editor pré-save) | Cor do clube do treinador pintava a interface inteira (ex.: Flamengo deixava tudo vermelho) | **Verde clássico fixo** ("Liga Clássica 98"), igual para qualquer clube: fundo verde, topo e menu verde-escuro, destaque amarelo `#FFD700`, texto branco |
| **MATCH TEAM COLORS** (placares ao vivo, confrontos, divisões agrupadas, Taça por fase, listas de jogos do calendário e da Taça) | Já usava a paleta de cada time no quadro de jogos | Mantido e ampliado: cada time aparece com **sua própria** paleta, inclusive na tabela da Taça e no calendário |

A infraestrutura de cores da 014C (catálogo, fontes, persistência, `ColorPalette`, `TeamColorName`, seletor de cores) **não foi removida**; mudou apenas o local de aplicação.

## 2. Implementação

- `client/elifoot_flutter/lib/club_theme.dart`
  - `ClassicTheme.classicGreen({variant, brightness, strongTeamColors})`: tokens verdes fixos (mesmos tons da base pré-014C: `#0A4D1C`, `#0E3B19`, `#10421C`, `#073612`, `#062E0F`, borda `#3E8450`, botão `#136F2D`, destaque `#FFD700`). Variações `Classico`, `Escuro`, `AltoContraste`; brilho de -2 a +2, sempre com contraste ≥ 4,5:1.
  - `ClassicTheme.derive(...)`: modos `Classic`, `Club` (legado) e `Neutral` → verde fixo. `Custom` e `ClubGlobal` → tema dinâmico (opções avançadas).
  - Novo token `strongTeamColors`: `TeamColorName` pinta o bloco do time com a paleta registrada (cores fortes) ou usa bloco neutro com faixa lateral na cor do clube (modo neutro).
- `lib/main.dart` / `lib/shell_actions.dart`: diretório `_clubPaletteById/_clubPaletteByName` carregado uma vez por mundo via `GET /api/worlds/{id}/clubs`. A tabela da Taça e as linhas do calendário passaram a usar `TeamColorName` (mandante no bloco esquerdo, placar central e visitante no bloco direito). Se a lista de clubes falhar, os blocos ficam neutros e nada mais é afetado.
- `lib/appearance_settings.dart`: menu reorganizado (ver seção 3).
- Backend: `AppearancePreferencesDto` recebeu campos **opcionais** `ThemeVariant`, `Brightness`, `MatchColors`; `ClubColorPolicy.Validate` aceita os modos `Classic` e `ClubGlobal` e valida os novos campos. Nenhuma regra de jogo foi tocada.

Clubes sem paleta verificada/personalizada continuam **neutros** nas partidas: nenhuma cor é inventada.

## 3. Menu APARÊNCIA

1. **TEMA DO JOGO**: explica que a interface é verde em qualquer clube.
2. **VARIAÇÃO DO VERDE CLÁSSICO**: CLÁSSICO / ESCURO / ALTO CONTRASTE.
3. **BRILHO**: `- BRILHO` / `+ BRILHO` (−2 a +2).
4. **CORES DOS CLUBES NAS PARTIDAS**: CORES FORTES DOS CLUBES (padrão) / MODO NEUTRO, com amostra do próprio clube.
5. **OPÇÕES AVANÇADAS** (recolhidas, marcadas como não recomendadas): PERSONALIZAR CORES (tema manual da 014C), **COR DO CLUBE NO JOGO INTEIRO** (comportamento global antigo, rebaixado a opção avançada), VOLTAR AO VERDE CLÁSSICO.
6. **RESTAURAR PADRÃO** grava `mode: Classic`.

O botão "USAR CORES DO CLUBE" como comportamento principal foi descontinuado.

## 4. Compatibilidade

- Preferências salvas antes do rebalanceamento (`{"mode":"Club"}`) passam a renderizar o verde clássico; `Custom` continua funcionando exatamente como antes; versões e escopo privado (usuário/mundo/clube) não mudaram.
- JSON antigo sem os novos campos é desserializado sem alterações (teste `PreferencesSavedBeforeTheRebalanceStillDeserialize`).
- Salvar aparência não altera o snapshot do mundo (teste `ClassicGreenPreferencesPersistPrivatelyWithoutTouchingTheWorld`).
- MatchEngine, economia, leilões, contratos, 012C/012D, 013A/B/C, 014A/B/C, persistência e multiplayer: sem alterações de regra.

## 5. Testes

- Flutter `test/visual_theme_rebalance_014c_test.dart` cobre: tema verde para clube vermelho e para clube azul (derive + shell em 390/1366); variações/brilho legíveis; preferências antigas, `Custom` e `ClubGlobal`; Flamengo vermelho/preto e clube azul azul/branco no quadro (390/1366); clube sem paleta neutro; placar centralizado; quatro divisões agrupadas; modo neutro; calendário e Taça coloridos por time; menu Aparência (variação, brilho, cores de partida, salvar, recarregar, restaurar).
- `test/club_theme_014c_test.dart` foi atualizado para a nova decisão (a troca de clube continua carregando a paleta, mas o tema global permanece verde).
- .NET `tests/Elifoot.Integration.Tests/VisualThemeRebalance014CTests.cs`: desserialização legada, validação dos novos campos, persistência privada.

## 6. Evidências

`docs/audit-014c-rebalance/` (logs, renders automatizados e capturas de navegador). Detalhes em `docs/VISUAL_AUDIT_014C_REBALANCE.md`.


## 7. Validação efetiva e compatibilidade

`docs/audit-014c-rebalance/test-summary.json` resume resultados conferidos nos logs e no TRX:

| Verificação | Resultado |
|---|---|
| `dotnet build Elifoot.slnx -c Release` | PASS; 0 erros, 0 avisos. |
| Regressão .NET | **628/628**: Application 7, Domain 409, Integration 186, Simulation 26; 0 falhas e 0 ignorados. |
| `flutter analyze` | **0 issues**. |
| `flutter test` | **134/134**; inclui os testes existentes de leilão e as 14 verificações do rebalanceamento. |
| `flutter build web --release` | PASS; build de revisão aponta à API própria 5155. |
| Harness de renders | **2/2**, 22 PNGs; contraste ≥ 4,5:1 em todos os blocos de equipes das cenas renderizadas. |
| Node aplicável | **2/2**; sintaxe do importador de capturas também válida. |
| Navegador real | **16 capturas**, desktop e mobile, dois treinadores e clubes reais, liga/taça, menu Aparência e atualização sem recarga. |
| HTTP + PostgreSQL | Novos campos mantidos após reinício real da API; quatro escritas concorrentes → uma aceita e três conflitos; outro treinador e snapshot do mundo preservados. |
| Saves antigos | Dois mundos já existentes, quatro contextos privados: todos os campos antigos e fingerprints preservados; novos campos opcionais retornam nulos. |

O comando de solução retomado já havia terminado Application/Domain/Integration. Para recolher a simulação interrompida, somente o projeto Simulation foi executado em Release, sem recompilar, no ambiente 014C com diretório de evidências isolado. Todos os 26 casos passaram em 39m45s; o TRX está versionado. Não se somaram resultados de uma base anterior para declarar a regressão atual.

As simulações completaram 100 temporadas em cada cenário de integração 014A (masculino/feminino), mundos reais 014B (masculino/feminino), economia 012D e os demais cenários da suíte. Os valores a seguir são resultados de cenários distintos, não saldos somados:

| Cenário, 100 temporadas | Jogos | Caixa final | Dívida final |
|---|---:|---:|---:|
| Mundo real 014B masculino | 35.100 | R$ 86.753.694,92 | R$ 0,00 |
| Mundo real 014B feminino | 35.100 | R$ 103.405.567,05 | R$ 0,00 |
| Fixture de integração 014A masculino | 35.100 | R$ 79.157.786,17 | R$ 4.366.066,93 |
| Fixture de integração 014A feminino | 35.100 | R$ 79.157.786,17 | R$ 4.366.066,93 |
| Economia 012D/balanceamento final | 35.100 | R$ 137.353.919,79 | R$ 3.270.104,68 |

Todos os cinco resumos registram zero violações de escalação, elenco mínimo e liquidação financeira duplicada. Dívidas não nulas continuam registradas. A contabilidade e a evolução não foram recalibradas por esta missão. Resumos originais em `audit-014c-rebalance/simulation-results/`; livros brutos completos preservados em `artifacts/014c-rebalance/simulation-verified-original/`, com hashes no manifesto versionado. O campo `SourceCheckpoint` dos resumos é metadado legado dos próprios cenários; a base efetivamente testada nesta missão é `9690d9578aba2ec6a35b344921d9429478bd54ab` mais as alterações deste checkpoint.

MatchEngine, motores econômicos, camada de aplicação, persistência e configuração de leilões não têm alterações de código em relação à base. Na `main.dart`, a rotina de leilão foi apenas formatada. Os quatro outros worktrees mantêm seus commits e nenhum arquivo rastreado alterado, conforme `scope-preservation.json`. Não houve merge, push ou publicação em produção.

## 8. Relatório final solicitado

| Campo | Resultado |
|---|---|
| TASK | ELIFOOT_VISUAL_THEME_REBALANCE_014C |
| STATUS | **PASS** |
| GLOBAL_THEME_GREEN | PASS — padrão verde fixo, inclusive para clube vermelho/azul e preferência legada `Club`. |
| MATCH_COLORS_BY_TEAM | PASS — blocos individuais em partidas, liga, taça e calendário; catálogo preservado. |
| DESKTOP | PASS — testes, renders e capturas reais. |
| MOBILE | PASS — testes e capturas reais em 390 px; navegação e rolagem existentes preservadas. |
| APPEARANCE_MENU | PASS — variações verdes, brilho, cores fortes/neutras, salvar/recarregar/reset; opções antigas em avançadas. |
| DOTNET_TESTS | PASS — 628/628; build Release sem erros/avisos. |
| FLUTTER_TESTS | PASS — 134/134; analyzer 0; build web PASS; harness 2/2. |
| NODE_TESTS | PASS — 2/2. |
| VISUAL_CAPTURES | PASS — 16 capturas originais de navegador + 22 renders identificados separadamente. |
| CHECKPOINT_COMMIT | Commit local que versiona este relatório na branch `feature/club-colors-theme-014c`; hash exato entregue no encerramento e registrado em `artifacts/014c-rebalance/checkpoint.json`. |
| KNOWN_ISSUES | Nenhum bloqueio desta missão. Algumas fases do calendário ainda exibem identificadores técnicos já existentes; nomes longos são abreviados no mobile; clubes sem paleta verificada permanecem neutros. |

A referência moderna foi usada para a identidade de cores; a navegação não foi redesenhada como uma cópia integral da imagem. Preferências `Custom` já salvas continuam explícitas e funcionais. A opção global do clube permanece disponível somente em avançadas, conforme autorizado na missão.

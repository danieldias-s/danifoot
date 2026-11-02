# Padronização visual Web e Mobile — 016B

**STATUS: PASS técnico.** Implementação e inspeção visual concluídas. Aprovação manual pelo usuário permanece uma etapa independente.

## Escopo e isolamento

Worktree: `D:\elifoot_98_016B`; branch `codex/visual-web-mobile-016b`; base `244c79a` (016A). As três instruções anexadas são idênticas. As seis referências originais estão em `evidence-016b/references`.

Esta entrega altera apresentação Flutter, componentes de navegação e runners locais. Os contratos HTTP, autenticação, regras de prontidão, relógio global, MatchEngine, táticas, mercado, economia e persistência do servidor permanecem com suas implementações da base. Três arquivos de testes .NET receberam somente a identificação do novo banco isolado na lista de ambientes permitidos.

API `http://127.0.0.1:5173`; Web `http://127.0.0.1:5174`; PostgreSQL exclusivo `127.0.0.1:55442`. Saves de outros ambientes não foram apagados. Não houve merge nem publicação.

## Telas e comparação com as referências

| Área | Implementação |
|---|---|
| Identidade global | Verde clássico, amarelo na seleção, tipografia condensada, bordas moderadas, títulos verdes, cartões claros e moral em barra conforme a preferência anterior do usuário. |
| Desktop | Logo e contexto da rodada, data, ação real do treinador, navegação lateral com destaque amarelo, Aparência e Trocar sala. Tabelas aproveitam toda a largura disponível. |
| Mobile | Cabeçalho compacto, card do clube ocupando a largura, quatro cartões menores e navegação inferior persistente com área segura. Ações e dados se reorganizam; não há escala global do desktop. |
| Aparência | Painéis claros, variantes do verde, brilho, cores das partidas, opções avançadas, prévia e salvamento. Tema personalizado, escuro e alto contraste continuam disponíveis. O rascunho não salvo é descartado ao sair. |
| Divisões e Taça | Abas de classificação/taça e quatro divisões. Classificação e confrontos da Taça em painéis claros com faixas alternadas; no celular o nome completo aparece e a linha expande os detalhes de técnico, gols e zona. Resumo das rodadas usa o calendário real. |
| Mercado e Leilão | Abas independentes, filtros responsivos, tabelas claras no desktop e linhas expansíveis no celular. Detalhes, lance, compra direta, anúncio e propostas continuam disponíveis. O salário exibido vem de SalaryPerRound do servidor. |
| Calendário e Resultados | Rodadas expansíveis, cabeçalhos verdes, linhas claras, situação real, filtros e nomes completos com identidade visual por clube quando houver paleta cadastrada. Rótulos das competições são traduzidos somente na apresentação. |

As imagens orientam o desenho, não o conteúdo. Nomes, treinador, caixa, moral, contratos, tabelas e partidas vêm da API. O botão superior continua apresentando a ação permitida pelo estado da sala; não se inventou um AVANÇAR que ignore a prontidão multiplayer. Clubes sem paleta cadastrada usam o fallback neutro existente. Não foram inventados escudos nem atletas reais. As salas de QA usam clubes reais do catálogo e jogadores explicitamente marcados FICTIONAL_GENERATED, conforme o modo autorizado na base.

## Componentes e arquivos

Reutilizados: ClassicTheme, ClassicPanel, AppHeader, AppSidebar, ClassicSummaryCard, TeamColorName, MoraleBar, MarketScreen e AppearanceSettings. Novos componentes em `client/elifoot_flutter/lib/visual_components.dart`: ClassicPaper/ClassicPaperPanel, ClassicSelectionTabs, ClassicBottomNavigation, ClassicStandings, ClassicRoundSummary e ClassicFixtureList.

Telas alteradas em `lib/main.dart`, `lib/shell_actions.dart`, `lib/market_screen.dart`, `lib/appearance_settings.dart`; estilos em `lib/club_theme.dart` e `lib/classic_layout.dart`; barra em `lib/morale_bar.dart`. A fonte Barlow Condensed acompanha a licença OFL em `assets/fonts/OFL.txt`; origem: [Google Fonts](https://github.com/google/fonts/tree/main/ofl/barlowcondensed).

## Testes e evidências

- Flutter analyzer: zero problemas.
- Flutter: 179 testes; inclui seis novos casos com todas as telas prioritárias em 360, 390, 430, 768, 1366 e 1920 px, navegação inferior, retorno do tema sem salvar e contraste do texto das tabelas claras.
- Testes existentes continuam cobrindo prontidão com um/vários treinadores, bloqueio da navegação durante a partida, leilão voluntário R$ 500.000/duas rodadas, filtros, salários, preferências e cores de cada clube nos confrontos.
- .NET: Application 7/7; Domain 425/425; Integration 222/222. A primeira execução detectou quatro rejeições da porta nova nas proteções dos próprios testes. Após cadastrar exclusivamente o ambiente 016B, a suíte completa de integração passou, sem ignorar casos.
- Simulation: 26/26, com execução efetiva das simulações de longo prazo (53 min 37 s). Total .NET aprovado por projeto: **680/680**, sem casos ignorados. O resumo reconciliado está em `artifacts/016b/dotnet-summary.json`.
- Build Web Release aprovada. SHA256 do artefato servido: `f1b419d95416e0008cc6cc9b27ebaf44767ed0d2948a2119befff2ef4e37927c`. A build e o servidor local foram reconciliados pelo health check exclusivo 016B.
- Navegador: Chromium do navegador integrado, viewports emulados; não equivale a teste em aparelho físico.

Interações reais pelo navegador, nesta base e banco isolado: salvar aparência, recarregar e recuperar MODO NEUTRO, restaurar padrão; recusar lance de 499.999, registrar lance de 500.000; confirmar compra de 650.000. Conferência posterior pela API: débito do comprador e crédito do vendedor exatos, dinheiro conservado, atleta transferido, anúncio removido e salário negociado de 2.645 preservado. Valores são moeda do jogo. A prova fica em `artifacts/016b/browser-financial-checks.json` (sem credenciais).

Os testes antigos foram adaptados para entrar na aba Taça, usar a nova classe de seletor, rolar até os controles e voltar da Aparência incorporada ao shell. As verificações de domínio e persistência foram mantidas.

## Executar para revisão

No PowerShell, em `D:\elifoot_98_016B`: `./scripts/run-local-016b.ps1 -NoBuild`. Em uma cópia sem os artefatos de build, omitir `-NoBuild`.

O runner verifica a identidade dos processos e preserva serviços de outros ambientes. Dados, credenciais locais, builds, logs e resultados extensos ficam em `artifacts/016b`, ignorado no Git. A sala da auditoria é **016B - Revisão visual**, código **ELI60A770AF**. Para reconectar ao treinador usado na auditoria local, informar apelido **QA 016B Masculino** e ID de dispositivo **016b-qa-male** no login LOCAL_DEV; clube Flamengo RJ. A sala também contém o treinador vendedor de QA, por isso entrar com outra identidade respeita o limite normal da sala.

## Limites da entrega

A fidelidade foi avaliada pela inspeção de telas reais, mantendo o conteúdo e os comandos existentes. Não se trata de reprodução de dados ou de uma comparação pixel a pixel. Escudos ausentes e paletas não cadastradas continuam com os fallbacks existentes. Aprovação manual do jogo pelo Daniel não foi declarada. Não foram implementadas mudanças de regras ou de conteúdo do catálogo nesta missão.

## Comparação e correções encontradas pela inspeção

Foram corrigidos texto branco herdado sobre tabelas claras, cabeçalho sem espaço suficiente para ações longas, detalhes de salário ocultos nas linhas compactas, título do mercado fora do painel claro, altura excessiva da tabela do calendário no celular e contraste das linhas recolhidas. A Taça reaproveita o mesmo painel claro, mantendo seus cálculos, campeão, etapas e placares. Os nomes de competição reconhecem os valores reais CupRoundOf64/32/16 do contrato, sem alterar o payload.

A identidade reproduz a estrutura, hierarquia, seleção amarela, painéis, faixas e navegação das referências. As diferenças intencionais são o uso de dados atuais da sala, a barra de moral já solicitada e as ações/abas efetivamente disponíveis na base. Não foram copiados o erro de limite de treinadores, nomes ou valores ilustrativos das referências.

## Inventário de arquivos

- `client/elifoot_flutter/lib/appearance_settings.dart`
- `client/elifoot_flutter/lib/classic_layout.dart`
- `client/elifoot_flutter/lib/club_theme.dart`
- `client/elifoot_flutter/lib/main.dart`
- `client/elifoot_flutter/lib/market_screen.dart`
- `client/elifoot_flutter/lib/morale_bar.dart`
- `client/elifoot_flutter/lib/shell_actions.dart`
- `client/elifoot_flutter/pubspec.yaml`
- `client/elifoot_flutter/test/classic_desktop_015b_test.dart`
- `client/elifoot_flutter/test/club_theme_014c_test.dart`
- `client/elifoot_flutter/test/complete_gameplay_011_test.dart`
- `client/elifoot_flutter/test/visual_theme_rebalance_014c_test.dart`
- `tests/Elifoot.Integration.Tests/Integration014ATests.cs`
- `tests/Elifoot.Integration.Tests/PlayableWorld013CIntegrationTests.cs`
- `tests/Elifoot.Integration.Tests/WorldCreator013ATests.cs`
- `client/elifoot_flutter/lib/visual_components.dart`
- `client/elifoot_flutter/test/visual_standard_016b_test.dart`
- `client/elifoot_flutter/assets/fonts/`
- `scripts/run-local-016b.ps1`
- `scripts/test-visual-016b.ps1`
- `scripts/serve-web-016b.mjs`
- `scripts/review-visual-016b.py`
- `scripts/prepare-visual-market-016b.py`
- `docs/evidence-016b/`

## Capturas e resultado final

[Galeria desktop e mobile](evidence-016b/GALLERY.md), [matriz real de larguras](evidence-016b/browser-widths.json) e [manifesto de validação](evidence-016b/validation.json). As quatro telas prioritárias foram inspecionadas em desktop e mobile; as demais larguras têm captura adicional e os seis viewports exercitam todas as telas prioritárias nos testes de widgets. Nenhuma rolagem horizontal da página foi encontrada; tabelas extensas da Taça preservam sua rolagem interna. O console da página não apresentou avisos ou erros na auditoria.

Pendências: nenhuma falha técnica identificada dentro do escopo. Teste manual pelo Daniel e validação em aparelho físico não foram declarados. Escudos/paletas ausentes continuam dependentes do catálogo, sem dados inventados.

# Entrega — ELIFOOT_CLASSIC_DESKTOP_UI_015B

TASK: ELIFOOT_CLASSIC_DESKTOP_UI_015B

STATUS: PARTIAL

WORKTREE: D:\elifoot_98_015B

BRANCH: feature/classic-desktop-ui-015b

BASE_COMMIT: 3595668d073cb29d710839288f84095db896cfa0 (015A validada; incorpora dd7b783 e 9690d95)

CHECKPOINT_COMMIT: checkpoint local que contém este relatório; consultar `git log -1 --format=%H -- docs/DELIVERY_015B.md`. Hash também informado na entrega ao proprietário.

A interface funcional foi implementada e todas as regressões passaram. A missão permanece PARTIAL porque a auditoria visual final obrigatória foi interrompida pela configuração “Local browser use is disabled in Work settings”. Não se declara aprovação visual com base apenas nos testes.

| Campo | Estado e evidência |
|---|---|
| CLASSIC_HEADER | Implementado; marca, contexto e data reais; comandos autoritativos preservados; observado no desktop |
| CLASSIC_SIDEBAR | Implementado; sete itens, ativo amarelo, desktop fixo e drawer mobile; testes em seis larguras |
| PLANTEL_TATICAS | Implementado; campo real, duas colunas, filtros e atributos/ações preservados; captura desktop |
| DIVISOES_TACA | Implementado; abas e consultas reais, tabela completa, resumo e Taça; capturas da 1ª/4ª divisões |
| CALENDARIO_RESULTADOS | Implementado; filtros ativos, rodadas expansíveis, sem JOGAR individual; captura desktop; correção final de estado vazio testada |
| RANKINGS_JOGADORES | Tema e consultas preservados; observado no navegador, captura final pendente |
| MERCADO_LEILAO | Tema compartilhado, duas modalidades e formulários 015A preservados; testes aprovados; fluxo visual final pendente |
| FINANCAS | Padrão compartilhado e comandos preservados; testes aprovados; captura real final pendente |
| OTHER_SCREENS | Tema, tabelas e diálogos compartilhados; carreira, histórico e aparência preservados; auditoria final pendente |
| MATCH_TEAM_COLORS | Implementação 014C/015A preservada; cores observadas no calendário; captura final de partida pendente |
| MORALE_BAR | Somente barra visível, valor/faixa do servidor; desktop observado, testes das três faixas |
| MATCH_SCREEN_LOCK | Preservado; novo teste encontrou e corrigiu drawer aberto no início; testes aprovados, confirmação visual final pendente |
| PRE_SAVE_EDITOR_PRESERVED | Código do editor/criador preservado; regressão completa aprovada; nenhuma aba reintroduzida |
| DESKTOP_RESPONSIVE | Testes 1024/1440/1920 aprovados; inspeção real 1440 feita; demais capturas pendentes |
| MOBILE_RESPONSIVE | Novo layout conforme referência; testes adicionais 320/360/390/430/600 aprovados; prévias Flutter inspecionadas; Chrome final pendente |
| DOTNET_TESTS | PASS — 640/640 (7 Application, 409 Domain, 198 Integration, 26 Simulation), zero ignorados; build zero erros/avisos |
| FLUTTER_TESTS | PASS — 180/180 na revisão mobile; analyzer e build registrados em evidence-015b-mobile/validation.json |
| NODE_TESTS | PASS — 2/2 |
| REAL_BROWSER_TEST | PARTIAL — API + PostgreSQL DEV reais e inspeção desktop; continuação impedida pelo navegador desabilitado |
| VISUAL_COMPARISON | PARTIAL — diferenças registradas; auditoria completa final pendente |
| SCREENSHOTS | 4 PNG reais desktop intermediários; faltam capturas obrigatórias finais de mercado, compra, diálogo, rankings, partida e mobile |
| MATCH_ENGINE_PRESERVED | PASS — nenhum arquivo de src foi alterado em relação à 015A |
| ECONOMY_PRESERVED | PASS — código financeiro intacto e simulações de 100 temporadas repetidas |
| MULTIPLAYER_PRESERVED | PASS — regressão completa e bloqueio/espera 2/4/6/8, inclusive drawer mobile |
| KNOWN_ISSUES | Auditoria visual final incompleta; limites detalhados em KNOWN_ISSUES_015B.md |

Simulações de economia e catálogo real completaram 100 temporadas e 35.100 partidas por cenário, com zero violações de escalação, elenco e dupla liquidação. Valores e nomes dos testes estão em evidence-015b e REGRESSION_015B.md. Não houve mudança de fórmula ou dinheiro artificial para produzir o layout.

URL_DO_JOGO: http://127.0.0.1:5168/

URL_DA_API: http://127.0.0.1:5167/api/health

POSTGRES_DEV: 127.0.0.1:55440, bancos exclusivos elifoot_015b_

SALA_DE_TESTE: 015B - Mercado e partidas / ELI115608A3, preparação R3

TREINADOR: Daniel 015B, dispositivo 015b-daniel, Flamengo RJ. Os outros humanos são Comprador 015B/015b-comprador/FC Porto e Rival 015B/015b-rival/Arsenal FC. Usar o mesmo dispositivo para recuperar a identidade persistida.

Para iniciar neste computador: `powershell -ExecutionPolicy Bypass -File D:\elifoot_98_015B\scripts\run-local-015b.ps1 -NoBuild`. A sala aguarda três humanos prontos; o botão do cabeçalho respeita esse fluxo.

Os serviços da 015B ficam ativos para revisão. A 015A continua nas portas 5165/5166 e banco 55439. Todos os worktrees anteriores mantêm seus commits e nenhum arquivo rastreado alterado. Sem merge, push ou publicação. O teste manual do proprietário não foi declarado aprovado.

## Adendo do teste manual

Corrigidos os controles de formação para ajustar a escalação e reorganizar o campo conforme o esquema escolhido. Novo teste de 5-3-2 passou em 390/1440 px; cinco casos novos elevam a regressão Flutter a 173/173. API real confirmou persistência, início 1/1 e conclusão oficial em sala QA separada.

Para teste individual do proprietário, usar **015B - Teste individual Daniel / ELIC18AA38C**, com Daniel 015B e dispositivo 015b-daniel, Flamengo RJ, preparação R3 e contador 0/1. A sala anterior ELI115608A3 continua preservada com três treinadores e por isso aguarda 3/3. Ver PLAYTEST_FORMATION_SOLO_FIX_015B.md. STATUS global permanece PARTIAL pela auditoria visual final pendente.

## Adendo mobile conforme a referência

O pedido posterior do proprietário foi implementado em MOBILE_REFERENCE_LAYOUT_015B.md. Abaixo de 768 px, clube e indicadores ficam em painel claro; Plantel exibe resumo, titulares, banco e elenco; o campo fica em Táticas. A barra inferior fixa abre cinco destinos e Mais mantém os demais menus. A moral continua somente em barra, com o valor/faixa usados pelo desktop.

A regressão Flutter completa passou 180/180. Sete casos novos cobrem navegação, fichas, filtros, salário contratado, leilões, moral e bloqueio ao vivo; o caso de filtros também verifica o reinício de rolagem ao abrir Táticas. O aviso de equipe com dez aptos permaneceu coberto pelo teste 012C original. Analyzer/build e os hashes das prévias constam em evidence-015b-mobile/validation.json; runtime em runtime-health.json.

A sala individual do proprietário foi somente consultada para registrar a cena; a leitura encontrou preparação R5. Não foi reiniciada nem avançada por esta alteração. As imagens novas são renderizações Flutter de respostas GET reais, identificadas como tal. A auditoria Chrome continua PARTIAL pelo navegador desabilitado nas configurações do Work. Nenhuma aprovação manual foi atribuída ao proprietário.

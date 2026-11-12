# V3 — interface consolidada para teste local

Data: 11/11/2026. Branch: `main`. Base: `4c0b64fb465a24d31a6798236606f361cd1654fa`.

## Consolidação

As seis alterações pendentes de interface da worktree `local-historical-catalog` foram integradas à main por comparação de três vias, sem conflitos. Foram preservadas as correções V3 já existentes, incluindo A/M no servidor, gols da Taça, calendário e menus responsivos.

A versão reúne o botão **Jogo** entre Configurações e Táticas, dropdown abaixo dos botões, painel de dados do jogador, ação Listar Jogador, telas de Banco e Estádio e novo desenho do estádio. O título do navegador identifica **Liga Clássica 98 — V3**.

O criador atual de oito etapas e o login por conta foram mantidos. Alterações antigas de um criador de sete etapas e um stash incompleto de login LocalDev foram preservadas nas origens; não substituem os fluxos atuais da main.

## Correções solicitadas depois da consolidação

**Modal salarial:** a condição anterior aceitava RESULTS/FINISHED sem verificar se a classificação ainda estava aberta. Agora exige o plantel, sem tela de partida, aparência, táticas ou retenção da classificação. O temporizador existente continua mostrando a classificação por cinco segundos, retorna ao plantel e só então permite o modal. A decisão continua obrigatória e o bloqueio `SALARY_PENDING_TRANSITION` do servidor permanece intacto.

**Contrato:** a tabela clássica de titulares e reservas apresenta CONTRATO imediatamente após SALÁRIO. Mostra RENOVADO exclusivamente quando `contractRenewedForSeason` é verdadeiro; caso contrário, mostra um traço. Aceitar um aumento espontâneo não equivale a renovar o contrato. A largura mínima permite as seis colunas em 1024 px; em telas pequenas há rolagem horizontal.

## Verificação da IA no mercado

- `MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch` anuncia até dois excedentes elegíveis por clube na lista de transferências e permite lances da IA em leilões. Respeita elenco mínimo, renovação da temporada e reserva em leilão.
- `ClassicEconomyEngine.ProcessAiFinances` / `ResolvePendingAiSalaryRequests` podem recusar pedidos salariais quando as condições existentes permitem; a recusa gera leilão obrigatório. `EvaluateInsolvency` também pode gerar leilão financeiro para clubes da IA.
- Não foi encontrada uma chamada da IA para `VoluntaryAuctionEngine.Create`. A chamada atual está no comando financeiro do treinador humano. Portanto, anunciar excedentes para transferência não significa criar um leilão voluntário.
- Consulta PostgreSQL somente leitura no mundo **testeno dois jogadores**, temporada 1, rodada 4: **36 anúncios de transferência da IA**, **0 leilões de recusa salarial da IA**. Os quatro leilões registrados nesse momento tinham origem em clubes humanos: três voluntários encerrados e um de recusa salarial aberto. Não foi avançada rodada para produzir evidência artificial.

Consultas e resultados: `artifacts/v3-manual-play/ai-market-readonly*` e `ai-auctions-readonly*`. Nenhuma regra da IA ou da economia foi alterada.

## Arquivos de código e testes

- `client/elifoot_flutter/lib/classic98_shell.dart`
- `client/elifoot_flutter/lib/coach_finance_screens.dart`
- `client/elifoot_flutter/lib/main.dart`
- `client/elifoot_flutter/lib/stadium_visual.dart`
- `client/elifoot_flutter/test/classic_squad_bank_stadium_test.dart`
- `client/elifoot_flutter/test/rankings_calendar_finance_update_test.dart`
- `client/elifoot_flutter/test/salary_request_modal_test.dart`

Inicializador local: `INICIAR-V3-TESTE.cmd`, com auxiliares em `artifacts/v3-manual-play/`. Reutiliza serviços saudáveis e o banco existente; não cria mundos automaticamente.

## Validações e evidências

A consolidação inicial passou em 79 testes relacionados. A validação ampliada passou em **107 testes Flutter**, abrangendo A/M, gols da Taça, calendário, leilão, salários, contratos, carreira, navegação automática e apresentação clássica/moderna. O teste salarial verifica RESULTS e FINISHED, a espera da classificação, o retorno ao plantel, o bloqueio de avanço até a decisão e a continuidade após a resposta.

A conferência visual identificou um recorte parcial do cabeçalho Contrato em 1024 px. A largura foi ajustada e o teste reforçado para exigir acesso à coluna sem rolagem nessa resolução. A repetição dos **107 testes passou em 40 segundos**, o **Flutter analyze passou em 8,7 segundos**, e o **build Web release passou**, incluindo o Wasm dry run. Os testes não foram alterados para esconder falhas: a interface foi corrigida e o teste ganhou uma verificação adicional.

Logs em `artifacts/v3-manual-play/`: `unified-final-tests-1024.log`, `unified-final-analyze-1024.log`, `unified-final-build-1024.log`, `unified-final-validation.json`, `unified-services-check.log`, `unified-health-final.json`. Build release final: **75,4 segundos**. Capturas: `consolidated-contract.jpg`, `consolidated-menu-final.jpg`, `consolidated-bank.jpg`, `consolidated-stadium.jpg` e `consolidated-1024.jpg`.

Verificação final no Chrome: título V3, botão Jogo na posição solicitada, dropdown abaixo de Táticas com A/M, nove estatísticas do jogador, Contrato visível à direita do salário, Banco e Estádio novos. Em 1024 × 900 o cabeçalho Contrato aparece completo sem rolagem horizontal. Janela restaurada à resolução original e jogo deixado aberto no plantel, com login e sala existentes.

Não foram executadas suítes .NET ou simulações demoradas: não houve alteração no backend. Os testes de UI usam APIs falsas e não modificam partidas locais. A verificação no navegador limita-se a navegação e leitura; não foram tomadas decisões salariais, empréstimos, obras ou envio de táticas no jogo do usuário.

## Preservação e execução local

Backups, hashes, patches e manifesto: `artifacts/v3-ui-consolidation/20261111-163517/`. As alterações originais da worktree de origem e os dois JSON de auditoria previamente modificados foram preservados byte a byte. Nenhum commit ou push foi feito.

Interface: http://127.0.0.2:5242/ . API: http://127.0.0.2:5288/api/health . PostgreSQL de QA: `127.0.0.1:55442`, banco `elifoot_016b_v3gate_browser`. Catálogo disponível é sintético de QA; não é um catálogo histórico oficial.

Build: `V3-consolidada-20261111-salario-contrato`. Serviços permanecem executando. A sessão de login e os mundos existentes foram reutilizados. Nenhum banco foi apagado ou recriado, nenhuma migration foi executada e nenhum save foi convertido.

Status do escopo: **PASS** — consolidação, correções de interface, testes relacionados, análise estática, build e serviços locais aprovados. Nenhuma pendência neste escopo. A verificação do modal após partida foi feita por testes de widget integrados ao shell; não foi jogada uma rodada nem respondido pedido no save do usuário para produzir a demonstração. O status de auditorias gerais anteriores não é substituído por este resultado de UI.

Atualização posterior em 11/11/2026: a versão unificada recebeu o botão Renovar contrato no plantel, modal de oferta e contraproposta com decisão explícita. Resultado e novas validações em `ELIFOOT98_V3_RENOVACAO_PLANTEL_RESULTADO.md`. Build corrente: `V3-consolidada-20261111-renovacao`.

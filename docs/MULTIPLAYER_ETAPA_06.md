# Multiplayer · Etapa 6 · Fluidez do Flutter

**PARTIAL.** A atualização de minutos passou a reconstruir menos widgets nos temas Atual e Clássico. As medições Web Release são de Chrome desktop no ambiente de laboratório, incluindo uma janela estreita. Falta validar os fluxos restantes em navegador móvel físico e repetir as medições com aceleração gráfica.

## Atualizações da partida

Cada atualização comum do relógio passava pelo `setState` da tela inteira. Agora o shell aceita a nova versão e atualiza o cabeçalho de minuto e a área da partida por um `ValueNotifier`. O filtro exige avanço de minuto na mesma projeção, sem mudança de fase, estado de prontidão ou presença. Eventos de pausa, pênaltis, resultado, troca de sala e nova época SignalR continuam pelo fluxo completo. O teste confirmou que um tick troca a projeção da partida com a mesma instância de `Scaffold`. Um evento com outro `worldId` foi ignorado. [Teste de reconstrução](../artifacts/stage6-run/flutter-granular.log)

A tela de jogo converte os dados do calendário apenas quando a lista de origem muda. A seleção e o controlador de rolagem continuam no widget existente. O cache de consultas com invalidação por área, o verificador de respostas atrasadas e os indicadores de espera anteriores foram exercitados na regressão. Esta entrega concentrou as alterações de renderização na partida. A hierarquia dos menus, tabelas, listas, escudos e modais continua como no checkpoint. [Checkpoint de 70 arquivos e manifesto SHA-256](../artifacts/stage6-checkpoint-09337c664b40/sha256.csv)

## Comparação Web Release

Flutter 3.38.5, Chrome 157, CanvasKit local e renderização por software. As duas versões usaram a mesma API instrumentada em `AccountTest`, com PostgreSQL, ligas descartáveis de 64 clubes e semente fixa. Cada ensaio abriu um perfil novo do navegador e cronometrou a navegação até a semântica do botão de retomada. O tempo da tela seguinte vai do clique à primeira confirmação pela árvore de acessibilidade. No desktop, foi usada a janela de 1.366 × 900. A janela de 390 × 844 usa o layout responsivo no próprio desktop.

O FPS abaixo é a cadência média aproximada de callbacks `requestAnimationFrame` por cinco segundos de jogo. O p95 é o intervalo entre callbacks. O número acima de 33,5 ms indica intervalos longos observados, sem equivalência direta a quadros descartados pela GPU ou tempo de rasterização Flutter. A memória é o heap JavaScript lido pelo Chrome, excluindo a memória total do navegador e parte da memória do CanvasKit. As amostras são individuais e estão sujeitas à carga do host.

| Cenário | FPS antes → depois | p95 de intervalo | Intervalos >33,5 ms | Heap JS |
| --- | ---: | ---: | ---: | ---: |
| Desktop Clássico | 28,4 → 31,4 | 83,3 → 66,7 ms | 55 → 54 | 53,6 → 34,6 MiB |
| Desktop Atual | 23,6 → 23,0 | 100,1 → 100,0 ms | 44 → 44 | 79,0 → 49,6 MiB |
| Janela de 390 px, Atual | 32,8 → 32,2 | 66,7 → 66,7 ms | 54 → 52 | 46,2 → 47,6 MiB |

No Clássico desktop, a abertura fria oscilou de 3,23 s para 5,14 s. A retomada da sala levou 593 → 274 ms, Mercado/Leilões 557 → 467 ms, Escalação 470 → 524 ms e o início da partida por botão 1.019 → 572 ms. No Atual em janela estreita, a abertura fria foi de 3,40 → 3,52 s e o início da partida por botão de 740 → 960 ms. O p95 na janela de rolagem variou de 100 → 83 ms no Clássico desktop e 50 → 67 ms no Atual estreito. O cenário de 390 px mostra que o menor número de reconstruções não garante melhoria de FPS ou de latência em toda tela. [Clássico antes](../artifacts/stage6-run/pair-before-classic.json) · [Clássico depois](../artifacts/stage6-run/pair-after-classic.json) · [Atual 390 px antes](../artifacts/stage6-run/pair-before-narrow.json) · [Atual 390 px depois](../artifacts/stage6-run/pair-after-narrow.json)

No Atual desktop, a abertura fria mediu 5,36 → 4,18 s, a retomada 777 → 359 ms, o início da partida 1.079 → 1.375 ms e a entrada na pausa 739 → 1.310 ms. O p95 da rolagem foi de aproximadamente 100 ms nas duas versões. O tempo acumulado de tarefas na thread principal do renderizador (`Performance.TaskDuration`) recuou de 4.474 para 4.328 ms durante a janela de cinco segundos. A queda do heap JavaScript de cerca de 37% veio com FPS praticamente estável. A partida terminou nas duas versões com o mesmo total por tipo de evento e resultado durável na versão 97. A coleta do build anterior atingiu o limite da sondagem de acessibilidade depois de já registrar esse resultado. [Atual antes](../artifacts/stage6-run/fast-before-current.json) · [Atual depois](../artifacts/stage6-run/fast-after-current.json)

Em uma passagem funcional separada no Atual, a confirmação pelos widgets levou 265 ms para Classificação, 258 ms para Calendário, 1.193 ms para Carreira, 341 ms para Mercado e 647 ms para Escalação. Os dados de quadros desse ensaio ficaram inválidos e foram excluídos da comparação. No Clássico, a passagem funcional confirmou o painel de pausa e a classificação após salvar a rodada. As amostras de desempenho em janela estreita no Clássico variaram entre execuções, sem estimativa confiável do efeito da mudança. [Navegação Atual](../artifacts/stage6-run/live-after-current.json) · [Partida Clássico](../artifacts/stage6-run/live-after-classic.json)

O `main.dart.js` Release passou de 3.886.735 para 3.888.793 bytes, aumento de cerca de 2 KiB. A coleta do navegador registra fontes, recursos CanvasKit e respostas REST separadamente. Os escudos da liga sintética usam o caminho visual de fallback. Essa amostra não mede custo de escudos de catálogo ou tempo entre o recebimento de um frame SignalR e a pintura do placar. Quantificação de CPU total e quadros descartados durante a rasterização permanece pendente.

## Regressões e cobertura

- A suíte Flutter registrou 576 aprovações e oito falhas já presentes na linha de base. Os nomes correspondem ao teste de isolamento/observação da partida, cinco tamanhos da referência de plantel e dois testes de permanência na classificação pós-rodada. O teste específico de reconstrução passou no código final. A análise dos três arquivos Dart alterados passou. [Suíte](../artifacts/stage6-run/flutter-regression.log) · [Linha de base](../artifacts/stage1-release/flutter-baseline-regressions-v2.log) · [Reteste de reconstrução](../artifacts/stage6-run/flutter-granular-final-fixture-sdk.log) · [Análise Dart](../artifacts/stage6-run/dart-analyze-final.log)
- O teste Flutter autenticado com dois treinadores sobre PostgreSQL passou antes e depois da mudança. Os clientes sincronizaram a versão durável. Os testes de widgets listaram o save no portal e o Chrome Release abriu a liga e completou a partida nos dois temas. A suíte .NET passou em 32 casos e ignorou um caso de HTTPS comercial. O ensaio de fila executou seis mundos independentes. O relatório da Etapa 5.1 detalha a cobertura funcional. [Flutter antes](../artifacts/stage6-run/flutter-postgres-account2.log) · [Flutter depois](../artifacts/stage6-run/flutter-postgres-after.log) · [Etapa 5.1](MULTIPLAYER_ETAPA_051.md)
- O ambiente informou somente Windows e Chrome como dispositivos Flutter disponíveis. A janela de 390 px e os testes de resolução cobrem layout em software. Android e iOS físicos, gestos de toque e pressão de memória móvel permanecem abertos.

## Arquivos alterados

- `client/elifoot_flutter/lib/main.dart` e `lib/live_match_view.dart` tratam as atualizações e o calendário da partida.
- `client/elifoot_flutter/test/live_match_screen_test.dart` verifica o widget que recebe um novo minuto e o isolamento entre mundos.
- `docs/audit-scalability/stage6-chrome.mjs` mede o app real por CDP e guarda os dados brutos. `stage51-recovery.mjs`, `stage51-aged.mjs`, `load.mjs`, `RoomClockMetrics.cs`, `IRoundTickTiming.cs`, `Program.cs` e `MultiplayerRoundOrchestrator.cs` pertencem ao ensaio e à instrumentação da Etapa 5.1.

## Reprodução

Em ambiente descartável, iniciar a API `AccountTest` com PostgreSQL na porta 54987. Gerar os dois builds com `flutter build web --release --no-web-resources-cdn --dart-define=API_BASE_URL=http://127.0.0.1:54987`. Servir os builds no mesmo host estático (`127.0.0.1:5242`), sob `/before/` e `/after/`, com `<base href>` e os caminhos locais de CanvasKit de cada cópia correspondendo ao subdiretório. Executar `node docs/audit-scalability/stage6-chrome.mjs captura.json CURRENT 1366 900 /after/` e repetir com `CLASSIC_98`, `/before/` e janela `390 844`. `AUDIT_FINISH_ROUND=1` mantém o navegador aberto até o resultado; `AUDIT_WIDGET_ADJUSTMENT=1` acrescenta a tentativa de interações na pausa em uma liga própria, e `AUDIT_WAIT_NEXT_ROUND=1` verifica o avanço disparado pela interface. A API de testes cria apenas contas e ligas sintéticas.

## Recomendação para a Etapa 7

Usar uma máquina dedicada e celulares físicos antes de decidir sobre lançamento. Repetir a comparação em várias amostras com GPU, medir a pintura do placar por evento e concluir o roteiro da partida e carreira nos dois temas. Em paralelo, atribuir a pressão de memória dos saves históricos com ensaio prolongado. O principal custo de servidor já separado no laboratório foi a persistência dos checkpoints. [Etapa 5.1](MULTIPLAYER_ETAPA_051.md)

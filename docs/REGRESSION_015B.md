# Regressão — 015B

Base validada 015A: 640 .NET, 158 Flutter e 2 Node. A missão adiciona 10 testes Flutter específicos de layout, navegação responsiva, consulta de divisões, calendário e contraste. Nenhum teste anterior foi excluído.

As verificações antigas de cor foram atualizadas para o amarelo #FFD400 e o verde profundo da nova referência. Os testes de menu móvel agora abrem o drawer. Onde a HUD acrescentou o mesmo nome do treinador/jogador, os testes localizam a tabela correspondente para verificar a ação correta.

Os primeiros ciclos encontraram transbordamento do campo/controles em larguras menores, um breakpoint de cabeçalho inadequado em 768 px e seletores de teste que dependiam da posição antiga do menu. As correções mantêm todos os callbacks. Na inspeção real foi corrigida a mensagem do calendário para rodadas em que o clube não participa.

A regressão .NET completa terminou com 640/640 aprovados: Application 7, Domain 409, Integration 198 e Simulation 26, zero ignorados. Build Release: zero erros e avisos. Node: 2/2. Flutter: 168/168 após a correção final do drawer; analyzer: zero issues. A build Web final está registrada junto às evidências.

O caso adicional de início com drawer já aberto demonstrou que fechar apenas rotas não fechava o menu: a transição autoritativa agora também chama closeDrawer. O teste cobre essa correção em 390 e 1440 px e passou antes da regressão completa.

Os comandos executados são:

```powershell
Set-Location D:\elifoot_98_015B
. .\scripts\test-classic-desktop-015b.ps1
dotnet build Elifoot.slnx -c Release
dotnet test Elifoot.slnx -c Release --no-build --logger trx --results-directory artifacts/015b/test-results
Set-Location client/elifoot_flutter
flutter analyze
flutter test
flutter build web --dart-define=API_BASE_URL=http://127.0.0.1:5167 --pwa-strategy=none --output=D:/elifoot_98_015B/artifacts/015b/web
Set-Location D:\elifoot_98_015B
node --test tools/catalog-013b/openfootball-directory.test.mjs
```

A suíte .NET completa inclui os cenários longos anteriores de 100 temporadas, embora nenhuma regra do domínio tenha mudado. Os relatórios novos ficam exclusivamente em artifacts/015b/regression. API 5167, Web 5168 e PostgreSQL 55440 são próprios da 015B. O catálogo de origem foi copiado em modo somente leitura e restaurado em banco novo; saves anteriores não foram apagados.

## Resultados das simulações executadas nesta worktree

| Cenário | Temporadas | Partidas concluídas | Caixa final | Dívida final |
|---|---:|---:|---:|---:|
| Balanço 012D FIX | 100 | 35.100 | R$ 137.353.919,79 | R$ 3.270.104,68 |
| Clubes reais masculino | 100 | 35.100 | R$ 86.753.694,92 | R$ 0 |
| Clubes reais feminino | 100 | 35.100 | R$ 103.405.567,05 | R$ 0 |

Zero violações de escalação, limite de elenco e dupla liquidação nos resumos econômicos. O cenário 012D verificou 5.059.273 contratos preservados, juros cobrados de R$ 6.200.000, pagos de R$ 3.429.895,32 e principal pago de R$ 1.000.000. Nenhuma fórmula foi modificada para obter esses resultados. SourceCheckpoint nos JSON é metadado histórico do cenário; a execução ocorreu na 015B.

Os quatro TRX e relatórios integrais permanecem em artifacts/015b. Resumos e nomes dos 640 testes estão versionados em docs/evidence-015b. A auditoria real de interface tem seu próprio estado, separado do sucesso da regressão automatizada.

## Revisão mobile solicitada pelo proprietário

A regressão Flutter completa foi repetida após o layout mobile e passou 180/180. Os 173 casos existentes foram mantidos; sete casos adicionais verificam as larguras 320/360/390/430/600, dados da moral, navegação inferior, ficha, filtros, salário contratado, leilão, retorno ao início ao abrir Táticas e bloqueio do drawer durante a partida. O helper de entrada passou a revelar o botão na área rolável antes de clicar. As verificações de formação agora abrem Táticas no celular, e os testes de tema verificam o painel mobile correspondente.

O teste de inferioridade 012C continuou inalterado e passou com dez jogadores aptos em 390/1280 px. O renderer adicional em tool/mobile_reference_capture_015b_test.dart passou e produziu sete PNG de respostas GET da API real. Ele é executado separadamente da suíte de 180, não substitui Chrome e não muda saves. Testes/analyzer/build desta revisão ficam em artifacts/015b/mobile-*.log; manifesto versionado em evidence-015b-mobile/validation.json.

Não houve mudança de backend nesta revisão; os 640 testes .NET, dois Node e cenários de 100 temporadas acima são resultados da regressão anterior, não uma nova execução atribuída à revisão mobile.


## Regressão das interações de partida

Implementação e validação em MATCH_INTERACTIONS_015B.md. Pênaltis durante a partida aguardam o cobrador humano; disputa final automática. Abertura/observação não debita parada, somente alterações reais; intervalo é gratuito em paradas e toda troca conta. Recuperação e idempotência foram verificadas em SQLite/PostgreSQL e com reinício real da API. Resultado: 654/654 .NET, 211/211 Flutter, analyzer zero issues e simulações de 100 temporadas repetidas. Dez prévias Flutter registradas como renderizações; Chrome/aprovação manual continuam pendentes. STATUS global PARTIAL.

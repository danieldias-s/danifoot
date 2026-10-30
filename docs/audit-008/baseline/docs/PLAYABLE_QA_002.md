# ELIFOOT_98_COMPLETE_PLAYABLE_QA_002

Data: 29/10/2026. Projeto auditado: `D:\elifoot_98`. Somente desenvolvimento local; nenhuma publicação ou alteração de produção.

## Resultado e critérios de evidência

**STATUS: PARTIAL**, por falta de execução nativa Android confirmada. O jogo Web foi operado em navegador real contra a API e o SQLite persistente. Uma temporada normal de 128 clubes foi concluída exclusivamente com controles gráficos, chegando à temporada 2. Oito abas independentes participaram da mesma sala.

| Campo | Resultado | Evidência / alcance |
|---|---|---|
| WEB_PLAYABLE | PASS | Login, sala, clube, plantel, tática, partida, resultados, mercado e carreira no navegador |
| MULTIPLAYER_8 | PASS | Oito treinadores e clubes diferentes; contador 0/8 até 8/8 |
| GLOBAL_PAUSE | PASS | Cliente 3 pediu parada; os oito ficaram no minuto 2; repetição no build final: minuto 1 |
| SUBSTITUTIONS | PASS | Troca real propagada aos oito clientes; limites 5/3 e 6/4 verificados no motor/orquestrador |
| AI_TACTICS | PASS | Adaptação contextual, diversidade e preferências preservadas após mudança de clube |
| AI_TRANSFERS | PASS | Lotes reais do mercado e economia em 100 temporadas; métricas abaixo |
| MARKET | PASS | Compra direta, venda liquidada entre humanos, proposta e lance real |
| STADIUM | PASS | Reforma cobrada no caixa e avanço das obras com rodadas |
| FULL_SEASON | PASS | Temporada 1 completa e temporada 2 acessível por botão da interface |
| EDITOR | PASS | Clube novo, nomes/cores, jogador, técnico, validação, exportação e importação JSON |
| CLASSIC_VISUAL | PASS com aproximações | Verde, tabelas densas, amarelo, menus horizontais, botões retangulares; sem fidelidade pixel a pixel |
| ANDROID_BUILD | PASS | APK **debug** compilado com SDK/JDK oficiais instalados no projeto |
| ANDROID_RUNTIME | BLOCKED | Emulador permaneceu `offline`; driver de aceleração ausente; tentativa em modo software sem boot confirmado |
| TESTS | PASS | 19 testes .NET distintos; 2 testes Flutter; análise sem problemas; builds Web/QA móvel/APK |

Os testes Flutter de widgets usam respostas controladas e comprovam regressões de interface/layout. **Não são a prova de jogabilidade**: essa prova são as operações de navegador registradas aqui. Os testes do motor executam código de produção com mundos de teste. A integração inclui HTTP/SignalR e persistência real PostgreSQL; o uso humano simulado ocorreu contra SQLite real.

## Como abrir o jogo

Pré-requisitos Web: .NET 10, Flutter 3.38.5/Dart 3.10.4 e Node.js disponíveis no PATH. PostgreSQL é opcional neste fluxo.

Em dois terminais PowerShell, a partir da raiz:

```powershell
Set-Location D:\elifoot_98
.\scripts\run-dev.ps1 -DatabaseProvider Sqlite -ApiPort 5080
```

```powershell
Set-Location D:\elifoot_98
.\scripts\run-web.ps1 -WebPort 8083
```

Abra **http://127.0.0.1:8083**. Para repetir somente o servidor estático de um build já gerado:

```powershell
.\scripts\run-web.ps1 -WebPort 8083 -SkipBuild
```

No jogo: entrar no lobby → criar sala de 1 treinador para jogo solo → entrar com clube → selecionar clube → auto-escalar ou escolher titulares/reservas → enviar tática. Nos intervalos, confirmar a continuação; em resultados, avançar a rodada. Depois da última rodada, o mesmo botão inicia a temporada seguinte. Na sala multiplayer, todos os participantes presentes precisam enviar tática; desempregados usam “PRONTO PARA ACOMPANHAR”.

O script SQLite usa `elifoot-local.db`. A auditoria usou **outro arquivo**, `playable-qa.db`, para não alterar saves anteriores. Para reabrir especificamente os mundos auditados:

```powershell
$env:Elifoot__DatabaseProvider = 'Sqlite'
$env:ConnectionStrings__DefaultConnection = 'Data Source=D:\elifoot_98\playable-qa.db'
$env:Elifoot__AutoSeedDefaultWorld = 'false'
$env:ASPNETCORE_ENVIRONMENT = 'LocalDev'
dotnet run --project src/Elifoot.Api -c Release --no-build --no-launch-profile --urls http://127.0.0.1:5080
```

Builds antigos podiam registrar service worker na porta 8080. A validação final dos oito clientes usou a origem 8083, sem esse cache, e os novos scripts compilam com `--pwa-strategy=none`. Ao mudar de origem, a sessão por aba não é compartilhada; os formulários permitem recuperar a identidade usando o mesmo apelido e ID de dispositivo.

## Inventário de telas e ações reais

As telas existentes eram agrupadas em sete abas; seleção de clube, calendário e formulários do editor estavam ausentes. Foram acrescentados à aplicação existente.

| Tela / função | Ação executada e resultado esperado | Resultado real | Evidência |
|---|---|---|---|
| Menu inicial | Abrir app e informar treinador | Login alcançou lobby real | 15 |
| Criar partida | Criar sala solo e sala de oito | Salas independentes, sem sobrescrever a mesma seed | 08, log multiplayer |
| Entrar em partida | Entrar pela lista e por código | Código abriu a sala importada correta | 16 |
| Seleção de clube | Escolher Atlético Paulistano; oito clientes escolherem clubes distintos | Clube e elenco atribuídos corretamente | 01, 08 |
| Escalação | Auto-escalar 11 e banco; enviar seleção | Escalação aceita e usada pelo motor | 01, 16 |
| Táticas | Enviar e retirar pronto | Contador sincronizado; retirada observada em 6/8 | log multiplayer |
| Jogadores | Consultar plantel, editar força/estrela/disciplina | Força 51 recusada; estrela acrescentada, retirada e recolocada; Fair Play, Sarrafeiro e Cordeirinho persistidos e recarregados | 06, 24 |
| Mercado | Comprar Gustavo Freitas de Palestra Verde | R$ 1.103.952 debitados; elenco 18 → 19 | 04 e registro abaixo |
| Venda | Anunciar Sérgio Lima e liquidar com outro humano | Jogador saiu do vendedor, entrou no comprador, dinheiro transferido | 11 |
| Leilão | Dar lance de R$ 87.751 em Igor Nascimento e aguardar encerramento | Lance aceito; IA depois arrematou por R$ 93.016; encerrado sem debitar o perdedor | 12 e registro complementar |
| Negociação | Grêmio Guanabara propor 90%; vendedor aceitar | R$ 967.316 transferidos; proposta encerrada e atleta no comprador | 11 |
| Finanças | Consultar compra, venda, folha, bilheteria e reforma | Livro-caixa mostrou descrições, sinais e valores reais | 04, 11 |
| Estádio | Iniciar reforma de R$ 128.790 | Caixa caiu de R$ 3.322.426 para R$ 3.193.636; condição foi de 90,5% a 99,47% após obra e desgaste anual | 04 |
| Competições | Consultar divisões e fases da Taça | Dados das competições acessíveis pela interface | 05, 02 |
| Calendário | Abrir a oitava aba | Rodadas e confrontos, resultados ou indicação de definição após fase anterior | UI e teste HTTP de 22 rodadas |
| Classificação | Consultar depois da primeira rodada da liga | Atlético Paulistano: 3 pontos, 1 jogo, 2–0, saldo +2 | 05 |
| Taça | Jogar preliminares e fases eliminatórias | Prorrogações e decisões resolvidas antes de avançar | 02, log solo |
| Ranking | Abrir carreira na temporada 2 | Técnico, retrospecto e campeões preservados | 07, 17 |
| Convites | Entrar desempregado e aceitar Ferroviária Grená | Técnico assumiu clube, deixando estado desempregado | 13 |
| Demissão | Jogar 22 rodadas com elenco enfraquecido no editor | Clube caiu para distrital, técnico ficou desempregado e aceitou Joinville na temporada 2 | 22, 23 e log de carreira |
| Editor de clubes | Criar QA Clube Novo e renomear Atlético QA Persistido | Novo distrital recebeu elenco e técnico; pacote passou de 128 para 129 clubes | 14 |
| Editor de técnicos | Renomear técnico e salvar | QA Treinador Persistente apareceu no seletor e exportação | 14 |
| Importar/exportar | Exportar JSON, validar e criar outro mundo | Pacote de 129 clubes, 2.326 atletas e 129 técnicos válido; mundo importado acessível por código | 14, 16 |
| Resultados | Concluir jogos e consultar placares | Estado RESULTS e avanço disponível | 02 |
| Simulação ao vivo | Assistir relógio/eventos; usar aceleração gráfica | Gols, cartões, substituições, intervalo e resultados atualizados | 03, 09, 20 |

### Temporada normal de 128 clubes

Sala `QA Temporada Completa`, treinador `QA Solo`, Atlético Paulistano. As **22 rodadas** foram iniciadas e concluídas por botões da interface, incluindo as preliminares e partidas eliminatórias que não envolviam o clube humano. Não houve alteração de placares, tick por chamada externa ou acesso ao banco para avançar essa temporada.

O clube venceu o primeiro confronto da liga por 2–0 contra Grêmio Guanabara. Depois da rodada final, a interface mostrou **Temp 2 • Rodada 1** e a galeria:

- Campeão da primeira divisão: Estrela Solitária.
- Campeão da Taça: Sport Club Atlântico.
- Artilheiro: Lucas Marques*, 16 gols.
- QA Solo: 7 vitórias, 5 empates, 7 derrotas no retrospecto de partidas disputadas.

O log automatizado registra 20 finais; as rodadas 2 e 3 foram acompanhadas manualmente durante a validação de resultados/substituição e estão explicitadas em `solo-ui-log.json`. Isso totaliza as 22 rodadas; não são 22 rodadas inferidas de uma simples compilação.

### Multiplayer real

Oito abas de navegador, apelidos QA MP1 a QA MP8, IDs de dispositivo independentes e os oito clubes da primeira divisão. Foram observados os contadores 0/8, 1/8, 2/8, 3/8, 4/8, 5/8, 6/8, 7/8. Enquanto faltava o oitavo, não havia relógio em execução. Retirar pronto reduziu 7/8 para 6/8; enviar novamente e completar o oitavo iniciou a rodada.

Na rodada 3, QA MP3 pediu pausa no minuto **2**. Todos os oito mostraram o mesmo minuto e o mesmo solicitante. A troca **Wagner Ribeiro → Leandro Alves** apareceu no evento de todos os clientes. Recarregar o cliente 3 recuperou Palestra Verde, minuto 45 e a quota já utilizada de 1/5. O cliente 6 foi navegado para uma página vazia: os demais passaram a mostrar **DESCONECTADO**; voltar recuperou sua sessão.

Oito cliques concorrentes em “AVANÇAR PRÓXIMA RODADA” resultaram na rodada **4 em todos os clientes**, sem chegar à rodada 5. Após reiniciar a API e carregar os clientes na origem sem cache antigo, a pausa foi repetida: os oito congelaram no minuto **1**, solicitante QA MP3. Ver logs JSON e screenshots 19/20.

Os limites estritos foram exercitados no código real: sexta substituição recusada no tempo normal, sexta aceita na prorrogação, sétima recusada; quarta parada normal recusada; intervalos gratuitos; cota adicional e oportunidade extra na prorrogação. Esses casos de limite usam cenários de teste controlados do motor/orquestrador, não oito operações manuais de prorrogação em navegador. A propagação normal de substituições foi validada nos oito navegadores.

## IA e simulação de 100 temporadas

Arquivo completo: [AI_100_SEASONS_QA002.json](AI_100_SEASONS_QA002.json). Baseline antes da correção econômica: [AI_100_SEASONS_QA002_before.json](AI_100_SEASONS_QA002_before.json).

Foram executadas 100 temporadas consecutivas, seed 199801, 128 clubes, com as funções de produção de escalação, transferências, leilões, salários, bilheteria, obras, promoção/rebaixamento e rotação de técnicos. **Os placares dessa simulação são gerados por um modelo agregado determinístico para acelerar a medição**, conforme a suíte existente. Não corresponde a 100 temporadas de navegador nem a 100 temporadas completas do motor minuto a minuto.

| Métrica real | Antes | Depois |
|---|---:|---:|
| Compras bilaterais liquidadas | 725 | 5.490 |
| Vendas bilaterais liquidadas | 725 | 5.490 |
| Arremates pagos | 6.908 | 10.910 |
| Gasto bilateral acumulado | R$ 1.048.236.854 | R$ 7.337.755.939 |
| Recebimentos bilaterais acumulados | R$ 1.048.236.854 | R$ 7.337.755.939 |
| Clubes com caixa negativo na temporada 100 | 120 | 0 |
| Leilões ainda abertos na temporada 100 | 843 | 5 |
| Violações de elenco | 0 | 0 |
| Clube repetido na mesma rodada | 0 | 0 |

No fim da temporada 100, o menor caixa era R$ 32.172.240,79; o caixa agregado era R$ 9.601.309.694,21. A economia tem crescimento nominal expressivo e **não modela inflação**; solvência e consistência não significam balanceamento definitivo.

Foram usadas nove formações: 4-4-2 (14.386), 4-3-3 (11.903), 5-3-2 (6.350), 3-5-2 (6.152), 5-4-1 (5.102), 3-4-3 (4.421), 4-5-1 (4.192), 3-6-1 (43), 4-2-4 (5). As preferências pertencem a `AiManager`, não a `Club`; um teste adicional força a rotação de clube e confirma a preservação da personalidade e dos pesos.

A revisão de `AiManagerBrain` e do mercado confirmou lacunas por posição, reservas, excedentes, adaptação a orçamento/adversário e limitação de preço de compra a 1,25 vezes a avaliação no processo de busca bilateral. As contagens financeiras não são apresentadas como prova estatística de otimização de cada compra: não foi armazenado um contrafactual de todos os jogadores alternativos em cada negociação.

### Correção econômica

A ausência de renda recorrente criava déficit estrutural e eliminava a atividade de mercado ao longo das temporadas. Foi adicionada receita de patrocínio/transmissão auditável por rodada, com chave idempotente: primeira R$ 140 mil, segunda R$ 100 mil, terceira R$ 75 mil, quarta R$ 60 mil, distrital R$ 20 mil. São regras novas deste projeto, não uma alegação histórica sobre Elifoot 98.

Leilões de uma temporada anterior agora expiram; o fallback não entrega jogadores gratuitamente a um humano. Uma oferta cuja transferência falha não é mais marcada como aceita.

## Aparência, responsividade e dados

Referências consultadas: [site oficial Elifoot](https://www.elifoot.com/pt-BR) e [Elifoot 98 Online](https://www.elifoot98.com.br/), port comunitário ligado pelo site oficial. Comparação visual por navegação, sem copiar imagens ou arquivos do jogo original para o produto.

Foram preservados o fundo verde, cabeçalhos amarelos, tabelas de plantel/classificação, nomes com `*`, menus compactos e placares simultâneos. Os botões principais foram tornados cinzentos e retangulares. Tipografia, tons e alguns controles Flutter são aproximações, sem reprodução exata do Windows clássico. As cores dos clubes são dados editáveis e aparecem na escolha de clube.

Desktop verificado a 1280×720. Para largura de telefone, `lib/qa_mobile.dart` aplica restrições de **390 pixels** à mesma aplicação e usa a API real. Login, lobby, convite por código, plantel e placares foram operados nessa versão. Tabelas e menu possuem rolagem horizontal; o cabeçalho tem altura limitada com rolagem própria. O teste Flutter a 390×844 detectou e verificou a correção do overflow no formulário de criação de sala. **Isso valida Web responsivo, não Android nativo.**

Os jogadores são gerados sinteticamente. Os clubes incluem nomes fictícios/inspirados e alguns nomes coincidentes com clubes reais; não constituem um banco oficial de clubes ou elencos de 1998. Nenhum scraping ou pacote de fotos sem licença foi acrescentado. A ficha de jogador mantém fallback para ícone e metadados de licença/crédito.

## Android

O ambiente inicial tinha somente JRE 17, sem Android SDK. Foram instalados dentro de `.tools/` o SDK de linha de comando oficial do Google, platform-tools, plataforma Android 36, build-tools, componentes solicitados pelo Gradle e JDK 17 Temurin. O arquivo de ferramentas do Google teve SHA-256 `90ae805d20434428bffcb699c290860f19bb5f66a67e6b330067e3de801fb04a`, igual ao checksum publicado na [página oficial](https://developer.android.com/studio). JDK obtido pela [API oficial Adoptium](https://adoptium.net/en-GB/installation/ci-scripts).

Foi corrigida a permissão INTERNET no manifesto principal e habilitado HTTP somente no manifesto **debug**, para comunicação com o servidor local. A URL é configurável por `--dart-define=API_BASE_URL=...`.

```powershell
Set-Location D:\elifoot_98
.\scripts\build-android-qa.ps1
```

Artefato: `client/elifoot_flutter/build/app/outputs/flutter-apk/app-debug.apk` (158.404.721 bytes). SHA-256: `EE94B45B25D2282AC89EA1F2F6A4BDA0E4A00B9C3FADB6A0C76F76893D86D3DF`. Metadados e inventário das 25 screenshots em [QA002_ARTIFACTS.json](QA002_ARTIFACTS.json). Destino padrão do build de teste: `http://10.0.2.2:5080`, endereço do host visto pelo emulador. Não publicar esse APK como versão de produção.

Também foram instalados emulador oficial e imagem Android 35. `emulator -accel-check` retornou código 6: driver do hipervisor ausente. Foi tentado boot com `-no-accel -no-window -gpu swiftshader`, mas o dispositivo permaneceu `offline` e não concluiu inicialização. A tentativa foi encerrada. Não foi instalado driver nem alterada a virtualização do Windows. Instalação do APK, inicialização, navegação e rede nativas **não recebem PASS**.

Diagnósticos: [QA002_ANDROID_ACCEL.log](QA002_ANDROID_ACCEL.log), [QA002_ANDROID_DEVICES.log](QA002_ANDROID_DEVICES.log), [QA002_ANDROID_BUILD.log](QA002_ANDROID_BUILD.log).

## Defeitos encontrados e corrigidos

1. Enums serializados como números tornavam os botões condicionais da interface inacessíveis. Contratos HTTP, SignalR e pacotes agora usam nomes, mantendo leitura de valores antigos.
2. Seed idêntica reutilizava ID/código de sala e podia sobrescrever a anterior. Mundos e convites novos têm identidade própria; a seed continua controlando a simulação.
3. `HashCode.Combine` não garantia identidade entre processos; substituído por SHA-256 estável.
4. Todas as abas começavam com o mesmo ID de dispositivo. Agora recebem IDs independentes, com armazenamento de sessão por aba na Web.
5. WebSocket não tinha heartbeat/reconexão e o servidor não tratava fechamento abrupto. Foram acrescentados heartbeat, reconexão e controle de conexões no Hub.
6. Snapshots atrasados podiam fazer relógio/estado retroceder. Versões antigas são descartadas e comandos principais são bloqueados durante envio.
7. Depois de avançar, era possível enviar zero titulares antes do refresh. “Enviar tática” aguarda uma escalação completa; a transição de rodada tem proteção de comando.
8. Não havia escolha de clube nem entrada por código na interface. Ambas foram conectadas aos serviços existentes.
9. Calendário e resultados históricos da temporada não tinham tela própria. Oitava aba e consulta de calendário acrescentadas.
10. Editor era essencialmente uma caixa de JSON. Foram acrescentados criação de distrital com elenco/técnico e formulários de clube, jogador e treinador, com validações.
11. Alterações de clube/desemprego não eram reconciliadas no cliente. O estado do participante agora atualiza a sessão e o plantel.
12. Lobby, placares e seletores de criação estouravam a largura de telefone. Layouts passam a quebrar linha; seletores se expandem dentro do campo.
13. Listas cortavam jogadores do mercado e técnicos do ranking. Retirados os cortes arbitrários de 30/35 registros.
14. Déficit estrutural de 100 temporadas e leilões que nunca expiravam no novo ano. Corrigidos e medidos novamente.
15. Fallback de leilão podia impor jogador a um humano; agora considera clubes IA com reserva financeira.
16. Oferta podia aparecer aceita apesar de a transferência falhar. Estado agora reflete o resultado real.
17. Outro treinador podia recusar proposta alheia. Verificada participação do clube e estado pendente/contraproposta antes da resposta.
18. Saves concorrentes podiam duplicar inserções dos resumos financeiros. Persistência serializada por mundo.
19. Reconexão com outro clube ou entrada nova durante jogo permitia mudanças indevidas de participação. Restrições entre rodadas adicionadas, mantendo reconexão do treinador existente.

## Testes e reprodução

```powershell
dotnet build Elifoot.slnx -c Release
dotnet test Elifoot.slnx -c Release --no-build

Set-Location client\elifoot_flutter
flutter analyze
flutter test
flutter build web --release --no-web-resources-cdn --pwa-strategy=none
```

Foram executados 19 testes .NET distintos: 9 de domínio, 3 de aplicação, 5 de integração, 2 de simulação/benchmark. A suíte completa passou com os primeiros 7 testes de domínio; depois, os 2 novos cenários de personalidade persistente e limite de substituições foram acrescentados e os **9 testes de domínio passaram**. Não houve alteração do código C# de produção após esses testes adicionais. O Flutter foi reanalisado, testado e recompilado depois do último ajuste visual.

- [QA002_DOTNET_TESTS.log](QA002_DOTNET_TESTS.log): suíte completa, incluindo PostgreSQL real e simulação de 100 temporadas.
- [QA002_DOMAIN_EXTRA_TESTS.log](QA002_DOMAIN_EXTRA_TESTS.log): 9 testes de domínio, incluindo os 2 adicionais.
- [QA002_FLUTTER_TESTS.log](QA002_FLUTTER_TESTS.log): 2 testes aprovados.
- [QA002_FLUTTER_ANALYZE.log](QA002_FLUTTER_ANALYZE.log): análise sem problemas.
- [QA002_WEB_BUILD.log](QA002_WEB_BUILD.log), [QA002_MOBILE_WEB_BUILD.log](QA002_MOBILE_WEB_BUILD.log): builds Web reais.
- [BENCHMARK_REPORT.json](BENCHMARK_REPORT.json): benchmark reexecutado pela suíte.

Uma execução intermediária de build .NET falhou porque a própria API de QA ainda mantinha DLLs abertas. A API foi encerrada, os testes foram repetidos com sucesso e o servidor foi reiniciado com o SQLite preservado. Uma execução intermediária do teste de telefone falhou por overflow; os dois seletores foram corrigidos e os dois testes Flutter passaram. Esses erros intermediários não foram tratados como PASS.

## Screenshots reais

Diretório: `docs/screenshots/qa002/`. Imagens JPEG capturadas do navegador, sem reconstrução gráfica ou mocks de tela. As primeiras capturas registram os checkpoints antes do ajuste final dos botões; 19/20 e as capturas móveis mostram a aparência retangular atual.

| Nº | Arquivo | Conteúdo |
|---:|---|---|
| 01 | [01-plantel.jpg](screenshots/qa002/01-plantel.jpg) | Plantel real |
| 02 | [02-resultados.jpg](screenshots/qa002/02-resultados.jpg) | Resultado de rodada |
| 03 | [03-pausa-substituicao.jpg](screenshots/qa002/03-pausa-substituicao.jpg) | Substituição no jogo solo |
| 04 | [04-financas-estadio.jpg](screenshots/qa002/04-financas-estadio.jpg) | Reforma e livro-caixa |
| 05 | [05-classificacao.jpg](screenshots/qa002/05-classificacao.jpg) | Classificação |
| 06 | [06-editor-jogador.jpg](screenshots/qa002/06-editor-jogador.jpg) | Editor e validação de força |
| 07 | [07-temporada-completa.jpg](screenshots/qa002/07-temporada-completa.jpg) | Temporada 2 e campeões |
| 08 | [08-multiplayer-0de8.jpg](screenshots/qa002/08-multiplayer-0de8.jpg) | Oito participantes, 0/8 |
| 09 | [09-pausa-cliente3.jpg](screenshots/qa002/09-pausa-cliente3.jpg) | Pausa solicitada por MP3 |
| 10 | [10-substituicao-outro-cliente.jpg](screenshots/qa002/10-substituicao-outro-cliente.jpg) | Evento visto por outro cliente |
| 11 | [11-venda-negociacao.jpg](screenshots/qa002/11-venda-negociacao.jpg) | Venda liquidada |
| 12 | [12-leilao-lance.jpg](screenshots/qa002/12-leilao-lance.jpg) | Lance registrado |
| 13 | [13-convites.jpg](screenshots/qa002/13-convites.jpg) | Desemprego e convites |
| 14 | [14-editor-pacote-validado.jpg](screenshots/qa002/14-editor-pacote-validado.jpg) | Pacote válido de 129 clubes |
| 15 | [15-web-390-lobby.jpg](screenshots/qa002/15-web-390-lobby.jpg) | Lobby a 390 px |
| 16 | [16-web-390-plantel.jpg](screenshots/qa002/16-web-390-plantel.jpg) | Plantel a 390 px |
| 17 | [17-final-desktop.jpg](screenshots/qa002/17-final-desktop.jpg) | Temporada persistida após reinício |
| 18 | [18-web-390-resultados.jpg](screenshots/qa002/18-web-390-resultados.jpg) | Placares responsivos |
| 19 | [19-cliente-desconectado.jpg](screenshots/qa002/19-cliente-desconectado.jpg) | Participante desconectado |
| 20 | [20-final-pausa-oito.jpg](screenshots/qa002/20-final-pausa-oito.jpg) | Pausa repetida na versão final |
| 21 | [21-web-390-criar-sala.jpg](screenshots/qa002/21-web-390-criar-sala.jpg) | Criação de sala em largura de telefone |
| 22 | [22-demissao-real.jpg](screenshots/qa002/22-demissao-real.jpg) | Desemprego após rebaixamento real |
| 23 | [23-convites-apos-demissao.jpg](screenshots/qa002/23-convites-apos-demissao.jpg) | Convites depois da demissão |
| 24 | [24-editor-sem-estrela.jpg](screenshots/qa002/24-editor-sem-estrela.jpg) | Estrela removida e Sarrafeiro persistido |
| 25 | [25-temporada-final-persistida.jpg](screenshots/qa002/25-temporada-final-persistida.jpg) | Temporada normal no build final e origem sem cache antigo |

Transcrições compactas: [solo-ui-log.json](screenshots/qa002/solo-ui-log.json), [multiplayer-ui-log.json](screenshots/qa002/multiplayer-ui-log.json).

## Limitações e acompanhamento

- Android nativo sem boot, instalação e teste de conectividade confirmados; o APK compilado não substitui essas provas.
- As 100 temporadas usam placares agregados; não houve execução integral minuto a minuto das 100 temporadas.
- Os extremos de quotas/prorrogação foram cobertos por testes do motor; os oito navegadores comprovaram o fluxo normal, sincronismo e propagação.
- Importação pela interface é de pacote JSON; CSV é exportável e seu parser é testado no domínio, mas não tem fluxo de importação de arquivo pela interface.
- Identidade persistida na Web é por aba/origem; não existe recuperação de conta de produção nem sessão persistente Android implementada nesta auditoria.
- Aparência clássica aproximada e tabelas com rolagem horizontal no telefone. Não houve teste em aparelho Android físico.
- O balanceamento econômico de longo prazo merece calibração adicional apesar da solvência e das invariantes terem passado.

## Registro complementar de carreira

Foi exportado um pacote pelo editor e seus 18 atletas da Ferroviária Grená foram alterados para força 1, sem estrela, com contratos longos e salários de teste. O pacote foi validado e importado pela própria interface. QA Rebaixamento entrou desempregado, aceitou convite da quarta divisão e jogou as **22 rodadas** por controles gráficos. Nenhum resultado, classificação ou comando de avanço foi aplicado fora da interface.

Ao iniciar a temporada 2, a interface mostrou Ferroviária Grená na **Distrital**, treinador **Desempregado** e botão **PRONTO PARA ACOMPANHAR**. Abrir convites exibiu clubes elegíveis; aceitar Joinville Tricolor devolveu ao treinador um clube da quarta divisão e o botão **ENVIAR TÁTICA**. Evidência: [career-ui-log.json](screenshots/qa002/career-ui-log.json), screenshots 22 e 23. Esse segundo ciclo também percorreu a temporada no build com as correções finais de sincronismo e carreira.

O leilão observado na temporada normal foi liquidado na primeira rodada da temporada 2. Igor Nascimento (D, força 16), antes liderado pelo humano por R$ 87.751, apareceu **ENCERRADO**, vencedor **Juventude Serrana**, lance R$ 93.016. O humano perdeu a disputa, sem cobrança de arremate. A reforma também foi conferida depois da temporada: capacidade utilizável 35.487 de 35.676 e condição estrutural 99,47%, frente aos 90,5% iniciais.

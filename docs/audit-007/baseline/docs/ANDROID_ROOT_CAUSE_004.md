# ELIFOOT_98_ANDROID_ROOT_CAUSE_004

Projeto: `D:\elifoot_98`. Investigação em 29/10/2026, DEV privado. Evidências em `docs/android-root-cause-004/`. O motor, os módulos aprovados e as configurações críticas do Windows não foram alterados.

## Evidência causal obtida

Foi reproduzida falha do próprio Android em AVD limpo, antes de instalar Elifoot ou o Flutter de referência. O DropBox do Android registrou:

| Horário bruto do Android (UTC) | Processo | Motivo do ANR |
|---|---|---|
| 22:59:10 | `com.android.systemui` | Broadcast `android.intent.action.TIME_TICK` |
| 22:59:20 | `com.android.deskclock` | Broadcast `LOCKED_BOOT_COMPLETED`, `AlarmInitReceiver` |
| 23:00:20 | `android.process.media` | Broadcast `MEDIA_MOUNTED`, `DownloadReceiver` |
| 23:24:52 | `com.android.systemui` | Execução do serviço `ImageWallpaper`, no segundo AVD, sem GPU do host |
| 23:26:23 | `com.android.systemui` | Broadcast `TIME_TICK` no segundo AVD, antes do envio do APK Elifoot às 23:26:32 |

Os horários locais de São Paulo são três horas antes. O processo de referência só foi instalado a partir de 23:04:05 UTC e lançado a partir de 23:05:29. O Elifoot ainda não estava instalado quando ocorreram esses ANRs. A lista de pacotes e os processos do controle estão preservados; a imagem Android inclui exemplos nativos do SDK, que não foram confundidos com Elifoot ou Flutter.

Fontes primárias locais: `A-auto/bugreport.zip`, `A-auto/bugreport.txt`, `B-auto-native/dropbox-anr.stdout.log`, `A-auto/processes.stdout.log`, `A-auto/third-party-packages.stdout.log`, `C-auto-reference/commands.json` e `auto-logcat-stream.log`.

**Classificação demonstrada: ANDROID_SYSTEM_FAILURE.** O Elifoot não pode causar os ANRs anteriores à sua instalação. Isso não demonstra, por si só, ausência de qualquer problema de desempenho no jogo, nem identifica um defeito específico no binário do emulador.

### O que está comprovado e o que permanece hipótese

- **Comprovado:** firmware com VT-x/SLAT habilitados, `HypervisorPresent=false`, `emulator -accel-check` retornando 6 e driver de hipervisor ausente. O emulador executou com aceleração de CPU desabilitada/TCG.
- **Comprovado:** a CPU virtual chegou a 100% com processos nativos, antes de Flutter/Elifoot. O dump de CPU aponta `system_server`, telefone e launcher como consumidores. São percentuais da CPU virtual; não significam 100% dos 20 processadores lógicos do Windows.
- **Comprovado:** no bugreport, RAM Android de 1.019.528 KB, estado `normal`, RAM livre incluindo caches de 640.924 KB. Não foi demonstrado OOM como causa dos ANRs.
- **Comprovado:** o Flutter mínimo renderiza e mantém animação, mas seus contadores mostram todos os quadros observados acima de 100 ms; por exemplo, 46, 111 e 182 quadros em mensagens separadas por dez segundos. O controle não usa rede, banco, SharedPreferences ou código Elifoot.
- **Hipótese sustentada, sem contraprova acelerada:** a tradução de CPU sem hipervisor e a concorrência do Android durante inicialização contribuem para os atrasos. Não foi possível provar que sejam a causa única, pois não há execução equivalente com aceleração funcional neste host.
- **Não demonstrado:** bug do Flutter, defeito do motor Elifoot, corrupção do AVD antigo ou falha do driver físico AMD como explicação exclusiva.

Uma observação adicional de armazenamento encontrou 137.731.215.360 bytes livres no volume D:; não há evidência de falta de espaço. Uma única amostra de contadores de I/O não permite excluir latências intermitentes. A menor ocupação de CPU em parte do boot software é mais uma razão para não apresentar saturação de CPU como explicação única de todos os bloqueios.

A documentação oficial explica que [aceleração depende de hipervisor e evita tradução de código bloco a bloco](https://developer.android.com/studio/run/emulator-acceleration). A documentação atual classifica [execução sem aceleração como muito lenta e não suportada](https://developer.android.com/studio/run/emulator-commandline). Isso contextualiza o ambiente; não substitui as evidências desta execução.

### Limites da instrumentação

A coleta também consome CPU. O primeiro controle incluiu `dumpsys meminfo`, que retornou `SERVICE DUMP TIMEOUT EXPIRED`, e um bugreport, que levou 177,227 segundos. Os ANRs SystemUI/DeskClock antecedem o início do bugreport, mas algumas consultas de diagnóstico já haviam sido executadas. Portanto, não foi presumido que instrumentação seja neutra. A segunda comparação usa espera passiva antes de consultas pesadas.

Na segunda comparação, não foi observada conclusão do boot nos oito minutos de polling. Houve então espera sem consultas de **23:24:01.817 a 23:25:01.825 UTC**. O ANR `ImageWallpaper` foi registrado às **23:24:52**, dentro dessa janela e antes da instalação de qualquer aplicativo de teste. Essa ocorrência mostra que consultas pesadas e Elifoot não são condições necessárias para a falha. Trata-se de falha durante inicialização; não foi confundida com um Android previamente estável em repouso. O boot foi confirmado posteriormente na pré-condição do teste D, a partir de 23:25:53.

O snapshot de threads do bugreport é posterior aos eventos. Ele mostra, por exemplo, a thread principal de SystemUI esperando por `MessageQueue`, mantida por `PhoneStatusBar`; não permite afirmar que esse seja um deadlock permanente ou a causa original do ANR. Não houve evidência suficiente para declarar que `system_server` caiu: ANR, timeout de serviço e morte de processo são resultados diferentes.

`dumpsys activity lastanr` não é suportado por essa imagem API 25. Sua saída `Unknown command` foi mantida e não foi tratada como ausência de ANR. Foram usados DropBox, eventos `am_anr` e o bugreport para a análise.

## Ambiente e desenho do isolamento

| Item | Valor |
|---|---|
| Windows | Windows 10 Pro 22H2, 10.0.19045 |
| CPU | Xeon E5-2630 v4, 10 núcleos/20 processadores lógicos |
| RAM física | 17.081.327.616 bytes |
| GPU física | AMD Radeon RX 5600 XT, driver 32.0.21030.2001 |
| Flutter / Dart | 3.38.5 / 3.10.4 |
| SDK / ADB | SDK Android 36 / Platform Tools 37.0.1, ADB 1.0.41 |
| Emulator SDK | 37.2.13; aceleração indisponível |
| Emulator usado para controle | 32.1.15, instalado separadamente na tarefa 003 |
| Imagem de controle | Android 7.1.1/API 25, default x86_64 |
| Hardware virtual | Nexus S, 480×800, densidade 240, 1.024 MB, um núcleo |

Foram criados `elifoot_qa004_auto` e `elifoot_qa004_software`, com dados novos e a mesma imagem, preservando os AVDs antigos. A API 25 satisfaz o minSdk 24 dos APKs usados, mas é uma imagem antiga; a versão da imagem continua sendo variável externa não eliminada por essa comparação.

O modo `-gpu auto` efetivamente resolveu para **`swiftshader_indirect`** em execução headless. Portanto, não é um teste de renderização na Radeon física. A segunda tentativa usa `-gpu off`, removendo a emulação gráfica do host. Nos aplicativos Flutter de controle foi usado software rendering com Impeller explicitamente desabilitado, evitando o aborto já identificado na tarefa 003.

A consulta de Windows Hypervisor Platform exigiu elevação. O estado exato desse recurso não pôde ser lido com o token atual. Não foram habilitados recursos, instalados drivers de kernel, alterados BIOS/segurança, reiniciado o Windows ou encerrados aplicativos de outros projetos. Ao fim da compilação, foi parado apenas o daemon Gradle do cache dedicado deste repositório para reduzir interferência de memória.

Há duas cópias de ADB após a instalação de scrcpy na tarefa anterior: ambas são 37.0.1. A coleta usa explicitamente o ADB do SDK; não foi encontrada divergência de versões. A [compatibilidade retroativa esperada pelo fornecedor](https://developer.android.com/tools/releases/platform-tools) não comprova ausência de bugs, mas não havia evidência para iniciar uma sequência de downgrades de ADB.

## Isolamento executado

| Caso | Experimento e evidência |
|---|---|
| A — Android sem aplicativo de teste | AVD limpo, boot completo, pacotes/processos registrados. Três ANRs nativos confirmados em DropBox. `A-auto/` e bugreport. |
| A2 — Segundo AVD, GPU do host desligada | Boot não confirmado em oito minutos; ANR de SystemUI/ImageWallpaper durante espera passiva, antes das consultas pesadas e sem app de teste instalado. `A-software/`. |
| B — Aplicativo nativo | Configurações abriu com `Status: ok`; `ThisTime=5020 ms`, `WaitTime=5609 ms`. Tela real em `B-auto-native/screen.png`. |
| C — Flutter mínimo | Projeto independente em `tools/android_reference`; instalação `Success` em 84,354 s; abertura `Status: ok`, `ThisTime=9908 ms`. Animação e heartbeats observados. Tela em `C-auto-reference/screen.png`. |
| D — Elifoot no menu | Primeira tentativa: solicitações de encerramento dos controles excederam 30 s; instalação do APK de 70,7 MB excedeu 150 s. O menu não foi validado nessa tentativa. Timeout de instalação não foi classificado como crash do Elifoot. `D-auto-menu/`. |
| D2 — Elifoot x64 no segundo AVD | Boot completo confirmado tardiamente; envio do APK de 30,3 MB em 2,975 s; `pm install` também excedeu 150 s. Nenhum lançamento foi certificado. `D-software-menu/`. |
| E — Elifoot conectado | **BLOCKED nesta tarefa:** não houve Android estável nem abertura certificada em D/D2. REST e SignalR iniciais comprovados em 003 são evidência histórica, não revalidação 004. |
| F — Elifoot durante partida | **BLOCKED:** não executados partida de 90 minutos, prorrogação, quotas de substituições/paradas ou desconexão/reconexão nativas nesta tarefa. Não foram simulados para produzir PASS. |

A tentativa D teve interferência do controle de referência que não pôde ser encerrado no prazo. Não é uma comparação limpa de consumo de CPU entre os dois apps. Essa limitação motivou o AVD separado para a segunda hipótese, em vez de repetir indefinidamente a mesma instalação.

Na segunda comparação, o controle Flutter de referência não foi instalado. O ANR de ImageWallpaper antecede tanto o envio do APK menor quanto o pedido de instalação. Assim, o resultado não depende de o Flutter mínimo estar animando em paralelo. O screenshot `A-software/screen.png` retornou com zero bytes, apesar do código de saída ADB zero: não é uma captura válida e não foi apresentado como prova de renderização. A presença de `boot_completed=1`, isoladamente, também não foi tratada como certificado de estabilidade.

O limite de 150 s é um orçamento de teste explícito. Um timeout não prova que o Package Manager jamais terminaria, que o aplicativo sofreu crash ou que a instalação foi desfeita no guest. Ele encerra aquela tentativa de validação; os comandos de leitura posteriores registram o estado resultante. Não se confundiu término do cliente ADB com cancelamento garantido de todo trabalho no Android.

Os coletores são ferramentas de diagnóstico, não certificadores de gameplay. Nas primeiras execuções, `accepted` aplicava uma busca textual de erros também às saídas de logcat e o script podia terminar com código zero mesmo após falha de um passo; esses registros originais foram preservados. A versão final limita a heurística aos comandos de controle e retorna código 2 se instalação/abertura/encerramento do controle falhar. As conclusões acima usam duração, código, conteúdo bruto e captura válida, e não apenas esse campo ou o código de saída do coletor.

## Alterações controladas

- Criado Flutter mínimo independente, com animação, contador de toque e heartbeat de frame timing, sem dependências do Elifoot.
- Criados coletores com limites de tempo e preservação separada de stdout, stderr, duração e argumentos de cada comando.
- Mantidos os APKs 003. Preparado APK profile só para x64, com o mesmo código e URL DEV, para reduzir bytes verificados durante instalação no emulador; isso não é correção comprovada dos ANRs.
- O APK x64 foi inspecionado com `aapt`: pacote `com.example.elifoot_flutter`, minSdk 24, targetSdk 36. A biblioteca AOT não contém os marcadores do teste `QA003_STAGE`/`Native Android UI and real server runtime`; trata-se do aplicativo normal, não do APK de integração.
- Nenhum módulo de produção do cliente ou do servidor foi reescrito para atribuir artificialmente a falha ao aplicativo.

## Comandos reproduzíveis

Em PowerShell, na raiz, configure os caminhos locais:

```powershell
$env:ANDROID_HOME='D:\elifoot_98\.tools\android-sdk'
$env:ANDROID_SDK_ROOT=$env:ANDROID_HOME
$env:ANDROID_AVD_HOME='D:\elifoot_98\.tools\avd'
$env:JAVA_HOME='D:\elifoot_98\.tools\jdk\jdk-17.0.20.1+1'
$env:GRADLE_USER_HOME='D:\elifoot_98\.tools\gradle'
$env:PATH="C:\flutter\bin;$env:PATH"
flutter doctor -v
& "$env:ANDROID_HOME\platform-tools\adb.exe" devices -l
& "$env:ANDROID_HOME\emulator\emulator.exe" -accel-check
```

O comando do emulador de controle usa o executável local separado:

```powershell
$fallback='D:\elifoot_98\.tools\emulator-fallback\emulator'
$env:PATH="$fallback;$fallback\lib64;$fallback\lib64\qt\lib;$env:PATH"
& "$fallback\emulator.exe" -avd elifoot_qa004_auto -no-window -no-snapshot `
  -no-accel -no-audio -no-boot-anim -cores 1 -memory 1024 `
  -gpu auto -feature -Vulkan -show-kernel -verbose
```

Para a comparação software, selecione `elifoot_qa004_software` e `-gpu off`. Não use `-wipe-data`. Foram feitas somente essas duas inicializações de controle, cada qual com hipótese definida.

Coletores na raiz, com um único dispositivo online:

```powershell
node scripts/collect-android-isolation.mjs A-auto --wait --bugreport
node scripts/run-android-isolation-case.mjs B-auto-native native
node scripts/run-android-isolation-case.mjs C-auto-reference reference
node scripts/run-android-isolation-case.mjs D-auto-menu elifoot
# Segundo AVD, espera passiva e coleta leve:
node scripts/collect-android-isolation.mjs A-software --wait --passive --light
$env:QA_ANDROID_APK='artifacts/android/elifoot-qa004-profile-x64.apk'
node scripts/run-android-isolation-case.mjs D-software-menu elifoot
```

Os scripts chamam os executáveis sem montar strings de shell, registram comandos e limitam esperas. São coletados `adb shell getprop`, processos, serviços, CPU/memória, `adb logcat`, screenshots e `adb bugreport`. Arquivos vazios, consultas não suportadas ou timeouts são evidências de tentativa, não coleta bem-sucedida.

## Dispositivo físico e validação pendente

O APK x64 serve ao emulador x86_64. Para telefone ARM/ARM64, usar o APK profile universal preservado ou recompilar a ABI apropriada; não instalar o x64 como se fosse universal.

```powershell
& "$env:ANDROID_HOME\platform-tools\adb.exe" devices -l
# Com telefone autorizado em estado device:
.\scripts\run-android-qa.ps1 -DeviceSerial SERIAL -Transport UsbReverse -Mode profile
```

O script configura `adb reverse tcp:5080 tcp:5080` e recompila a origem para o loopback encaminhado do telefone. A API DEV continua na porta 5080; Web na 8083. HTTP só permanece permitido em debug/profile. Release mantém HTTPS obrigatório e `usesCleartextTraffic=false`.

Em Android estável, repetir duas interfaces reais: 0/2 → 1/2 → 2/2, início automático, pausa pedida pelo Android no mesmo minuto Web, cinco substituições em três paradas, retomada e 90 minutos, partida de Taça com prorrogação e substituição/parada adicionais, desconexão e reconexão sem duplicar participante/comando/resultado. Os testes de domínio não substituem essa validação nativa.

## Resultado final

**STATUS: PARTIAL. Diagnóstico fundamentado, Android jogável ainda BLOCKED.**

| Campo | Resultado |
|---|---|
| ROOT_CAUSE | **ANDROID_SYSTEM_FAILURE comprovada:** ANRs de serviços/aplicativos nativos antes de Elifoot/Flutter, incluindo espera passiva no segundo AVD. **ENVIRONMENT_FAILURE:** aceleração indisponível. Causa única dos atrasos/bloqueios ainda **INCONCLUSIVE**; emulação TCG sem hipervisor é hipótese principal sustentada, não uma causalidade exclusiva demonstrada. |
| EVIDENCE | Cinco ANRs independentes do jogo; bugreport completo, DropBox, CPU/memória, fases e timestamps; Flutter mínimo renderizando com média de 6,38 FPS e 939/939 quadros medidos acima de 100 ms antes da tentativa de instalar Elifoot. |
| EMULATOR_STABILITY | **FAIL:** dois AVDs novos, modos auto/SwiftShader e sem GPU do host; não obtida base estável para certificar gameplay. Foram somente duas inicializações de controle. |
| ANDROID_SYSTEM | **FAIL no ambiente de controle:** SystemUI, DeskClock e Media/Downloads registraram ANRs; consultas/operações excederam limites. Não foi comprovada morte de `system_server` nem OOM como causa. |
| FLUTTER_RUNTIME | **PARTIAL:** referência independente abriu e animou por vários minutos; desempenho severamente degradado. Nenhum defeito específico do framework Flutter foi comprovado. |
| APP_RUNTIME | **BLOCKED:** D/D2 não certificaram abertura de Elifoot; não há evidência para declarar `ELIFOOT_APPLICATION_FAILURE`. As telas já comprovadas em 003 permanecem evidência histórica. |
| REST | **BLOCKED para revalidação Android 004**; conexão Android inicial PASS em 003 preservada. Backend local permaneceu utilizável. |
| SIGNALR | **BLOCKED para revalidação Android 004**; JoinRoom/frame nativo inicial PASS em 003 preservado. Não substituído por polling como prova de WebSocket. |
| WEB_ANDROID_MULTIPLAYER | **BLOCKED:** não reexecutada progressão 0/2 → 1/2 → 2/2 e partida nas duas interfaces. |
| GLOBAL_PAUSE | **BLOCKED:** pausa Android e minuto sincronizado com Web não executados em 004. |
| SUBSTITUTIONS | **BLOCKED:** cinco trocas normais, troca adicional na prorrogação e quotas de paradas não executadas nativamente em 004. Testes de domínio aprovados não são prova desse runtime. |
| RECONNECT | **BLOCKED:** desconexão/reconexão Android durante partida não executada. |
| ANDROID_PHYSICAL_DEVICE | **UNAVAILABLE:** nenhum telefone autorizado apareceu no ADB; procedimento USB acima. |
| TESTS | **PASS: 19 .NET e 8 Flutter**, `flutter analyze` limpo. Controle Flutter mínimo compilado/analisado. Nenhum teste de partida Android declarado PASS. |
| APK | `artifacts/android/elifoot-qa004-profile-x64.apk`, aplicativo normal profile para emulador x64; APKs universais 003 preservados para teste em telefone compatível. |
| LOG_PATHS | `docs/android-root-cause-004/`; índice/hash em `evidence-manifest.json`; comandos por fase em `commands.json`; arquivos brutos não foram substituídos por resumos. |
| REMAINING_BLOCKERS | Um Android estável: hipervisor/aceleração utilizáveis com imagem suportada, ou telefone físico autorizado. Falta contraprova acelerada para atribuir causa única e falta executar a matriz de partida real. |

### Evidências para revisão rápida

- [Bugreport bruto completo do controle sem Elifoot](android-root-cause-004/A-auto/bugreport.zip) e [texto principal extraído](android-root-cause-004/A-auto/bugreport.txt).
- [ANRs SystemUI, DeskClock e Media](android-root-cause-004/B-auto-native/dropbox-anr.stdout.log).
- [ANR ImageWallpaper na janela passiva](android-root-cause-004/A-software/dropbox-anr.stdout.log), [cronologia passiva](android-root-cause-004/A-software-collector.log).
- [Configurações Android efetivamente aberto](android-root-cause-004/B-auto-native/screen.png).
- [Flutter mínimo efetivamente renderizado](android-root-cause-004/C-auto-reference/screen.png), [métricas de frames](android-root-cause-004/reference-frame-summary.json).
- [Tentativa D e comandos](android-root-cause-004/D-auto-menu/commands.json), [tentativa D2](android-root-cause-004/D-software-menu/commands.json). O screenshot de D ainda mostra o controle mínimo e não foi confundido com Elifoot.
- [Diagnóstico Windows](android-root-cause-004/windows-host.json), [amostras de CPU/memória](android-root-cause-004/host-resource-samples.csv), [amostras do segundo controle](android-root-cause-004/host-resource-software.csv), [armazenamento](android-root-cause-004/storage-observation.json).
- [Testes .NET](android-root-cause-004/dotnet-tests.log), [testes Flutter](android-root-cause-004/flutter-tests.log), [análise estática](android-root-cause-004/flutter-analyze.log).

### Encerramento e próximos passos

A investigação termina após os controles e hipóteses acima. Não foi executado terceiro boot nem nova sequência de recuperação do mesmo AVD. Não se mudou lógica de partida, pausa, substituição, reconexão ou rede para mascarar o ambiente.

Ambos os AVDs foram encerrados pelo comando `adb emu kill`, preservando seus dados. Os AVDs antigos também foram preservados. API e Web permaneceram disponíveis e retornaram HTTP 200 na verificação final (`final-state.json`). Nenhum telefone físico foi visto no ADB antes do encerramento intencional do último emulador.

Para liberar a validação, apresentar este diagnóstico a um administrador e avaliar aceleração suportada no Windows, com autorização explícita para qualquer recurso/reinicialização que afete outros trabalhos; alternativamente, usar telefone físico autorizado. Habilitar virtualização no BIOS não é uma recomendação baseada nestes dados: ela já está habilitada. Não há evidência que justifique atualizar o driver AMD ou reescrever o Flutter/Elifoot como correção comprovada.

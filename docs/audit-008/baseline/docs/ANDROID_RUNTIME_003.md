# ELIFOOT_98_ANDROID_RUNTIME_003

Projeto: `D:\elifoot_98`. Execução local em 29/10/2026, Windows 10 Pro 22H2 (19045.6466). Nenhuma publicação em produção. O motor de partidas não foi alterado.

## Diagnóstico e recuperação

O SDK, JDK e as licenças estavam válidos. O AVD anterior `elifoot_qa002` (API 35, Google APIs, x86_64) não concluía a inicialização. O diagnóstico encontrou:

- VT-x e SLAT disponíveis e virtualização habilitada no firmware.
- `HypervisorPresent=false`; `emulator -accel-check` retornou código 6: `Android Emulator hypervisor driver is not installed on this machine`.
- O processo desta tarefa não possui token de Administrador do Windows. A consulta dos recursos opcionais do Windows exigiu elevação; o estado exato do recurso WHPX não pôde ser consultado. Remover o sandbox do comando não concede elevação UAC.
- Um único ADB foi encontrado inicialmente, em `.tools/android-sdk/platform-tools/adb.exe`. Nenhum telefone físico apareceu no ADB. Não foi encontrada evidência de conflito entre servidores ADB.
- A execução direta do backend QEMU sem seus diretórios de DLL no PATH retornou `0xC0000135`. Com `emulator`, `emulator/lib64` e `emulator/lib64/qt/lib` no PATH, `qemu ... -version` passou. Isso resolveu o teste de carregamento; não resolveu, por si só, a inicialização do emulador 37.

Foi reiniciado o servidor ADB e atualizado o Android Emulator de **37.2.12 para 37.2.13** pelo SDK Manager oficial. Foram tentados cold boot, ausência de snapshots, renderização SwiftShader, renderização desligada, backend headless, backend Qt e chamada direta do QEMU. Os logs 37 encerraram antes do boot ou ficaram na inicialização gráfica/main loop. Não foi usado `-wipe-data`.

Foi criado um AVD separado `elifoot_qa003_light`, Nexus S, Android 7.1.1/API 25, imagem `system-images;android-25;default;x86_64`, 480×800, 1.024 MB, um núcleo. O AVD anterior foi preservado.

Como contorno, o **Android Emulator 32.1.15 (10696886)** foi baixado do arquivo oficial e extraído em `.tools/emulator-fallback/emulator`, sem substituir o SDK atualizado. Com `-no-accel -gpu swiftshader_indirect -feature -Vulkan`, o kernel iniciou, o ADB passou a `device` e `sys.boot_completed` chegou a **1**. O primeiro boot levou vários minutos e apresentou ANR do próprio sistema.

O APK foi efetivamente instalado: `pm install -r` retornou **Success**, e `pm path com.example.elifoot_flutter` retornou `/data/app/com.example.elifoot_flutter-1/base.apk` (o sufixo muda após reinstalação). Houve tentativas tanto com streaming quanto com `--no-streaming` e instalação a partir de `/data/local/tmp`. O log do Flutter Drive confirmou outra instalação em 243,1 segundos. A lentidão inclui verificação de assinatura e `dex2oat`, não apenas transferência do arquivo.

Uma tentativa de software rendering sem desligar explicitamente o Impeller abortou com:

```text
Check failed: !settings.enable_impeller.
Impeller does not support software rendering.
```

O lançamento de diagnóstico foi corrigido para combinar `enable-software-rendering=true` e `enable-impeller=false`. Isso é um parâmetro local do teste; o renderer do release não foi alterado. O Flutter emitiu aviso de futura descontinuação do opt-out do Impeller.

## Correções no cliente e ferramentas

- `lib/api_environment.dart` centraliza a origem usada por REST e SignalR. Android DEV usa `http://10.0.2.2:5080`; `API_BASE_URL` permite configuração explícita. Web DEV deriva o host da página e a porta 5080. Release nativo exige origem HTTPS explícita e rejeita HTTP.
- `AndroidManifest.xml` principal declara `usesCleartextTraffic=false`. Os manifests DEV debug e profile sobrescrevem para `true`; profile é usado somente para a tentativa de desempenho local. Os manifests mesclados debug, profile e release foram gerados e inspecionados: valores **true/true/false**, respectivamente; minSdk 24. Nenhum release recebeu permissão para HTTP.
- O armazenamento nativo, que era um stub, usa `SharedPreferencesWithCache`, inicializado antes da restauração. Login, entrada e troca de sala aguardam a gravação. Web mantém `sessionStorage` por aba.
- O cliente oferece indicadores de diagnóstico de JoinRoom e número de mensagens SignalR recebidas. Polling HTTP não é considerado prova de WebSocket.
- `kotlin.incremental=false` evita a falha observada `this and base files have different roots` entre o cache Pub em C: e o projeto em D:. O primeiro build usou fallback do compilador; o build seguinte concluiu sem esse erro.
- As chaves de widgets para sala e clube permitem testes sem depender da ordem visual das salas.
- `scripts/build-android-qa.ps1` compila explicitamente `lib/main.dart` e entrega uma cópia separada do APK normal em `artifacts/android/elifoot-qa003-debug.apk`, evitando confusão com APKs que executam testes.
- `scripts/run-android-qa.ps1` verifica dispositivo online, boot completo, encaminhamento USB opcional, instalação, lançamento, processo e logs/captura. Possui limite de tempo para comandos ADB e rejeita `Status: timeout` como abertura bem-sucedida.
- `scripts/start-android-qa-emulator.ps1` reproduz o cold boot, incluindo a opção de contorno em software, sem apagar dados.

## Versões usadas

| Item | Versão |
|---|---|
| Flutter / Dart | 3.38.5 / 3.10.4 |
| Android SDK Platform / Build Tools | android-36 / 36.0.0 |
| Platform Tools / ADB | 37.0.1-15733141 / 1.0.41 |
| Emulator instalado no SDK | 37.2.13, build 16740729 |
| Emulator usado no contorno | 32.1.15, build 10696886 |
| Android do contorno | 7.1.1, API 25, x86_64 |
| Java | Temurin 17.0.20.1+1 |
| Gradle / AGP / Kotlin | 8.14 / 8.11.1 / 2.2.20 |
| shared_preferences / Android | 2.5.5 / 2.4.23 |
| scrcpy de diagnóstico | 5.0.1, origem oficial GitHub |

`flutter doctor -v` também aponta canal Flutter `[user-branch]`/upstream desconhecido e componentes ausentes do Visual Studio para desktop. A cadeia Android é reconhecida. Não foi trocado o Flutter nem instalado um workload desktop, pois isso não resolve a ausência de aceleração Android.

## Rede e execução reproduzível

A API real responde em **http://127.0.0.1:5080**; a interface Web em **http://127.0.0.1:8083**. O backend local usa SQLite `playable-qa.db`. Os dados das salas da auditoria anterior foram preservados.

Em PowerShell 7, a partir da raiz:

```powershell
$env:ANDROID_HOME = 'D:\elifoot_98\.tools\android-sdk'
$env:ANDROID_SDK_ROOT = $env:ANDROID_HOME
$env:ANDROID_AVD_HOME = 'D:\elifoot_98\.tools\avd'
$env:JAVA_HOME = 'D:\elifoot_98\.tools\jdk\jdk-17.0.20.1+1'
$env:GRADLE_USER_HOME = 'D:\elifoot_98\.tools\gradle'
$env:PATH = "C:\flutter\bin;$env:PATH"
flutter doctor -v
flutter devices
flutter emulators
& "$env:ANDROID_HOME\platform-tools\adb.exe" devices -l
```

Se o servidor ainda não estiver ativo, iniciar em outro terminal:

```powershell
.\scripts\run-dev.ps1 -DatabaseProvider Sqlite -DatabasePath D:\elifoot_98\playable-qa.db
.\scripts\run-web.ps1 -WebPort 8083 -ApiUrl http://127.0.0.1:5080
```

Emulador com aceleração utilizável: iniciar o AVD existente com cold boot. O caminho recomendado para corrigir o ambiente é habilitar o Windows Hypervisor Platform com um administrador e reiniciar o Windows quando solicitado, então exigir `emulator -accel-check` utilizável antes de repetir a auditoria. Nenhuma alteração UAC, BIOS, driver de kernel ou reinicialização do Windows foi feita por esta tarefa. Consulte a [documentação oficial de aceleração](https://developer.android.com/studio/run/emulator-acceleration).

O contorno local já instalado pode ser repetido assim, mantendo o terminal aberto:

```powershell
.\scripts\start-android-qa-emulator.ps1 -UseSoftwareFallback
```

Depois do boot, em outro terminal:

```powershell
.\scripts\run-android-qa.ps1 -DeviceSerial emulator-5554 -Transport Emulator `
  -SoftwareRendering -BootTimeoutSeconds 1200
```

Para compilar/instalar o aplicativo normal com AOT de desenvolvimento, adicione `-Mode profile`. A cópia jogável fica em `artifacts/android/elifoot-qa003-profile.apk`; a cópia debug fica em `artifacts/android/elifoot-qa003-debug.apk`. Os arquivos cujo nome contém `integration` executam a auditoria automatizada e não devem ser confundidos com esses aplicativos normais.

O endereço `10.0.2.2` é o acesso do emulador ao loopback do host; veja a [rede oficial do Android Emulator](https://developer.android.com/studio/run/emulator-networking). Não usar `localhost` para chegar ao Windows a partir do emulador. A tentativa adicional com GPU desligada é reproduzida adicionando `-Gpu off` ao script de inicialização.

Para um telefone autorizado e online via USB, o script faz `adb reverse tcp:5080 tcp:5080` e compila para o loopback do **telefone**, encaminhado ao Windows:

```powershell
.\scripts\run-android-qa.ps1 -DeviceSerial SERIAL_DO_TELEFONE -Transport UsbReverse
```

Para LAN, usar `-Transport Lan -ApiUrl http://IP_DO_WINDOWS:5080`, com API escutando na interface local correspondente e rede/firewall configurados pelo operador. Não foi aberta porta pública nem modificado o firewall nesta execução. Não reutilizar um APK compilado para `10.0.2.2` em telefone sem recompilar a URL. HTTP é exclusivamente DEV/debug/profile; release continua exigindo HTTPS.

## Comandos relevantes da execução

```powershell
# Recuperação ADB e ferramentas oficiais
adb kill-server
adb start-server
sdkmanager --update
sdkmanager 'system-images;android-25;default;x86_64'
avdmanager create avd -n elifoot_qa003_light -k 'system-images;android-25;default;x86_64' -d 'Nexus S'
# A lista exata de pacotes efetivamente alterados está em sdk-update.log.

# Cold boot do contorno (PATH e SDK configurados como acima)
.tools\emulator-fallback\emulator\emulator.exe -avd elifoot_qa003_light `
  -no-window -no-snapshot -no-accel -no-audio -no-boot-anim `
  -cores 1 -memory 1024 -gpu swiftshader_indirect -feature -Vulkan -show-kernel -verbose

# Instalação e lançamento de diagnóstico
adb -s emulator-5554 install --no-streaming -r artifacts\android\elifoot-qa003-debug.apk
adb -s emulator-5554 shell -x pm install -r /data/local/tmp/elifoot-qa003-debug.apk
adb -s emulator-5554 shell -x am start -S -W `
  -n com.example.elifoot_flutter/.MainActivity `
  --ez enable-software-rendering true --ez enable-impeller false

# Regressões e manifests
dotnet test Elifoot.slnx --no-restore --verbosity minimal
flutter analyze
flutter test
flutter build apk --debug --target=lib/main.dart --dart-define=API_BASE_URL=http://10.0.2.2:5080
flutter build web --release --no-web-resources-cdn --pwa-strategy=none --dart-define=API_BASE_URL=http://127.0.0.1:5080
# Na pasta client/elifoot_flutter/android:
.\gradlew.bat :app:processReleaseMainManifest --console=plain
```

Os comandos abreviados `adb`, `sdkmanager` e `avdmanager` acima representam os executáveis da única instalação em `.tools/android-sdk`; as invocações da tarefa usaram caminhos explícitos. O Emulator 32.1.15 veio do [arquivo oficial](https://developer.android.com/studio/emulator_archive), pacote `emulator-windows_x64-10696886.zip`, mantido separado. O espelhamento scrcpy veio do [repositório oficial](https://github.com/Genymobile/scrcpy).

## Teste nativo e roteiro de aceitação

O teste `integration_test/android_runtime_test.dart` usa o cliente REST/WebSocket verdadeiro. A criação da sala de fixture usa a API; login do treinador, escolha de clube, navegação e comandos de jogo usam widgets do aplicativo. A fixture tem 64 clubes para iniciar com a liga, conforme suporte já existente no motor. Não representa uma nova auditoria da temporada de 128 clubes concluída na tarefa anterior.

O segundo treinador deve ser operado pela interface Web real. O teste aguarda esse treinador; não cria um par simulado. Portanto, o comando abaixo sozinho não certifica multiplayer.

```powershell
cd D:\elifoot_98\client\elifoot_flutter
flutter drive --driver=test_driver/runtime_driver.dart `
  --target=integration_test/android_runtime_test.dart -d emulator-5554 `
  --enable-software-rendering --no-enable-impeller `
  --dart-define=API_BASE_URL=http://10.0.2.2:5080
```

O primeiro teste tentou converter a superfície Android para ImageView para capturas. Não chegou a concluir a captura do menu e foi interrompido. A segunda versão mantém a superfície original, emite checkpoints `QA003_STAGE` e usa capturas do framebuffer do Android via ADB. Um teste escrito/compilado ou uma conexão ao Dart VM não é contabilizado como gameplay aprovado.

Roteiro para repetir a aceitação, com capturas Android e Web de cada etapa:

1. Verificar `adb devices -l` com estado `device`, `sys.boot_completed=1`, instalar APK e exigir menu visível e responsivo, sem ANR.
2. Criar treinador Android distinto do Web; entrar na mesma sala e escolher clubes disponíveis. Conferir plantel, 11 titulares, reservas e formação.
3. Observar **0/2**, enviar a tática Web e observar **1/2** nas duas telas; enviar a tática Android e observar **2/2** e início automático. Em sala de 128 clubes, avançar as preliminares até haver jogo do clube humano.
4. Pedir parada pelo Android. Registrar o mesmo `currentMinute` e estado pausado nas duas telas após alguns segundos. Conferir mensagem, quotas e autor da parada.
5. No Android, trocar um titular por reserva elegível, aplicar o ajuste e verificar ambos os snapshots, identificadores de jogadores e contagem de substituições. Continuar nos dois clientes e observar relógio avançar.
6. Concluir intervalo e partida; conferir resultados e calendário. Abrir mercado, leilões, finanças/estádio e voltar ao plantel, com rolagem e botões acessíveis.
7. Encerrar/reabrir o app e interromper/restaurar a conexão. Conferir o mesmo treinador/clube/sala, apenas dois participantes e nenhuma duplicação de comandos ou resultados. A reconstrução do cliente do teste é distinta de um teste de perda da rede física.
8. Repetir em telefone pequeno e tela grande, retrato e paisagem; abrir teclado virtual nos campos, verificar tabelas e rolagem, mensagens e painel de substituições. Comparar capturas. Aprovação de largura 390 em widget test não certifica esses cenários nativos.

## Evidências

Tudo em `docs/android-runtime-003/`. Arquivos principais:

- `flutter-doctor-initial.log`, `flutter-devices-initial.log`, `flutter-emulators-initial.log`, `adb-after-restart.log`, `sdk-packages.log`, `sdk-update.log`.
- `cold-boot-api35.log`, `cold-boot-api25.log`, `cold-boot-api25-gpu-off.log`, `cold-boot-api25-qt.log`, `cold-boot-api25-direct.log`, `cold-boot-api25-emulator32.log`.
- `host-diagnostics.json`, `boot-completed.log`, `flutter-devices-final.log`, `flutter-emulators-final.log`, `adb-final.log`.
- `qemu-backend-version.log`, `qemu-backend-version-with-path.log`, `qemu-dependencies.json`, `qemu-qt-dependencies.json`.
- `install-no-streaming.log`, `install-legacy-shell.log`, `native-app-launch.log`, `native-software-launch.log`, `native-skia-launch.log`.
- `flutter-native-crash.log`, `native-skia-logcat.log`, `native-created-worlds.json`, `native-integration-run.log`, `native-integration-run-2.log`.
- `dotnet-tests.log`, `flutter-tests.log`, `flutter-analyze.log`, `android-build.log`, `android-integration-build-2.log`, `web-build.log`, `release-manifest-build.log`, `apk-sha256.log`.

Capturas reais do Android, sem montagem:

- [Boot com ANR do sistema](android-runtime-003/android-boot.png).
- [Tela inicial Android após a primeira tentativa de abertura](android-runtime-003/native-launch.png).
- [Diálogo do aborto na tentativa software/Impeller incompatível](android-runtime-003/native-skia.png).
- [Menu Elifoot efetivamente renderizado, coberto por ANR durante o primeiro teste](android-runtime-003/integration-current.png).
- [Plantel Android e contador 0/2 efetivamente renderizados](android-runtime-003/runtime-last.png), com dois treinadores, 11 titulares e banco.
- [Lobby Web com o cliente atualizado](android-runtime-003/web-lobby.jpg), que é evidência Web e não prova de Android.
- [Sala Web restaurada ao final](android-runtime-003/web-final-room.jpg).

## Resultado consolidado

**STATUS: PARTIAL. Android não aprovado como plataforma jogável.**

| Campo | Resultado desta tarefa |
|---|---|
| ANDROID_EMULATOR | **PARTIAL** — API 25 iniciou e concluiu boot usando Emulator 32.1.15 sem aceleração; ANRs e perda dos serviços Package Manager/Activity Manager impediram estabilidade. Emulator 37.2.13 não concluiu boot nas alternativas testadas. |
| ANDROID_INSTALL | **PASS** — APK realmente instalado e reinstalado no AVD; `Success`, `pm path` e logs Flutter Drive comprovam instalação. A última tentativa voltou a falhar no Package Manager. |
| ANDROID_APP_LAUNCH | **PARTIAL** — menu, lobby e plantel foram alcançados; houve timeout de abertura, ANRs e um aborto da combinação software/Impeller corrigido nos parâmetros de teste. Não houve abertura estável sustentada. |
| ANDROID_API | **PASS** — o cliente executando no Android criou salas, fez login e entrou com o Atlético Paulistano no servidor Windows por `http://10.0.2.2:5080`. |
| ANDROID_SIGNALR | **PASS de conexão inicial** — no teste profile, o cliente nativo registrou `signalR:true` e `frames:1` após completar JoinRoom; não é inferência do polling REST. Estabilidade durante a partida não validada. |
| ANDROID_GAMEPLAY | **BLOCKED** — nenhuma partida concluída pela interface Android. Não foi atribuído PASS por compilar APK, conectar Dart VM ou alcançar plantel. |
| WEB_ANDROID_MULTIPLAYER | **PARTIAL** — dois treinadores reais entraram na mesma sala, com clubes distintos e contador 0/2 nas interfaces Android e Web (`runtime-last.png` e `web-android-counter-0-of-2.jpg`). Não foram comprovados 1/2, 2/2 e início automático nessa combinação. |
| GLOBAL_PAUSE | **BLOCKED nesta tarefa** — pausa Android e sincronização de minuto/substituição com Web não chegaram a ser executadas. O PASS Web da QA_002 continua sendo evidência separada. |
| ANDROID_LAYOUT | **PARTIAL** — menu/lobby/plantel alcançados em AVD 480×800, densidade 240 (aproximadamente 320×533 lógicos). Não concluídas telas de mercado/estádio, rolagem completa, teclado real, paisagem e tela grande. O widget test de 390 pixels continua aprovado, sem ser tratado como prova nativa. |
| ANDROID_RECONNECT | **BLOCKED** — interrupções ocorreram e o Web mostrou Android desconectado; não se concluiu o ciclo de reconexão nativa com partida/persistência/ausência de duplicação. O adaptador de armazenamento tem teste unitário aprovado. |
| TESTS | **PASS: 19 .NET + 8 Flutter**, análise estática limpa; builds Web, APK debug e profile normais, APK de integração e manifest release aprovados. Teste nativo **incompleto/interrompido**, sem PASS. Scripts PowerShell e capturador Node tiveram sintaxe verificada. |
| APK_PATH | `D:\elifoot_98\artifacts\android\elifoot-qa003-debug.apk` e `D:\elifoot_98\artifacts\android\elifoot-qa003-profile.apk` — aplicativos normais DEV. |
| EVIDENCE_PATHS | `D:\elifoot_98\docs\android-runtime-003\`, este relatório, `apk-final.json`, `profile-apk-final.json` e `evidence-manifest.json`. |
| KNOWN_ISSUES | Sem hipervisor utilizável/token administrador, emulação TCG lenta e instável, nenhum telefone físico disponível; validação de partida/pausa/substituição/layout/reconexão Android permanece pendente. |

### Evidência positiva e limite do resultado

O teste profile registrou em `native-profile-retry.log`:

```text
QA003_STAGE 01-native-menu
QA003_STAGE 02-native-lobby
QA003_STAGE 03-native-squad
QA003_ROOM {"worldId":"b011cbb3-b8f8-469d-b196-98551d1200cd","name":"QA003 Android 1793312432317","clubId":"a935b33d-eb58-5547-a4cd-a5bb8e05d078","signalR":true,"frames":1}
```

O Web entrou nessa sala como **QA003 Web / Grêmio Guanabara**, adversário do **QA003 Android / Atlético Paulistano**. Houve também a sala anterior `9d6ad09d-38a6-4aa0-b6db-44b04698bf16`, registrada na captura `web-android-counter-0-of-2.jpg`. As salas extras são fixtures de tentativas distintas, não prova de duplicação de um treinador após reconexão.

Na continuação, os processos da tentativa anterior já não estavam ativos. A API foi reiniciada explicitamente sobre `playable-qa.db`, e os treinadores/salas continuaram no banco. O script DEV agora aceita `-DatabasePath` para reproduzir esse uso sem trocar o save por outro arquivo. A interface Web atualizada foi servida novamente em 8083, preservando o navegador antigo em 8080.

O mesmo AVD foi iniciado novamente, sem apagar dados. Foram confirmados ADB `device`, boot completo e existência do Activity Manager. Algumas chamadas pelo canal `shell` ficaram presas; o canal `exec-out` respondeu às verificações básicas. A instalação pelo segundo canal terminou, porém, com:

```text
Error: Could not access the Package Manager.  Is the system running?
```

Esse erro, documentado em `native-exec-out-start.log`, impediu a última repetição. A presença de um serviço ou a propriedade de boot não significa que o Android esteja funcional. O script de instalação detecta também erros Android emitidos com código de saída zero, evitando falso sucesso. Essa diferença entre canais ADB é uma observação do ambiente; não se demonstrou que atualizar/trocar somente ADB resolva a instabilidade do sistema.

Também foi registrado `Can't connect to activity manager; is the system running?` no lançamento final. Os diagnósticos desta continuação estão em `continued-device-diagnostics.json`, `api-continued.log`, `emulator-continued.log` e `native-profile-continued.log`. A observação de processos ADB travados e esses erros de serviços determinou o encerramento da tentativa; não foi substituído o cliente Android por um cliente de API no Windows para produzir uma partida artificial.

O campo persistido `isConnected` pode ficar desatualizado após término abrupto do emulador/reinício do servidor: o snapshot final manteve esse indicador para o treinador Android, embora não houvesse uma conexão nativa viva. Portanto, **esse campo sozinho não foi usado como evidência de conexão**. O PASS inicial de SignalR se baseia no retorno JoinRoom recebido pelo cliente nativo e no frame contado, antes da interrupção. Tratar presença após reinício abrupto é uma pendência de produto registrada; o orquestrador não foi alterado nesta tarefa.

Ao terminar, foi removido o encaminhamento de porta temporário criado no diagnóstico, encerrados os capturadores e desligado o AVD pelo comando `adb emu kill`. Seus dados e o AVD anterior foram mantidos. A API e a interface Web atualizada permaneceram em execução local. Um ADB sem emulador após esse encerramento não contradiz os logs de boot e instalação obtidos anteriormente.

### Testes e artefatos finais

Os 19 testes .NET são 9 Domain, 3 Application, 5 Integration e 2 Simulation. Os 8 Flutter incluem os dois testes de UI existentes e seis novos testes de origem da API e armazenamento nativo. O teste de armazenamento usa backend em memória no host, portanto não comprova gravação persistente física no Android. Veja `dotnet-tests.log`, `flutter-tests-completed.log` e `flutter-analyze-completed.log`.

Os APKs normais foram compilados a partir de `lib/main.dart`. A cópia debug tem 181.736.973 bytes e SHA-256 `E3F53AA3EC34359203756CB7C56479ABA163AE548AD9E1F58E4BA786ADD51BD1`. A cópia profile tem 70.740.682 bytes e SHA-256 `76063AE7D5F87353C233B7B16B1B3996A9D834F7A8AD7907B52ED20521D533A0`. Sua identificação está em `profile-apk-final.json`; o build completo está em `android-profile-game-build.log`. O arquivo `elifoot-qa003-integration-profile.apk` contém o teste e não é o aplicativo normal a distribuir ao jogador.

O capturador `scripts/capture-android-qa-stages.mjs` registra o framebuffer real e snapshots do servidor a cada checkpoint quando há um dispositivo funcional. Nesta continuação o teste não alcançou novos checkpoints; os arquivos existentes não foram substituídos por imagens simuladas.

### Pendência externa para concluir

É necessário **um Android estável**: habilitar aceleração suportada no Windows com um administrador e concluir eventual reinicialização, ou conectar um telefone Android autorizado por USB. Depois, repetir o roteiro acima e exigir partida real, pausa global, substituição, retomada, resultados e reconexão nas duas interfaces. Nenhum dado de AVD foi apagado; não foram desativadas proteções de release nem alterado o motor de partidas para contornar o ambiente.

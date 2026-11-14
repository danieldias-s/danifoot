# Elifoot 98 Multiplayer — preservação, acesso e executor

13 de novembro de 2026 · Etapas 0 e 1

## Etapa 0

O checkpoint local `artifacts/stage1-checkpoint-4ddb1e2d4e25` contém o HEAD `2b79c366`, status detalhado, patches do índice e da árvore, 35 arquivos copiados byte a byte e manifesto SHA-256. Os demais caminhos preexistentes foram registrados no status e ficaram no local original. O checkpoint preserva as alterações de navegação Flutter e os checkpoints de rodada já em andamento. A auditoria trabalhou na mesma árvore e os testes usaram bancos descartáveis.

Foram lidos os três documentos de referência. A linha de base funcional passou em 10 casos de integração SQLite/SignalR. As duas variantes PostgreSQL da primeira seleção ficaram sem fixture e foram incluídas na verificação com PostgreSQL isolado. A linha de base de desempenho anterior registrou navegação em cache de 10 para 0 GETs, mercado de 11 para 2 GETs, carreira de 117 para 13 ms, repetição de avanço de 117 para 7 ms e primeira gravação de resultado de 265 para 315 ms de mediana. Os dados de 50/100 mundos e 18 MiB por conexão seguem como referência das etapas seguintes. [Performance](PERFORMANCE_MULTIPLAYER_2026-11-13.md) · [Auditoria](audit-scalability/RELATORIO.md) · [Reprodução](audit-scalability/REPRODUZIR.md)

Os fluxos selecionados foram visibilidade e ingresso, autorização de comandos, prontidão e replay, coordenação do processo e recuperação de conexão. Os cálculos esportivos, financeiros e de carreira ficaram fora das alterações.

## Etapa 1

| Problema | Correção |
| --- | --- |
| A lista geral entregava convites e participantes a qualquer conta | A lista retorna apenas mundos vinculados à conta ou criados por um de seus perfis. Leituras de mundo e lobby exigem vínculo, com regras adicionais para finanças e editor. O ingresso direto exige assento ou perfil criador. |
| O fluxo por código dependia da lista geral | A prévia via POST valida o código e retorna opções de ingresso sem participantes ou snapshot. O Flutter mantém a escolha de clube, desemprego e lobby nos dois temas. A autorização é revalidada no lock que grava o assento. |
| Criação de sala vazia podia deixar a primeira vaga sem dono definido | Mundos novos salvam o perfil criador no JSON. O lobby legado continua fornecendo o criador quando existe. Saves antigos com treinador mantêm o vínculo por perfil, inclusive por concessão administrativa. Um save legado sem assento nem criador exige o convite existente. |
| A configuração de produção podia iniciar sem autenticação funcional | Production exige modo Account, PostgreSQL provisionado, origens HTTPS explícitas e chaves de cookies protegidas por certificado. O modo usa cookies Secure, CSRF e o armazenamento de sessões existente. Modo de teste e LocalDev continuam disponíveis em ambientes de teste e desenvolvimento. |
| A exclusão de executor dependia dos locks em memória | O modo Account mantém uma única API autoritativa por banco PostgreSQL. Uma sessão dedicada sustenta o advisory lock. Cada gravação de mundo verifica a sessão sob um lock transacional compartilhado. A troca de dono aguarda escritas em voo. Perda da sessão para o relógio e encerra a API. SQLite em arquivo local pode optar por exclusão por arquivo. |
| Prontidão em memória podia colidir entre treinadores ou reconhecer replay antes de persistir | Recibos de tática, pronto e retirada são persistidos por mundo, ator e ação com hash do pedido. Duas pessoas podem usar a mesma chave textual. O replay após reinício não muda a versão. Checkpoints pendentes conservam a operação a gravar e a API informa falha de persistência. |
| Repetições tardias podiam avançar outra rodada quando os parâmetros eram omitidos | No modo conta, avançar exige temporada e rodada esperadas. Em Account, continuar exige ID da pausa e chave. Reconectar no mesmo assento de conta evita uma nova gravação quando a escolha de clube permanece igual. |
| Falha ao persistir ingresso do hub podia deixar a conexão no grupo | A remoção do registro local é acompanhada da remoção do grupo SignalR. |

Os locks são cooperativos e a conexão de autoridade requer acesso direto ou pool em modo sessão. O modelo admite substituição da API e mantém uma API ativa por banco. Distribuição simultânea por mundo entre réplicas, presença distribuída e backplane permanecem pendentes. [Advisory locks do PostgreSQL](https://www.postgresql.org/docs/18/functions-admin.html) · [Data Protection](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview)

### Critérios e medições

- O visitante autenticado vê lista vazia até ter acesso, recebe 403 para mundo alheio, ingressa por convite no mundo selecionado e continua sem acesso ao segundo mundo.
- Duas contas confirmam prontidão em paralelo. O teste verifica os dois assentos prontos, um único estado inicial de rodada e replay persistido após reiniciar. Uma reutilização da chave com outro conteúdo falha.
- SQLite e PostgreSQL, em processos separados, rejeitam o segundo servidor. Após encerrar o dono, a substituição recupera a mesma versão e o replay a preserva. No PostgreSQL, encerrar a sessão do lock também faz a API sair.
- O teste final usa uma amostra por provedor. Rejeição do segundo processo em 0,46 s no SQLite e 0,93 s no PostgreSQL. Substituição até health em 2,95 e 3,38 s, respectivamente. O PostgreSQL encerrou a API em 1,60 s após perda da sessão. O replay mediu 1,19 e 2,29 s a frio, incluindo hidratação e compilação da primeira requisição. Esses tempos incluem inicialização dos processos, em loopback e host compartilhado. [SQLite](../artifacts/stage1-authority-final-sqlite/result.json) · [PostgreSQL](../artifacts/stage1-authority-final-pg/result.json)

### Testes executados

- 32 testes de integração passaram no build final. Incluem modo Account, CSRF, cookies, autorizações, convite privado, vínculos legados, confirmação concorrente, falhas antes e depois do commit, reconexão SignalR, SQLite e PostgreSQL descartáveis. [TRX](../artifacts/stage1-commercial-final/results/stage1-final.trx)
- 70 testes de aplicação passaram no build da etapa. [TRX](../artifacts/stage1-app-tests/results/application.trx)
- 25 testes Flutter de conta, sessões e jogabilidade passaram na seleção inicial. Os dez testes de conta passaram depois da inclusão do teste que verifica o novo transporte de convites. Análise Dart sem diagnósticos nos arquivos alterados. [Seleção](../artifacts/stage1-release/flutter-final.log) · [Conta e convite](../artifacts/stage1-release/flutter-invite.log)
- A suíte Flutter completa, antes de adicionar o teste de transporte de convites, teve 571 aprovações e oito falhas. A cópia da linha de base reproduziu os mesmos oito casos: isolamento visual de atualizações na partida, cinco larguras no plantel móvel e duração da tela de preparação nos dois temas. A comparação dos nomes das falhas coincidiu. [Suíte completa](../artifacts/stage1-release/flutter-all.log) · [Linha de base](../artifacts/stage1-release/flutter-baseline-regressions-v2.log)

### Arquivos da etapa

- API: `AccountLogin.cs`, `Program.cs`, `GameRoomHub.cs`, `RoomClockBackgroundService.cs`.
- Aplicação e dados: `MultiplayerRoundOrchestrator.cs`, `Entities.cs`, `EfWorldRepository.cs`, `WorldAuthority.cs`, `WorldCreatorService.cs` e `WorldAssemblyService.cs`.
- Flutter: `account_shell.dart`, `api_client.dart`, `main.dart`.
- Testes e documentação: `AccountLoginTests.cs`, `account_entry_test.dart`, `docs/audit-scalability/stage1-process-test.mjs` e este relatório.

### Compatibilidade e pendências

- Clientes de conta atualizados enviam convites no corpo do POST. Integrações antigas que usavam listagem pública ou ingresso de visitante por GUID precisam atualizar o fluxo. O portal usa as mesmas rotas para os temas clássico e atual. O usuário pode voltar à sessão via lista privada.
- O modo Account pressupõe esquema já provisionado e HTTPS visto pela API. Configurar `Accounts:AllowedOrigins`, `Accounts:KeyDirectory` e `Accounts:KeyCertificate`. O teste comercial usou certificado e chave privada PEM por `Accounts:KeyCertificatePemPrivateKey`, após a importação PFX falhar no Windows restrito. Validar PFX se for escolhido, terminação TLS, rotação de certificado, limites do proxy e recuperação de sessão no ambiente de implantação. Os catálogos de prévia restritos a DEV continuam dependentes de validação própria antes do lançamento.
- Uma queda antes do commit pode exigir recomputar a partir do último checkpoint durável. A etapa preserva a exclusão de escritores e confirmações persistidas. Idempotência de criação/importação, recuperação automática após perda do processo e coordenação ativa entre várias réplicas permanecem fora dos critérios demonstrados.
- O novo teste comercial exige um PostgreSQL de teste provisionado. O procedimento de migração, backup e rollback de bases existentes pertence à Etapa 4. Nenhuma base real foi migrada ou implantada.
- O trabalho para nesta revisão. As medições de payload e alterações do protocolo SignalR da Etapa 2 ainda não foram iniciadas.

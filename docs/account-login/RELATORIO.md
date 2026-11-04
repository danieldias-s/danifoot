# Conta, início e lobby — Liga Clássica 98

Implementação local/de teste, sobre o checkpoint `733bf73`, em worktree isolado
`D:/elifoot_98/.worktrees/account-login-lobby`, branch `codex/account-login-lobby`.
O checkpoint inicial desta missão é `e374326`; o checkpoint de implementação
validada é `a7efbef`. As evidências ficam no commit seguinte da mesma branch.
Não houve merge ou publicação.

## Fluxo entregue

`Login / Criar conta de teste → Início → Criar campeonato ou Entrar no lobby`.

- O login pede usuário ou e-mail e senha. Há cadastro de teste, mostrar/ocultar
  senha, validação, mensagens acessíveis e envio pelo teclado. O menu de
  campeonatos só aparece depois da autenticação.
- Início tem identificação da conta, Sair, Criar campeonato, Entrar no lobby e
  Pagamento (sem anúncios) — EM BREVE. Pagamento é somente informativo.
- Meus campeonatos vem dos vínculos reconhecidos no servidor. Não há salas,
  clubes, preços ou estatísticas ilustrativas embutidos na listagem.
- Lobby mantém busca, código de sala, capacidade, participantes, escolha de
  carreira, sorteio e aparência. Oferece Todas as salas, Minhas salas e Com vagas.
  As salas próprias têm estrela, destaque e RETOMAR MINHA SESSÃO, mesmo lotadas.
- O Criador de Mundos original continua com suas oito etapas. Nenhuma de suas
  regras/configurações foi reconstruída nesta missão.
- As versões ATUAL e CLÁSSICA (ANTIGA) continuam individuais por treinador e
  usam o mesmo jogo. A seleção no lobby é expansível para preservar espaço.

## Backend e autorização

Backend existente: ASP.NET Core 10, Entity Framework Core, PostgreSQL para o
ambiente local e SQLite nos testes isolados. O `accountId` e o primeiro ID de
treinador são UUIDs gerados pelo servidor. O nome de login é normalizado e único;
o nome visível é separado.

Senhas usam `PasswordHasher<AccountRecord>` com salt, sem senha universal ou
credenciais de demonstração no código. As respostas não contêm senha, hash nem
segredo de sessão. As contas de QA foram criadas com senhas aleatórias durante
os testes; essas senhas não fazem parte da entrega.

A autenticação usa cookie ASP.NET Core HttpOnly, SameSite Strict, Secure quando
a conexão é HTTPS, expiração de 12 horas e revogação no servidor. O segredo
aleatório da sessão é armazenado somente como SHA-256 no banco; o cookie contém
o ticket protegido. As ações de escrita exigem antiforgery por cookie e header,
inclusive login/logout. As origens web aceitas são explícitas. A implementação
segue os mecanismos de [autenticação por cookie](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0)
e [antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
do framework.

Há limite por IP de 20 solicitações de autenticação em cinco minutos e bloqueio
temporário da conta após cinco falhas de senha. Credenciais inválidas e usuário
inexistente têm a mesma resposta de login. O cadastro informa usuário indisponível.

O backend valida a conta e a posse de qualquer ID de treinador recebido por HTTP
ou SignalR. `accountId`, nickname, deviceId ou o antigo bearer localdev não
autorizam ações no perfil AccountTest. Dados financeiros privados exigem vínculo
com o clube. O tick manual de testes não está disponível para contas; o relógio
existente do servidor permanece responsável pela simulação.

SignalR usa a mesma sessão e verifica a posse antes de entrar na sala. O logout
revoga somente a sessão correspondente e fecha seus transportes, preservando
outro dispositivo legitimamente conectado. Dois dispositivos da mesma conta
controlam o mesmo treinador, sob a serialização existente dos comandos; não
consomem outra vaga. A contagem de conectados continua por treinador, não por
socket. A integração usa o contexto de usuário previsto pelo
[SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0).

No navegador, a senha e a sessão secreta não são gravadas no localStorage.
Somente dicas de navegação por conta (sala, treinador, seleção visual) são salvas,
e o servidor as revalida no reload. No cliente nativo, cookies ficam em memória
e são compartilhados por HTTP e WebSocket; reabrir o processo pode exigir login.

## Estrutura e migração aditiva

`AccountSchema.Ensure`, em `src/Elifoot.Infrastructure/AccountRecords.cs`, cria
idempotentemente estas tabelas adicionais, tanto em PostgreSQL como em SQLite:

| Tabela | Finalidade |
|---|---|
| `ef_accounts` | UUID, login normalizado único, nome visível, hash, estado, data e bloqueio |
| `ef_account_coaches` | Associação explícita conta–treinador; treinador tem um proprietário |
| `ef_account_sessions` | Hash de segredo aleatório, conta, perfil ativo, expiração/revogação |
| `ef_account_link_grants` | Hash de código de vínculo, destinatário, treinador, prazo e consumo |
| `ef_account_link_audit` | Emissão, vínculo e conflitos, com motivo e data |

O repositório não utilizava migrations EF versionadas para essa área. Foi
mantida a estratégia aditiva de schema já empregada nas preferências visuais,
sem alterar o JSON de mundos nem remover as identidades legadas. A migração não
recalcula jogadores, salários, força, economia ou resultados.

Vagas antigas ficam pendentes: não há associação automática por nome, deviceId
ou ID adivinhável. Como o antigo acesso local não comprovava posse de forma
segura, esta fase utiliza o caminho de autorização explícita permitido no pedido.

Após comprovar a posse e obter autorização do proprietário, o operador local
pode executar `scripts/new-authorized-account-link.ps1` com usuário destinatário,
ID do treinador, justificativa e `-AuthorizedByOwner`. O script pede a senha por
entrada protegida e usa o segredo administrativo local, sem imprimi-lo. Ele
somente emite um código de 30 minutos; não transfere a vaga automaticamente.
O destinatário aplica o código em Menu da conta → Perfil e vínculos. O código é
de uso único e vinculado à conta destinatária. IDs de treinador, clube e sala,
deviceId legado e preferências existentes são preservados. Rejeições e conflitos
são auditados. Nenhuma vaga antiga real foi vinculada na prévia.

## Rotas novas

| Método e rota | Ação |
|---|---|
| GET `/api/auth/csrf` | Preparar o token antiforgery |
| POST `/api/auth/register` | Criar conta e sessão de teste |
| POST `/api/auth/login` | Validar senha e abrir sessão |
| GET `/api/auth/session` | Carregar conta e perfis próprios |
| POST `/api/auth/logout` | Revogar sessão e conexões |
| POST `/api/auth/profile` | Selecionar perfil pertencente à conta |
| POST `/api/accounts/legacy-link-grants` | Emissão administrativa autorizada |
| POST `/api/accounts/legacy-link` | Consumir código destinado à conta autenticada |

As rotas existentes do jogo foram mantidas e receberam validação de identidade
no perfil AccountTest. LocalDev continua disponível apenas como compatibilidade
explícita dos testes/ambientes anteriores; o novo worktree usa AccountTest por
padrão e não oferece o antigo formulário de dispositivo.

## Prévia e evidências

Prévia nova: <http://127.0.0.1:5242/>. API: localhost:5241. A prévia anterior em
5232 e os arquivos do diretório principal não foram substituídos. Foram feitos
backups e clones dos bancos para `account018_013a_worlds` e
`account018_013a_catalog` no PostgreSQL local 55436.

Para testar, abra a prévia e escolha Criar conta de teste. Use suas próprias
credenciais. A mesma conta pode ser usada em outro navegador que tenha acesso
ao servidor. A validação móvel desta entrega é web responsiva e teste de
transporte nativo; não houve ensaio em um telefone físico nem build de loja.

Após desligamento, execute `scripts/INICIAR-CONTAS.cmd`. O iniciador reutiliza
serviços ativos, aguarda a recuperação do PostgreSQL e verifica a API e o HTML.
Ele exige os bancos preparados e o build existente; não substitui saves por um
banco vazio. Segredos e dumps ficam somente em artifacts, fora do Git.

As seis imagens do pacote foram inspecionadas antes da implementação.
`COMPARATIVO.html` coloca cada referência ao lado da captura real. O visual
usa a paleta solicitada, ícones vetoriais e fontes incluídas no aplicativo;
os dados capturados pertencem ao campeonato fictício criado por QA.

`evidence.json` registra o fluxo real do criador, sorteio, conta/treinador iguais
em dois navegadores, uma única vaga, clube mantido no reload, cookies protegidos,
bloqueios de falsificação e larguras. Não contém credenciais.

## Limites deliberados

Somente ambiente local/de teste. Não há JWT, login social, recuperação pública
de senha, checkout, preço ou remoção de anúncios. AccountTest recusa inicializar
em Production. Contas e salas antigas sem comprovação/autorização continuam
pendentes de vínculo; não foram apropriadas pelo novo login.

MatchEngine, domínio, regras de multiplayer, evolução, contratos e economia não
receberam alterações. As modificações dos testes históricos apenas explicitam
o perfil LocalDev usado por suas fixtures anteriores.

## Testes executados e resultado

| Verificação | Resultado observado | Log |
|---|---|---|
| Flutter completo | 341 aprovados | `flutter-regression-final.log` |
| Conta/transporte após refinamentos finais | 10 aprovados | `flutter-account-final.log` |
| Flutter analyze final | No issues found | `flutter-analyze-final.log` |
| Web release | Compilado com êxito | `web-build-final.log` |
| .NET domínio | 466 aprovados | `domain-regression.log` |
| .NET aplicação | 17 aprovados | `application-regression.log` |
| .NET integração completa | 266 aprovados, 2 falhas de ambiente | `integration-final.log` |
| Reexecução dos 2 casos com ambiente corrigido | 2 aprovados | `integration-fixtures-final.log` |
| Casos novos de conta/HTTP/SignalR | 6 aprovados, incluídos na integração | `account-tests-final.log` |

A execução completa de integração não foi rotulada como integralmente aprovada:
o teste antigo de persistência PostgreSQL excedeu o timeout de leitura de 30 s
sob carga, e o teste antigo de rankings não encontrou `real-season-world.json`
na pasta de saída específica. O arquivo foi disponibilizado nessa pasta e o
tempo de comando do ensaio isolado foi ampliado para 120 s; ambos passaram na
reexecução. Não há teste omitido sob um rótulo PASS genérico. A primeira rodada
de regressão também exigiu preparar bancos PostgreSQL e snapshots históricos
014B; os logs anteriores ficam em artifacts para rastreabilidade.

No navegador, foram exercitados cadastro, logout, senha incorreta, restauração
da sessão, Criador de Mundos original até a confirmação, escolha da versão
clássica, sorteio de clube da quarta divisão, início do campeonato de QA e
retomada em um segundo contexto sem compartilhar localStorage. Os dois
contextos receberam o mesmo accountId, treinador e clube, com uma vaga ocupada.
Uma conta de QA diferente recebeu 403 ao falsificar o treinador ou accountId;
escrita sem CSRF recebeu 400. Após logout, a listagem privada recebeu 401 e a
outra sessão legítima continuou autenticada.

As três telas de entrada foram medidas em 360, 390, 430, 768, 1366 e 1920 px
(18 verificações no navegador), sem largura de documento excedente. O teclado
percorreu usuário, senha, mostrar senha e ações. O teste de widget também
simulou a área ocupada pelo teclado no celular. Capturas de referência e do
produto foram comparadas visualmente; pequenas diferenças de ícones/fontes
mantêm o sistema visual local e evitam dependências de fontes externas.

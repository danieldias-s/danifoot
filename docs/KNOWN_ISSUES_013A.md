# Limites e integração — 013A

1. Todos os clubes, atletas e rankings carregados nesta missão são fixtures identificadas na UI e nos snapshots. A carga real permanece dependente de credenciais, cobertura e licença. O adapter HTTPS espera JSON normalizado; não há conversor nativo homologado de um fornecedor pago.
2. Os testes Flutter cobrem desktop e celular por viewport, inclusive confirmação. Não houve novo ensaio em telefone físico ou APK/emulador Android, nem execução nativa Windows. O build entregue é Flutter Web local.
3. O agendamento foi preparado como runner explícito e testado em execução controlada. Nenhum serviço permanente foi instalado. A ativação periódica operacional depende da frequência/quota contratada.
4. O catálogo é um banco novo separado, criado por EnsureCreated. Evolução futura do schema requer migrations próprias. Snapshots/drafts/jobs são retidos; uma política de expurgo deve preservar referências usadas nos mundos.
5. Identidades seguem o modo LocalDev existente. As rotas do criador recusam outros modos até integração de autenticação de produção. Nenhum servidor foi publicado.
6. O fallback de ranking ausente é explícito e estável, com clubes sem rank ao final. Rankings de fornecedores/competições diferentes só devem ser combinados se suas posições forem comparáveis; essa homologação depende do dataset real.
7. Ensaios longos de 100 temporadas e benchmarks não foram repetidos: MatchEngine, regulamentos, pesos/tetos de força e economia não foram alterados. A regressão curta da 012C e a suíte de domínio/aplicação/integração foram executadas.

## Integração futura com a 012D

Partir do estado aprovado da 012D. Reconciliar mudanças por código, sem substituir arquivos inteiros. A 013A alterou pontos compartilhados mínimos em `Program.cs`, `Entities.cs` (GameWorld partial), `Dtos.cs` (categoria/origem opcionais no pacote), `WorldEditorAndPackageService.cs` (proveniência do backup), `main.dart` (entrada para o novo fluxo), `api_client.dart` (transporte do criador) e um teste de integração para aceitar o banco isolado.

`WorldCreatorEngine` monta jogadores usando as funções econômicas da base 012C. Na integração, conferir os campos obrigatórios de contrato/salário da 012D e reutilizar os construtores/fábricas aprovados naquela missão. Não copiar esta fábrica sobre o código da economia. Nenhum arquivo da 012D foi editado por esta missão. As alterações observadas no projeto principal pertencem à outra sessão e foram preservadas.

Nenhum merge automático, reset, clean, checkout ou restore foi executado no projeto principal. Artefatos gerados pelos testes da 013A em relatórios antigos foram limpos somente no worktree 013A antes do checkpoint.

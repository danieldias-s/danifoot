# Integração futura — 012D/013B

Trabalho exclusivo em D:\elifoot_98_013A, branch feature/real-football-data-013b, base 40e71b4. A branch feature/world-creator-013a permanece nesse commit. Nenhum arquivo em D:\elifoot_98, sua branch, banco ou container foi modificado por esta missão.

Preservados: MatchEngine, probabilidades, evolução/tetos de força, lesões, suspensões, disciplina, regulamentos, contratos/salários existentes e economia. A 013B não adiciona sistema financeiro. WorldCreatorEngine continua chamando CalculateFairSalaryPerRound/CalculateFairValue somente na fundação do mundo, como na 013A. Não há recálculo de salários contratados durante evolução ou atualização do catálogo.

Após aprovação da 012D, reconciliar por código o ponto de construção de Player herdado da 013A com suas interfaces/campos contratuais. Não copiar WorldSeedFactory, Entities ou arquivos econômicos desta base sobre a 012D. As mudanças compartilhadas novas são contratos do catálogo, Program/WorldCreatorEndpoints, solução e transporte/visualização do criador; o restante são arquivos de catálogo/calibração/importação e testes.

O nome Catalog013A e as rotas /api/world-creator e /api/catalog-013a foram mantidos para compatibilidade. Catalog013B:Enabled registra os adapters e exige bancos com 013b no nome, separados dos mundos. O novo runner usa porta 55434, API 5134. A 013A original continua disponível em sua própria branch e seus bancos não receberam alterações de schema.

Sem merge automático, publicação ou inicialização de serviço permanente. A autenticação do criador permanece LocalDev; autenticação de produção e homologação das licenças são pendências de lançamento.

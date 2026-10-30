# Snapshot independente do mundo — 013A

`WorldCreationDraft` salva no catálogo o pedido, a prévia completa e o proprietário, com validade de duas horas. A confirmação usa esses dados persistidos, nunca remonta contra uma API. Por atualização condicional, o draft recebe um único WorldId e o JSON inicial; repetir a confirmação retorna o mesmo mundo. Se houver falha entre os dois bancos, o JSON persistido permite repetir a gravação sem gerar outro WorldId.

O snapshot de origem contém categoria, temporada, países, seed, data, hash, clubes/divisões/distritais, todos os atletas/posições/forças iniciais, rankings e referências independentes de clubes/elencos/rankings. Os IDs do catálogo e dos fornecedores ficam como proveniência. A cópia é serializada e desserializada para não compartilhar listas com o objeto da prévia.

Após criar, o mundo utiliza `ElifootDbContext` e os motores existentes. Nenhuma sincronização do catálogo acessa clubes, jogadores, transferências, salários, contratos, lesões, suspensões, força ou históricos dos mundos. Um teste atualiza o catálogo depois da confirmação e verifica que o JSON do mundo permanece igual.

A origem e a categoria sobrevivem ao save, restauração PostgreSQL, reinício real da API e exportação/importação de pacotes. Saves antigos continuam legíveis sem origem. Os dois mundos do smoke HTTP foram restaurados sem API externa; os hashes permanecem iguais em `docs/audit-013a/api-restart.json`.

A última configuração usa uma linha por OwnerId, é persistida após confirmação e não aparece para outro proprietário. O Flutter oferece USAR ÚLTIMA CONFIGURAÇÃO e CRIAR NOVA CONFIGURAÇÃO. A autorização utiliza o contrato LocalDev existente; não foi implementado um provedor de identidade de produção nesta missão.

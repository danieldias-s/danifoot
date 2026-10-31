# Persistência e isolamento

Nível público: ClubColorCache no CatalogDbContext, chave ClubId canônica. PaletteJson contém principal/secundária, fonte, evidência, data, confiança, versão, escopo e precisão. LastAttempt, NextAttempt e LastIssue registram carga e lacunas. O alias do catálogo é resolvido antes de aceitar uma importação administrativa. Não há casamento por nome.

O refresh preserva a última paleta válida em falha ou resposta vazia. Cache válido aguarda 30 dias, ausência 7 dias e falha 6 horas, salvo atualização forçada explícita. Fontes online usam ProviderHttp, orçamento, intervalo, cache HTTP e cooldown do serviço. Refresh/import no mesmo serviço são serializados; gravações privadas usam concorrência otimista no banco. O mecanismo local não é uma lease distribuída para múltiplas réplicas de importação: não houve implantação dessa topologia.

A seleção lê somente o banco. A criação copia a paleta para Club no snapshot JSON e a exportação de pacote conserva o metadado. Não é feita atualização implícita de todos os mundos ao mudar catálogo ou montagem. A montagem conserva fonte confiável apenas quando os campos e a identidade não foram alterados; edição explícita recebe IsPersonalized e ColorVerified=false. Mundo sem paleta é compatível com o novo contrato opcional.

Nível privado: tabela PlayerAppearance, chave OwnerId/WorldId/ClubId, PreferencesJson, Version concorrente e UpdatedAtUtc. GET/POST `/api/world-creator/appearance/{worldId}/{clubId}` usam a identidade LocalDev existente e verificam participação. Gravar exige controle atual do clube; ler recupera somente preferências daquele usuário, inclusive do clube anterior. Versão repetida/concorrente retorna 409; não participa do estado esportivo da sala nem é difundida por SignalR.

EnsureSchema acrescenta somente as duas tabelas em PostgreSQL/SQLite, sem destruir estruturas anteriores. Preferências não chamam SaveWorld. Testes comparam serialização/fingerprint completo, incluindo atletas, salários, contratos, dívida, caixa e histórico, antes/depois das preferências.

Evidências: `appearance-http.json`, `color-presets-http.json`, `appearance-restart.json`, `color-presets-restart.json`, `colors-backend-final.log` e TRX. Reinício do controlador tem teste próprio; reinício nativo do navegador continua pendente. A persistência comprovada por processo/BD e a restauração pelo controlador não são apresentadas como auditoria de Chrome.

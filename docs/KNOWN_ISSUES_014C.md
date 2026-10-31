# Pendências 014C

STATUS: PARTIAL.

1. Cobertura de cores: 17 de 1398 registros. Os demais 1381 ficam sem paleta oficial e usam neutro. A pesquisa Wikidata encontrou maxlag/429; novas cargas devem respeitar cooldown e revisar identidade/proveniência. Não existe promessa de cobertura completa por país/categoria.
2. Representações RGB tradicionais não são manuais de marca certificados. O vermelho #C3281E do Flamengo é o exemplo representativo solicitado, com vermelho/preto confirmado pela fonte do clube. Cores adicionais/ambíguas e fonte são descritas nos registros.
3. Auditoria nativa de navegador, reinício do navegador e carreira com mudança real de clube ainda pendentes. Os 18 PNGs são renderer Flutter de HTTP real, identificados como tal. Não há imagem de referência disponível para comparação objetiva.
4. Contrato de autenticação é o LocalDev já existente. Produção continua fora do escopo, sem publicação. Refresh/import usa serialização local; múltiplas réplicas de importação exigiriam lease distribuída adicional.
5. O comando de regressão excluiu os testes longos de simulação, conforme a instrução de não repeti-los sem mudança de mecânica. O artefato real de ranking da 014B é pré-requisito herdado: a primeira execução não o encontrou; após cópia somente de leitura e verificação de hash, o teste específico passou. Ambos os resultados estão preservados. Runners agora preparam a cópia quando a origem existe.
6. Para um checkout novo sem os dados locais ignorados, é preciso importar o coverage-bundle 013B preservado ou uma fonte autorizada antes de criar mundos reais. As 17 paletas estão versionadas no JSON; o banco e as credenciais não estão no Git.

Nenhuma mudança em MatchEngine, economia ou regras de competições. Nenhum merge, push ou deploy.

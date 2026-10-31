# Elencos reais — 013B

O JSON da 013A foi estendido com origem, desempenho opcional, posições originais, datas de vínculo, estimativa de força e relatório de cobertura. Campos legados continuam aceitos para fixtures. Dados reais exigem IdentityOrigin e método explícito da força estimada. Nome não disponível nunca é substituído por um nome humano inventado.

API-Football utiliza jogadores/estatísticas por clube, liga e temporada, validando todas as páginas. Sportmonks utiliza a rota histórica de elenco por equipe/temporada. football-data.org verifica a temporada atual antes de ler Team.squad. Wikidata usa vínculos P54 com início e fim compatíveis com AsOf e posições P413. Wikidata pode conter vínculos desatualizados/incompletos; isso permanece visível.

Posições: goalkeeper→G; centre/full/wing back e defender→D; defensive/central/attacking midfielder→M; winger, forward e striker→A. Posições desconhecidas/ambíguas ficam em Issues e não viram outra posição silenciosamente. País e categoria são os da equipe revisada, não a nacionalidade ou sexo inferido pelo nome do atleta.

O catálogo pode preservar até 100 atletas utilizáveis por clube; o mundo seleciona até 24, reservando vagas por posição antes de preencher pelos demais candidatos. Exige pelo menos 11 atletas, um goleiro, dez de linha e defensor utilizável. Elencos insuficientes permanecem identificados como parciais. O checkbox de atletas fictícios é separado do complemento de clubes e fica desmarcado por padrão; os gerados têm nome e IdentityOrigin=Generated explícitos.

Transferências respeitam MembershipStart/MembershipEnd na data de referência. IDs canônicos duplicados em clubes diferentes impedem a criação. Uma atualização parcial não substitui um elenco completo válido. API-Football com vínculos estatísticos duplicados sem datas inequívocas é rejeitada, preservando cache, até conciliação autorizada.

# Importação e validação do mundo — 013B

Fluxo preservado: seleção → atualização autorizada/cache → validação → prévia persistida → confirmação idempotente. O mundo copia clubes, divisões, distritais, atletas, posições, forças finais, ranking/método, direitos, fontes, datas, versões e seed. As referências do snapshot se limitam às fontes efetivamente usadas pelos clubes escolhidos.

Direitos são rechecados na confirmação. O mundo não consulta a API para continuar jogando. Atualizações/mapeamentos posteriores não alteram seus atletas, força, contratos, salários, transferências, lesões, suspensões ou resultados. Exportação de pacotes é bloqueada quando os direitos não autorizam esse uso.

A seleção inicial reduz elencos acima de 24 por distribuição de posições. Elenco curto/sem goleiro e vagas de clubes ficam explícitos. Os dois consentimentos de complemento ficam falsos por padrão. O piloto real produziu zero atletas gerados; o masculino resultou em 24 selecionados de 35, o feminino manteve seis e recusou confirmação. Faltam 127 clubes em cada categoria; não foi criado um mundo real completo.

Automático, personalizado, distritais por ranking/manual/seed e última configuração continuam compatíveis com a 013A e foram testados com fixtures. Duplicações canônicas de atletas, identidades de clubes conflitantes e incompatibilidade de categoria/temporada impedem confirmação. A origem real e a força estimada são mostradas separadamente no Flutter.

Exemplos de configuração e execução estão em tools/catalog-013b/README.md. A extensão de schema foi criada somente nos bancos novos da 013B. Não há migração automática dos bancos antigos 013A; qualquer importação futura de seus dados exige backup e plano próprio.

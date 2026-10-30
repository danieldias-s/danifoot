# Identidade esportiva — 013B

A 013A agrupava clubes pelo nome. A 013B remove essa fusão: cada identidade interna possui aliases qualificados por país, categoria, fornecedor e ID externo. Nome/escudo não são chaves de identidade. Masculino e feminino permanecem separados mesmo com o mesmo nome/ID externo.

O mesmo ID do mesmo fornecedor em várias ligas resolve a mesma equipe. IDs de fornecedores diferentes criam identidades distintas. Nomes normalizados semelhantes geram CatalogIdentityConflict, sem fusão automática. Se ambas as equipes conflitantes forem usadas na montagem, a confirmação exige revisão. Não há comparação de escudos ou download de imagens.

Rotas administrativas existentes, protegidas por token local/loopback: GET `/api/catalog-013a/identities?country=BR&gender=Male`, GET `/identity-conflicts`, POST `/map-club`. A revisão informa InternalClubId, país, categoria, fornecedor, ExternalId e Evidence. Ela pode associar o ID de um fornecedor de ranking sem importar outro catálogo de clubes. Vínculos entre países/categorias incompatíveis são recusados.

Escolhas antigas do usuário continuam reconhecidas por seus aliases de origem. A chave canônica conserva o primeiro vínculo explícito; snapshots de mundos já salvos não são reescritos após uma revisão.

Jogadores possuem CanonicalPlayerId; adapters nativos usam IDs qualificados, e Wikidata utiliza o QID. Importadores autorizados podem informar um ID canônico revisado para conciliar fornecedores. Não se fundem atletas por nome. Sem vínculo confiável entre IDs de jogadores de fornecedores diferentes, a integração precisa de revisão adicional antes de uma carga ampla; esse limite permanece documentado.

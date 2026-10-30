# Exemplos de importação

O manifesto JSON é um exemplo fictício com autorização desativada. É rejeitado até que os direitos do dataset sejam configurados pelo operador. Não substitua nomes/IDs por dados de terceiros sem licença. Para CSV, envie `{ "manifest": <manifesto>, "csv": "..." }` à rota `/api/catalog-013a/import-csv`.

A rota exige loopback e o token `Catalog013A:ImportToken` em `X-Catalog-Import-Token`. Importe primeiro os clubes; depois, Squads e Rankings devem referenciar o mesmo ClubProvider/ClubExternalId e país/categoria/temporada/liga. As funções geradoras das fixtures completas estão em `src/Elifoot.Infrastructure/Catalog/CatalogFixtureFactory.cs`.

CSV de elenco: PlayerExternalId,ClubExternalId,ClubProvider,Gender,Season,PlayerName,Position,Strength.
CSV de ranking: ClubExternalId,ClubProvider,Gender,Season,Rank.
CSV de países: CountryCode,CountryName.
CSV de ligas: LeagueExternalId,LeagueName,Gender,Season.

Source/UpdatedAt vêm do manifesto. Posições aceitam G/D/M/A e aliases documentados em CatalogImportCodec; força já deve estar convertida para 1–50. Um país não pode ter elencos femininos completados por masculinos. Reduções de contagem exigem aprovação explícita do operador em AllowCountDecrease.

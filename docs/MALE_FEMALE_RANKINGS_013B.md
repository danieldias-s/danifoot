# Rankings por categoria — 013B

A pesquisa consultou a [metodologia oficial da Opta](https://theanalyst.com/articles/power-rankings-your-club-ranked), que descreve universos masculino/feminino independentes. Não foi encontrado acesso público programático cuja licença cobrisse este projeto. Não houve extração do site, importação de ranking Opta ou uso de sua fórmula.

CatalogRank persiste Source, UpdatedAt, Score, Rank, Method, Confidence, ComparableGroup e Origin. Importação real/derivada exige esses metadados e direitos compatíveis. Rankings podem usar aliases revisados de outro fornecedor. Datas antigas são preservadas e sinalizadas, sem apresentação como dados atuais.

As tabelas domésticas de API-Football/football-data são classificações locais, identificadas como DomesticStanding-v1; não são Power Rankings. Quando os grupos selecionados não são comparáveis, essas posições deixam de determinar uma suposta classificação mundial. Clubes sem rank comparável ficam em ordem provisória estável por identificador, com método ProvisionalIdentifierOrder-v1 e confiança zero. O piloto CC0 usa essa política; não tem ranking real de força.

`DerivedClubRanking` oferece um Elo local opcional sobre resultados históricos autorizados: base 1500, K=20, expectativa logística com divisor 400 e confiança games/(games+20). É identificado como derived-elo-v1, Origin=Estimated e Source=elifoot-derived-elo. Não inclui a hierarquia/xG da Opta e não se apresenta como oficial. Sua implementação e reprodução foram testadas; nenhum ranking derivado real foi carregado no piloto.

O automático mantém 1–8/DIV1, 9–16/DIV2, 17–24/DIV3, 25–32/DIV4 e 96 restantes. O personalizado mantém a colocação soberana do usuário. Promoção, rebaixamento e resultados posteriores usam os motores existentes.

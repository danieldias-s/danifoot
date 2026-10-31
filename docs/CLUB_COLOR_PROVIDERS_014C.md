# Fontes e pesquisa de cores

A prioridade é a paleta já entregue pelo catálogo, Wikidata quando habilitado, outras implementações de IClubColorSource autorizadas, arquivo JSON/CSV revisado e configuração manual. Não há pesquisa por semelhança de nome nem uso de escudos/imagens. A aplicação preserva a identidade país/categoria/provedor/ID externo.

P6364 (cor oficial) e P462 (cor) apontam para itens, não para hexadecimais. O provedor resolve os itens e lê P465, que contém seis dígitos sem #. Valida o QID revisado no catálogo e P17 do país; categorias continuam independentes. Usa P6364 e só recorre a P462 quando não há aquela propriedade. Uma única cor permanece única; cores adicionais ficam descritas na evidência, sem inventar uma segunda. RGB de um item genérico não equivale a especificação exata de marca.

Fontes primárias consultadas:

- https://www.wikidata.org/wiki/Property:P6364
- https://www.wikidata.org/wiki/Property:P462
- https://www.wikidata.org/wiki/Property:P465
- https://www.wikidata.org/wiki/Q17479
- https://museuflamengo.com.br/simbolos/escudos
- https://www.w3.org/TR/css-color-4/#named-colors

O museu do próprio Flamengo confirma vermelho/preto nos símbolos. #C3281E é uma representação escolhida a partir do exemplo do proprietário, sem autenticação de manual de marca. O registro inclui essa limitação. Flamengo feminino Q27044292 não recebeu compartilhamento automático: não se confirmou associação adequada à regra de identidade.

A consulta em lote dos QIDs revisados encontrou maxlag e HTTP 429. O script de pesquisa interrompe a carga ao falhar, preserva o resultado e não contorna o limite. A recuperação offline usou entidades Wikidata brutas já preservadas pela 013A/013B; 16 registros com propriedades de cor e um registro Flamengo revisado resultaram nas 17 paletas. Lacunas não foram completadas por estimativa ou nomes.

`tools/catalog-013b/data/club-colors-014c.json` contém o conjunto revisável, com evidência, precisão e identidade de cada linha. `artifacts/014c/color-research/` guarda cache bruto e origem local, fora do Git. `audit-014c/color-coverage-database.json` e `coverage-summary.json` registram cobertura e ausência.

Importação JSON: comando do importer `014c-colors FILE OUTPUT`, limitado ao PostgreSQL próprio 55438/catalog_real. Importação administrativa unitária também existe em `/api/catalog-013a/club-colors/import` e conserva as exigências anteriores de loopback/token. Ela exige alias de identidade já associado.

CSV autorizado pode ser convertido com `python scripts/import-club-colors-014c.py entrada.csv saida.json`, depois importado pelo mesmo caminho. Colunas: countryCode, gender, provider, externalId, primaryColor, secondaryColor, preferredTextColor, colorSource, evidence, version, lastColorUpdate (ISO com fuso), colorConfidence, colorVerified; precision opcional. Hex curto, alfa, identidade repetida e fonte ausente são rejeitados. A conversão não atesta direitos de uma fonte: a revisão da importação continua necessária.

Pesquisa online é explícita em Catalog014C:OnlineColorResearch, desligada no runner local para manter a auditoria reproduzível. Falhas de uma fonte permitem tentar a importação autorizada seguinte. Renderizar ou navegar não pesquisa na internet.

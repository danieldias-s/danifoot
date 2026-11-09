# Arquivo JSON de clube

Use o [modelo de cadastro](../catalogo/modelos/clube.json) e o [JSON Schema](../catalogo/modelos/club.schema.json). O importador lê arquivos UTF-8 em `catalogo/paises/PAÍS/ANO/times/`. Os modelos ficam fora da pasta indexada.

| Campo | Regra |
| --- | --- |
| `ClubId` | Identificador estável de até 80 caracteres ASCII. Começa com letra ou dígito e aceita hífen e sublinhado. Reutilize o ID para cadastrar a versão do mesmo clube em outro ano. |
| `ClubName` | Nome de exibição do clube sem sufixo de ano. |
| `Country` | Nome do país exibido no jogo. Deve corresponder à pasta após remover acentos e trocar separadores por sublinhados. |
| `CountryCode` | Código de duas letras maiúsculas. Use sempre o mesmo par de código e nome para o país. |
| `CatalogYear` | Inteiro positivo igual ao nome da pasta do ano. |
| `Category` | `Masculino` ou `Feminino`. |
| `InitialRanking` | Posição inicial inteira e positiva. O menor número vem primeiro. |
| `RankingScore` | Número finito. Entre posições iguais, a maior pontuação vem primeiro. |
| `PrimaryColor`, `SecondaryColor` | Cores hexadecimais no formato `#RRGGBB`. |
| `Players` | Lista de jogadores da versão cadastrada. Use `[]` enquanto o cadastro do elenco estiver pendente. |
| `ShortName`, `League` | Nome curto e competição de origem opcionais. |

Cada entrada em `Players` exige `PlayerId`, `Name` e `Position`. Os códigos de posição são `G` para goleiro, `D` para defesa, `M` para meio-campo e `A` para ataque. `Number` aceita um inteiro de 0 a 999. `Source` pode trazer a referência usada para cadastrar o jogador. Os dois campos são opcionais.

Cadastre apenas jogadores documentados para aquela versão do clube. O JSON não recebe força individual. O jogo calcula a força inicial como uma estimativa para a simulação.

O mesmo ID pode aparecer em anos ou categorias diferentes. O importador identifica o clube por código do país, ano, ID e categoria. Cada jogador recebe uma identidade vinculada à versão exata do clube. IDs que diferem somente em maiúsculas e minúsculas são equivalentes.

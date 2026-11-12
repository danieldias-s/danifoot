# Catálogo histórico offline — schemas 1.0 e 1.1

## Estado da versão 1.1 (12/11/2026)

A estrutura 1.1 e a ferramenta disciplinar estão implementadas. **Status geral PARTIAL:** a criação de mundos 1.1 está bloqueada até aprovação de uma regra que converta `teamStrength` em forças individuais. O caminho 1.0 permanece disponível com suas forças literais. A descrição original 1.0 abaixo continua válida para esse formato; as extensões 1.1 são descritas ao final. Resultados atuais em `ELIFOOT98_CATALOGO_V11_RESULTADO.md`.

Projeto `D:\elifoot_98`, branch `main`. Implementação de preparação do catálogo; nenhum clube real ou elenco histórico foi cadastrado. Não usa APIs externas e não implementa aprendizado tático ou V4.

## Organização dos arquivos

```text
catalogos/
  plano.json                         # 8 países planejados; 5 ainda não definidos
  _schemas/
    manifest-1.0.schema.json
    clube-1.0.schema.json
  brasil/
    1998/manifest.json
    1998/clubes/.gitkeep
    2026/manifest.json
    2026/clubes/.gitkeep
  portugal/1998/manifest.json
  portugal/1998/clubes/.gitkeep
  espanha/1998/manifest.json
  espanha/1998/clubes/.gitkeep
  modelos/
    manifest.template.json
    clube.template.json
    exemplo/brasil/1998/
      manifest.json
      clubes/clube-sintetico.json
```

O loader ignora `modelos` e pastas iniciadas com `_`. O exemplo completo tem apenas dados **sintéticos**, fora dos catálogos disponíveis para criação. Os quatro manifests ativos estão `DRAFT`, sem clubes. Brasil 1998/2026, Portugal 1998 e Espanha 1998 estão preparados para preenchimento, **não prontos para jogar**. `plano.json` registra três países e cinco posições nulas; não inventa os países restantes.

A fonte QA anterior continua em `catalogo/paises` ou no caminho `CatalogLocal:Root` já configurado. A nova fonte é `catalogos`, configurável por `CatalogOffline:Root`. Não há reindexação, migração, fusão com o SQLite QA ou substituição de arquivos anteriores.

## Manifest

Exemplo didático, cujo clube deve existir antes de marcar `READY`:

```json
{
  "$schema": "../../_schemas/manifest-1.0.schema.json",
  "schemaVersion": "1.0",
  "catalogId": "br-1998-principal",
  "country": {"code": "BR", "name": "Brasil", "slug": "brasil"},
  "year": 1998,
  "name": "Brasil 1998 — preencher com fontes do proprietário",
  "status": "DRAFT",
  "gender": "Male",
  "clubs": ["clubes/clube-exemplo.json"]
}
```

País: ISO 3166-1 alpha-2 maiúsculo, usando o vocabulário local de 249 códigos. Nacionalidades usam o mesmo vocabulário. Não existe dependência de Portugal/Espanha nem consulta online. O nome do país é uma legenda fornecida pelo proprietário; o validador não comprova historicamente essa legenda. O slug deve coincidir com a pasta, conter letras minúsculas/números/hífens e começar por letra. Um país usa uma pasta estável. Ano: inteiro 1..9999, igual ao nome canônico da pasta, sem zeros à esquerda. Cada pasta de ano contém um manifest.

`catalogId` identifica a edição; `catalogClubId` identifica o clube e pode repetir em anos diferentes. IDs aceitam 1..80 caracteres ASCII, começando por letra/número, seguidos por letras, números, `_` ou `-`. Devem ser atribuídos uma vez e mantidos quando o nome visível muda. Não são derivados automaticamente de nomes, posição no ranking, ordem de arquivos ou IDs de saves.

`gender` é opcional, padrão `Male`; também aceita `Female`. Não misture categorias no mesmo mundo. `DRAFT` permite preparação incompleta, exibindo avisos de completude. `READY` exige todos os campos e referências válidos. Um catálogo `READY` pode ter poucos clubes e ainda ser um arquivo válido; a criação exige a seleção completa de 128 clubes, possivelmente de catálogos/anos diferentes.

## Clube, jogador e treinador

O arquivo completo editável está em `catalogos/modelos/exemplo/brasil/1998/clubes/clube-sintetico.json`: inclui os onze atletas, estádio e treinador. Trecho ilustrativo **sintético**, com o restante do elenco omitido apenas neste documento:

```json
{
  "schemaVersion": "1.0",
  "catalogClubId": "demo-clube-001",
  "country": "BR",
  "year": 1998,
  "name": "Clube Exemplo Sintético",
  "shortName": "Exemplo Sintético",
  "abbreviation": "EXS",
  "colors": ["#184687", "#FFFFFF"],
  "stadium": {"name": "Estádio Exemplo Sintético", "capacity": 10000},
  "coach": {
    "id": "demo-tecnico-001",
    "name": "Treinador Exemplo Sintético",
    "nationality": "BR",
    "preferredTactics": {
      "home": ["4-4-2", "4-3-3"],
      "away": ["5-3-2", "4-5-1"]
    }
  },
  "players": [{
    "id": "demo-atleta-001",
    "name": "Atleta Exemplo Sintético 01",
    "position": "G",
    "strength": 25,
    "nationality": "BR",
    "behavior": 0,
    "isStar": false
  }],
  "status": "DRAFT"
}
```

`shortName` e `abbreviation` são independentes. Nome: até 160 caracteres; nome curto: 80; sigla: 12. Duas cores `#RRGGBB`; estádio com nome e capacidade de 1..1000000. `initialDivision` é opcional, de 1 a 5 (5 = distrital). Quando ausente, a distribuição automática usa as vagas do formato atual. A escolha explícita de divisão na tela substitui a divisão inicial; não muda a força fornecida.

`crestFile`, opcional, aponta para PNG/JPEG **relativo à pasta do ano**, por exemplo `escudos/demo.png`. Não é URL. O loader valida caminho, extensão e assinatura do arquivo; limite 2 MB. A cópia importada armazena uma data URL no mundo, sem depender do arquivo depois. Não há download de imagens. O renderer clássico antigo ainda usa o escudo por cores: o novo escudo e a sigla independente ficam preservados nos metadados, sem expansão das telas antigas nesta preparação.

Posições reais do motor: `G`, `D`, `M`, `A`. Força obrigatória de 1 a 50, literal, sem sorteio/recalibração 017B. `behavior` é inteiro: 0 FairPlay, 1 Cordeirinho, 2 Cavalheiro, 3 Caneleiro, 4 Caceteiro, 5 Sarrafeiro. `isStar` é booleano explícito; ausência não equivale a false. Esses campos são copiados sem converter nome em atributo nem preencher lacunas.

O novo catálogo aceita **11..23 jogadores**, pelo menos um goleiro e dez de linha. O servidor continua com máximo **24**: a restrição 23 só vale para importar este formato. Transferências posteriores continuam sujeitas ao limite 24 do servidor. Elenco incompleto fica em rascunho; nunca recebe atletas gerados automaticamente. Homônimos são permitidos; o ID, não o nome, distingue jogadores.

O ID de jogador é estável e único entre os clubes de um mesmo ano, inclusive países selecionáveis. Reutilizar a identidade em outro ano é permitido: versões de anos diferentes tornam-se entidades distintas no mundo. Duplicatas de ID de clube são avaliadas por país/ano; IDs de treinador por ano. Um treinador não pode iniciar associado a dois clubes na mesma edição anual. `catalogId` e referências a arquivos também não podem repetir. IDs são comparados sem diferenciar maiúsculas/minúsculas para detectar duplicação.

## Preferências e carreira do treinador

Duas preferências distintas em `home` e duas distintas em `away`, exatamente. A mesma formação pode aparecer em ambos os contextos. As dez formações nativas são `4-4-2`, `4-3-3`, `3-4-3`, `5-3-2`, `5-4-1`, `4-5-1`, `3-5-2`, `4-2-4`, `3-6-1`, `5-0-5`. `F2`/`F5` não são códigos do motor. O validador chama `TacticalFormations.TryGet`, que também aceita códigos canônicos D-M-A com soma dez. Os schemas auxiliam a edição; soma, referências, duplicatas e compatibilidade são verificadas pelo validador .NET.

Somente treinadores importados recebem o perfil contextual opcional `OfflineIdentity`. O planejamento usa o par adequado de casa/fora como preferência, mantendo a pontuação estratégica existente e todas as dez alternativas nativas disponíveis; as preferências não são uma trava. A reação durante a partida segue a mesma política. Treinadores anteriores, sem esse perfil, seguem exatamente o caminho anterior. Não houve mudança em cálculo de gols, táticas de humanos ou regras de escalação A/M.

O treinador tem `AiManager.Id` próprio no mundo; o clube guarda apenas a associação atual. A carreira existente move a mesma entidade e preserva identidade, histórico e preferências originais. O teste de contratação por outro clube comprova isso.

Ponto de extensão no treinador **do mundo**, não no catálogo: `LearnedHome` e `LearnedAway`, strings opcionais ausentes por padrão. `OriginalHome` e `OriginalAway` conservam as quatro preferências importadas. Nenhum código calcula, seleciona, pontua ou usa os campos aprendidos nesta etapa. Não adicionar campos de aprendizado ao JSON histórico 1.0: campos desconhecidos são rejeitados; uma evolução do schema exigirá versão explícita.

## Preencher um novo clube, ano ou país

1. Copie `modelos/clube.template.json` para `<pais>/<ano>/clubes/<arquivo>.json`.
2. Ajuste o `$schema` relativo (para arquivo dentro de `clubes`, normalmente `../../../_schemas/clube-1.0.schema.json`).
3. Atribua IDs estáveis, país/ano, nomes independentes, cores, estádio, treinador e elenco real documentado pelo proprietário. Comece como `DRAFT`.
4. Inclua `clubes/<arquivo>.json` no manifest desse ano. Arquivos sem referência não são importados.
5. Para novo ano, copie o manifest template, ajuste ano/pasta/edição e referências. Para novo país, crie slug e informe o código ISO; não precisa alterar código nem consultar API.
6. Valide, resolva os avisos, marque os clubes completos e depois o manifest como `READY`; valide novamente.
7. No criador, abra **USAR CATÁLOGO OFFLINE**, filtre país e ano, escolha catálogos READY, carregue e selecione os clubes e divisões. Confira a prévia antes de **CRIAR NOVO MUNDO**.

Os templates não constituem informação histórica e não são cadastrados automaticamente. Os JSONs podem ser editados em qualquer editor de texto. O editor privado antigo não é utilizado neste caminho, pois perderia nacionalidades, sigla independente, escudo e preferências contextuais. A auditoria dos campos ausentes foi registrada antes da expansão em `ELIFOOT98_EDITOR_COMPATIBILIDADE_INICIAL.md`.

## Validar sem API ou banco

Na raiz do projeto, após compilar localmente:

```powershell
dotnet build src/Elifoot.Api/Elifoot.Api.csproj -c Release
dotnet src/Elifoot.Api/bin/Release/net10.0/Elifoot.Api.dll offline-catalog-validate --CatalogOffline:Root=D:\elifoot_98\catalogos
dotnet src/Elifoot.Api/bin/Release/net10.0/Elifoot.Api.dll offline-catalog-list --CatalogOffline:Root=D:\elifoot_98\catalogos
```

Os comandos saem antes de construir o host: não abrem banco, não executam migrations, não iniciam serviços e não acessam rede. Saída JSON com catálogos/avisos/erros/Ready; código de saída 2 quando houver erro estrutural ou READY inválido. Rascunhos incompletos retornam avisos e `ready:false`, sem falhar a leitura do conjunto. Leitura e validação também podem ser chamadas diretamente por `OfflineCatalogLoader`.

Salve os arquivos em UTF-8 sem BOM. JSON estrito: tipos, campos desconhecidos, propriedades repetidas, conteúdo malformado, versão, referências e IDs são verificados. Paths absolutos, `..`, barras invertidas, links simbólicos/reparse points e caminhos fora da raiz são rejeitados. Manifest até 1 MB, clube até 10 MB e no máximo 4096 referências por catálogo. O schema `$schema` é apenas indicação para o editor, nunca baixado pelo loader. Mensagens de completude em DRAFT bloqueiam uso como catálogo pronto.

## API e criação independente

O fluxo usa o perfil existente de criação `Catalog013A:Enabled=true` e o lobby V3 `Catalog016B:Enabled=true`. As rotas autenticadas são:

| Rota relativa a `/api/world-creator/offline` | Uso |
|---|---|
| GET `/catalogs` | Lê manifests, clubes e relatórios locais |
| POST `/clubs` | Seleciona edições READY por `catalogIds` |
| POST `/preview` | Valida 128 clubes/distribuição e grava cópia congelada |
| POST `/confirm/{draftId}` | Cria somente um novo mundo a partir da cópia |

Leitura simples pela CLI não depende desse perfil nem de banco. Prévia/confirmar pela interface utilizam a infraestrutura existente de drafts e mundos; nenhuma tabela ou migration foi adicionada. O request inclui `name`, `catalogIds`, `seed`, `maxHumans`, `clubKeys` e `assignments` opcionais. Prévia expira em duas horas. A confirmação verifica proprietário, versão, hash e distribuição; reenvio retorna o mesmo mundo. A confirmação anterior e o editor privado recusam drafts offline para impedir importação com perda de campos.

Não houve mudança no campeonato: quatro divisões de oito e 96 distritais, 128 no total, calendário atual de 21 rodadas globais. Países diferentes compartilham essa estrutura; não foram criadas ligas nacionais paralelas. Menos de 128 clubes não autoriza complemento fictício, e mais de 128 exige selecionar o subconjunto. A distribuição automática usa ordem estável de identidade e respeita `initialDivision`; o usuário pode redefinir os slots. Não há ranking histórico inferido.

`CatalogClubId` permanece na origem. `Club.Id`, `Player.Id` e `AiManager.Id` são novos GUIDs por criação de mundo, sem reaproveitar os IDs do QA. O save guarda origem congelada, hashes dos arquivos e metadados individuais. Força, nome, nacionalidade, comportamento, estrela, estádio/capacidade e treinador vêm da definição. Defaults financeiros, personalidade auxiliar e duração inicial de contratos usam os moldes da fundação já existente; não foi introduzida nova fórmula. A economia inicial mantém o padrão vigente do criador; testes também habilitam a economia clássica por sua opção existente e verificam transferências.

Nos clubes novos, `Country` recebe ISO para o preço por nacionalidade funcionar para qualquer país; a legenda original permanece na origem. `ShortName` guarda o nome curto, e a sigla independente fica em `OfflineIdentity.Abbreviation`. O escudo fica embutido na identidade. Não são gerados jogadores livres, leilões iniciais sintéticos ou atletas ausentes.

Depois da confirmação, alterações em arquivos não atualizam o save nem a prévia congelada. As entidades evoluem pelas regras normais do mundo. Transferências mantêm a identidade de origem do jogador; mudanças de clube do treinador mantêm sua identidade de carreira. Campos opcionais novos são omitidos quando nulos na serialização antiga, sem migração automática de saves. Exportação antiga de pacotes/montagens não é backup completo desse formato; use a persistência integral do mundo para conservar todos os metadados.

## Validação e limitações da entrega

Evidências, contagens, arquivos alterados, eventuais falhas anteriores e decisão de commit são registrados em `ELIFOOT98_CATALOGOS_RESULTADO.md` nesta pasta. Testes novos: `OfflineCatalogTests` e `offline_catalog_creator_test.dart`. Cobrem arquivos válidos/incompletos, países/anos, duplicatas, tipos/atributos, quatro táticas, offline, criação/congelamento, novos IDs, saves anteriores, preferências/carreira, alternativa estratégica, ambas economias/mercado e interface em 360/768/1024/1440 px.

Limitações deliberadas: não há dados históricos reais, verificação de veracidade histórica, pesquisa externa, editor visual de todos os novos campos ou aprendizado tático. Os cinco países restantes dependem de definição do proprietário. Não há criação de mundo enquanto os catálogos não forem preenchidos com 128 clubes válidos. A instância do jogo já em execução não é reiniciada nem seus saves alterados por esta preparação; a interface nova pertence à compilação deste código.


## Extensão 1.1: identidade compartilhada e dados anuais

Árvore real adicionada (todos os novos manifests ativos continuam DRAFT/vazios):

```text
catalogos/
  _schemas/
    clube-1.0.schema.json
    clube-1.1.schema.json
    identidade-1.1.schema.json
    manifest-1.0.schema.json
    manifest-1.1.schema.json
  identidades/.gitkeep
  brasil/1998/{manifest.json,clubes/.gitkeep}     # preservado 1.0
  brasil/2026/{manifest.json,clubes/.gitkeep}     # preservado 1.0
  portugal/1998/{manifest.json,clubes/.gitkeep}   # preservado 1.0
  espanha/1998/{manifest.json,clubes/.gitkeep}    # preservado 1.0
  espanha/2026/{manifest.json,clubes/.gitkeep}
  inglaterra/2026/{manifest.json,clubes/.gitkeep}
  italia/2026/{manifest.json,clubes/.gitkeep}
  _pesquisa/comportamento/{brasil,espanha}/{1998,2026}/.gitkeep
  modelos/
    identidade.template.json
    clube-1.1.template.json
    manifest-1.1.template.json
    manifest.template.json                      # preservado 1.0
    clube.template.json                         # preservado 1.0
    exemplo/                                    # preservado 1.0
    exemplo-1.1/
      identidades/demo-v11/identidade.json
      brasil/1998/manifest.json
      brasil/1998/clubes/clube-sintetico.json
      brasil/2026/manifest.json
      brasil/2026/clubes/clube-sintetico.json
```

Cada `catalogClubId` 1.1 aponta para `identidades/<id>/identidade.json`. O ID deve coincidir exatamente com a pasta. Nome, nome curto, sigla, duas cores HEX e escudo pertencem à identidade; o arquivo anual rejeita suas cópias, inclusive campos desconhecidos. `crestFile` pode ser null; quando informado, deve ser PNG/JPEG local existente, relativo à pasta **da identidade**, até 2 MB. Paths absolutos, URLs, traversal e links/junctions são rejeitados. Uma leitura por identidade em cada varredura atende todos os anos; nova varredura relê as mudanças, sem cache permanente obsoleto. Hash da origem inclui identidade, imagem e arquivo anual. A prévia serializa os dados resolvidos, deixando de depender de alterações posteriores nos arquivos. A futura personalização histórica exigirá extensão versionada explícita, sem duplicação obrigatória hoje.

`OfflineClubV11`/`OfflinePlayerV11` são contratos próprios; não aceitam `players[].strength`, nem quando null. `OfflineV11Metadata` conserva a identidade e os campos anuais na origem congelada. O DTO 1.0 permanece independente; sua propriedade opcional V11 é omitida quando null, preservando snapshots antigos. O loader lê ambos os tipos e manifests 1.0/1.1, sem conversão automática. Uma montagem não mistura versões, pois não existe equivalência aprovada entre a força literal 1.0 e a força coletiva 1.1.

### Preenchimento 1.1

1. Copie a identidade template para `identidades/<id>/identidade.json`; ajuste `$schema` relativo. Preencha os nomes e as duas cores; mantenha `crestFile:null` até possuir a imagem local.
2. Copie `clube-1.1.template.json` para `<pais>/<ano>/clubes/<id>.json`; ajuste `$schema`, país ISO, ano, treinador e elenco. Informe `teamStrength` inteiro 1..20. Estádio é opcional; quando informado exige nome/capacidade válidos.
3. Mantenha `status:DRAFT` e `behavior:null` enquanto a pesquisa estiver incompleta. Não interprete ausência como Fair Play. Cada atleta usa ID, nome, posição G/D/M/A, nacionalidade ISO, comportamento 0..5 e `isStar` explícito. Só M/A admitem estrela. Nenhuma estatística disciplinar decide estrela.
4. Declare um ranking da época: `historicalRanking:{rating,source,referenceDate}`; maior rating é melhor. A fonte identifica também a escala. A data deve pertencer ao ano do arquivo. Ranking manual deve declarar `source:"MANUAL:<origem/escala>"`. Não há pesquisa ou comprovação automática da veracidade da fonte.
5. Na ausência de ranking confiável, use `historicalRanking:null` e `tieBreakPriority` inteiro não negativo, fornecido manualmente (menor é melhor). Não são fabricados ratings. Prioridades iguais ainda podem exigir resolução na prévia.
6. Inclua a referência no manifest. Use `manifest-1.1.template.json` para edições novas. Países/anos novos exigem somente arquivos, com código ISO e slug estável; Inglaterra usa GB. Há cinco países preparados e três posições do plano ainda indefinidas.
7. Execute a CLI .NET de validação. Corrija avisos de completude, marque explicitamente os clubes e depois o manifest READY, valide novamente. `READY` não é atribuído automaticamente e não significa criação liberada quando a política de forças está pendente.

11..23 atletas, pelo menos um G e dez de linha, continuam sendo exigidos para READY. Máximo 24 do servidor inalterado. As duas táticas distintas de casa e duas de fora usam a validação nativa existente. A IA mantém alternativas às preferidas; `learnedHome`/`learnedAway` continuam apenas nos metadados opcionais do treinador do mundo, sem aprendizado ou efeito.

### Ranking, seleção e divisões

A ordenação 1.1 agrupa por `teamStrength DESC`. Um grupo só compara ratings quando **todos** os seus clubes possuem a mesma fonte/escala e a mesma data de referência. Assim, anos diferentes e ClubElo/UEFA não são tratados como equivalentes. Se houver fonte/data incompatível ou ranking ausente no grupo de força, todo esse grupo usa apenas prioridade manual, com aviso. Em ranking comparável: rating DESC, prioridade ASC. Chave estável é apenas apresentação de empate.

Todos os candidatos são carregados, sem cotas por país. A tela mostra a classificação completa, os primeiros 128 pré-selecionados e todos os excedentes identificados; permite trocar a seleção e escolher divisões manualmente. Com 384 candidatos, são 128 iniciais e 256 excedentes, sem apagar nenhum arquivo. Exatamente 128 selecionados e a confirmação explícita continuam obrigatórios. A pré-seleção é uma proposta revisável, nunca uma criação automática.

Distribuição automática: posições 1–8, 9–16, 17–24 e 25–32 nas divisões 1–4; 33–128 nos distritais. Ajustes explícitos ocupam vagas primeiro; o restante segue a ordem. A prévia exige 8/8/8/8/96. Se um empate atravessar divisões, informe prioridades que o resolvam ou atribua explicitamente a divisão de **todos** os membros empatados. Empates dentro de uma divisão podem ser apresentados pela chave. Nenhum ranking é bônus de partida. Nove clubes força 20: os oito melhores ratings comparáveis entram na primeira; o nono entra na segunda e continua com força 20.

### Dependência de geração de forças — PARTIAL

`WorldCatalogRules016B.PowerAt`/`ClubInitialPower` classificam clubes na escala 1..20, sem converter atributos individuais. `InitialPlayerStrength` usa desempenho/ranking relativo e seed; não define mapeamento de teamStrength. A regra 017B, documentada em `docs/INITIAL_STRENGTH_RULES_017B.md`, depende da divisão e da posição, incluindo faixa 26–38 na segunda divisão. Aplicá-la ao nono clube força 20 imporia uma força pela divisão, contrariando este catálogo. Não existe conversão aprovada suficientemente definida.

A prévia 1.1 funciona e conserva seus metadados/elenco; a lista de atletas jogáveis fica vazia até haver forças válidas. Retorna erro explícito de bloqueio e a confirmação do servidor rejeita 1.1 mesmo se os erros forem removidos de um request. Não se criam jogadores força 0/50, roster artificial, mundo parcial ou migration. Força real 1..50 nos mundos existentes e no importador 1.0 permanece intacta. Essa dependência exige aprovação futura de uma regra determinística independente da divisão, comportamento e estrela; não foi inventada nesta entrega.

### Exemplos completos, somente sintéticos

Identidade única (compartilhada por 1998 e 2026):

```json
{
  "$schema": "../../../../_schemas/identidade-1.1.schema.json",
  "schemaVersion": "1.1",
  "catalogClubId": "demo-v11",
  "name": "Clube Sintético 1.1",
  "shortName": "Sintético",
  "abbreviation": "SYN",
  "colors": [
    "#123456",
    "#FFFFFF"
  ],
  "crestFile": null
}
```

Arquivo anual 1998 completo:

```json
{
  "$schema": "../../../../../_schemas/clube-1.1.schema.json",
  "schemaVersion": "1.1",
  "catalogClubId": "demo-v11",
  "country": "BR",
  "year": 1998,
  "teamStrength": 12,
  "historicalRanking": {
    "rating": 100,
    "source": "MANUAL:escala-didatica-sintetica",
    "referenceDate": "1998-07-01"
  },
  "tieBreakPriority": 1,
  "stadium": null,
  "coach": {
    "id": "demo-tecnico-001",
    "name": "Treinador Exemplo Sintetico",
    "nationality": "BR",
    "preferredTactics": {
      "home": [
        "4-4-2",
        "4-3-3"
      ],
      "away": [
        "5-3-2",
        "4-5-1"
      ]
    }
  },
  "players": [
    {
      "id": "demo-atleta-001",
      "name": "Atleta Exemplo Sintetico 01",
      "position": "G",
      "nationality": "BR",
      "behavior": 0,
      "isStar": false
    },
    {
      "id": "demo-atleta-002",
      "name": "Atleta Exemplo Sintetico 02",
      "position": "D",
      "nationality": "BR",
      "behavior": 1,
      "isStar": false
    },
    {
      "id": "demo-atleta-003",
      "name": "Atleta Exemplo Sintetico 03",
      "position": "D",
      "nationality": "BR",
      "behavior": 2,
      "isStar": false
    },
    {
      "id": "demo-atleta-004",
      "name": "Atleta Exemplo Sintetico 04",
      "position": "D",
      "nationality": "BR",
      "behavior": 3,
      "isStar": false
    },
    {
      "id": "demo-atleta-005",
      "name": "Atleta Exemplo Sintetico 05",
      "position": "D",
      "nationality": "BR",
      "behavior": 4,
      "isStar": false
    },
    {
      "id": "demo-atleta-006",
      "name": "Atleta Exemplo Sintetico 06",
      "position": "M",
      "nationality": "BR",
      "behavior": 5,
      "isStar": false
    },
    {
      "id": "demo-atleta-007",
      "name": "Atleta Exemplo Sintetico 07",
      "position": "M",
      "nationality": "BR",
      "behavior": 0,
      "isStar": false
    },
    {
      "id": "demo-atleta-008",
      "name": "Atleta Exemplo Sintetico 08",
      "position": "M",
      "nationality": "BR",
      "behavior": 1,
      "isStar": false
    },
    {
      "id": "demo-atleta-009",
      "name": "Atleta Exemplo Sintetico 09",
      "position": "M",
      "nationality": "BR",
      "behavior": 2,
      "isStar": false
    },
    {
      "id": "demo-atleta-010",
      "name": "Atleta Exemplo Sintetico 10",
      "position": "A",
      "nationality": "BR",
      "behavior": 3,
      "isStar": false
    },
    {
      "id": "demo-atleta-011",
      "name": "Atleta Exemplo Sintetico 11",
      "position": "A",
      "nationality": "BR",
      "behavior": 4,
      "isStar": true
    }
  ],
  "status": "READY"
}
```

Manifest do exemplo:

```json
{
  "$schema": "../../../../_schemas/manifest-1.1.schema.json",
  "schemaVersion": "1.1",
  "catalogId": "demo-v11-1998",
  "country": {
    "code": "BR",
    "name": "Brasil",
    "slug": "brasil"
  },
  "year": 1998,
  "name": "Exemplo sintético 1.1 1998",
  "status": "READY",
  "gender": "Male",
  "clubs": [
    "clubes/clube-sintetico.json"
  ]
}
```

O arquivo de 2026 no mesmo exemplo referencia `demo-v11`, mas possui `teamStrength:15` e rating sintético 110, enquanto 1998 possui força 12 e rating sintético 100. Os elencos e treinador são objetos anuais independentes (os IDs podem ser preservados entre anos). Alterar o elenco ou preferências de 2026 não altera 1998; escudo e cores continuam centralizados. As datas dos rankings diferem, portanto esses ratings não seriam comparados diretamente em uma montagem multi-ano. Nenhum desses números representa informação histórica real; nenhum clube real foi cadastrado.

Classificação de comportamento e aprovação manual: consulte `ELIFOOT98_CLASSIFICADOR_COMPORTAMENTO.md`. O jogo ignora `_pesquisa` e `modelos`, e não executa o classificador durante partidas.

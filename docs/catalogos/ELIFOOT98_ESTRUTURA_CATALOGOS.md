# Catálogo histórico offline — schema 1.0

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

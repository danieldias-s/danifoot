# Elifoot 98 Multiplayer — Regras de cadastro de clubes (Catálogo offline V1.1)

**Documento de referência para montagem de clubes reais, por país e temporada.**  
**Base verificada:** estrutura descrita pelo Codex no commit local `aa5130b` da branch `main` (12/11/2026).  
**Escopo:** cadastro offline; não altera saves existentes, regras do motor de partidas, economia nem carreira.

> A V1.1 valida cadastros e permite criar mundos quando os requisitos READY e a quantidade de clubes são atendidos. A geração de força inicial 1..50 já está integrada. O formato V1.0 permanece compatível. Os exemplos são ilustrativos.

---

## 1. Princípios aprovados

1. **Sem dependência de APIs:** os dados dos clubes são mantidos em arquivos JSON locais; os escudos são arquivos de imagem locais.
2. **Organização por país e ano:** um arquivo de clube por edição/temporada em `catalogos/<pais>/<ano>/clubes/`.
3. **Identidade compartilhada entre anos:** nome, sigla, duas cores e escudo ficam em `catalogos/identidades/<id-do-clube>/`. Assim, Barcelona 1998 e Barcelona 2026 reutilizam uma única identidade visual.
4. **Dados esportivos por ano:** cada edição do clube possui força coletiva, ranking histórico, estádio opcional, treinador e jogadores próprios.
5. O JSON de cada jogador V1.1 guarda ID, nome, posição, nacionalidade, comportamento e asterisco. O jogador recebe força individual de 1 a 50 na criação do mundo, pela regra do jogo.
6. **Força coletiva do clube:** `teamStrength` é um inteiro de **1 a 20**. Ela determina a prioridade principal na distribuição inicial dos clubes, mas **não** substitui a força individual dos atletas.
7. **Ranking histórico:** desempata clubes com a mesma força coletiva na formação inicial das divisões. Não aumenta força no motor da partida.
8. O treinador READY tem uma a quatro táticas comprovadas, até duas por contexto. A IA pode selecionar outras táticas válidas.
9. **Atributo de comportamento:** cada jogador pronto para uso deve possuir um dos seis comportamentos clássicos; não deve ser sorteado na criação do mundo.
10. **Persistência:** ao criar um mundo, os dados devem ser congelados no próprio save. Modificações posteriores nos catálogos não podem modificar carreiras já existentes automaticamente.

---

## 2. Estrutura de pastas do projeto

**Raiz local:** `D:\elifoot_98\catalogos\`

```text
D:\elifoot_98\catalogos\
├── _pesquisa\
│   └── comportamento\
│       ├── brasil\1998\
│       ├── brasil\2026\
│       ├── espanha\1998\
│       └── espanha\2026\
├── _schemas\
│   ├── clube-1.0.schema.json
│   ├── clube-1.1.schema.json
│   ├── identidade-1.1.schema.json
│   ├── manifest-1.0.schema.json
│   └── manifest-1.1.schema.json
├── identidades\
│   └── <id-do-clube>\
│       ├── identidade.json
│       └── escudo.png                 # Opcional
├── brasil\
│   ├── 1998\
│   │   ├── manifest.json
│   │   └── clubes\
│   └── 2026\
│       ├── manifest.json
│       └── clubes\
├── espanha\
│   ├── 1998\
│   │   ├── manifest.json
│   │   └── clubes\
│   └── 2026\
│       ├── manifest.json
│       └── clubes\
├── portugal\
│   └── 1998\
│       ├── manifest.json
│       └── clubes\
├── inglaterra\
│   └── 2026\
│       ├── manifest.json
│       └── clubes\
├── italia\
│   └── 2026\
│       ├── manifest.json
│       └── clubes\
├── modelos\
│   ├── identidade.template.json
│   ├── clube-1.1.template.json
│   ├── manifest-1.1.template.json
│   └── exemplo-1.1\                # Exemplo sintético, não carregado como catálogo ativo
└── plano.json
```

**Importante:** a pasta ativa `identidades/` estava vazia (somente `.gitkeep`) e os catálogos reais ainda estavam sem clubes cadastrados na auditoria. As pastas e arquivos dos clubes abaixo são **destinos para criar**, não arquivos já existentes.

### Arquivos necessários por clube

| Arquivo | Local | Responsabilidade |
|---|---|---|
| Identidade | `identidades/<clubId>/identidade.json` | Nome, nome curto, sigla, duas cores HEX, escudo compartilhado |
| Escudo | `identidades/<clubId>/escudo.png` | Imagem opcional reutilizada em qualquer ano |
| Clube de 2026 | `espanha/2026/clubes/<clubId>.json` | Força coletiva, ranking, treinador, elenco e estádio daquele ano |
| Clube de 1998 | `espanha/1998/clubes/<clubId>.json` | Dados esportivos de 1998, referenciando a mesma identidade |
| Manifesto | `<pais>/<ano>/manifest.json` | Relação dos arquivos de clubes do catálogo daquele país e ano |

---

## 3. Identidade compartilhada (modelo real V1.1)

**Template a copiar:** `D:\elifoot_98\catalogos\modelos\identidade.template.json`  
**Destino de exemplo:** `D:\elifoot_98\catalogos\identidades\fc-barcelona\identidade.json`

```json
{
  "$schema": "../../_schemas/identidade-1.1.schema.json",
  "schemaVersion": "1.1",
  "catalogClubId": "fc-barcelona",
  "name": "Barcelona",
  "shortName": "Barcelona",
  "abbreviation": "BAR",
  "colors": [
    "#004D98",
    "#A50044"
  ],
  "crestFile": null
}
```

> Os códigos de cores acima são **ilustrativos/pendentes de conferência** antes do cadastro definitivo do clube.

**Regras:**

- `catalogClubId` deve corresponder exatamente ao nome da pasta de identidade, permanecer estável e ser referenciado em todos os anos do mesmo clube.
- `colors` possui **exatamente duas cores**, em HEX `#RRGGBB`.
- Coloque o escudo na mesma pasta e altere `"crestFile": null` para `"crestFile": "escudo.png"` quando a imagem estiver disponível.
- Escudos locais aceitos pelo loader: **PNG ou JPEG**, até **2 MB**.
- O escudo é opcional, inclusive no clube pronto (`READY`).
- O arquivo anual não precisa repetir nome, sigla, cores e escudo.
- O loader deve resolver a imagem dentro da pasta da identidade, com proteção contra caminhos externos.

---

## 4. Clube por país e ano (modelo real V1.1)

**Template a copiar:** `D:\elifoot_98\catalogos\modelos\clube-1.1.template.json`  
**Destino de exemplo:** `D:\elifoot_98\catalogos\espanha\2026\clubes\fc-barcelona.json`

Modelo **DRAFT** para iniciar o preenchimento:

```json
{
  "$schema": "../../../_schemas/clube-1.1.schema.json",
  "schemaVersion": "1.1",
  "catalogClubId": "fc-barcelona",
  "country": "ES",
  "year": 2026,
  "teamStrength": null,
  "historicalRanking": null,
  "tieBreakPriority": null,
  "stadium": null,
  "coach": null,
  "players": [],
  "status": "DRAFT"
}
```

### Campos do clube

| Campo | Regra |
|---|---|
| `schemaVersion` | `"1.1"` |
| `catalogClubId` | Mesmo ID da identidade compartilhada |
| `country` | Código ISO em letras maiúsculas, igual ao manifest (ex.: `ES`) |
| `year` | Ano do catálogo, igual ao manifest |
| `teamStrength` | Inteiro `1..20` em READY; `null` permitido em DRAFT |
| `historicalRanking` | Objeto completo ou `null` |
| `tieBreakPriority` | Inteiro não negativo ou `null` |
| `stadium` | Objeto opcional (`name`, `capacity`) ou `null` |
| `coach` | Treinador completo em READY; `null` permitido em DRAFT |
| `players` | Lista de atletas; para READY, **11 a 23 jogadores**, com pelo menos 1 goleiro e 10 jogadores de linha |
| `status` | `DRAFT` enquanto incompleto; `READY` somente após validação de todas as regras |

Se o estádio for informado, fornecer `name` e `capacity`, com capacidade de **1 a 1.000.000**.

### Ranking histórico (quando disponível)

```json
"historicalRanking": {
  "rating": 1900,
  "source": "EXEMPLO_MANUAL_NAO_HISTORICO",
  "referenceDate": "2026-07-01"
}
```

**O valor `1900` é fictício**, apenas para demonstrar a estrutura. O ranking precisa registrar fonte e data pertinente ao ano do clube. Não comparar notas de escalas diferentes (por exemplo, pontos ClubElo e posições UEFA) sem normalização aprovada. Se faltar ranking confiável, manter `historicalRanking: null` no rascunho; utilizar `tieBreakPriority` (prioridade manual) quando necessário. READY exige **ranking ou prioridade manual**.

### Desempate para posicionamento inicial

1. Maior `teamStrength`.
2. Maior `historicalRanking.rating` **quando as notas forem comparáveis**.
3. Menor `tieBreakPriority` para desempate manual, quando aplicável.
4. Se houver empate não resolvido na fronteira entre divisões, não decidir aleatoriamente: exigir resolução antes da criação do mundo.

O ranking é usado **somente no posicionamento inicial**, sem bônus de força em jogos.

---

## 5. Jogadores: formato exato do V1.1

Os jogadores são objetos dentro de `players[]` do arquivo anual do clube. **Não é necessário criar um arquivo separado por jogador.**

```json
{
  "id": "barca-2026-001",
  "name": "Nome do Jogador",
  "position": "A",
  "nationality": "BR",
  "behavior": null,
  "isStar": true
}
```

Este objeto é apenas um **exemplo de rascunho**, não um jogador real.

| Campo | Regra |
|---|---|
| `id` | Obrigatório; 1 a 80 caracteres; padrão `^[A-Za-z0-9][A-Za-z0-9_-]{0,79}$` |
| `name` | Nome não vazio; até 160 caracteres |
| `position` | Exatamente `G`, `D`, `M` ou `A` |
| `nationality` | Código ISO de duas letras maiúsculas, presente na lista local |
| `behavior` | Inteiro `0..5`; `null` permitido enquanto `DRAFT`; obrigatório em `READY` |
| `isStar` | Booleano `true`/`false`; **sempre `false` para G e D** |

**Não colocar `strength` no V1.1.** O schema rejeita esse campo, mesmo se seu valor for `null`. Também rejeita outros campos não previstos. A força individual `1..50` pertence ao **save do mundo**, não ao cadastro histórico.

### Posições

| Código | Posição | Asterisco |
|---|---|---|
| `G` | Goleiro | Proibido |
| `D` | Defensor | Proibido |
| `M` | Meio-campista | Permitido |
| `A` | Atacante (inclui extremos) | Permitido |

### Comportamentos (tipos)

| Código | Tipo | Interpretação para o cadastro |
|---:|---|---|
| `0` | Fair Play | Disciplina alta e jogo limpo |
| `1` | Cordeirinho | Pouca agressividade/participação física |
| `2` | Cavalheiro | Disputas controladas e boa disciplina |
| `3` | Caneleiro | Faltas e contato relativamente frequentes |
| `4` | Caceteiro | Perfil muito físico/indisciplinado |
| `5` | Sarrafeiro | Padrão extremo de indisciplina e faltas duras |

**Escolha baseada na temporada do jogador:** os tipos podem variar entre anos. Quando houver estatísticas, use o classificador auxiliar offline para sugerir o comportamento (cartões, faltas, minutos, duelos; fontes e justificativas). Revisão humana é permitida. **Não transformar estatística ausente em zero** nem classificar automaticamente todos como Fair Play. Enquanto a evidência não for suficiente, mantenha `behavior: null` e `status: DRAFT`.

Para sugestões experimentais V16, confirme a decisão individual ou em lote e aplique os IDs aprovados pela CLI local. O JSON anual recebe apenas o código de jogo. Evidências, decisões e recibos ficam em arquivos auxiliares. O fluxo inclui pesquisa manual para temporadas com estatísticas incompletas. [Contrato de aprovação e proveniência V16](ELIFOOT98_V16_APROVACAO_PROVENIENCIA.md)

O asterisco é **independente** do comportamento: um meia ou atacante pode ser estrela e ter qualquer um dos seis tipos. Goleiro e defensor nunca podem ser estrelas no catálogo.

---

## 6. Treinador: formato exato do V1.1

O treinador fica no objeto `coach` do arquivo anual do clube:

```json
{
  "id": "tecnico-barcelona-2026",
  "name": "Nome do Treinador",
  "nationality": "ES",
  "preferredTactics": {
    "home": ["4-3-3", "4-4-2"],
    "away": ["5-3-2", "4-5-1"]
  }
}
```

Exemplo de **formato**, não de treinador histórico verificado.

- `id`: identificador estável, com o mesmo padrão alfanumérico dos IDs de jogadores.
- `name`: não vazio, até 160 caracteres.
- `nationality`: código ISO de duas letras.
- `preferredTactics.home` e `preferredTactics.away` aceitam até duas formações distintas em cada contexto, com pelo menos uma no total em READY.
- Uma mesma formação pode ser preferida tanto em casa quanto fora.
- A IA pode usar outras formações válidas quando sua estratégia considerar melhor.
- As extensões `learnedHome` e `learnedAway` pertencem ao estado do treinador **dentro do mundo** e **não** devem ser adicionadas ao JSON de cadastro. Aprendizado futuro ainda não implementado.

**Formações nativas relatadas no V1.1:** `4-4-2`, `4-3-3`, `3-4-3`, `5-3-2`, `5-4-1`, `4-5-1`, `3-5-2`, `4-2-4`, `3-6-1`, `5-0-5`. O validador também reconhece formações D-M-A canônicas válidas cuja soma seja dez.

---

## 7. Manifesto do país e ano

**Template a copiar:** `D:\elifoot_98\catalogos\modelos\manifest-1.1.template.json`  
**Destino:** `D:\elifoot_98\catalogos\espanha\2026\manifest.json`

Exemplo demonstrativo (os nomes/IDs devem preservar o manifest real já existente):

```json
{
  "$schema": "../../_schemas/manifest-1.1.schema.json",
  "schemaVersion": "1.1",
  "catalogId": "es-2026-exemplo",
  "country": {
    "code": "ES",
    "name": "Espanha",
    "slug": "espanha"
  },
  "year": 2026,
  "name": "Espanha 2026 - em preenchimento",
  "status": "DRAFT",
  "gender": "Male",
  "clubs": [
    "clubes/fc-barcelona.json"
  ]
}
```

**Não substituir o manifest existente por esse exemplo.** Abra o manifest real e **adicione** `"clubes/fc-barcelona.json"` à lista `clubs`, preservando `catalogId` e as demais referências. O caminho é relativo à pasta `espanha/2026/`.

### Versões dos manifests confirmadas na auditoria

| Catálogo | Manifest reportado |
|---|---|
| Brasil 1998 | V1.0 |
| Brasil 2026 | V1.0 |
| Portugal 1998 | V1.0 |
| Espanha 1998 | V1.0 |
| Espanha 2026 | V1.1 |
| Inglaterra 2026 | V1.1 |
| Itália 2026 | V1.1 |

Para cadastrar clubes V1.1 em países/anos ainda com manifest V1.0, é necessário adaptar o manifest para V1.1 **com segurança**, preservando IDs, dados e compatibilidade. Não sobrescrever por cópia indiscriminada.

---

## 8. READY x DRAFT: o que pode ficar pendente

| Campo | Em DRAFT | Para READY |
|---|---|---|
| Identidade: ID, nome, sigla, duas cores | Deve ser válido | Deve ser válido |
| Escudo | `crestFile: null` | Continua opcional |
| `teamStrength` | `null` permitido | Inteiro `1..20` |
| `historicalRanking` | `null` permitido | Ranking válido **ou** `tieBreakPriority` |
| `tieBreakPriority` | `null` permitido | Obrigatório se não houver ranking |
| `stadium` | `null` permitido | Continua opcional |
| `coach` | `null` permitido | Treinador completo |
| `players` | `[]` permitido | 11–23; ao menos 1 G e 10 jogadores de linha |
| `players[].behavior` | `null` permitido | Valor inteiro de `0` a `5` |
| `players[].isStar` | Obrigatório em cada jogador incluído | Regras por posição aplicadas |
| Manifest `clubs` | `[]` permitido | Apenas clubes devidamente referenciados e validados |

Um JSON com sintaxe válida **não implica** que o clube esteja pronto para uso. O validador também verifica regras de negócio.

---

## 9. Criação de mundos e distribuição inicial

A organização atual de um mundo segue:

| Categoria | Número de clubes |
|---|---:|
| 1ª divisão | 8 |
| 2ª divisão | 8 |
| 3ª divisão | 8 |
| 4ª divisão | 8 |
| Distritais | 96 |
| **Total por mundo** | **128** |

O catálogo pode possuir **mais de 128 clubes por país/ano**, e o criador pode combinar vários países; o limite é de **128 selecionados por mundo**, não por pasta. Ao selecionar, por exemplo, 128 clubes espanhóis, 128 ingleses e 128 italianos, haveria 384 candidatos, mas só 128 poderiam integrar o mundo atual. Os excedentes permanecem nos arquivos de catálogo, sem exclusão.

Na prévia de distribuição: ordenar por força coletiva e ranking comparável; resolver empates na fronteira das divisões sem sorteio; permitir ajustes manuais validados. Um nono clube com força 20 pode começar na 2ª divisão se houver oito clubes melhor posicionados na 1ª — **sem perder sua força 20**. Depois da criação do mundo, acesso/rebaixamento decorrem dos resultados das partidas.

A criação V1.1 usa a geração inicial de forças individuais 1..50 já integrada. O comportamento utilizado é o que consta no JSON anual validado e congelado na prévia.

---

## 10. Passo a passo para cadastrar o primeiro time

Exemplo: **Barcelona, Espanha, 2026**.

1. Acesse `D:\elifoot_98\catalogos\`.
2. Crie a pasta `identidades\fc-barcelona\`.
3. Copie `modelos\identidade.template.json` para `identidades\fc-barcelona\identidade.json`.
4. Ajuste `$schema` para `../../_schemas/identidade-1.1.schema.json`; preencha ID, nome, sigla e **duas cores**. Adicione `escudo.png` quando disponível e altere `crestFile`.
5. Copie `modelos\clube-1.1.template.json` para `espanha\2026\clubes\fc-barcelona.json`.
6. Ajuste `$schema` para `../../../_schemas/clube-1.1.schema.json`; use o mesmo `catalogClubId`, `country: "ES"` e `year: 2026`.
7. Cadastre jogadores em `players[]`, com **apenas os seis campos permitidos**. Não use `strength`.
8. Defina `teamStrength` (1..20), ranking histórico comparável ou prioridade manual, treinador e uma a quatro táticas comprovadas.
9. Abra `espanha\2026\manifest.json` e **acrescente** `"clubes/fc-barcelona.json"` em `clubs`, sem remover os demais.
10. Mantenha o clube em `DRAFT` enquanto houver dados pendentes; só altere para `READY` quando cumprir os requisitos.
11. Execute o validador local. A validação **não cria mundo nem altera saves**.

### Comando para validação

Compilação localizada pelo Codex em 12/11/2026 (caminho temporário; confirme que ainda existe):

```powershell
dotnet "C:\Users\Daniel\AppData\Local\Temp\elifoot-v11-build\bin\Elifoot.Api\release\Elifoot.Api.dll" offline-catalog-validate "--CatalogOffline:Root=D:\elifoot_98\catalogos"
```

- O comando avalia os catálogos ativos **referenciados pelos manifests**; não valida individualmente um clube não registrado.
- Saída `0` com avisos de `DRAFT` **não significa** que o clube esteja pronto.
- Problemas estruturais ou conteúdo READY inválido podem produzir saída `2`.
- A pasta `modelos/` e a pasta `_pesquisa/` não entram na seleção de catálogos ativos.
- Esse comando, conforme descrito pelo Codex, não inicia servidor, não acessa banco e não executa migrations.

---

## 11. Orientações para pesquisa e preenchimento dos clubes reais

- **Times e temporadas:** verificar o elenco da temporada selecionada; não misturar jogadores de 1998 com 2026.
- **Posição:** simplificar a posição real para `G`, `D`, `M`, `A`. Extremos são `A`.
- **Nacionalidade:** guardar o código ISO correspondente à nacionalidade do atleta.
- **Estrela:** marcar somente em meias e atacantes conforme o critério aprovado no projeto; não deduzir a estrela automaticamente a partir do ranking do clube.
- **Tipo de comportamento:** consultar estatísticas ou evidências históricas do período; o classificador disciplinar offline auxilia, com revisão manual. Sem dados, `null` em DRAFT.
- Cadastre nome, nacionalidade e uma a quatro táticas comprovadas do treinador, até duas por mando. A IA pode usar outras táticas válidas.
- **Força coletiva:** registrar uma força de 1 a 20 por clube/ano, separada do ranking histórico. **Não calcular forças individuais no cadastro.**
- **Escudo e cores:** pesquisar uma vez por identidade e reutilizar nos anos; a validação aceita escudo opcional.
- **Rastreabilidade:** manter as fontes utilizadas na pesquisa, especialmente para ranking, tipos e dados históricos, nos arquivos auxiliares ou documentação.

---

## 12. Limitações e itens ainda em acompanhamento

- A estrutura e os exemplos desta seção registram a auditoria de `aa5130b`. A criação V1.1 e a revisão de comportamentos foram integradas posteriormente.
- **Catálogos reais:** nenhum clube real estava cadastrado na árvore ativa apresentada pelo Codex. Exemplos em `modelos/` são sintéticos.
- **Escudos:** a pasta `identidades/` estava vazia no levantamento; imagens devem ser adicionadas manualmente.
- **Manifests antigos:** Brasil 1998/2026, Portugal 1998 e Espanha 1998 ainda em V1.0 naquele levantamento.
- **Quantidade máxima por mundo:** 128 clubes (8/8/8/8 + 96 distritais); qualquer expansão exige mudança explícita do jogo.
- **Táticas aprendidas:** campos futuros presentes no estado do treinador, mas aprendizado não implementado.
- **Instância local:** o jogo em execução pode estar usando compilação anterior; não presumir que o novo código V1.1 está ativo no servidor aberto sem reinicialização controlada.

**Regra central:** o catálogo histórico define **quem é o clube, quem são os jogadores e como eles jogam**. O mundo criado define as **forças individuais**, a carreira, os contratos, as partidas e a evolução desses jogadores.

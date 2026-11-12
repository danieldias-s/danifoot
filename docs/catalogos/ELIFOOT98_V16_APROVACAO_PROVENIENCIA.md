# Aprovação e proveniência de comportamentos V16

A CLI local `tools/offline_catalog/behavior_approval_v16.py` usa o contrato `behavior-approval-1.0`. O classificador experimental produz uma sugestão. O operador confirma uma decisão em separado. Depois de outra confirmação, a CLI aplica o valor aprovado ao campo `players[].behavior` do clube anual V1.1. Uma nova prévia congela o JSON validado e o criador de mundos copia o código para `Player.Behavior` com a identidade offline do atleta. Uma prévia já congelada e um mundo já salvo conservam o valor que receberam.

| Código | Tipo |
|---|---|
| 0 | FAIR PLAY |
| 1 | CORDEIRINHO |
| 2 | CAVALHEIRO |
| 3 | CANELEIRO |
| 4 | CACETEIRO |
| 5 | SARRAFEIRO |

`behavior: null` indica comportamento pendente em DRAFT. Um catálogo READY continua sujeito à validação V1.1 completa, inclusive quando todos os comportamentos estiverem preenchidos. O fluxo aceita como alvo um clube V1.1 referenciado por um manifest V1.1. O loader continua aceitando os formatos legados V1.0 e V1.1. A auditoria fica fora dos objetos de jogador e o jogo carrega os valores do JSON anual pelo fluxo existente.

## Arquivos e identificadores

| Arquivo | Conteúdo |
|---|---|
| `catalogos/_pesquisa/comportamento/<relatório>.json` | Saída do classificador experimental com a configuração, pesos, estatísticas, recorte, índice, percentil e sugestão. Arquive o relatório original pelos bytes. |
| `catalogos/_pesquisa/comportamento/v16/history.json` | Envelope `version`, `head` e `events`. Sequência de registros `EVIDENCE`, `REVIEW` e `APPLY`. Cada evento guarda operador, data UTC e hash do evento anterior. |
| `catalogos/_pesquisa/comportamento/v16/transaction.json` | Diário temporário com os bytes anteriores e previstos, hashes de manifest, transação e IDs das aprovações. Serve à recuperação após interrupção. |
| `catalogos/_pesquisa/comportamento/v16/workflow.lock` | Arquivo local usado para exclusão entre escritores da CLI. Pode permanecer no disco depois de liberado. |
| `behavior-operators.local.json` ao lado da pasta `catalogos` | Política privada de aliases e contas do sistema. O Git ignora a política, o diário temporário e o arquivo de bloqueio da raiz ativa. |

Um registro `EVIDENCE` vincula cada atleta ao `catalogId`, país, ano, `clubId`, `playerId`, posição e caminho anual. Guarda nome, valor atual, hashes do manifest e do clube, estatísticas disponíveis, fontes, situação das fontes, alertas, qualidade, versão e configuração do classificador. Cada fonte possui `name`, `reference`, `referenceDate` e `collectedAt`. A referência pode ser uma URL, uma página de acervo ou outra referência local. A CLI registra esse texto para o revisor. Datas desconhecidas ficam `null` e fonte ausente aparece como `sourceStatus: "MISSING"`. O relatório experimental V16 não fornece confiança calibrada, portanto `confidence` permanece `null`. O índice e o tamanho da coorte permanecem disponíveis para avaliação.

`suggestionId` é o SHA-256 da evidência em JSON canônico do projeto. Na decisão, `evidenceSha256` repete esse vínculo. Cada `decisionId` inclui evidência, ação, código sugerido, código aprovado, operador, data UTC, justificativa e `supersedes`, quando informado. Para uma decisão aprovada, o mesmo ID é usado por `apply --approval-id`. `eventId` encadeia os registros. O recibo `APPLY` guarda transação, IDs, comportamento anterior e final por jogador, hashes de cada JSON e resultado do validador. Os hashes dos JSON e do relatório experimental são calculados sobre os bytes. A cadeia detecta inconsistências e edições avulsas. A autenticidade de fontes e a guarda do histórico e dos relatórios cabem ao operador.

## Estados e revisão

| Estado | Regra |
|---|---|
| `PENDING` | Sugestão experimental reproduzível com fonte datada declarada, aguardando decisão. |
| `REVIEW_REQUIRED` | Fonte ausente, data de referência ausente, amostra insuficiente, pesquisa manual ou conflito declarado. |
| `APPROVED` | Operador confirmou `ACCEPT` ou `MANUAL`. O registro indica separadamente se o JSON já recebeu o valor. |
| `REJECTED` | Operador confirmou `REJECT`. A evidência e qualquer aprovação anterior de outra sugestão são preservadas. |
| `SUPERSEDED` | Uma aprovação mais recente indicou explicitamente o ID ativo em `supersedes`. A decisão anterior e seus recibos continuam no histórico. |

`list` mostra nome, posição, sugestão, estatísticas, fontes, confiança, alertas e estado. Evidência ou alvo alterados aparecem em `staleAlert`. Registre nova evidência se o relatório ou JSON do alvo mudar. O campo `sourceConflict` recebe a descrição de divergências identificadas pelo revisor. A conferência factual da fonte faz parte da revisão.

O arquivo de revisão tem `version` e `selected`. Uma entrada executa revisão individual. Várias entradas executam um lote. Deixe fora de `selected` os jogadores que deseja excluir. `DEFER` registra o adiamento e conserva a situação pendente para revisão posterior. `ACCEPT` copia uma sugestão em `PENDING`. `MANUAL` exige código inteiro de 0 a 5 e justificativa. Use-o para corrigir a sugestão ou documentar uma escolha sem estatísticas. A pesquisa manual pode ser registrada com `proposal: null` e lista vazia de fontes quando indisponíveis. Todas as ações exigem justificativa por jogador.

O comando `review` apresenta as decisões previstas. Execute com `--confirm` para registrá-las. A seleção é validada inteira antes do registro. A CLI verifica duplicatas por jogador e ano, ignorando diferenças de caixa no ID. Uma sugestão já decidida fica arquivada. Para substituir aprovação ativa, registre a nova evidência, informe `supersedes` com o ID anterior e confirme. Para trocar um `behavior` já presente no JSON, informe também `replaceCatalogBehavior: true`. Um motivo deve explicar a nova escolha, inclusive como tratou eventual conflito entre fontes.

## Aplicação e recuperação

`apply --dry-run` mostra caminho, jogador, valor atual e valor aprovado. A omissão de `--confirm` também executa a previsão. O plano é somente leitura e pode ser gerado antes de disponibilizar o validador. A aplicação exige `--confirm`, os IDs aprovados explicitamente selecionados e a DLL Release local `Elifoot.Api.dll`. O processo usa o comando offline da DLL e recebe o relatório do loader.

1. A CLI verifica o histórico, os IDs ativos, a versão e os hashes das evidências, a identidade do jogador, o país, ano, clube, posição e os bytes de todos os alvos e manifests. O lote inteiro é preparado em memória.
2. A CLI executa o validador sobre uma cópia temporária com o lote completo. Se o validador aprovar a cópia, a CLI grava o diário e aplica cada arquivo por substituição atômica, rechecando os bytes. A serialização usa UTF-8 sem BOM. O campo de dados alterado é `behavior`. O formato textual do JSON pode ser normalizado.
3. A CLI valida os arquivos finais, confere os hashes e registra um único evento `APPLY`. Reexecutar IDs já aplicados retorna `ALREADY_APPLIED` se o alvo, o manifest, a evidência e a aprovação ativa ainda corresponderem ao recibo. Para arquivos alterados depois da aplicação, a repetição retorna conflito. Selecione em execuções distintas IDs já aplicados e IDs novos.
4. Uma falha antes do recibo restaura os arquivos gravados que ainda correspondem à versão da própria transação. Execute `recover --confirm` após uma interrupção para concluir a limpeza ou restaurar os bytes anteriores. Se o arquivo contiver uma edição concorrente divergente, o diário é mantido e a CLI identifica o alvo. O operador deve reconciliar a divergência antes de retomar.

Agrupe num lote as aprovações pendentes do mesmo JSON quando foram feitas sobre a mesma versão. Se uma aplicação anterior mudar esse arquivo, as outras decisões baseadas nos bytes antigos exigem nova revisão. A exclusão protege instâncias desta CLI na mesma raiz. Editores externos que ignoram o bloqueio estão sujeitos às checagens de conflito, mas não há isolamento transacional do filesystem entre vários arquivos. Durante uma operação evite edição externa ou criação de prévias na mesma raiz. O diário permite recuperação em caso de interrupção do processo.

## Operador local

O proprietário provisiona a política privada na pasta pai da raiz de catálogos. O alias indicado em `--operator` precisa corresponder à conta real do sistema. O histórico grava o alias. A autorização local depende das permissões do sistema operacional sobre a política, a pasta pai, os catálogos e o histórico. Reserve a escrita nesses caminhos aos operadores autorizados. Quem controla esses arquivos diretamente tem o mesmo acesso que a CLI. Jogadores comuns acessam o jogo por sua API, que não expõe estes comandos. `list` e `history` seguem as permissões locais de leitura.

## Exemplo completo em cópia sintética

Execute a partir da raiz do projeto. O exemplo copia o modelo existente para uma pasta temporária. Use Python 3.12+ e indique a DLL do seu build Release.

```powershell
$demo = Join-Path $env:TEMP ('elifoot-behavior-demo-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $demo | Out-Null
$root = Join-Path $demo 'catalogos'
Copy-Item -LiteralPath catalogos/modelos/exemplo-1.1 -Destination $root -Recurse
$dll = Join-Path $env:TEMP 'elifoot-v16-build/bin/Elifoot.Api/release/Elifoot.Api.dll'
$tool = 'tools/offline_catalog/behavior_approval_v16.py'
$account = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name.Split('\')[-1]
@{ operators = @{ 'operador-demo' = $account } } | ConvertTo-Json |
  Set-Content (Join-Path $demo 'behavior-operators.local.json') -Encoding utf8
$operator = @('--root', $root, '--operator', 'operador-demo')

$stats = Join-Path $demo 'estatisticas.json'
@'
[
  {"playerCatalogId":"demo-atleta-001","position":"G","initialDivision":1,"comparisonGroup":"demo-1998","baseBehavior":0,"minutes":1200,"fouls":5,"yellowCards":1,"redCards":0},
  {"playerCatalogId":"demo-atleta-002","position":"D","initialDivision":1,"comparisonGroup":"demo-1998","baseBehavior":1,"minutes":1500,"fouls":14,"yellowCards":2,"redCards":0},
  {"playerCatalogId":"demo-atleta-003","position":"D","initialDivision":1,"comparisonGroup":"demo-1998","baseBehavior":2,"minutes":800,"fouls":null,"yellowCards":null,"redCards":null}
]
'@ | Set-Content $stats -Encoding utf8
$relative = '_pesquisa/comportamento/proposta-demo.json'
python -B tools/offline_catalog/behavior_v16.py --input $stats --root $root --output $relative

$targets = foreach ($index in 1..3) {
  @{
    catalogId = 'demo-v11-1998'; clubId = 'demo-v11'; country = 'BR'; year = 1998
    playerId = ('demo-atleta-{0:D3}' -f $index)
    position = $(if ($index -eq 1) { 'G' } else { 'D' })
    target = 'brasil/1998/clubes/clube-sintetico.json'
    sources = @(@{ name = 'Acervo sintético'; reference = 'ficha-demo-1998'
                  referenceDate = '1998-07-01'; collectedAt = $null })
    sourceConflict = $null
  }
}
$evidence = Join-Path $demo 'evidencia.json'
@{
  version = 'behavior-approval-1.0'
  proposal = @{ path = $relative; sha256 = (Get-FileHash (Join-Path $root $relative)).Hash.ToLowerInvariant() }
  players = @($targets)
} | ConvertTo-Json -Depth 8 | Set-Content $evidence -Encoding utf8
$record = python -B $tool @operator register --input $evidence | ConvertFrom-Json
python -B $tool --root $root list

$review = Join-Path $demo 'revisao.json'
@{
  version = 'behavior-approval-1.0'
  selected = @(
    @{ suggestionId = $record.suggestionIds[0]; action = 'ACCEPT'
       reason = 'Amostra sintética conferida para o exercício.'; replaceCatalogBehavior = $true },
    @{ suggestionId = $record.suggestionIds[1]; action = 'MANUAL'; behavior = 3
       reason = 'Escolha manual no exemplo sintético.'; replaceCatalogBehavior = $true }
  )
} | ConvertTo-Json -Depth 8 | Set-Content $review -Encoding utf8
python -B $tool @operator review --input $review
$decisions = python -B $tool @operator review --input $review --confirm | ConvertFrom-Json
$ids = @('--approval-id', $decisions.decisions[0].decisionId, '--approval-id', $decisions.decisions[1].decisionId)
python -B $tool @operator apply @ids --dry-run
python -B $tool @operator apply @ids --confirm --validator-dll $dll
python -B $tool @operator apply @ids --dry-run
python -B $tool --root $root history
```

O terceiro atleta fica fora do lote. Para uma revisão individual, use apenas uma entrada em `selected`. Para rejeitar ou adiar, use `REJECT` ou `DEFER` com justificativa e omita `behavior`. Para testar pesquisa manual sem estatísticas, use `proposal = $null` e `sources = @()` no registro de um atleta e faça revisão individual `MANUAL` com a pesquisa descrita em `reason`. Se a nova decisão substituir uma aprovação do mesmo atleta, use o ID ativo mostrado em `list` no campo `supersedes`.

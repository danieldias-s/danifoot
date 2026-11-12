# Classificador auxiliar de comportamento — offline

Ferramenta: `tools/offline_catalog/behavior_classifier.py`, Python 3.12+ e biblioteca padrão; não exige instalação de pacotes, rede, API ou banco. Nenhum módulo do jogo a importa. Configuração editável: `tools/offline_catalog/behavior-config-1.0.json`. Todos os dados e números usados nos testes são sintéticos.

## Entrada e escopo

CSV com cabeçalho ou JSON contendo array de registros. Campos desconhecidos, propriedades/cabeçalhos duplicados, números negativos, números fracionários nas contagens, booleanos como números e registros idênticos são rejeitados. Limites: 50 MB e 100.000 registros; contagens até 2.147.483.647. Nenhuma coleta automática é realizada.

Registro completo didático:

```json
[
  {
    "playerCatalogId": "demo-atleta-001",
    "club": "demo-v11",
    "country": "BR",
    "year": 1998,
    "position": "G",
    "competition": "competicao-sintetica",
    "minutes": 1800,
    "matches": 20,
    "yellowCards": 1,
    "redCards": 0,
    "fouls": 4,
    "duels": 70,
    "duelsWon": 40,
    "source": "fonte-sintetica-mesma-metodologia",
    "collectedAt": "2026-11-12"
  }
]
```

Os oito campos de identidade/escopo são obrigatórios: `playerCatalogId`, `club` (catalogClubId), `country`, `year`, `position`, `competition`, `source`, `collectedAt`. IDs são estáveis; posição G/D/M/A. A data de coleta usa AAAA-MM-DD, pode ser posterior ao ano histórico e nunca substitui a temporada analisada. A origem identifica o recorte/metodologia; o programa não comprova a veracidade da fonte. O país/ano da execução deve corresponder ao manifest local. Estatísticas são inteiros não negativos ou null. Em CSV, célula vazia significa desconhecida. **Zero conhecido é diferente de null.** Não use 0 para preencher lacunas. `duelsWon`, opcional, não pode exceder `duels` quando ambos são conhecidos.

## Cálculos reproduzíveis

Para minutos positivos: `métrica90 = contagem × 90 / minutes`. São exportados `yellow90`, `red90`, `fouls90`, `duels90`, `duelsWon90` e `cards90`. Falta de minutos/eventos deixa a taxa null. A entrada também aceita taxas já documentadas: `yellowCardsPer90`, `redCardsPer90`, `foulsPer90`, `duelsPer90`, `duelsWonPer90` (números finitos não negativos). Elas não são convertidas em contagens inventadas; mínimos de minutos/partidas ainda se aplicam, e Sarrafeiro continua exigindo as contagens acumuladas. Se contagem e taxa forem fornecidas simultaneamente, devem ser consistentes; entradas contraditórias são rejeitadas para revisão.

`cards90 = yellow90 × yellowPoints + red90 × redPoints`. Valores iniciais: amarelo 1 e vermelho 3. Só há taxa ponderada se os dois números de cartões forem conhecidos.

Cada coorte compara **país + ano + posição + competição + source**, todos iguais. Não agrega temporadas, fontes, competições ou atletas duplicados. Atletas com minutos/partidas insuficientes não são referências da coorte. Cada métrica utiliza apenas valores conhecidos dessa coorte.

Percentil com empates na posição média: `(n_menores + (n_iguais − 1)/2)/(n_amostra − 1)`, limitado a 0..1; amostras menores que dois deixam o percentil null. Evento conhecido igual a zero recebe percentil zero (inclusive coorte toda zero), sem converter ausências. As amostras são ordenadas uma vez e consultadas por busca binária.

**Indisciplina:** `cardWeight × percentil(cards90) + foulWeight × percentil(fouls90)`. Pesos iniciais 0,65 e 0,35, soma obrigatória 1. Não renormaliza pesos para esconder métricas ausentes.

**Intensidade física:** percentil de `duels90` separado, com `fouls90` como evidência adicional nos perfis intensos. Duelos vencidos são exportados como contexto, sem peso obrigatório. Falta de duelos exige revisão; não se deduz automaticamente intensidade física pela quantidade de cartões. Não há inferência de violência ou qualidade técnica a partir dessas sugestões.

## Configuração inicial e interpretação

| Parâmetro | Padrão | Papel |
|---|---:|---|
| `version` | discipline-percentile-1.0 | Semântica do algoritmo |
| `minimumMinutes` / `minimumMatches` | 900 / 10 | Amostra individual mínima |
| `minimumCohort` | 10 | Mínimo conhecido em cada métrica |
| `cardWeight` / `foulWeight` | 0.65 / 0.35 | Pesos após normalização |
| `yellowPoints` / `redPoints` | 1 / 3 | Pontuação dos cartões |
| `cleanMaximum` | 0.25 | Indisciplina baixa |
| `lowPhysicalMaximum` | 0.25 | Intensidade baixa |
| `highMinimum` | 0.65 | Indisciplina/faltas/intensidade acima da referência |
| `veryHighMinimum` | 0.85 | Faltas e cartões elevados |
| `extremeMinimum` | 0.95 | Extremos consistentes de faltas/cartões |
| `extremeMinimumMinutes` / `extremeMinimumMatches` | 1800 / 20 | Amostra maior para extremo |
| `extremeMinimumCards` / `extremeMinimumFouls` | 6 / 20 | Evidência acumulada para extremo |

Esses limites são uma **configuração inicial para estudo**, não calibragem definitiva nem fórmula original do Elifoot. Os limiares podem ser ajustados no arquivo e são registrados integralmente e com hash em cada relatório. Uma mudança de semântica do algoritmo requer outra versão; mudanças de parâmetros conservam a versão mas mudam `configHash` e os resultados reproduzíveis.

| Código | Perfil | Critério inicial |
|---:|---|---|
| 0 | FAIR PLAY | Indisciplina baixa e intensidade acima do patamar baixo: disputa bolas com poucas punições |
| 1 | CORDEIRINHO | Indisciplina baixa **e** baixa intensidade física |
| 2 | CAVALHEIRO | Perfil intermediário/controlado, sem evidência conjunta para os perfis intensos |
| 3 | CANELEIRO | Indisciplina, faltas e intensidade atingem `highMinimum` |
| 4 | CACETEIRO | Critérios do 3, mais cartões e faltas em `veryHighMinimum` |
| 5 | SARRAFEIRO | Critérios intensos, cartões/faltas em `extremeMinimum`, amostra maior e mínimos acumulados |

As três primeiras categorias não são três faixas de cartões. Os perfis 3–5 combinam dimensões e evidências; um vermelho isolado não atende o mínimo de cartões para 5. A ferramenta não identifica faltas duras diretamente: essa limitação exige revisão humana e fontes adicionais quando disponíveis. Com dados insuficientes, uma eventual sugestão é apenas candidata e não uma certeza.

## Qualidade, revisão e aplicação

- `SUGERIDO`: todas as métricas principais conhecidas e amostras suficientes; confiança **MODERADA**, jamais certeza automática.
- `REVISAO`: pouco tempo/partidas, coorte pequena, estatística ausente ou mais de um recorte do atleta no mesmo país/ano. Não soma fontes divergentes, nem duplica eventos. Confiança **BAIXA**, com motivos.
- `SEM_DADOS`: minutos ausentes/zero ou ausência de todas as contagens de eventos. Sem comportamento sugerido e sem padrão Fair Play.
- `MANUAL`: operador aprovou um comportamento 0..5 com justificativa explícita; a decisão pode divergir da sugestão. A evidência estatística e seus motivos permanecem preservados.

Relatórios ficam em `catalogos/_pesquisa/comportamento/<slug>/<ano>/`. Cada resultado guarda `rowId`, `statistics` originais, taxas, `cohort`, `sampleSizes`, `percentiles`, `indiscipline`, `physicalIntensity`, `suggestedBehavior`, `suggestedName`, `status`, `confidence`, `explanation`, `manualDecision`. O envelope contém `classifierVersion`, `config`, `configHash`, `inputHash`, `scope`. A revisão guarda status MANUAL, aprovação, comportamento, justificativa, caminho do clube anual e SHA-256 do alvo. Nomes de arquivo por hash preservam reproduzibilidade; não sobrescrevem pesquisa anterior. A ordem da entrada não muda os resultados.

Comandos na raiz do repositório (substitua caminhos/IDs pelos arquivos preparados pelo operador):

```powershell
python tools/offline_catalog/behavior_classifier.py classify --input C:/dados/estatisticas.json --config tools/offline_catalog/behavior-config-1.0.json --root D:/elifoot_98/catalogos --country BR --slug brasil --year 1998

python tools/offline_catalog/behavior_classifier.py review --root D:/elifoot_98/catalogos --report _pesquisa/comportamento/brasil/1998/HASH.json --row-id ROW_ID --behavior 2 --justification "Fonte e recorte revisados pelo operador" --target brasil/1998/clubes/clube.json

python tools/offline_catalog/behavior_classifier.py apply --root D:/elifoot_98/catalogos --report _pesquisa/comportamento/brasil/1998/HASH.review.json --row-id ROW_ID
```

`classify` apenas lê estatísticas e exporta sugestões. `review` cria outro relatório com decisão explícita, sem alterar catálogo. **Somente `apply` escreve no catálogo.** A aplicação exige evidência reproduzível e aprovação justificada, versão 1.1, correspondência de jogador/clube/posição/país/ano, referência no manifest e arquivo igual ao hash aprovado. Mudar outro campo após a revisão exige nova aprovação. O único campo de jogo alterado é `players[ID].behavior`; nunca estrela, força, identidade, outro ano, status READY ou save. A ferramenta rejeita força individual no alvo 1.1.

A aplicação cria backup integral dos bytes anteriores, escreve temporário na mesma pasta, verifica alteração concorrente e substitui o arquivo. Registra recibo da aplicação na pesquisa. Repetir a mesma aplicação reconhece `JA_APLICADO` somente quando o restante corresponde ao backup original; uma alteração alheia não é tratada como repetição válida. Não é um editor completo: após preencher ou aplicar decisões, execute **também** `offline-catalog-validate` para a validação integral do catálogo .NET. A ferramenta permite completar um DRAFT, sem promover rascunhos automaticamente.

O classificador não entra no runtime, não altera IA, A/M, força, partidas, economia, mercado ou contratos. `_pesquisa` é ignorada pelo loader. A aprovação manual é local, deliberada e auditável, sem alteração automática de temporadas anteriores.

## Testes

```powershell
python -m unittest discover -s tools/offline_catalog/tests -v
```

Cobrem normalização, pesos/configuração, seis perfis, fontes/coortes, nulos versus zero, amostra e incerteza, CSV/JSON, reprodutibilidade, revisão com justificativa, alvo/hash, caminhos, aplicação única, idempotência e CLI completa. Contagens efetivamente executadas no relatório de resultado 1.1.

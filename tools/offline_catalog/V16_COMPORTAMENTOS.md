# Classificação experimental V1.6

Ferramenta independente: `behavior_v16.py`. O classificador anterior `behavior_classifier.py` e sua configuração 1.0 permanecem disponíveis, sem mudança de fórmula ou aplicação.

Entrada: lista JSON de amostras com `playerCatalogId` (único), `position` G/D/M/A, `initialDivision` 1..5, `comparisonGroup` (identifica período, competição e fontes comparáveis), `baseBehavior` 0..5 ou null, `minutes`, `fouls`, `yellowCards`, `redCards`. Estatísticas faltantes são null; zero é um valor informado. Somente com pelo menos 900 minutos e todos os totais presentes há sugestão automática. O operador deve documentar as fontes; o programa não as verifica externamente.

Índice padrão: `(fouls + 2*yellowCards + 5*redCards)*90/minutes`. `--config` permite configuração versionada com `minimumMinutes` >=900, `weights` e `targets`. Metas por tipos 0..5: D1/D2 `[20,20,25,20,10,5]`, D3/D4 `[15,15,20,25,15,10]`, distrital `[10,10,15,30,20,15]`. Os grupos são posição + faixa de divisão inicial + comparisonGroup, atravessando clubes. Não são quotas por clube. Empates recebem o percentil médio e o mesmo tipo; grupos pequenos e empates podem afastar o resultado das metas. Nenhuma identidade desempata artificialmente o índice.

Saída: `historicalStatistics`, `index`, `percentile`, `baseBehavior` preservado e `effectiveBehaviorProposal`, rotulado `GAMEPLAY_ESTIMATE`. Menos minutos/dados => REVISAO e proposta null. A saída exige revisão, fica somente sob `_pesquisa/`, não sobrescreve arquivo existente e não é lida pelo jogo.

Exemplo de comando (a entrada precisa existir, não há dados reais fabricados):

```powershell
python -B tools/offline_catalog/behavior_v16.py --input amostras-revisadas.json --root catalogos --output _pesquisa/comportamento/proposta-v16.json
```

## Revisão e aplicação no catálogo

O contrato V16 permite ao operador registrar o relatório experimental como evidência, revisar individualmente ou em lote e aplicar decisões confirmadas ao JSON anual V1.1. A divisão informada ao classificador faz parte do recorte experimental. O criador de mundos usa o `behavior` que já consta no JSON validado e congela esse valor na prévia e no save.

O passo a passo de revisão, pesquisa manual, histórico e aplicação com dry-run está no [contrato de aprovação e proveniência](../../docs/catalogos/ELIFOOT98_V16_APROVACAO_PROVENIENCIA.md).

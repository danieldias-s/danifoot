# Editor Integral e Importador/Exportador de Pacotes (`WorldEditorAndPackageService`)

O módulo de edição em `src/Elifoot.Domain/WorldEditorAndPackageService.cs` permite customizar clubes, jogadores, treinadores de IA e exportar/importar mundos completos em **JSON** e **CSV**.

## 1. Regras de Validação (`ValidatePackage`)

Antes de criar um novo mundo a partir de um pacote editado, o validador verifica:
1. **IDs Únicos**: nenhum ID duplicado entre clubes, atletas ou treinadores de IA.
2. **Limites de Elenco por Clube**:
   - Mínimo de **11 jogadores** e máximo de **24 jogadores**.
   - Pelo menos **1 goleiro (`G`)** e **10 jogadores de linha (`D/M/A`)** em cada clube.
3. **Atributos de Jogadores**:
   - Posição em `{G, D, M, A}`.
   - Força inteira estritamente em `1..50`.
   - Comportamento em `{FairPlay, Cordeirinho, Cavalheiro, Caneleiro, Caceteiro, Sarrafeiro}`.
   - Indicador de craque `IsStar` (`*` literal no nome).
   - Salário, preço e contrato não negativos.
4. **Pesos Táticos dos Técnicos IA**:
   - Cada `AiManager` deve ter entre `1` e `3` formações preferidas válidas cuja soma de `WeightPercent` seja **exatamente `100%`**.

## 2. Importação/Exportação JSON e CSV com Preview de Diferenças

- `GET /api/worlds/{worldId}/editor/export-json`: exporta o pacote completo (`WorldPackageExportDto`).
- `GET /api/worlds/{worldId}/editor/export-csv`: exporta todos os atletas em formato CSV compatível com planilhas (`Id,ClubId,Name,Position,Strength,Behavior,IsStar,ContractRounds,SalaryPerRound,MarketPrice,PhotoUrl,PhotoLicense,PhotoCredit`).
- `POST /api/worlds/{worldId}/editor/validate-package`: valida o pacote e retorna `DiffPreview` listando clubes e atletas adicionados ou modificados em relação ao mundo base.
- `POST /api/worlds/editor/import-package`: instancia um **novo mundo isolado** a partir do pacote validado, garantindo que temporadas em andamento nunca sejam corrompidas.

## 3. Política de Imagens e Licenciamento

- O jogo **não realiza scraping de sites de terceiros (como Transfermarkt)**.
- Fotos de jogadores são opcionais (`PhotoUrl`), acompanhadas de `PhotoLicense` e `PhotoCredit`, e são exibidas **exclusivamente no modal de detalhe do jogador** com fallback textual/ícone, mantendo as tabelas densas 100% textuais e rápidas.

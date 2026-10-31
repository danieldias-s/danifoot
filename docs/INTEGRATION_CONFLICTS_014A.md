# Merge e compatibilidade

STATUS: PASS

Merge no worktree independente: base 7a00bdd, origem 47608e9, ancestral 50e9c12, --no-commit --no-ff. Zero conflitos textuais. Sem escolha global ours/theirs ou substituição manual de arquivos inteiros.

| Área | Resolução |
|---|---|
| Entities.cs | GameWorld partial da 013A; campos econômicos 012D preservados. |
| Dtos.cs / Enums.cs | Origem/categoria opcionais; modo de força e proteção salarial nos pacotes; enums esportivos intactos. |
| MarketAndEconomyEngine / AiManagerBrain | Implementação 012D integralmente mantida. |
| ClubAndMarketApplicationService / MultiplayerRoundOrchestrator | Sincronização, autorização e idempotência 012D preservadas. |
| WorldSeedFactory / WorldCreatorEngine | Fábrica 012D e criador 013B preservados; fundação validada semanticamente. |
| WorldEditorAndPackageService | Corrigidos modo clássico, proteção contratual, regras comerciais e registro de saldo inicial ao recriar pacote. |
| EfWorldRepository | 012D preservada; origem/categoria no JSON aditivo. |
| Program.cs | Rotas do criador e guards de exportação integrados às finanças. |
| main.dart / api_client.dart | Criador/proveniência com salário real e banco 012D. |
| Importadores | Alvos 014A explícitos, sem fallback ao banco original; relatórios próprios. |

Defeito confirmado: o pacote anterior perdia ForceEvolutionMode e NegotiatedContractProtected; sua recriação usava regras comerciais legadas e não registrava o saldo inicial. Novos campos opcionais preservam o estado exportado. Pacotes antigos de catálogo sem modo recuperam ClassicAdapted, conforme a invariável da 013A; genéricos mantêm LegacySeasonal. Contratos vigentes sem o campo de proteção são preservados; campos explícitos continuam respeitados. Novos mundos recebem a economia atual. Nenhuma fórmula de motor mudou.

Ajustes de ensaio: renovação mínima de cinco partidas respeitada; PostgreSQL novo por execução evita tentar baixar versão de catálogo atualizado. Saves históricos reutilizam um convite e foram testados em bancos distintos preservando o convite e a restrição única. Logs intermediários ficam separados dos resultados finais.

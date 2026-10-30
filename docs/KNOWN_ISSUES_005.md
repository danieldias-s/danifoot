# Problemas Conhecidos e Limitações Documentadas (`KNOWN_ISSUES_005.md`)

## 1. Ambiente de Emulação Android (Bloqueio Externo de Ambiente)
- **Status**: Bloqueado por ausência/instabilidade do Android SDK no host Windows (`Unable to locate Android SDK` em `flutter doctor`).
- **Ação na Tarefa `005`**: Conforme diretriz expressa da especificação (`NÃO trabalhar novamente na investigação Android`), nenhuma alteração ou tentativa de emulação Android foi realizada. O código Flutter compartilha 100% da camada de domínio/cliente e permanece validado em **Flutter Web (Release)** e testes de widget.

## 2. Escopo de Retenção em Memória durante Simulações Longas (100+ Temporadas)
- **Observação**: Em execuções contínuas de 100 temporadas (35.100 partidas e ~600.000 transações financeiras por experimento), manter todas as transações e súmulas históricas em listas em memória causaria crescimento linear de RAM.
- **Mitigação Aplicada**:
  - Adicionado `HashSet<string> AppliedTransactionKeys` em [`GameWorld`](file:///d:/elifoot_98/src/Elifoot.Domain/Entities.cs#L40-L41) para verificação de idempotência financeira em $O(1)$.
  - Ao final de cada temporada em [`CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance`](file:///d:/elifoot_98/src/Elifoot.Domain/CompetitionAndSeasonEngine.cs#L604-L613), registros transitórios (`Fixtures`, `Transactions`, `Auctions` encerrados, `TransferOffers` concluídas e `PauseWindows` fechadas) anteriores à temporada imediatamente precedente (`SeasonNumber - 1`) são podados da memória ativa após a consolidação de `SeasonHistories`.

## 3. Problemas de Gameplay Identificados e Resolvidos na Tarefa `005`
Todos os 4 bugs funcionais (`BUG-001` a `BUG-004`) e os 2 pontos de balanceamento (`BALANCEAMENTO-001` e `BALANCEAMENTO-002`) descobertos durante a auditoria de 300 temporadas com o motor real foram integralmente corrigidos e cobertos por testes automatizados de regressão:
- `BUG-001` (Suspensão por 3º cartão amarelo limpa na mesma rodada): **Resolvido**.
- `BUG-002` (Cobrança salarial em rodadas preliminares inativas e ausência de cota visitante na Taça): **Resolvido**.
- `BUG-003` (Limiar de força para contratação de reservas pela IA): **Resolvido**.
- `BUG-004` (Escalação de 2º/3º goleiro na linha em situações de múltiplos desfalques): **Resolvido**.

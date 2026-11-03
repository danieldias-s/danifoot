# Pendências conhecidas da 017B

- O catálogo DEV masculino fornece clubes reais com elencos simulados autorizados para o campeonato exportado. O caso de um atleta real com vínculo comprovado foi exercitado em teste com dados controlados.
- A edição de montagens ainda permite preencher o campo de força do rascunho. A confirmação de um campeonato recalcula a força dos clubes de liga pela 017B e conserva os salários e prazos do rascunho. A interface do editor ainda exibe o campo original.
- O Flutter Analyzer da base registra `unnecessary_const` em `live_match_view.dart:241`. A compilação web e os 276 testes Flutter passaram.

# 🗺️ Roadmap de Funcionalidades

> Legenda: `🟢 Fácil` `🟡 Médio` `🔴 Difícil` `⭐ Prioridade`

---

## 📦 Sprint 1 — Economia & MongoDB

- [X] `⭐🟢` **Sistema de Economia**
  - [ ] `macaco daily` — reivindicar moedas diárias (cooldown 24h)
  - [ ] `macaco saldo` ou `macaco coins` — ver saldo
  - [ ] `macaco bet <quantia>` — apostar 50/50 (cara ou coroa)
  - [ ] `macaco pagar @user <quantia>` — transferir moedas
  - [ ] `macaco ranking` — ranking de riqueza do servidor
  - **Arquivos afetados:** `Data/Repository/UserRepository.cs`, `Commands/EconomyModule.cs`

- [X] `⭐🟢` **Sistema de GIFs no MongoDB**
  - [ ] Criar `Entity/Gif.cs` (nome, url, categoria, addedBy)
  - [ ] `macaco gif add <nome> <url>` — adicionar GIF
  - [ ] `macaco gif <nome>` — buscar e enviar GIF por nome
  - [ ] `macaco gif random` — GIF aleatório
  - **Arquivos afetados:** `Commands/GifModule.cs`, `Data/Repository/`

---

## 📦 Sprint 2 — APIs & Integrações

- [ ] `⭐🟡` **Slash Commands**
  - [ ] Adicionar `Discord.Net.Interactions` (já incluso no meta-package)
  - [ ] Configurar `InteractionService` no `DiscordBotService`
  - [ ] Registrar comandos globalmente no `ReadyAsync`
  - [ ] Migrar gradualmente comandos de texto para `/comando`
  - **Arquivo de exemplo:** `Commands/Slash/GeneralSlashModule.cs`

- [X] `🟢` **Previsão do Tempo**
  - [ ] Criar `Services/Interfaces/IWeatherService.cs`
  - [ ] Criar `Services/WeatherService.cs` (OpenWeatherMap API via `IHttpClientFactory`)
  - [ ] Implementar `macaco tempo <cidade>` — temperatura, clima, umidade
  - **Arquivos afetados:** `Commands/ApiModule.cs`, `Services/`

- [ ] `🟡` **Integração com IA (OpenAI / Gemini)**
  - [ ] Criar `Services/Interfaces/IChatService.cs`
  - [ ] Criar `Services/ChatService.cs` (API de chat com personalidade de gorila)
  - [ ] `macaco perguntar <texto>` — resposta inteligente
  - **Arquivos afetados:** `Commands/FunModule.cs`, `Services/`

---

## 📦 Sprint 3 — Moderação & Utilidades

- [ ] `🟡` **Sistema de Warns**
  - [ ] Criar `Entity/Warn.cs` (userId, moderatorId, motivo, data)
  - [ ] `macaco warn @user <motivo>` — aplicar warn
  - [ ] `macaco warns @user` — listar warns
  - [ ] `macaco unwarn @user <id>` — remover warn
  - **Arquivos afetados:** `Commands/ModerationModule.cs`, `Data/Repository/`

- [ ] `🟡` **Sistema de Níveis/XP**
  - [ ] Acumular XP por mensagem no `DiscordBotService.HandleCommandAsync`
  - [ ] `macaco level` — seu nível e progresso
  - [ ] `macaco rank` — ranking do servidor
  - **Arquivos afetados:** `DiscordBotService.cs`, `Commands/UtilityModule.cs`

- [ ] `🟢` **Sistema de Piadas**
  - [ ] Popular coleção `jokes` no MongoDB com comandos seed
  - [ ] `macaco piada` — piada aleatória
  - [ ] `macaco piada add <texto>` — adicionar piada
  - **Arquivos afetados:** `Commands/FunModule.cs`, `Entity/Joke.cs`

---

## 📦 Sprint 4 — Experiência do Servidor

- [ ] `🟡` **Sistema de Ticket/Suporte**
  - [ ] `macaco ticket [motivo]` — abre canal privado
  - [ ] Bot cria canal, envia mensagem embed
  - [ ] `macaco fechar` — staff fecha o ticket
  - **Arquivos afetados:** `Commands/TicketModule.cs`, `Services/TicketService.cs`

- [ ] `🔴` **Música (Lavalink)**
  - [ ] Adicionar pacote `Lavalink4NET` ou `Victoria`
  - [ ] Configurar servidor Lavalink (docker-compose)
  - [ ] `macaco play <url>` — tocar música
  - [ ] `macaco skip`, `macaco queue`, `macaco stop`, `macaco pause`
  - **Arquivos afetados:** `Commands/MusicModule.cs`, `Services/MusicService.cs`

- [ ] `🟡` **Welcomes & Goodbyes**
  - [ ] Escutar eventos `UserJoined` / `UserLeft`
  - [ ] Canal configurável por servidor via comando
  - [ ] Mensagem personalizada com embed
  - **Arquivos afetados:** `DiscordBotService.cs`, `Commands/GuildModule.cs`

---

## 📦 Sprint 5 — Qualidade & DevOps

- [ ] `🟢` **Comando `ajuda` dinâmico**
  - [ ] Gerar help automaticamente via `CommandService.Commands`
  - [ ] Agrupar por módulo com embed bonito
  - **Arquivos afetados:** `Commands/UtilityModule.cs`

- [ ] `🟡` **Testes Unitários**
  - [ ] Criar projeto de teste `GorillazDiscordBot.Tests`
  - [ ] Testar serviços (F1, Cotação) com mock de HttpClient
  - [ ] Testar módulos de comando com contexto mockado

- [ ] `🟢` **Logging Estruturado**
  - [ ] Configurar `Serilog` ou `ILogger` para arquivo + console
  - [ ] Logs com nível (Info, Warn, Error)

---

## 📋 Checklist de Arquitetura Futura

- [ ] Adicionar prefixo configurável por servidor (salvar no MongoDB)
- [ ] Adicionar cooldown por comando
- [ ] Adicionar `[RequireUserPermission]` nos comandos de moderação
- [ ] Separar GIFs NSFW em módulo com restrição de canal
- [ ] Docker: garantir que o bot espere o MongoDB iniciar (healthcheck)

---

> **Como usar:** Marque `[x]` quando implementar. Os itens com `⭐` são os de maior retorno com menor esforço.

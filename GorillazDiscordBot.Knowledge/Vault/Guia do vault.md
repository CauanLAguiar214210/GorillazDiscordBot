---
tags:
  - vault
  - guia
tipo: nota
atualizado: 2026-10-10
---

# Guia do vault

Padrões de organização da base de conhecimento (`GorillazDiscordBot.Knowledge/`). Siga ao criar ou mover notas.

[[Home|Home]] · [[MOCs/Vault|← Vault]]

## Estrutura

- A **raiz contém apenas** `Home.md` (índice geral).
- Os **MOCs de área** ficam em `MOCs/` (`MOCs/Área.md`), um por área.
- Cada área tem uma pasta `Área/` com suas notas; o índice correspondente é `MOCs/Área.md`.
- Notas transversais ao vault ficam em `Vault/`.
- Nunca deixe nota solta na raiz: encaixe na área correspondente.

## Nomenclatura

- Arquivos em **Title Case**, com acentos, sem sufixos redundantes (`Assunto.md`).
- O MOC tem o mesmo nome da pasta de área (`Arquitetura/` ↔ `MOCs/Arquitetura.md`).
- Notas datadas com prefixo ISO: `Diário/YYYY-MM-DD.md`, `Reuniões/YYYY-MM-DD <título>.md`.
- Evite ` e ` no nome do arquivo.

## Links

- Cross-área sempre com caminho completo e rótulo: `[[Arquitetura/Backend|Backend]]`.
- Link curto `[[Nota]]` só quando o nome é único no vault.
- Toda nota começa com a barra: `[[Home|Home]] · [[Área|← Área]]` (+ irmãos relevantes).

## Frontmatter

```yaml
---
tags:
  - arquitetura        # 1 tag de área (minúscula, sem acento)
  - banco-de-dados     # 0..n tags de contexto
tipo: nota             # moc | nota | adr | ideia | investigacao | glossario | diario | reuniao | template
status:                # só quando houver ciclo de vida (ver abaixo)
atualizado: 2026-10-10
---
```

- **Obrigatórios:** `tags` (lista) e `atualizado` (`YYYY-MM-DD`).
- **Recomendados:** `tipo` e `status` (quando aplicável).
- Atualize `atualizado` sempre que o conteúdo mudar.

## Status (enum)

| Tipo | Valores |
| --- | --- |
| `adr` | `proposto` · `aceito` · `superado` |
| `ideia` | `ideia` · `priorizada` · `em-andamento` · `feita` · `descartada` |
| `investigacao` | `aberta` · `em-andamento` · `concluida` · `descartada` |

Sempre minúsculo, sem acento e com hífen em palavras compostas.

## Tags

- Uma tag de **área** por nota: `arquitetura`, `comandos`, `economia`, `loja`, `infra`, `operacao`, `integracao`, `decisoes`, `investigacao`, `ideias`, `glossario`, `projetos`, `desenvolvimento`, `agentes`, `reunioes`, `diario`, `vault`, `moc`.
- Contexto em tags **aninhadas** quando útil: `comandos/cassino`, `integracao/luckymonkey`.
- `moc` apenas nas notas-folha. Sem acentos; `kebab-case` em termos compostos.

## Templates

- Os templates ficam em `Templates/` (configurado no plugin core *Templates*); os MOCs de área em `MOCs/`.
- Todo template começa com o frontmatter acima (`tipo`, `status`, `atualizado: {{date}}`).

## Painéis

- Os índices usam **Bases** (nativo), definidos em `Vault/Painéis.base` e embutidos em [[Home|Home]] e [[MOCs/Vault|Vault]].
- As listas manuais dos MOCs de catálogo (ex.: [[MOCs/Comandos|Comandos]]) são mantidas à mão; painéis cobrem Ideias, Investigações e ADRs.

## Ciclo de vida

- Nunca apague uma nota: mude o `status` (`feita`, `concluida`, `superado`, `descartada`).
- Ideia que vira decisão estrutural gera uma [[MOCs/Decisões|ADR]]; ADR superado gera nova ADR que aponta para a anterior.

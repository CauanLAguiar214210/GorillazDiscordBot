---
tags:
  - dataview
  - painel
atualizado: 2026-09-19
---

# Dataview

[[Home|Home]]

**Dataview** é um plugin da comunidade que executa consultas sobre as notas (frontmatter, tags, links). Não está instalado neste vault — as consultas abaixo ativam depois de instalá-lo.

## Instalar

1. Settings → **Community plugins** → *Turn on community plugins* → *Browse*.
2. Buscar **Dataview** → *Install* → *Enable*.
3. (Opcional) Habilitar *Dataview JS* e datas inline.

## Consultas úteis

### Painel de status do vault

````text
```dataview
TABLE status, file.folder AS Pasta
FROM #ideias
WHERE status
SORT status ASC
```
````

````text
```dataview
TABLE status, atualizado
FROM #investigacao
SORT atualizado DESC
```
````

### Notas por área

````text
```dataview
LIST
FROM #moc
```
````

### Notas recentes

````text
```dataview
LIST
FROM ""
WHERE date(atualizado) >= date(today) - dur(14 days)
SORT atualizado DESC
LIMIT 20
```
````

> As datas usam o campo `atualizado: YYYY-MM-DD` do frontmatter. Mantenha esse campo em dia para o painel ficar correto. Ver [[Home|Home]] ("Painel (Dataview)").
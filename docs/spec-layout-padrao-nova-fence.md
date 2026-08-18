# Spec — Layout padrão para novas fences

> Recorte: o utilizador personaliza cores e alinhamento do título, aplica às fences actuais se quiser, e **grava essa aparência como padrão**. Daí em diante, **Nova fence** nasce com esse visual.
>
> **Status:** implementado no código (`v0.6.3`); gate Windows 11 pendente.
>
> Fora: packs de tema, partilhar temas entre PCs, sincronizar o padrão com “Aplicar a todas” de forma automática.

---

## 1. Problema

Hoje cada fence guarda o próprio `Theme` e `TitleAlignment` no `layout.json` (schema v2). **Nova fence** (`FenceLayoutRules.PlaceNew`) copia só **tamanho** da última fence e força:

- `TitleAlignment = Left`
- tema omitido → `FenceTheme.Default()` (cores de fábrica do MVP 1)

“Aplicar a todas” pinta as fences **já abertas**. “Restaurar padrão” volta essas fences às cores de fábrica. Nenhum dos dois define o que uma fence **nova** vai herdar. O utilizador que gasta tempo no visual tem de reaplicar a cada “Nova fence”.

---

## 2. Decisão (encaixe no layout actual)

O sítio certo é o **documento**, não a última fence da lista:

| Conceito | Onde vive | Quando aplica |
|---|---|---|
| Aparência desta fence | `FenceState.Theme` + `FenceState.TitleAlignment` | Sempre (já existe) |
| Aparência de **novas** fences | `LayoutDocument.DefaultTheme` + `DefaultTitleAlignment` (opcionais, como `uiLanguage`) | Só em `PlaceNew` / `CreateDefault` |
| Pintar as que já existem | checkbox “Aplicar a todas” | Imediato, fences actuais |

Não se bumpa `version` (campos opcionais, JSON ignora o que falta). Layouts antigos: `null` → fábrica (`Left` + `FenceTheme.Default()`), igual ao comportamento actual.

**Porque não copiar da última fence:** a última pode ser uma excepção (uma fence “alerta” vermelha). O padrão é uma intenção explícita do utilizador, não um efeito colateral da ordem da lista.

**Porque não um ficheiro à parte:** um save já atómico; zero runtime; o recovery/snapshot da Fase 6 já serializa o `LayoutDocument`.

---

## 3. O que entra no padrão

Incluir (é o que o utilizador vê nas Settings de aparência):

- `FenceTheme` normalizado: Fill, Border, Header, Text (hex + alpha já clampados)
- `TitleAlignment` (`Left` / `Center`)

Não incluir (são geometria / conteúdo, não “layout visual”):

- posição, tamanho, monitor, título, itens, collapsed

`PlaceNew` continua a offsetar `X`/`Y` em +40 e a copiar `Width`/`Height` da última fence.

---

## 4. UI

Nas Settings, junto de “Aplicar a todas” / “Restaurar padrão”:

**Definir como padrão** — grava tema + alinhamento da **fence seleccionada** no documento. Um save. Tooltip: *Novas fences nascem com estas cores e este alinhamento. As que já existem não mudam.*

Comportamento:

1. Personalizar a seleccionada (ou aplicar a todas primeiro, se quiser o mesmo visual em todo o lado).
2. Clicar **Definir como padrão**.
3. **Nova fence** usa esse par tema+alinhamento.
4. Mudar o padrão mais tarde **não** reescreve fences antigas. Para isso continua a existir “Aplicar a todas”.

**Restaurar padrão** (botão actual) **não muda**. Continua a significar: cores de **fábrica** MVP 1 nas fences visadas (seleccionada ou todas). Não apaga o padrão de novas fences e **não** aplica o padrão do utilizador — aplica `FenceTheme.Default()`. Alinhamento do título: o botão actual só restaura cores; manter isso (não resetar alinhamento aqui) para não alargar o contrato.

Se o utilizador quiser voltar o padrão de **novas** fences à fábrica: **Definir como padrão** com uma fence já restaurada às cores MVP 1 e alinhamento à esquerda. Não é obrigatório um terceiro botão neste recorte.

---

## 5. Regras de `PlaceNew` / `CreateDefault`

Assinatura passa a receber o documento (ou um record `FenceAppearance`):

```
PlaceNew(existing, title, defaultAlignment, defaultTheme)
CreateDefault(title, defaultAlignment, defaultTheme)
```

- `defaultTheme` null ou omitido → `FenceTheme.Default()` (e pode omitir-se no JSON da fence nova, como hoje).
- `defaultAlignment` ausente → `Left`.
- `EnsureAtLeastOne` usa os mesmos defaults do documento quando a lista está vazia.

Core puro; sem Win32. Testes em `FenceLayoutRulesTests`.

---

## 6. Performance

Impacto **zero** em runtime: um objecto extra no JSON (~200 bytes), lido uma vez no `Start()` e escrito no clique. Sem timers, sem DWM, sem Shell.

---

## 7. Testes e gate

- `PlaceNew` sem defaults no documento → Left + tema de fábrica (regressão).
- `PlaceNew` com defaults → nova fence herda tema+alinhamento; tamanho ainda da última; posição offset.
- Gravar `defaultTheme` / `defaultTitleAlignment` e reler o JSON (omitir quando null).
- Mudar o padrão no documento **não** altera `Theme` das fences já na lista (teste de persistência, não de UI).

Gate Windows 11: definir padrão com título centrado e cor não-fábrica → Nova fence nasce assim; fence antiga intacta; Restaurar padrão na antiga volta às cores MVP 1 sem mexer no padrão de novas.

---

## 8. Fora

- Packs / ficheiros `.theme`.
- “Aplicar a todas” a gravar sozinho o padrão (tem de ser o clique explícito).
- Copiar o padrão para fences existentes sem “Aplicar a todas”.
- Incluir tamanho da fence no padrão (a última fence já define o tamanho).

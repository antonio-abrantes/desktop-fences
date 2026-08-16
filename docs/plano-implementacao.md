# Plano de implementação — DesktopFences

Fonte de verdade do **estado** e da **ordem das fases**. A spec descreve o produto; este arquivo descreve o que já existe e o que construir a seguir.

Convenção:

- `[x]` feito no repositório (agente) e/ou confirmado pelo desenvolvedor na sessão.
- `[ ]` pendente — **não implementar** sem o gate no `SESSION-HEADER.md` **e** pedido explícito.
- A Fase 1 abaixo está **fechada**. N fences, Configurações, cores por fence, Sobre, iniciar com o Windows (portable), mutex.
- A Fase 4 (snap) está **fechada** (validada no Windows 11). A Fase 5 (Explorer / DPI / Win+D) está **no código**; o gate do desenvolvedor no Windows 11 ainda está aberto. A Fase 6 de custódia transacional dos itens do Desktop está planejada, mas não implementada. O instalador passa a ser a Fase 7. Duplo clique no vazio do desktop e packs de tema estão fora deste ciclo.

---

## MVP 1 — fechado

**Objetivo:** uma fence útil no Windows 11, com ícones reais escondidos no Explorer e grade nossa por cima.

### Entregas

- [x] Docs de contexto: `AGENTS.md`, `SESSION-HEADER.md`, spec, este plano, ADRs
- [x] Solution em três projetos + testes de domínio (ocupância, reorder, paths, `LayoutStore`)
- [x] Native: achar ListView (Progman / WorkerW), ler nome+posição com memória remota; hide via store + registry
- [x] Core: modelos, hit-test, schema JSON, match de ícone por nome, `DesktopPaths`
- [x] Fence translúcida (`AllowsTransparency` + brush alfa, radius 8), atrás dos apps (`HWND_BOTTOM`), sem `GWL_HWNDPARENT` no Progman
- [x] Ícones extraídos via `SHGetFileInfo`; abrir com duplo clique
- [x] Drop inbound (desktop e Explorer): um ou vários ícones; ghost com +N; seta no lugar do “proibido”; soltar agrupa e esconde os ícones reais
- [x] Drop outbound: ghost + restore no desktop
- [x] Seleção / multi-seleção / reordenação na grade
- [x] Alça ⋮⋮ para mover; thumbs de resize ao vivo; faixa leste some quando há scrollbar
- [x] Recolher (▴ / duplo clique na barra vazia); rename só com duplo clique no texto; clique fora / LostFocus grava; Escape cancela; ellipsis no título
- [x] Scrollbar custom (fina, escura)
- [x] Persistência `%AppData%\DesktopFences\layout.json`
- [x] Bandeja Pausar / Retomar / Sair; ícone `Assets/app.ico` no exe, atalho e tray
- [x] Workflow de release **somente** em tags `v*`
- [x] Validação no Windows 11: inbound drop + ghost + cursor

**Critério de saída:** cumprido.

---

## Ciclo atual (pós-MVP 1)

### Ordem

| Fase | Bloco | Complexidade | Por que nesta ordem |
|---|---|---|---|
| **1** | Várias fences + settings (bandeja, criar/remover, alinhamento do título) | Média | Sem host de N janelas e sem UI de criar, nada do resto existe. O JSON já tem lista; a App ainda abre uma janela e **grava só ela**. |
| **2** | Idioma da UI: português e inglês | Baixa | Só texto da App. Não mexe em Native, hide/restore, drop nem no schema dos ícones. Quanto antes, menos string nova para extrair nas fases seguintes. |
| **3** | Arrastar item de uma fence para outra | Média | **Fechada** (validada no Windows 11). Reusa o ghost; N fences sem isso são ilhas. |
| **4** | Snap a bordas da tela e a outras fences | Média | **Fechada** (validada no Windows 11). Posicionamento livre estável primeiro (fase 1); ímã no soltar da alça. |
| **5** | Explorer reiniciado / DPI / Win+D | Média–alta | **No código** (gate Windows 11 pendente). Sobrevivência no Windows real. Piora com N fences. |
| **6** | Custódia transacional de itens do Desktop | Alta | Store por `ItemId`, JSON atômico/backup/recovery, transferência por metadados e lote. Um único gate; sem itens externos. |
| **7** | Instalador (path estável no arranque) | Baixa–média | Distribuição somente depois do gate de integridade. Ajustar o “iniciar com o Windows” para a pasta de instalação. Sem packs de tema. |

Não implementar a Fase 6 sem fechar o gate da 5 **e** pedido explícito. Não implementar a Fase 7 antes do gate final da 6.

### Fora deste ciclo (outra versão, se um dia)

**Empurrar a fence de baixo ao expandir** — fora do plano. Não é fase, não tem checklist, não entra na landing. Se for feito, é depois do ciclo atual, como recorte de outra versão, e só como pilha explícita (nunca física global).

**Duplo clique no vazio do desktop cria fence** — fora do plano. Settings continua o caminho de criar. Reavaliar mais tarde, só com validação e necessidade reais. O `WH_MOUSE_LL` já existe; o risco é criar fence ao clicar ícone ou ao abrir o menu do desktop.

**Packs de tema** — fora do plano. O vidro da fence fica travado. Sem packs nomeados neste ciclo.

---

## Fase 1 — várias fences + configurações

**Status:** fechada. Inclui N fences, Configurações, cores e iniciar com o Windows (portable).

**Objetivo:** N fences no desktop, cada uma com o comportamento do MVP 1. Sempre **pelo menos uma**. Criar e remover só nas configurações. Alinhamento do título (esquerda = padrão de hoje, ou centro) configurável por fence.

### O que o utilizador vê

Bandeja (WinForms `NotifyIcon`, como hoje):

```
Pausar / Retomar
────────
Configurações
────────
Sobre
Sair
```

- **Configurações** abre (ou foca) uma janela normal — não é fence, não vai para `HWND_BOTTOM`.
- **Sobre** abre a mesma janela do **?** nas Configurações (nome, versão da tag, autor, links).
- Duplo clique no ícone da bandeja, se não estiver pausado, também abre Configurações. Se estiver pausado, continua a Retomar.

Janela de configurações — tokens da fence, **não** o vidro layered no desktop:

- Fundo escuro `#0C0C12`, texto claro, acento gelo `#5B8DEF`, cantos 8 px, borda suave.
- Janela WPF normal (`ShowInTaskbar=true`), acima das apps, arrastável, um único exemplar (reabrir foca a que já existe).
- UI em português na Fase 1; a Fase 2 passou a pt/en.

Conteúdo:

1. Lista das fences (título).
2. **Nova fence** — sempre disponível. A nova nasce com título `Nova fence`, alinhamento à esquerda, tamanho padrão, posição deslocada (~40 DIP) para não coincidir com outra.
3. **Remover** — desativado quando só resta uma. Remover restaura os ícones reais **daquela** fence no desktop, fecha a janela, grava o JSON.
4. Alinhamento do título: **Esquerda** (padrão, depois da alça ⋮⋮) ou **Centro**. Checkbox **Aplicar a todas as fences** (desmarcado por padrão): se marcado, alinhamento e aparência valem para todas; se não, só para a selecionada.
5. Lista com altura máxima; se passar, scroll no mesmo estilo das fences. **?** no título abre Sobre.
6. **Iniciar com o Windows** — checkbox nas Configurações. Default desligado. Path = o `.exe` desta pasta; ao abrir o app, se o arranque estiver ligado, o atalho é atualizado (portable).

Fora da fase 1: idioma, snap, drag entre fences, temas, duplo clique no desktop, Explorer/DPI/Win+D, instalador.

### Decisões desta fase

| Tema | Decisão |
|---|---|
| Mínimo de fences | Sempre ≥ 1. JSON vazio na carga → cria a padrão. Não dá para apagar a última. |
| Onde criar/apagar | Só nas Settings. O menu da fence **não** apaga. |
| Menu “Fechar fence” | No MVP 1 pausava o app. Na fase 1 some desse menu (Recolher / Diagnóstico ficam). Pausar e Sair ficam na bandeja. |
| Persistência | Um `FenceHost` grava **todas** as fences. Hoje cada `FenceWindow.SaveLayout` **sobrescreve** o JSON com uma só — isso quebra N janelas e é o primeiro buraco a fechar. |
| Hide/restore | Por conjunto de itens da fence. Remover ou pausar uma não restaura as outras. Pausar na bandeja pausa **todas**. Sair restaura todas. |
| Z-order | Fences continuam `HWND_BOTTOM`. Settings é janela normal. |
| Título | Continua: duplo clique no texto para editar; Enter / clique fora grava; Escape cancela. Só muda o `HorizontalAlignment` (Left / Center) no `TitleDisplay` e no `TitleEdit`. |
| Visual da fence | Travado (alfa, radius, `AllowsTransparency`). Settings não mexe nisso. |
| Camadas | Core: enum + regra “≥ 1” + round-trip JSON. App: host, Settings, bandeja. Native: sem API nova nesta fase. |

### Schema (`layout.json`)

`version` permanece **1** (campo novo é opcional; ausente = `left`).

```json
{
  "id": "guid",
  "title": "Trabalho",
  "titleAlignment": "left",
  "x": 100, "y": 100, "width": 420, "height": 280,
  "collapsed": false,
  "items": []
}
```

`titleAlignment`: `"left"` | `"center"`. Core: enum `TitleAlignment { Left, Center }`.

### Arquitetura (ficheiros)

```
DesktopFences.Core
  Models/LayoutDocument.cs     ← TitleAlignment em FenceState
  Fences/FenceLayoutRules.cs   ← EnsureAtLeastOne, CanRemove, PlaceNew (novo)

DesktopFences.App
  FenceHost.cs                 ← N FenceWindow, load/save único, pause/resume/exit
  SettingsWindow.xaml/.cs      ← lista, nova, remover, alinhamento
  Services/TrayService.cs      ← item Configurações + Sobre
  AboutWindow.xaml/.cs         ← Sobre (ícone ?, bandeja)
  App.xaml.cs                  ← deixa de criar uma FenceWindow à mão
  FenceWindow.xaml/.cs         ← recebe estado/id do host; não grava o documento inteiro sozinha
```

Não copiar `FenceWindow.xaml.cs` N vezes — um tipo, N instâncias.

### Testes (Core)

- Round-trip de `titleAlignment`.
- JSON antigo sem o campo carrega como `Left`.
- `EnsureAtLeastOne`: lista vazia → uma fence padrão.
- `CanRemove` é falso quando `Count == 1`.
- `PlaceNew` devolve geometria deslocada da última (não igual a X/Y dela).

### Checklist (agente marca `[x]` só depois de implementar e testar no código)

- [x] `TitleAlignment` + persistência + testes
- [x] `FenceLayoutRules` (≥ 1, PlaceNew) + testes
- [x] `FenceHost`: instancia todas, save único, pause/resume/exit por conjunto
- [x] `FenceWindow` deixa de sobrescrever o JSON com uma fence
- [x] Título Left / Center aplicado na fence
- [x] `SettingsWindow` no visual escuro/gelo (lista, nova, remover, alinhamento)
- [x] Bandeja: Configurações e Sobre; duplo clique abre settings se não pausado
- [x] Sempre ≥ 1 fence; remover a última impossível
- [x] Remover fence restaura só os ícones dela
- [x] Menu da fence sem “Fechar fence”
- [x] README / spec / landing alinhados ao que a Fase 1 passou a fazer
- [x] `dotnet test` verde
- [x] Tema visível da fence (fundo 45–85%, borda, header 15–85%, texto + ⋮⋮/seta); restaurar padrão; drop highlight derivado da borda; checkbox aplicar a todas

**Gate do desenvolvedor (Windows 11 real):** fase fechada no código. Confirmar o toggle Iniciar com o Windows (Definições → Início; uma só instância; portable ao mover a pasta).

---

## Fecho da Fase 1 — iniciar com o Windows

**Status:** no código.

**Complexidade:** baixa. **Risco de quebrar hide/drop/tema:** baixo.

O Windows já expõe “iniciar com o sistema” por utilizador na chave:

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

- Sem administrador (`asInvoker` mantém-se).
- Sem Native, sem `DllImport`, sem instalador.
- Path = `Environment.ProcessPath` (o `.exe` que está a correr). Se o utilizador mover a pasta portable e abrir o app no sítio novo, o atalho é regravado.
- Respeita o desligar em Definições do Windows → Aplicações → Início (`StartupApproved`).
- Default **desligado**.
- Mutex `Local\DesktopFences.SingleInstance`: a segunda instância sai sem tocar nos ícones.

Na Fase 7 o instalador pode passar a um path estável; até lá o portable atualiza o Run key em cada arranque.

### Checklist

- [x] Mutex: segunda instância não abre fences
- [x] Checkbox nas Settings; default off; lê e escreve `HKCU\...\Run`
- [x] Valor com path entre aspas; nome `DesktopFences`; refresh do path se já estiver ligado
- [x] Desmarcar remove a chave; falha de registry não derruba o app
- [x] Não pede admin; não toca hide/drop/tema
- [x] `dotnet test` verde (agente)

**Gate do desenvolvedor:** marcar, confirmar em Definições do Windows → Início; desmarcar e confirmar que some; abrir o `.exe` duas vezes e ficar só uma instância; mover a pasta, abrir de novo, confirmar que o atalho aponta para o sítio novo.

---

## Fase 2 — idioma (português / inglês)

**Fechada.** Validada no Windows 11. Incluída no MVP 2 (`v0.3.1`).

**Objetivo:** a UI do app (bandeja, fences, Configurações, Sobre) fala português **ou** inglês. O português de hoje continua o default satélite. Sem NuGet. Sem mexer em Native.

### Viabilidade

Baixa complexidade, baixo risco de quebrar o produto:

- As strings visíveis cabem quase todas na App (XAML + bandeja + Sobre + menus da fence). São da ordem de **80–120** chaves, não milhares.
- `DesktopFences.Native` não mostra texto ao utilizador. `Core` só persiste dados; chaves JSON (`title`, `theme`, …) **não** se traduzem.
- WPF/.NET 8 já traz `.resx` + assemblies satélite (`Strings.resx` = pt, `Strings.en.resx` = en). O `Directory.Build.props` já tem `NeutralLanguage=pt-BR`.
- O publish self-contained da tag `v*` inclui satélites sem mudar o workflow.

O que **não** traduzir (senão corrompe dados ou o desktop):

- Título que o utilizador já gravou na fence.
- Nomes de ficheiros/ícones do Explorer.
- Comentários de código e docs do repositório.
- Landing (`docs/index.html`) — já está em inglês; fica fora desta fase.

Título padrão de fence **nova**: a App passa a string localizada para `PlaceNew` / `CreateDefault` (parâmetro opcional). Core **não** lê cultura. Títulos já no JSON ficam como estão. Testes do Core continuam a passar um literal estável (ex. `"Nova fence"`).

### O que o utilizador vê

Nas Configurações, um seletor **Idioma / Language**:

- **Sistema** (padrão) — `pt*` → português; resto → inglês.
- **Português**
- **Inglês**

Persistir em `layout.json` um campo opcional `uiLanguage`: `"system"` | `"pt"` | `"en"`. Ausente = `system`. `version` do documento **não** sobe.

Aplicar na hora: reconstruir o menu da bandeja, reatribuir textos das janelas já abertas (settings, sobre, tooltips/menus da fence, hint vazio). Não recriar fences nem re-esconder ícones.

### Arquitetura

```
DesktopFences.App
  Localization/Strings.resx      ← default pt
  Localization/Strings.en.resx
  Localization/UiLanguage.cs     ← resolve system/pt/en; sem Win32
  SettingsWindow                 ← combo Idioma
  TrayService                    ← itens do menu via resources
  FenceWindow / AboutWindow      ← bindings ou refresh ao mudar idioma

DesktopFences.Core
  Models/LayoutDocument.cs       ← UiLanguage opcional
  Fences/FenceLayoutRules.cs     ← PlaceNew(title) opcional; default estável p/ testes
```

Diagnóstico: traduzir também (é janela do app), sem profundidade extra.

### Testes (Core)

- JSON sem `uiLanguage` carrega como `system`.
- Round-trip `"pt"` / `"en"` / `"system"`.
- `PlaceNew` com título passado pelo chamador; sem título continua o literal de teste.

### Checklist (agente marca `[x]` só depois de implementar)

- [x] `.resx` pt + en; zero string de UI hardcoded na App
- [x] Seletor nas Settings; persistência opcional; default `system`
- [x] Bandeja, fence (menus/tooltips/hint), Settings, Sobre
- [x] Fence nova usa título do idioma atual; fences já gravadas intactas
- [x] Mudança de idioma não toca hide/restore, drop, clip, tema
- [x] `dotnet test` verde; publish ainda inclui satélites

**Gate do desenvolvedor:** `[x]` — Windows em pt e em en; seletor Sistema / PT / EN; título antigo não muda; Pausar/Sair/drop como hoje.

### Patch `v0.3.1` — ícones de sistema

Lixeira / Este computador / Rede não são ficheiros. `SHGetFileInfo` no path de disco continua a ser o caminho dos atalhos e pastas. Só se o path **não existir** é que a Native resolve o PIDL no `IShellFolder` do desktop. Abrir esses itens só com `::{CLSID}` / `shell:` — não substitui o `Process.Start` de um `.lnk`. Hide/restore e drop inalterados. Validado no Windows 11.

---

## Fase 3 — item de uma fence para outra

**Status:** fechada (validada no Windows 11).

Ghost já segue o cursor. No soltar, se o ponto está no **corpo** de outra fence (não recolhida), o item muda de dono: sai da lista de origem, entra na de destino na célula sob o cursor. O ficheiro **não** volta ao Desktop — move-se entre pastas do store. Soltar no desktop continua a ejetar. Soltar na barra de título ou numa fence recolhida não transfere nem devolve o ícone ao desktop.

Sem isto, várias fences são ilhas. Fora: snap.

### Checklist (agente marca `[x]` só depois de implementar)

- [x] Hit-test de domínio (`FenceItemDrop`): corpo = transferir; chrome/recolhida = ficar; desktop = ejetar
- [x] `FenceHost` move o bloco (multi-select) sem `Restore` / `Hide` extra
- [x] Destino acende a borda de drop enquanto o ghost está por cima
- [x] Inbound OLE do desktop não consome o arraste entre fences
- [x] `dotnet test` verde

**Gate do desenvolvedor:** `[x]` — duas fences; arrastar um e vários ícones da A para a B; o original continua escondido no desktop; Pausar/Sair ainda restaura; soltar no vazio do desktop ainda ejetar.

---

## Fase 4 — snap

**Status:** fechada (validada no Windows 11).

Ao soltar a alça ⋮⋮, e ao terminar o resize: ímã nas bordas da área de trabalho (`rcWork`, respeita a taskbar) e nas arestas de outras fences. Folga de captura: 12 DIP; alinhamento encostado (sem margem extra). Não cria pilha automática nem empurra vizinhos. Recolhida: só posição, não mexe na altura da barra.

### Checklist (agente marca `[x]` só depois de implementar)

- [x] Hit-test de domínio (`FenceSnap.Translate` / `Edges`) + testes
- [x] Native: `MonitorFromWindow` + `GetMonitorInfo` (`rcWork`); App converte pixels → DIP
- [x] Soltar a alça ⋮⋮ aplica o ímã; resize completed também
- [x] Vizinhos não se mexem; fence recolhida não estica a barra
- [x] `dotnet test` verde

**Gate do desenvolvedor:** `[x]` — arrastar uma fence até ~uma dúzia de pixels da borda da tela e de outra fence; soltar cola. Resize da aresta leste/sul perto de vizinho ou da tela cola. A outra fence não se desloca.

---

## Fase 5 — Explorer / DPI / Win+D

**Status:** no código. Gate do desenvolvedor pendente.

Reaplicar hide se o Explorer reiniciar (ficheiros já estão no store; reaplicar só CLSID). Hide: mover para `%LocalAppData%\DesktopFences\Items\{FenceId}` — **não** Hidden, **não** coordenadas. Namespace (Lixeira etc.) via registry. Sem COM `IFolderView` no arranque e sem loop de 1s. `PerMonitorV2` + clip no DPI; Win+D: bloquear minimize/`SWP_HIDEWINDOW`.

### Checklist (agente marca `[x]` só depois de implementar)

- [x] `DesktopHide` + `FenceItemStore` + testes (move vs CLSID vs none)
- [x] `ExplorerListViewGuard`: ListView morta → nova → `RebindAfterExplorer`
- [x] `WM_SYSCOMMAND` / `WINDOWPOSCHANGING` / `WM_SHOWWINDOW`: fence não some com Win+D
- [x] Hide via move para o store + registry (sem Hidden; sem LVM coordenadas; sem watchdog de 1s)
- [x] `DpiChanged`: clip + z-order; manifest `PerMonitorV2` intacto
- [x] `dotnet test` verde

**Gate do desenvolvedor:** `[ ]` — Task Manager → finalizar o Windows Explorer, deixar voltar; ícones das fences somem de novo no desktop. Mudar escala DPI. Win+D: fences continuam no ambiente de trabalho. Pausar/Sair restaura.

---

## Fase 6 — custódia transacional de itens do Desktop

**Não implementar agora.**

**Objetivo:** entregar em uma única fase os quatro blocos aprovados após a auditoria:

1. store estável por `ItemId`;
2. transação, JSON atômico, backup e recovery;
3. transferência entre fences somente por metadados;
4. processamento em lote.

O escopo é somente o fluxo de itens do Desktop do usuário/público. Itens externos, OneDrive/redirected Desktop, progresso/cancelamento e as demais recomendações da auditoria ficam em stand-by.

Fontes de verdade da fase:

- [spec-fase-6-custodia-desktop.md](spec-fase-6-custodia-desktop.md) — contrato, invariantes, schema v2, recovery e critérios de aceite;
- [plano-fase-6-custodia-desktop.md](plano-fase-6-custodia-desktop.md) — marcos 6.1–6.4, checklists, testes e gate.

### Checklist resumido

- [ ] 6.1 — schema v2, `ItemId`, store estável e migração recuperável do v1
- [ ] 6.2 — commit atômico, backup, journal e recovery no arranque
- [ ] 6.3 — transferência entre fences com zero I/O de payload
- [ ] 6.4 — pipeline único de lote, uma captura/save/notificação por gesto
- [ ] Testes automatizados e matriz Windows 11 registrados

**Gate do desenvolvedor:** `[ ]` — somente depois dos quatro marcos. Não há release parcial desta fase.

---

## Fase 7 — instalador (path estável no arranque)

**Não implementar antes do gate da Fase 6 e de pedido explícito.**

Instalador = distribuição (release com instalador, não só zip portable). **Iniciar com o Windows já existe** (fecho da Fase 1, chave `HKCU\...\Run` com o path do `.exe` atual). Nesta fase **há de se ajustar** essa inicialização: o instalador grava um path estável (pasta de instalação) em vez do portable, sem duplicar o valor Run, e repor o atalho na atualização. Sem packs de tema. O vidro da fence permanece travado.

---

## Ordem de leitura para uma sessão nova

1. `docs/SESSION-HEADER.md`
2. `AGENTS.md`
3. Este arquivo (estado + fase vigente)
4. `docs/SPEC.md` na seção da área que for mexer
5. `docs/pos-mvp1.md` — mapa curto do ciclo (sem o item de empurrar)

---

## Ideias em reserva (não implementar)

Reavaliar **depois** do ciclo atual (1–7), só com recorte planejado **e** validação explícita. Não são fase, não têm checklist, não entram no ciclo.

**Duplo clique no vazio do desktop cria fence.** Settings continua o caminho confiável. O hook de rato já existe; o risco é criar fence ao clicar ícone ou ao abrir o menu do desktop. Só com análise de necessidade real.

**Novo → Fence no Explorer** (`ShellNew\Command` ou verbo no fundo do desktop). Tem fundamento, complexidade média: mutex exige IPC se o app já estiver aberto; o ShellNew costuma deixar um ficheiro `.ext` no desktop; no Windows 11 o item pode ir só para “Mostrar mais opções”; `IExplorerCommand` é COM/instalador. Reutilizar `FenceHost.TryAddNew()`. Não implementar até estar planejada e validada.

**Packs de tema** nomeados. O vidro (alfa, radius, `AllowsTransparency`) fica travado até haver pedido e ADR.

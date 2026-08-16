# DesktopFences — Spec técnica

App Windows 11 que agrupa ícones **reais** da área de trabalho em fences retangulares, com fundo translúcido, cantos arredondados, arrastáveis e persistentes. Inspirado no Stardock Fences; **não afiliado**.

**Stack:** C# / .NET 8 / WPF + P/Invoke. Decisão em [ADR-0001](adr/0001-stack-csharp-wpf.md).

**Princípio:** três camadas, para o ponto frágil (Explorer) poder quebrar sem reescrever UI.

| Projeto | Responsabilidade | Pode conhecer Win32? |
|---|---|---|
| `DesktopFences.Core` | Modelos, hit-test, JSON, paths | Não |
| `DesktopFences.Native` | SysListView32, COM Shell, OLE drop, DWM, âncora | Sim, e só aqui |
| `DesktopFences.App` | XAML, fence, bandeja, ghost | Só via serviços Native |

**Estado:** MVP 2 (`v0.3.1`) + Fases 3–4 fechadas + Fase 5 no código (Explorer/DPI/Win+D; gate Windows 11 pendente). N fences, Configurações, hide/restore, arrastar entre fences, snap. Próxima: instalador (Fase 6), só com pedido. Duplo clique no desktop, packs de tema e empurrar vizinhos estão **fora** deste ciclo. Detalhe: [plano-implementacao.md](plano-implementacao.md).

---

## 1. Como o desktop realmente funciona

```
Progman ("Program Manager")
 └── SHELLDLL_DefView
      └── SysListView32 "FolderView"   ← ícones

-- ou, em várias builds / multi-monitor / wallpaper por tela --

WorkerW
 └── SHELLDLL_DefView
      └── SysListView32 "FolderView"
```

A busca tenta Progman primeiro e cai para `EnumWindows` procurando `SHELLDLL_DefView`. Detalhe em [ADR-0002](adr/0002-esconder-icones-reais.md).

| Mensagem | lParam | Cross-process |
|---|---|---|
| `LVM_GETITEMCOUNT` | — | SendMessage direto |
| `LVM_SETITEMPOSITION` | `MAKELPARAM(x,y)` | SendMessage direto (só reposicionar ícone **visível**, no ejetar) |
| `LVM_GETITEMPOSITION` | ponteiro para `POINT` | **alocar no explorer.exe** |
| `LVM_GETITEMTEXTW` | ponteiro para `LVITEM` + buffer | **alocar no explorer.exe** |

A Native aloca o `POINT` / `LVITEM` remoto. Não confiar em marshalling automático dessas mensagens.

Fluxo de hide:

1. Resolver o item: pasta Desktop (`DesktopPaths`) ou CLSID do namespace (`::{GUID}` / `shell:`).
2. Atalho, ficheiro ou pasta: **mover** para `%LocalAppData%\DesktopFences\Items\{FenceId}`. Lixeira / Este computador / Rede: registry `HideDesktopIcons` com o CLSID.
3. Persistir `path` (store) e `originalPath` (origem). Um `SHChangeNotify` no fim do lote.
4. Desenhar a grade WPF (`SHGetFileInfo` no path do store).
5. Restaurar (mover de volta / registry `0`) no shutdown, Pausar, ejetar, remover fence.

**Não** usar `FILE_ATTRIBUTE_HIDDEN` nem coordenadas no ListView. Itens ocultos no Explorer não voltam a pintar o que já não está na pasta Desktop.

---

## 2. Funcionalidades por área

### 2.1 Native — ícones

- Localizar `SysListView32` (Progman + WorkerW).
- Enumerar: índice, nome, posição. Um `OpenProcess` por varredura, não por ícone.
- Esconder: mover para o store do Fence / registry (ADR-0002). Reposicionar no ListView só depois de o ícone voltar ao Desktop.
- `SHGetFileInfo` para bitmap PNG.
- Resolver nome → path (`DesktopPaths`: Desktop do usuário + público).
- Reconectar após restart do Explorer — Fase 5.
- DPI: manifest Per-Monitor V2; clip/z-order no `DpiChanged`.

### 2.2 Native — chrome da janela

Ver [ADR-0003](adr/0003-ancoragem-desktop-e-acrylic.md).

- `AllowsTransparency=True`; vidro = brush alfa no `Border` (`#A80C0C12`), `CornerRadius=8`.
- **Não** usar `SetWindowCompositionAttribute` enquanto a janela for layered (cinza opaco no W11 testado).
- Z-order: `HWND_BOTTOM` + `WS_EX_TOOLWINDOW`. **Não** `GWL_HWNDPARENT` → Progman (isso impedia o OLE drop).
- Sem minimize/maximize box.

### 2.3 App — fence

Uma janela por fence: título editável (duplo clique **no texto**); Enter, LostFocus, clique fora do campo **grava**; Escape cancela. Alinhamento do título: esquerda (padrão) ou centro, nas Configurações — por fence, com checkbox para aplicar a todas (vale também para cores). Alça ⋮⋮ para mover; ao soltar (e ao terminar o resize), ímã nas bordas da área de trabalho e nas arestas de outras fences, sem empurrar vizinhos. Roll-up ▴; resize ao vivo; grade WrapPanel; scrollbar custom; menu de contexto (recolher, diagnóstico). Remover fence só nas Settings.

O `App` usa `FenceHost`: N instâncias de `FenceWindow`, um único `layout.json`. Aparência por fence (fundo, borda, header, texto + alfa); radius 8 e `AllowsTransparency` continuam fixos. Packs de tema nomeados estão fora deste ciclo. Grade: atalho/pasta/ficheiro → `SHGetFileInfo` no path (igual ao MVP 1). Só se **não** existir no disco (Lixeira, Este computador, Rede) é que se usa o PIDL do `IShellFolder` do desktop. Abrir esses itens só via `::{CLSID}` / `shell:` — não se substitui o `Process.Start` de um `.lnk`.

### 2.4 Drag & drop

O WPF `AllowDrop` **não** funciona de forma confiável nesta janela layered: o Explorer não acerta o `IDropTarget` (thumbnail OLE no hotspot) e pinta o cursor de proibido.

Contrato vigente — [ADR-0004](adr/0004-ole-inbound-drop.md):

- **Inbound:** `RegisterDragDrop` nativo + alvo OLE minúsculo no cursor; ghost WPF próprio (com `+N` se a seleção do desktop tiver vários ícones); cursor de “proibido” substituído pela seta só enquanto o ponteiro está sobre a fence. A seleção do `SysListView32` é lida no início do arraste (`LVM_GETNEXTITEM` / `LVNI_SELECTED`).
- **Outbound / reorder:** não usamos `DoDragDrop` do Explorer; hook `WH_MOUSE_LL` + `DragGhostWindow` (click-through).
- Soltar na fence esconde o ícone real e adiciona na grade; soltar fora restaura.
- Entre fences: soltar no **corpo** de outra fence muda o dono (JSON) e move o ficheiro entre pastas do store. Barra de título / fence recolhida não transfere nem ejetar.

### 2.5 Persistência

Arquivo único `%AppData%\DesktopFences\layout.json` (`LayoutStore`). Itens de fence em `%LocalAppData%\DesktopFences\Items\{FenceId}`. Lista de fences persistida pelo `FenceHost`. `titleAlignment`: `"left"` | `"center"` (ausente = `left`). `theme` opcional (ausente = vidro do MVP 1). `uiLanguage` opcional: `"system"` | `"pt"` | `"en"` (ausente = `system`; `version` permanece 1). `originalPath` opcional (origem para restore). Fundo da fence: alfa do fill limitado a 45–85%. Sempre ≥ 1 fence.

```json
{
  "version": 1,
  "uiLanguage": "system",
  "fences": [
    {
      "id": "guid",
      "title": "Trabalho",
      "titleAlignment": "left",
      "theme": {
        "fill": "#A80C0C12",
        "border": "#4DFFFFFF",
        "header": "#33000000",
        "text": "#F2FFFFFF"
      },
      "x": 100, "y": 100, "width": 420, "height": 280,
      "monitorDeviceName": "\\\\.\\DISPLAY1",
      "collapsed": false,
      "items": [
        { "name": "Relatorio.docx", "path": "C:\\\\Users\\\\…\\\\AppData\\\\Local\\\\DesktopFences\\\\Items\\\\guid\\\\Relatorio.docx", "originalPath": "C:\\\\Users\\\\…\\\\Desktop\\\\Relatorio.docx", "originalX": 12, "originalY": 48 }
      ]
    }
  ]
}
```

Coordenadas da fence em **DIPs** WPF; posições de ícone em **pixels** do ListView. Conversão é responsabilidade da App (DPI). Core só persiste números.

### 2.6 Fora do que já está no código (ciclo em `plano-implementacao.md`)

- Fase 6: instalador (path estável no arranque). Sem packs de tema.
- **Fora do ciclo:** empurrar a fence de baixo ao expandir; duplo clique no vazio do desktop cria fence; packs de tema.
- **Reserva (reavaliar no fim):** Novo → Fence no Explorer. Não implementar até planejada e validada.

---

## 3. Riscos

| Risco | Mitigação |
|---|---|
| Árvore Progman/WorkerW muda | 100% em Native; fallback duplo |
| Crash a meio do move | Gravar layout depois do move; arranque resolve pelo nome no Desktop se o store falhar |
| DPI por monitor | DIPs vs pixels documentados; `PerMonitorV2` + `DpiChanged` (Fase 5) |
| Restart do Explorer | Ficheiro já não está no Desktop; reaplicar só CLSID de namespace |
| Defender / `WriteProcessMemory` | `asInvoker`; signing só quando houver release assinado |
| Acrylic + WPF layered | ADR-0003: vidro alfa, sem composition attribute |
| OLE inbound + janela layered | ADR-0004: alvo no cursor + override da seta |
| `GWL_HWNDPARENT` no Progman | Não usar; z-order via `HWND_BOTTOM` |

---

## 4. Estrutura do repositório

```
DesktopFences/
├── DesktopFences.sln
├── Directory.Build.props
├── AGENTS.md
├── README.md
├── .github/workflows/release.yml    ← somente tags v*
├── docs/
│   ├── index.html                   ← landing; GitHub Pages em /docs
│   ├── SESSION-HEADER.md
│   ├── SPEC.md
│   ├── plano-implementacao.md
│   ├── pos-mvp1.md
│   └── adr/
├── src/
│   ├── DesktopFences.Core/
│   ├── DesktopFences.Native/
│   └── DesktopFences.App/           ← Assets/app.ico
└── tests/
    └── DesktopFences.Core.Tests/
```

---

## 5. Fora de escopo (até alguém abrir ADR)

- Afiliação ou engenharia reversa do binário Stardock Fences.
- Sincronizar layout na nuvem.
- Linux / macOS.
- Injeção de DLL no Explorer.
- UI em WinUI 3.

# DesktopFences — Spec técnica

App Windows 11 que agrupa ícones **reais** da área de trabalho em fences retangulares, com fundo translúcido, cantos arredondados, arrastáveis e persistentes. Inspirado no Stardock Fences; **não afiliado**.

**Stack:** C# / .NET 8 / WPF + P/Invoke. Decisão em [ADR-0001](adr/0001-stack-csharp-wpf.md).

**Princípio:** três camadas, para o ponto frágil (Explorer) poder quebrar sem reescrever UI.

| Projeto | Responsabilidade | Pode conhecer Win32? |
|---|---|---|
| `DesktopFences.Core` | Modelos, hit-test, JSON, paths | Não |
| `DesktopFences.Native` | SysListView32, COM Shell, OLE drop, DWM, âncora | Sim, e só aqui |
| `DesktopFences.App` | XAML, fence, bandeja, ghost | Só via serviços Native |

**Estado:** MVP 2 (`v0.3.1`) + Fase 3 fechada (arrastar item entre fences, validada no Windows 11). N fences, Configurações (cores, idioma pt/en, iniciar com o Windows), hide/restore do MVP 1. Ícones virtuais do desktop (Lixeira, Este computador, Rede) usam o namespace da Shell, não um ficheiro. Próxima: snap (Fase 4), só com pedido. Resto do ciclo: [plano-implementacao.md](plano-implementacao.md). Empurrar a fence de baixo ao expandir está **fora** deste ciclo.

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
| `LVM_SETITEMPOSITION` | `MAKELPARAM(x,y)` | SendMessage direto |
| `LVM_GETITEMPOSITION` | ponteiro para `POINT` | **alocar no explorer.exe** |
| `LVM_GETITEMTEXTW` | ponteiro para `LVITEM` + buffer | **alocar no explorer.exe** |

A Native aloca o `POINT` / `LVITEM` remoto. Não confiar em marshalling automático dessas mensagens.

Fluxo de hide (MVP 1):

1. Ler posições.
2. Hit-test Core: ícone ∩ retângulo da fence (célula ~76×92 px, ajustável).
3. Guardar posição original em memória e no JSON.
4. Mover para `(-32000, -32000)`.
5. Desenhar a grade WPF (`SHGetFileInfo`).
6. Restaurar no shutdown, no Pausar, ao remover o item e se o hide falhar.

**Limitação:** “Auto organizar ícones” / “Alinhar à grade” no menu do desktop desfaz o hide. Detectar e avisar não é Fase 1. Não escrever no registry.

---

## 2. Funcionalidades por área

### 2.1 Native — ícones

- Localizar `SysListView32` (Progman + WorkerW).
- Enumerar: índice, nome, posição. Um `OpenProcess` por varredura, não por ícone.
- Mover / esconder / restaurar.
- `SHGetFileInfo` para bitmap PNG.
- Resolver nome → path (`DesktopPaths`: Desktop do usuário + público).
- Reconectar após restart do Explorer — Fase 5 do ciclo.
- DPI: manifest Per-Monitor V2.

### 2.2 Native — chrome da janela

Ver [ADR-0003](adr/0003-ancoragem-desktop-e-acrylic.md).

- `AllowsTransparency=True`; vidro = brush alfa no `Border` (`#A80C0C12`), `CornerRadius=8`.
- **Não** usar `SetWindowCompositionAttribute` enquanto a janela for layered (cinza opaco no W11 testado).
- Z-order: `HWND_BOTTOM` + `WS_EX_TOOLWINDOW`. **Não** `GWL_HWNDPARENT` → Progman (isso impedia o OLE drop).
- Sem minimize/maximize box.

### 2.3 App — fence

Uma janela por fence: título editável (duplo clique **no texto**); Enter, LostFocus, clique fora do campo **grava**; Escape cancela. Alinhamento do título: esquerda (padrão) ou centro, nas Configurações — por fence, com checkbox para aplicar a todas (vale também para cores). Alça ⋮⋮ para mover; roll-up ▴; resize ao vivo; grade WrapPanel; scrollbar custom; menu de contexto (recolher, diagnóstico). Remover fence só nas Settings.

O `App` usa `FenceHost`: N instâncias de `FenceWindow`, um único `layout.json`. Aparência por fence (fundo, borda, header, texto + alfa); radius 8 e `AllowsTransparency` continuam fixos. Packs de tema nomeados, se existirem, são Fase 7. Grade: atalho/pasta/ficheiro → `SHGetFileInfo` no path (igual ao MVP 1). Só se **não** existir no disco (Lixeira, Este computador, Rede) é que se usa o PIDL do `IShellFolder` do desktop. Abrir esses itens só via `::{CLSID}` / `shell:` — não se substitui o `Process.Start` de um `.lnk`.

### 2.4 Drag & drop

O WPF `AllowDrop` **não** funciona de forma confiável nesta janela layered: o Explorer não acerta o `IDropTarget` (thumbnail OLE no hotspot) e pinta o cursor de proibido.

Contrato vigente — [ADR-0004](adr/0004-ole-inbound-drop.md):

- **Inbound:** `RegisterDragDrop` nativo + alvo OLE minúsculo no cursor; ghost WPF próprio (com `+N` se a seleção do desktop tiver vários ícones); cursor de “proibido” substituído pela seta só enquanto o ponteiro está sobre a fence. A seleção do `SysListView32` é lida no início do arraste (`LVM_GETNEXTITEM` / `LVNI_SELECTED`).
- **Outbound / reorder:** não usamos `DoDragDrop` do Explorer; hook `WH_MOUSE_LL` + `DragGhostWindow` (click-through).
- Soltar na fence esconde o ícone real e adiciona na grade; soltar fora restaura.
- Entre fences: soltar no **corpo** de outra fence muda o dono (JSON). O hide do ícone real permanece; o tracker segue o item. Barra de título / fence recolhida não transfere nem ejetar.

### 2.5 Persistência

Arquivo único `%AppData%\DesktopFences\layout.json` (`LayoutStore`). Lista de fences persistida pelo `FenceHost`. `titleAlignment`: `"left"` | `"center"` (ausente = `left`). `theme` opcional (ausente = vidro do MVP 1). `uiLanguage` opcional: `"system"` | `"pt"` | `"en"` (ausente = `system`; `version` permanece 1). Fundo da fence: alfa do fill limitado a 45–85%. Sempre ≥ 1 fence.

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
        { "name": "Relatorio.docx", "path": "C:\\\\Users\\\\…\\\\Desktop\\\\Relatorio.docx", "originalX": 12, "originalY": 48 }
      ]
    }
  ]
}
```

Coordenadas da fence em **DIPs** WPF; posições de ícone em **pixels** do ListView. Conversão é responsabilidade da App (DPI). Core só persiste números.

### 2.6 Fora do que já está no código (ciclo em `plano-implementacao.md`)

- Fase 4: snap a bordas e a outras fences.
- Fase 5: Explorer reiniciado / DPI / Win+D.
- Fase 6: duplo clique em vazio do desktop → cria fence.
- Fase 7: instalador (ajustar o arranque com o Windows para path estável); packs de tema só com pedido.
- **Fora do ciclo:** empurrar a fence de baixo ao expandir.
- **Reserva (reavaliar no fim):** Novo → Fence no Explorer. Não implementar até planejada e validada.

---

## 3. Riscos

| Risco | Mitigação |
|---|---|
| Árvore Progman/WorkerW muda | 100% em Native; fallback duplo |
| Auto-arrange desfaz hide | Avisar mais tarde; não desligar registry agora |
| DPI por monitor | DIPs vs pixels documentados; `WM_DPICHANGED` na Fase 5 |
| Restart do Explorer | Redetectar handle e reaplicar hide (Fase 5) |
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

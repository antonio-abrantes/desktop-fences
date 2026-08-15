# Plano de implementação — DesktopFences

Fonte de verdade do **estado**. A spec descreve o produto; este arquivo descreve o que já existe. O que vem depois está em [`pos-mvp1.md`](pos-mvp1.md).

Convenção:

- `[x]` feito no repositório (agente) e/ou confirmado pelo desenvolvedor na sessão.
- `[ ]` pendente — não implementar sem pedido explícito.

---

## MVP 1 — fechado

**Objetivo:** uma fence útil no Windows 11, com ícones reais escondidos no Explorer e grade nossa por cima.

### Entregas

- [x] Docs de contexto: `AGENTS.md`, `SESSION-HEADER.md`, spec, este plano, análise dos clones, ADRs
- [x] Solution em três projetos + testes de domínio (ocupância, reorder, paths, `LayoutStore`)
- [x] Native: achar ListView (Progman / WorkerW), ler nome+posição com memória remota, hide/restore
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

**Fora deste MVP (proposital):** várias fences, tela de configurações, snap/empurrar vizinhos, temas, instalador, restart do Explorer.

**Critério de saída:** cumprido. Próximo bloco só com pedido explícito — ver [`pos-mvp1.md`](pos-mvp1.md).

---

## Depois do MVP 1

Não detalhar aqui para não divergir. A ordem, a complexidade e o que é viável estão em [`docs/pos-mvp1.md`](pos-mvp1.md).

Resumo da sequência pretendida:

1. Várias fences + tela de configurações (criar / remover)
2. Interação entre fences (arrastar item de uma para outra)
3. Comportamento espacial (snap; depois empurrar o de baixo ao expandir)
4. Robustez (Explorer, DPI, Win+D)
5. Polish (temas, iniciar com o Windows, instalador)

---

## Ordem de leitura para uma sessão nova

1. `docs/SESSION-HEADER.md`
2. `AGENTS.md`
3. Este arquivo (estado)
4. `docs/SPEC.md` na seção da área que for mexer
5. `docs/pos-mvp1.md` se o pedido for avanço de produto

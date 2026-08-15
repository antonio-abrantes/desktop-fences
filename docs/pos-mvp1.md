# Depois do MVP 1

Mapa curto do ciclo. O detalhe operacional (ficheiros, invariantes, checklists) está em [`plano-implementacao.md`](plano-implementacao.md). Cada fase pede autorização no `SESSION-HEADER.md` **e** pedido explícito.

O schema `layout.json` já tem `fences: []`. A Fase 1 grava a lista inteira via `FenceHost`. Idioma pt/en é a Fase 2. Hit-test entre fences (arrastar item) é a Fase 3.

---

## Ordem deste ciclo

| Fase | Bloco | Complexidade |
|---|---|---|
| 1 | Várias fences + settings (bandeja, criar/remover, alinhamento, cores, iniciar com o Windows) | Média |
| 2 | Idioma da UI: português e inglês | Baixa |
| 3 | Arrastar item entre fences | Média |
| 4 | Snap a bordas e a outras fences | Média |
| 5 | Explorer reiniciado / DPI / Win+D | Média–alta |
| 6 | Duplo clique no vazio do desktop cria fence | Média |
| 7 | Instalador (ajustar arranque com o Windows); temas só com pedido | Baixa–média |

Não pular a 1. A 6 é atalho; a Settings da fase 1 continua sendo o jeito confiável de criar fence.

---

## Fora deste ciclo

**Empurrar a fence de baixo ao expandir** não faz parte deste plano. Se um dia existir, é outra versão, depois de 1–7 prontos, e só como pilha explícita — nunca física global entre fences soltas.

**Novo → Fence no Explorer** fica em reserva no `plano-implementacao.md`: reavaliar no fim do ciclo, não implementar até estar planejada e validada.

---

## Fase 1 (fechada)

N fences, cada uma com o comportamento do MVP 1. Sempre ≥ 1. Tray abre **Configurações** e **Sobre**. Lá: lista (altura máxima + scroll), nova, remover, alinhamento do título, checkbox **aplicar a todas**, cores visíveis, **iniciar com o Windows** (portable: o atalho segue a pasta do `.exe`) e restaurar padrão. Hide/restore por fence. `FenceHost` grava a lista inteira. Mutex: uma só instância.

A seguir no plano: idioma pt/en (Fase 2), depois hit-test entre fences (Fase 3).

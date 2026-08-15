# Depois do MVP 1

Mapa curto do ciclo. O detalhe operacional está em [`plano-implementacao.md`](plano-implementacao.md). Cada fase pede autorização no `SESSION-HEADER.md` **e** pedido explícito.

**MVP 2** (`v0.3.1`) = Fases 1 + 2: N fences, Configurações (cores, idioma, iniciar com o Windows), hide/restore do MVP 1. Ícones de sistema (Lixeira, Este computador, Rede) mostram o pictograma da Shell. A Fase 3 (arrastar item entre fences) está **fechada**.

---

## Ordem deste ciclo

| Fase | Bloco | Complexidade |
|---|---|---|
| 1 | Várias fences + settings (bandeja, criar/remover, alinhamento, cores, iniciar com o Windows) | Média — **fechada** |
| 2 | Idioma da UI: português e inglês | Baixa — **fechada** |
| 3 | Arrastar item entre fences | Média — **fechada** |
| 4 | Snap a bordas e a outras fences | Média |
| 5 | Explorer reiniciado / DPI / Win+D | Média–alta |
| 6 | Duplo clique no vazio do desktop cria fence | Média |
| 7 | Instalador (ajustar arranque com o Windows); packs de tema só com pedido | Baixa–média |

Não pular a 4. A 6 é atalho; a Settings continua sendo o jeito confiável de criar fence.

---

## Fora deste ciclo

**Empurrar a fence de baixo ao expandir** não faz parte deste plano. Se um dia existir, é outra versão, depois de 1–7 prontos, e só como pilha explícita — nunca física global entre fences soltas.

**Novo → Fence no Explorer** fica em reserva no `plano-implementacao.md`: reavaliar no fim do ciclo, não implementar até estar planejada e validada.

---

## Fase 1 (fechada)

N fences, cada uma com o comportamento do MVP 1. Sempre ≥ 1. Tray abre **Configurações** e **Sobre**. Lá: lista (altura máxima + scroll), nova, remover, alinhamento do título, checkbox **aplicar a todas**, cores (fundo, borda, header, texto), **iniciar com o Windows** (portable: o atalho segue a pasta do `.exe`) e restaurar padrão. Hide/restore por fence. `FenceHost` grava a lista inteira. Mutex: uma só instância.

---

## Fase 2 (fechada)

UI em português (default) e inglês. Nas Configurações: seletor **Sistema** / **Português** / **Inglês**. Persistência opcional `uiLanguage` em `layout.json`. Troca ao vivo (bandeja, janelas abertas) sem recriar fences nem re-esconder ícones. Título já gravado não muda; fence nova usa o título do idioma atual.

---

## Fase 3 (fechada)

Arrastar um ou vários itens da grade de uma fence para o corpo de outra. O ghost é o mesmo. O ícone real no Explorer continua escondido — só muda o dono no `layout.json`. Soltar no desktop ainda devolve o ícone. Soltar na barra de título ou numa fence recolhida não tira o item da origem.

A seguir no plano (só com pedido): snap (Fase 4).

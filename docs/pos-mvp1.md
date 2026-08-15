# Depois do MVP 1

O MVP 1 é **uma** fence usável. Este arquivo é só o mapa do que construir a seguir — não é ordem para o agente implementar. Cada bloco pede pedido explícito.

O schema `layout.json` já tem `fences: []`. O buraco é a App: hoje o `App` cria um único `FenceWindow`.

---

## Ordem recomendada

| # | Bloco | Complexidade | Viável agora? | Por que nesta ordem |
|---|---|---|---|---|
| 1 | Várias fences + tela de configurações | Média | Sim | Sem isso, snap, empurrar e drag entre fences não existem. Settings é o jeito de criar a segunda fence sem “mágica” no desktop. |
| 2 | Arrastar item entre fences | Média | Sim, depois de 1 | Reusa o ghost já pronto; precisa de hit-test “qual fence está sob o cursor”. |
| 3 | Snap a bordas e a outras fences | Média | Sim, depois de 1 | Posicionamento livre estável primeiro; snap é ajuste no soltar/mover. |
| 4 | Empurrar a fence de baixo ao expandir | Alta | Sim, mas **depois** de 1–3 | Sem modelo de coluna/pilha, empurrar briga com o usuário que posicionou na mão. |
| 5 | Duplo clique no desktop cria fence | Média | Sim, depois de 1 | O hook de mouse já existe; falta hit-test “clique no vazio” e não no ícone. |
| 6 | Explorer reiniciado / DPI / Win+D | Média–alta | Sim | Robustez; não bloqueia o produto de várias fences, mas quebra no mundo real. |
| 7 | Temas, iniciar com o Windows, instalador | Baixa–média | Sim | Polish. O visual da fence está travado até você pedir tema. |

Não pular o 1. Não começar pelo 4.

---

## 1. Várias fences + configurações (primeiro passo)

**O quê.** N fences no desktop, cada uma com o comportamento do MVP 1. Uma janela normal de configurações (não é fence): lista, **Nova fence**, remover, talvez mostrar/ocultar. Nas settings: alinhamento do título da fence — centro, ou o lugar de hoje (à esquerda, depois da alça ⋮⋮).

**Por que primeiro.** Criar a segunda fence hoje não tem UI. Duplo clique no desktop é mais frágil (hook + falso positivo em ícone). Settings é explícito e testável.

**Complexidade.** Média. Extraír um `FenceHost` / controller: criar, persistir todas, pausar/restaurar ícones **por fence** sem restaurar as outras. Tray abre Settings. Z-order: todas `HWND_BOTTOM`, a última ativada pode subir entre as fences.

**Cuidado.** Hide/restore tem que ser por conjunto de itens, não “restaurar o desktop inteiro” ao fechar uma fence. Não copiar `FenceWindow.xaml.cs` N vezes — um tipo, N instâncias.

**Fora deste bloco.** Snap, empurrar, temas.

---

## 2. Item de uma fence para outra

Ghost já segue o cursor. Falta: no soltar, se o ponto está em outra fence, mover o item (JSON + hide continua no mesmo ícone real). Complexidade média. Sem isso, várias fences são ilhas.

---

## 3. Snap

Ao soltar a alça ⋮⋮ (e talvez no resize): ímã em bordas da tela e em arestas de outras fences, com folga de alguns pixels. Complexidade média. É o alicerce do bloco 4: sem snap, “pilha” não tem alinhamento.

---

## 4. Empurrar o de baixo ao expandir

**A ideia.** Fence A em cima, B embaixo, alinhadas. A recolhida ocupa só a barra; ao expandir, B desce e mantém o vão. Ao recolher A, B sobe.

**Viável?** Sim, se for uma **pilha explícita** (duas fences snapadas na vertical, mesmo eixo X, vão fixo). Não viável como física global em fences soltas: o usuário coloca uma no canto e outra no meio; “empurrar todo mundo” vira layout automático contra a vontade dele.

**Complexidade.** Alta. Tem que considerar: altura animada (já existem ~180 ms), recolhida vs expandida, resize manual no meio da animação, multi-monitor, B contra a borda inferior da tela (encolher A? scroll interno? parar o push?).

**Ordem interna do bloco 4.** (a) detectar par empilhado via snap vertical; (b) push só nesse par, sem animação; (c) depois interpolar com o expand; (d) só então pilhas de 3+.

Não implementar “todas as fences do desktop se empurram” no primeiro corte.

---

## 5–7. Resto

- **Duplo clique no vazio:** o `WH_MOUSE_LL` já está no processo. Risco: criar fence ao clicar ícone ou ao abrir o menu do desktop. Só depois das Settings existirem (a Settings continua sendo o caminho confiável).
- **Explorer / DPI / Win+D:** reaplicar hide no ListView novo; `WM_DPICHANGED`; não desaparecer com Show Desktop. Importante, mas é sobrevivência, não feature de produto.
- **Temas / startup / MSI:** não mexer no vidro sem pedido. Startup e instalador são distribuição, não interação.

---

## O que o MVP 1 já deixa pronto para isso

- `LayoutDocument.Fences` — lista no JSON.
- Uma classe de janela com move, resize, collapse, drop, persistência por `id`.
- Ghost e hook de mouse reutilizáveis entre janelas.

O que **não** está pronto: host de N janelas, UI de criar/apagar, hit-test entre fences, regras de pilha.

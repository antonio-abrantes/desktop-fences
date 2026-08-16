# Depois do MVP 1

Mapa curto do ciclo. O detalhe operacional está em [`plano-implementacao.md`](plano-implementacao.md). Cada fase pede autorização no `SESSION-HEADER.md` **e** pedido explícito.

**MVP 2** (`v0.3.1`) = Fases 1 + 2. As Fases 3–6 e o hotfix `v0.5.1` estão fechados. A Fase 7 está implementada para a `v0.6.0`, com gate manual pendente.

---

## Ordem deste ciclo

| Fase | Bloco | Complexidade |
|---|---|---|
| 1 | Várias fences + settings (bandeja, criar/remover, alinhamento, cores, iniciar com o Windows) | Média — **fechada** |
| 2 | Idioma da UI: português e inglês | Baixa — **fechada** |
| 3 | Arrastar item entre fences | Média — **fechada** |
| 4 | Snap a bordas e a outras fences | Média — **fechada** |
| 5 | Explorer reiniciado / DPI / Win+D | Média–alta — **fechada** |
| 6 | Custódia transacional de itens do Desktop | Alta — **fechada e validada** |
| 7 | Instalador, upgrade e desinstalação segura | Média — **implementada; gate manual pendente** |

Não fechar a Fase 7 sem validar os setups no Windows 11. Criar fence continua nas Settings.

---

## Fora deste ciclo

**Empurrar a fence de baixo ao expandir** — outra versão, se um dia, só como pilha explícita.

**Duplo clique no vazio do desktop cria fence** — reserva. Settings é o caminho. Só depois de validar necessidade real.

**Packs de tema** — reserva. O vidro fica travado.

**Novo → Fence no Explorer** — reserva no `plano-implementacao.md`.

---

## Fase 1 (fechada)

N fences, cada uma com o comportamento do MVP 1. Sempre ≥ 1. Tray abre **Configurações** e **Sobre**. Lá: lista (altura máxima + scroll), nova, remover, alinhamento do título, checkbox **aplicar a todas**, cores (fundo, borda, header, texto), **iniciar com o Windows** (portable: o atalho segue a pasta do `.exe`) e restaurar padrão. Hide/restore por fence. `FenceHost` grava a lista inteira. Mutex: uma só instância.

---

## Fase 2 (fechada)

UI em português (default) e inglês. Nas Configurações: seletor **Sistema** / **Português** / **Inglês**. Persistência opcional `uiLanguage` em `layout.json`. Troca ao vivo (bandeja, janelas abertas) sem recriar fences nem re-esconder ícones. Título já gravado não muda; fence nova usa o título do idioma atual.

---

## Fase 3 (fechada)

Arrastar um ou vários itens da grade de uma fence para o corpo de outra. O ghost é o mesmo. O ícone real no Explorer continua escondido — só muda o dono no `layout.json`. Soltar no desktop ainda devolve o ícone. Soltar na barra de título ou numa fence recolhida não tira o item da origem.

## Fase 4 (fechada)

Ao soltar a alça ⋮⋮ (e ao terminar o resize), a fence cola nas bordas da área de trabalho e nas arestas das outras. Não empurra o vizinho. Fence recolhida só muda de sítio, não estica a barra.

## Fase 5 (fechada)

Validada no Windows 11. Se o Explorer morrer, os ficheiros já não estão no Desktop (estão no store); CLSID de namespace é reaplicado. Mudança de DPI atualiza o clip. Para Win+D / Mostrar ambiente de trabalho, a fence é reancorada acima da banda Progman/WorkerW e abaixo dos aplicativos, inclusive quando o Windows ainda a reporta como visível. Pausar/Sair continua a devolver os ícones reais.

A Fase 6 entregou store por `ItemId`, transação/recovery, transferência por metadados e lote, somente para itens do Desktop. O hotfix `v0.5.1` acrescentou proteção contra downgrade, snapshot atômico das posições e `DesktopFences.Recovery.exe`. A Fase 7 acrescenta setups x64/ARM64, path estável, idioma inicial e desinstalação segura, sem packs de tema.

# SESSION-HEADER — DesktopFences 1.0

> Atualize os campos desta seção a cada nova sessão ou conclusão de fase antes de enviar.

---

## Contexto da Sessão ou fase

**Projeto:** DesktopFences 1.0
**Etapa atual:** Fase 4 — snap (**no código**; gate do desenvolvedor pendente).
**Objetivo desta sessão:** validar o ímã no Windows 11. Não avançar para a Fase 5 (Explorer/DPI/Win+D) sem pedido.

---

## Documentos que Você Deve Ler Antes de Qualquer Ação

Leia os seguintes arquivos integralmente, nesta ordem, antes de responder:

1. `AGENTS.md` — regras de comportamento, o que pode e não pode fazer, como gerenciar fases
2. `docs/SPEC.md` — arquitetura, stack, modelo de ícones, UI, persistência, riscos
3. `docs/plano-implementacao.md` — estado atual **e** o detalhe da Fase 5 (próxima)
4. `README.md` — o que o produto é e como rodar

Apoio (quando o assunto for relevante):

5. `docs/pos-mvp1.md` — mapa curto do ciclo (sem empurrar vizinhos)
6. `docs/adr/` — decisões já fechadas

> Não inicie nenhuma implementação antes de confirmar que leu todos os documentos obrigatórios acima.

---

## Instrução de Revisão

A Fase 4 está **no repositório** (ímã no soltar da alça ⋮⋮ e no resize). Não empurra vizinhos nem empilha. Explorer/DPI, duplo clique no desktop, instalador e empurrar vizinhos continuam fechados.

```
ETAPA ATUAL    : Fase 4 — snap (código pronto; validar no Windows 11)
ETAPA SEGUINTE : Fase 5 — Explorer/DPI/Win+D (só com pedido)
RESTANTES      : 6 duplo clique no desktop · 7 instalador
FORA DO CICLO  : empurrar fence de baixo ao expandir
```

---

## Lembrete de Regras Críticas

> Os itens `[x]` são estados confirmados pelo agente no repositório. O item `[ ]` é restrição ativa.

- [x] Documentação de contexto (`AGENTS.md`, `SESSION-HEADER`, spec, plano, ADRs)
- [x] Solution fatiada em Core / Native / App + testes de domínio
- [x] Native lê/move/restaura ícones no `SysListView32` (Progman e fallback WorkerW)
- [x] Fence translúcida (vidro escuro, wallpaper visível), radius 8, atrás dos apps
- [x] Drop inbound (desktop/Explorer) com ghost + seta; vários ícones de uma vez; drop outbound devolve o ícone
- [x] Grade própria (`SHGetFileInfo`), seleção, reordenação, hide/restore dos ícones reais
- [x] Mover pela alça ⋮⋮; resize ao vivo; recolher; título só com duplo clique no texto (clique fora grava)
- [x] Persistência `layout.json`; bandeja Pausar / Retomar / Configurações / Sobre / Sair; ícone do app
- [x] Desenvolvedor validou no Windows 11: inbound drop, ghost e cursor (seta, não “proibido”)
- [x] Fase 1 fechada (N fences, settings, cores, Sobre, iniciar com o Windows + mutex)
- [x] Fase 2 fechada (UI pt/en, seletor Sistema/PT/EN, `uiLanguage` opcional)
- [x] Ícones de sistema (Este computador, Lixeira, Rede): ícone e abrir via namespace do desktop, não só path de ficheiro
- [x] Desenvolvedor validou no Windows 11: Lixeira / Este computador / Rede visíveis na fence (patch `v0.3.1`)
- [x] Fase 3 fechada: soltar no corpo de outra fence muda o dono; chrome/recolhida não ejetar; desktop ejetar; tracker de hide segue o item
- [x] Desenvolvedor validou no Windows 11: arrastar um ou vários itens de uma fence para outra (ícone real continua escondido)
- [x] Fase 4 no código: ímã às bordas da área de trabalho e às arestas de outras fences ao soltar a alça e no resize; não empurra vizinhos
- [ ] Desenvolvedor validou no Windows 11: soltar a alça ⋮⋮ perto da borda da tela / de outra fence cola; resize idem; vizinho não se mexe
- [ ] Não implementar fases 5–7 (Explorer/DPI, duplo clique no desktop, instalador / packs de tema) sem pedido
- [ ] Não implementar empurrar a fence de baixo ao expandir neste ciclo

Se qualquer inconsistência for encontrada na revisão, reporte antes de sugerir qualquer ação.

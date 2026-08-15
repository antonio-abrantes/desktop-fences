# SESSION-HEADER — DesktopFences 1.0

> Atualize os campos desta seção a cada nova sessão ou conclusão de fase antes de enviar.

---

## Contexto da Sessão ou fase

**Projeto:** DesktopFences 1.0
**Etapa atual:** Fase 2 **fechada** (idioma pt/en). MVP 2 = Fases 1 + 2 (`v0.3.1`).
**Objetivo desta sessão:** não avançar para a Fase 3 (arrastar item entre fences) sem pedido.

---

## Documentos que Você Deve Ler Antes de Qualquer Ação

Leia os seguintes arquivos integralmente, nesta ordem, antes de responder:

1. `AGENTS.md` — regras de comportamento, o que pode e não pode fazer, como gerenciar fases
2. `docs/SPEC.md` — arquitetura, stack, modelo de ícones, UI, persistência, riscos
3. `docs/plano-implementacao.md` — estado atual **e** o detalhe da Fase 3 (próxima)
4. `README.md` — o que o produto é e como rodar

Apoio (quando o assunto for relevante):

5. `docs/pos-mvp1.md` — mapa curto do ciclo (sem empurrar vizinhos)
6. `docs/adr/` — decisões já fechadas

> Não inicie nenhuma implementação antes de confirmar que leu todos os documentos obrigatórios acima.

---

## Instrução de Revisão

A Fase 2 está **fechada** (validada no Windows 11). Não avançar para drag entre fences, snap, Explorer/DPI, duplo clique no desktop, instalador nem empurrar vizinhos. Cores por fence já estão no MVP 2; “temas” da Fase 7 são packs nomeados / instalador — não reabrir o vidro sem pedido.

```
ETAPA ATUAL    : Fase 2 fechada (MVP 2)
ETAPA SEGUINTE : Fase 3 — arrastar item entre fences (só com pedido)
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
- [ ] Não implementar fases 3–7 (drag entre fences, snap, Explorer/DPI, duplo clique no desktop, instalador / packs de tema) sem pedido
- [ ] Não implementar empurrar a fence de baixo ao expandir neste ciclo

Se qualquer inconsistência for encontrada na revisão, reporte antes de sugerir qualquer ação.

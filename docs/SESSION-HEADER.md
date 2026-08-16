# SESSION-HEADER — DesktopFences 1.0

> Atualize os campos desta seção a cada nova sessão ou conclusão de fase antes de enviar.

---

## Contexto da Sessão ou fase

**Projeto:** DesktopFences 1.0
**Etapa atual:** Fase 5 — Explorer / DPI / Win+D (**no código**; gate do desenvolvedor pendente).
**Objetivo desta sessão:** documentar, sem implementar, a Fase 6 de custódia transacional dos itens do Desktop. O gate Windows 11 da Fase 5 permanece pendente; não avançar para a Fase 6 sem novo pedido explícito.

---

## Documentos que Você Deve Ler Antes de Qualquer Ação

Leia os seguintes arquivos integralmente, nesta ordem, antes de responder:

1. `AGENTS.md` — regras de comportamento, o que pode e não pode fazer, como gerenciar fases
2. `docs/SPEC.md` — arquitetura, stack, modelo de ícones, UI, persistência, riscos
3. `docs/plano-implementacao.md` — estado atual e ordem das fases
4. `README.md` — o que o produto é e como rodar

Para implementar a próxima fase, ler também integralmente:

5. `docs/spec-fase-6-custodia-desktop.md`
6. `docs/plano-fase-6-custodia-desktop.md`

Apoio (quando o assunto for relevante):

7. `docs/pos-mvp1.md` — mapa curto do ciclo (sem empurrar vizinhos)
8. `docs/adr/` — decisões já fechadas

> Não inicie nenhuma implementação antes de confirmar que leu todos os documentos obrigatórios acima.

---

## Instrução de Revisão

A Fase 4 (snap) está **fechada**. A Fase 5 está **no repositório**: re-hide se o Explorer reiniciar (store + registry), `PerMonitorV2` + clip no DPI, a fence não some com Win+D / Mostrar ambiente de trabalho. A Fase 6 foi especificada para store por `ItemId`, transação/recovery, transferência por metadados e lote. O instalador passa a ser a Fase 7. Duplo clique no vazio do desktop, packs de tema e empurrar vizinhos estão **fora** deste ciclo.

```
ETAPA ATUAL    : Fase 5 — Explorer / DPI / Win+D (código pronto; validar no Windows 11)
ETAPA SEGUINTE : Fase 6 — custódia transacional de itens do Desktop (planejada; não implementada)
DEPOIS         : Fase 7 — instalador (path estável no arranque; sem packs de tema)
FORA DO CICLO  : duplo clique no vazio do desktop · packs de tema · empurrar fence de baixo ao expandir
```

---

## Lembrete de Regras Críticas

> Os itens `[x]` são estados confirmados pelo agente no repositório. O item `[ ]` é restrição ativa.

- [x] Documentação de contexto (`AGENTS.md`, `SESSION-HEADER`, spec, plano, ADRs)
- [x] Solution fatiada em Core / Native / App + testes de domínio
- [x] Native lê ícones no `SysListView32` (Progman e fallback WorkerW); hide via store + registry
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
- [x] Fase 4 fechada: ímã às bordas da área de trabalho e às arestas de outras fences ao soltar a alça e no resize; não empurra vizinhos
- [x] Desenvolvedor validou no Windows 11: soltar a alça ⋮⋮ perto da borda da tela / de outra fence cola; resize idem; vizinho não se mexe
- [x] Fase 5 no código: Explorer morre → re-Conceal; DPI atualiza clip; Win+D não esconde a fence; hide por move para o store
- [x] Fase 6 documentada: store por ItemId; transação/JSON atômico/backup/recovery; transferência por metadados; lote; somente Desktop
- [ ] Desenvolvedor validou no Windows 11: matar o Explorer e ver os ícones voltarem a esconder; mudar DPI; Win+D / Mostrar ambiente de trabalho — fences continuam visíveis; Pausar/Sair ainda restaura
- [ ] Não implementar a Fase 6 sem fechar o gate da Fase 5 e receber pedido explícito
- [ ] Não implementar a Fase 7 (instalador / path estável no arranque) antes do gate da Fase 6 e sem pedido explícito
- [ ] Não implementar duplo clique no vazio do desktop, packs de tema, nem empurrar a fence de baixo ao expandir

Se qualquer inconsistência for encontrada na revisão, reporte antes de sugerir qualquer ação.

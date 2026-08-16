# SESSION-HEADER — DesktopFences 1.0

> Atualize os campos desta seção a cada nova sessão ou conclusão de fase antes de enviar.

---

## Contexto da Sessão ou fase

**Projeto:** DesktopFences 1.0
**Etapa atual:** Fase 6 — custódia transacional de itens do Desktop (**fechada e validada; versão `v0.5.0` preparada**).
**Objetivo desta sessão:** fechar o gate da Fase 6, alinhar a versão `0.5.0` e preparar commit/tag, sem iniciar o instalador.

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

A Fase 6 está **fechada e validada no Windows 11**. O código implementa schema/store v2 por `ItemId`, migração v1 recuperável, JSON atômico com backup, journal/recovery, transferência somente por metadados e lote. A regressão de arranque encontrada num layout v1 misto foi corrigida e coberta por regressão. Builds Debug/Release e 159 testes automatizados estão verdes. A versão interna é `0.5.0`; o instalador é a Fase 7 e não foi iniciado.

```
ETAPA FECHADA  : Fase 6 — custódia transacional de itens do Desktop (validada no Windows 11)
VERSÃO PRONTA  : v0.5.0 — commit e tag pendentes do desenvolvedor
ETAPA SEGUINTE : Fase 7 — instalador (não iniciada; exige pedido explícito)
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
- [x] Fase 5 fechada: Explorer morre → re-Conceal; DPI atualiza clip; Win+D reancora a fence acima de Progman/WorkerW; hide por move para o store
- [x] Fase 6 documentada: store por ItemId; transação/JSON atômico/backup/recovery; transferência por metadados; lote; somente Desktop
- [x] Fase 6 implementada no código: schema v2/migração; commit atômico/journal/recovery; ownership por metadados; pipeline em lote
- [x] Fase 6 validada automaticamente: builds Debug/Release sem avisos e 159 testes verdes (131 Core + 28 App/Native)
- [x] Desenvolvedor validou a matriz da Fase 6 no Windows 11 real
- [x] Versão do aplicativo alinhada para `0.5.0` / tag `v0.5.0`
- [x] Desenvolvedor validou no Windows 11: Explorer/DPI e Win+D / Mostrar ambiente de trabalho; fences permanecem visíveis; Pausar/Sair continua restaurando
- [ ] Não implementar a Fase 7 (instalador / path estável no arranque) sem novo pedido explícito
- [ ] Não implementar duplo clique no vazio do desktop, packs de tema, nem empurrar a fence de baixo ao expandir

Se qualquer inconsistência for encontrada na revisão, reporte antes de sugerir qualquer ação.

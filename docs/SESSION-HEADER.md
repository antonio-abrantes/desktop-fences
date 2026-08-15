# SESSION-HEADER — DesktopFences 1.0

> Atualize os campos desta seção a cada nova sessão ou conclusão de fase antes de enviar.

---

## Contexto da Sessão ou fase

**Projeto:** DesktopFences 1.0
**Etapa atual:** MVP 1 **fechado** (uma fence usável)
**Objetivo desta sessão:** documentação alinhada ao código; próximos passos só no papel.

---

## Documentos que Você Deve Ler Antes de Qualquer Ação

Leia os seguintes arquivos integralmente, nesta ordem, antes de responder:

1. `AGENTS.md` — regras de comportamento, o que pode e não pode fazer, como gerenciar fases
2. `docs/SPEC.md` — arquitetura, stack, modelo de ícones, UI, persistência, riscos
3. `docs/plano-implementacao.md` — estado atual do projeto
4. `README.md` — o que o produto é e como rodar

Apoio (quando o assunto for relevante):

5. `docs/pos-mvp1.md` — o que vem depois do MVP 1 (ordem, complexidade, o que não fazer ainda)
6. `docs/analise-referencias.md` — o que aproveitar de DeskFrame, NoFences e OpenFences
7. `docs/adr/` — decisões já fechadas

> Não inicie nenhuma implementação antes de confirmar que leu todos os documentos obrigatórios acima.

---

## Instrução de Revisão

O MVP 1 está **implementado**. Não avançar para múltiplas fences / configurações / temas sem pedido explícito.

```
ETAPA ATUAL    : MVP 1 fechado
ETAPA SEGUINTE : múltiplas fences + tela de configurações (ver docs/pos-mvp1.md)
```

---

## Lembrete de Regras Críticas

> Os itens `[x]` são estados confirmados pelo agente no repositório. O item `[ ]` é restrição ativa até o desenvolvedor pedir o avanço.

- [x] Documentação de contexto (`AGENTS.md`, `SESSION-HEADER`, spec, plano, análise, ADRs)
- [x] Solution fatiada em Core / Native / App + testes de domínio
- [x] Native lê/move/restaura ícones no `SysListView32` (Progman e fallback WorkerW)
- [x] Fence translúcida (vidro escuro, wallpaper visível), radius 8, atrás dos apps
- [x] Drop inbound (desktop/Explorer) com ghost + seta; vários ícones de uma vez (seleção do desktop); drop outbound devolve o ícone
- [x] Grade própria (`SHGetFileInfo`), seleção, reordenação, hide/restore dos ícones reais
- [x] Mover pela alça ⋮⋮; resize ao vivo; recolher; título só com duplo clique no texto
- [x] Persistência `layout.json`; bandeja Pausar / Retomar / Sair; ícone do app
- [x] Desenvolvedor validou no Windows 11: inbound drop, ghost e cursor (seta, não “proibido”)
- [ ] Não implementar múltiplas fences / settings / temas sem pedido explícito

Se qualquer inconsistência for encontrada na revisão, reporte antes de sugerir qualquer ação.

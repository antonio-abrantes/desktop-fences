# ADR-0002: Esconder ícones reais via SysListView32

**Date**: 2026-08-14
**Status**: accepted
**Deciders**: Antonio + agente

## Context

DeskFrame, NoFences e OpenFences mostram fences/frames mas **deixam os ícones originais no desktop**. O Fences original lê a `SysListView32` (`Progman`/`WorkerW` → `SHELLDLL_DefView` → `FolderView`), move os ícones que caem no retângulo da fence para fora da área visível, e desenha a própria representação.

## Decision

O MVP replica esse modelo:

1. Localizar o ListView (Progman, fallback enumerando WorkerW).
2. Ler índice, nome e posição (memória remota no `explorer.exe` quando o `lParam` é ponteiro).
3. Hit-test contra o retângulo da fence (lógica em Core, coordenadas em pixels de tela).
4. `LVM_SETITEMPOSITION` para um ponto fora da vista (ex. -32000,-32000), guardando a posição original.
5. Restaurar sempre no close, crash path e “Restore” da POC.

Não usamos pasta backing / `.lnk` como modelo principal (OpenFences). Não usamos “hide all desktop icons” como substituto.

## Alternatives Considered

### Pasta backing + atalhos (OpenFences / NoFences)
- **Pros**: simples, sem WriteProcessMemory, AV mais calmo.
- **Cons**: ícones reais continuam lá; não é Fences.
- **Why not**: é exatamente a falha que o usuário apontou nos clones.

### Toggle global de ícones do desktop
- **Pros**: uma chamada, efeito imediato.
- **Cons**: some com a Lixeira e tudo que não está em fence.
- **Why not**: granularidade errada.

### Injetar DLL no Explorer
- **Pros**: controle máximo.
- **Cons**: AV, estabilidade, manutenção por build.
- **Why not**: desproporcional; mensagens de ListView bastam para o MVP.

## Consequences

### Positive
- Comportamento alinhado ao Fences original.
- Native isolado: quando uma build do W11 mudar a árvore de janelas, o conserto é num projeto.

### Negative
- `OpenProcess` + `WriteProcessMemory` no `explorer.exe` pode alertar Defender. Mitigação: `asInvoker`, documentar, code signing no Passo 4.
- “Alinhar à grade” / “Auto organizar” do Explorer pode desfazer posições. Detectar e avisar (Passo 1); não desligar no registry no Passo 0.

### Risks
- `LVM_GETITEMPOSITION` **não** é marshallado automaticamente cross-process. A POC aloca `POINT` no espaço do Explorer. Implementações que passam `ref POINT` no nosso processo falham de forma intermitente.

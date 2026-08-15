# ADR-0001: Stack C# / .NET 8 / WPF

**Date**: 2026-08-14
**Status**: accepted
**Deciders**: Antonio + agente

## Context

Precisamos de um app Windows 11 com janelas translúcidas, cantos arredondados, e interop pesado com Win32/COM (Explorer, DWM, Shell). O desenvolvedor já trabalha em C#/.NET.

## Decision

Usar **C# / .NET 8 / WPF** (`net8.0-windows`), com P/Invoke isolado em `DesktopFences.Native`. Sem Electron, WinUI 3 ou MAUI neste ciclo.

## Alternatives Considered

### WinUI 3 / Windows App SDK
- **Pros**: Acrylic/Mica de primeira; visual Win11 nativo.
- **Cons**: interop HWND ainda existe; empacotamento MSIX mais rígido; curva nova.
- **Why not**: WPF resolve transparência + HWND hoje; WinUI pode ser reavaliado num ADR futuro se o visual WPF saturar.

### C++ / Win32 cru
- **Pros**: controle total, sem marshalling.
- **Cons**: produtividade e UI modernas piores para o time.
- **Why not**: o gargalo não é performance de UI, é a API do Explorer.

### WinForms (caminho do NoFences)
- **Pros**: leve, blur via composition attribute já comprovado.
- **Cons**: visual e layout de grade inferiores ao WPF.
- **Why not**: o diferencial visível vs. OpenFences é justamente o chrome.

## Consequences

### Positive
- Interop C# é direto; DPI Per-Monitor V2 via manifest.
- XAML permite radius, storyboards e grid sem owner-draw.

### Negative
- Acrylic “de verdade” no WPF exige P/Invoke (`SetWindowCompositionAttribute`), não uma propriedade XAML.
- `AllowsTransparency` conflita com alguns truques de parenting; ver ADR-0003.

### Risks
- Quebras de Explorer entre builds do Windows 11 — mitigado isolando Native.

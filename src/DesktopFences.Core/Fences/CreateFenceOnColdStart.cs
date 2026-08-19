namespace DesktopFences.Core.Fences;

/// <summary>
/// Arranque a frio com --create-fence: se o layout já tinha fences,
/// o pedido cria mais uma; se estava vazio, EnsureAtLeastOne já cumpriu o pedido.
/// </summary>
public static class CreateFenceOnColdStart
{
    public static bool ShouldAddAnother(int fenceCountBeforeEnsureAtLeastOne) =>
        fenceCountBeforeEnsureAtLeastOne >= 1;
}

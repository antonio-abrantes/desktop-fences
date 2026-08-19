using DesktopFences.Core.Fences;
using DesktopFences.Native;
using Microsoft.Win32;

namespace DesktopFences.App;

internal static class ExplorerNewMenu
{
    private const string CacheKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\Discardable\PostSetup\ShellNew";
    private const string ClassesValue = "Classes";

    public static void RegisterFence() => Mutate(list =>
        ShellNewMenuCache.WithExtension(list, DesktopFenceStubRules.Extension));

    public static void UnregisterFence() => Mutate(list =>
        ShellNewMenuCache.WithoutExtension(list, DesktopFenceStubRules.Extension));

    private static void Mutate(Func<IReadOnlyList<string>, IReadOnlyList<string>> transform)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(CacheKey, writable: true);
        if (key is null)
            return;

        if (key.GetValue(ClassesValue) is not string[] existing)
            return;

        IReadOnlyList<string> next = transform(existing);
        if (!ReferenceEquals(next, existing))
            key.SetValue(ClassesValue, next.ToArray(), RegistryValueKind.MultiString);

        ShellDirectoryNotify.NotifyAssociationsChanged();
    }
}

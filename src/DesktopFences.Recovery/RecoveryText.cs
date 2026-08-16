using System.Globalization;

namespace DesktopFences.Recovery;

internal static class RecoveryText
{
    private static bool English => !CultureInfo.CurrentUICulture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase);

    public static string Title => "DesktopFences Recovery";
    public static string Heading => English ? "Emergency recovery" : "Recuperação de emergência";
    public static string Explanation => English
        ? "Restores missing items found in the DesktopFences Store without deleting the backup. Existing files are never overwritten and the current Desktop arrangement is preserved by default."
        : "Restaura os itens ausentes encontrados no Store do DesktopFences sem apagar o backup. Arquivos existentes nunca são sobrescritos e a organização atual do Desktop é preservada por padrão.";
    public static string Warning => English
        ? "Close DesktopFences before continuing. The active fence item lists will be cleared only after every payload is safely restored. Saved positions are optional and never applied automatically."
        : "Feche o DesktopFences antes de continuar. As listas ativas das fences só serão limpas depois que todos os payloads forem restaurados com segurança. Posições salvas são opcionais e nunca são aplicadas automaticamente.";
    public static string RestorePositionsOption => English
        ? "Reapply saved icon positions (optional)"
        : "Reaplicar posições salvas dos ícones (opcional)";
    public static string RestoreButton => English ? "Restore everything now" : "Restaurar tudo agora";
    public static string CloseButton => English ? "Close" : "Fechar";
    public static string Confirm => English
        ? "Restore every item to the Desktop now? The Store will remain untouched as a backup."
        : "Restaurar agora todos os itens para o Desktop? O Store continuará intacto como backup.";
    public static string Restoring => English ? "Restoring and verifying files…" : "Restaurando e verificando os arquivos…";
    public static string AppMustBeClosed => English
        ? "DesktopFences is running. Close it from the tray before starting emergency recovery."
        : "O DesktopFences está em execução. Feche-o pela bandeja antes de iniciar a recuperação de emergência.";
    public static string Failed(string errors) => English
        ? "Recovery stopped safely. No Store payload was deleted.\n\n" + errors
        : "A recuperação parou com segurança. Nenhum payload do Store foi apagado.\n\n" + errors;
    public static string Completed(
        int files,
        int directories,
        int positions,
        int conflicts,
        string session,
        bool positionsRequested)
    {
        string positionLine = positionsRequested
            ? (English ? $"\nIcon positions restored: {positions}" : $"\nPosições de ícones restauradas: {positions}")
            : (English ? "\nCurrent Desktop positions preserved." : "\nPosições atuais do Desktop preservadas.");
        return English
            ? $"Recovery completed.\n\nFiles copied: {files}\nFolders processed: {directories}{positionLine}\nConflicts preserved with another name: {conflicts}\n\nSafety record: {session}"
            : $"Recuperação concluída.\n\nArquivos copiados: {files}\nPastas processadas: {directories}{positionLine}\nConflitos preservados com outro nome: {conflicts}\n\nRegistro de segurança: {session}";
    }
}

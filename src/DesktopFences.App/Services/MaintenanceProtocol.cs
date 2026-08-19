using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Threading;

namespace DesktopFences.App.Services;

internal readonly record struct MaintenanceDispatch(bool Success, bool Shutdown);

internal static class MaintenanceProtocol
{
    public const string PrepareExit = "prepare-exit";
    public const string CreateFence = "create-fence";
    public const string Success = "ok";
    public const string Failed = "failed";

    public static bool IsPrepareExitCommand(string? command) =>
        string.Equals(command, PrepareExit, StringComparison.Ordinal);

    public static bool IsCreateFenceCommand(string? command) =>
        string.Equals(command, CreateFence, StringComparison.Ordinal);

    public static bool IsDestructiveShutdownCommand(string? command) =>
        IsPrepareExitCommand(command);

    public static MaintenanceDispatch Dispatch(
        string? command,
        Func<bool> prepareExit,
        Func<bool> createFence)
    {
        if (IsPrepareExitCommand(command))
        {
            bool ok = prepareExit();
            return new MaintenanceDispatch(ok, Shutdown: ok);
        }

        if (IsCreateFenceCommand(command))
            return new MaintenanceDispatch(createFence(), Shutdown: false);

        return new MaintenanceDispatch(false, Shutdown: false);
    }

    public static string PipeName
    {
        get
        {
            string identity = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToUpperInvariant()));
            return "DesktopFences.Maintenance." + Convert.ToHexString(hash.AsSpan(0, 8));
        }
    }
}

internal sealed class MaintenancePipeServer : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Func<bool> _prepareExit;
    private readonly Func<bool> _createFence;
    private readonly Action _shutdown;
    private readonly CancellationTokenSource _cancel = new();
    private readonly Task _listener;

    public MaintenancePipeServer(
        Dispatcher dispatcher,
        Func<bool> prepareExit,
        Func<bool> createFence,
        Action shutdown)
    {
        _dispatcher = dispatcher;
        _prepareExit = prepareExit;
        _createFence = createFence;
        _shutdown = shutdown;
        _listener = Task.Run(ListenAsync);
    }

    private async Task ListenAsync()
    {
        while (!_cancel.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    MaintenanceProtocol.PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(_cancel.Token);
                using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
                await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true)
                {
                    AutoFlush = true
                };
                string? command = await reader.ReadLineAsync(_cancel.Token);
                MaintenanceDispatch result = MaintenanceProtocol.Dispatch(
                    command,
                    () => SafeInvoke(_prepareExit),
                    () => SafeInvoke(_createFence));
                await writer.WriteLineAsync(result.Success ? MaintenanceProtocol.Success : MaintenanceProtocol.Failed);
                if (result.Shutdown)
                    _ = _dispatcher.BeginInvoke(_shutdown, DispatcherPriority.Background);
            }
            catch (OperationCanceledException) when (_cancel.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                if (!_cancel.IsCancellationRequested)
                    await Task.Delay(100, _cancel.Token).ConfigureAwait(false);
            }
        }
    }

    private bool SafeInvoke(Func<bool> action)
    {
        try
        {
            return _dispatcher.Invoke(action);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _cancel.Cancel();
        try { _listener.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _cancel.Dispose();
    }
}

internal static class MaintenancePipeClient
{
    public static bool RequestPrepareExit(TimeSpan timeout) =>
        Request(MaintenanceProtocol.PrepareExit, timeout);

    public static bool RequestCreateFence(TimeSpan timeout) =>
        Request(MaintenanceProtocol.CreateFence, timeout);

    private static bool Request(string command, TimeSpan timeout)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                MaintenanceProtocol.PipeName,
                PipeDirection.InOut,
                PipeOptions.None);
            pipe.Connect((int)timeout.TotalMilliseconds);
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true)
            {
                AutoFlush = true
            };
            writer.WriteLine(command);
            return string.Equals(reader.ReadLine(), MaintenanceProtocol.Success, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}

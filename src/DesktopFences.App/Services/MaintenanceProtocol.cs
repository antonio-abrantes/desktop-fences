using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Threading;

namespace DesktopFences.App.Services;

internal static class MaintenanceProtocol
{
    public const string PrepareExit = "prepare-exit";
    public const string Success = "ok";
    public const string Failed = "failed";

    public static bool IsPrepareExitCommand(string? command) =>
        string.Equals(command, PrepareExit, StringComparison.Ordinal);

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
    private readonly Action _shutdown;
    private readonly CancellationTokenSource _cancel = new();
    private readonly Task _listener;

    public MaintenancePipeServer(Dispatcher dispatcher, Func<bool> prepareExit, Action shutdown)
    {
        _dispatcher = dispatcher;
        _prepareExit = prepareExit;
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
                bool success = MaintenanceProtocol.IsPrepareExitCommand(command)
                               && _dispatcher.Invoke(_prepareExit);
                await writer.WriteLineAsync(success ? MaintenanceProtocol.Success : MaintenanceProtocol.Failed);
                if (success)
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

    public void Dispose()
    {
        _cancel.Cancel();
        try { _listener.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _cancel.Dispose();
    }
}

internal static class MaintenancePipeClient
{
    public static bool RequestPrepareExit(TimeSpan timeout)
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
            writer.WriteLine(MaintenanceProtocol.PrepareExit);
            return string.Equals(reader.ReadLine(), MaintenanceProtocol.Success, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}

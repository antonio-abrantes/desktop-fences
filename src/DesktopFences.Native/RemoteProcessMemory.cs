using System.Runtime.InteropServices;

namespace DesktopFences.Native;

/// <summary>
/// Aloca/lê/escreve memória no processo dono de uma janela (explorer.exe).
/// Necessário porque LVM_GETITEMPOSITION e LVM_GETITEMTEXTW recebem ponteiros.
/// </summary>
internal sealed class RemoteProcessMemory : IDisposable
{
    private readonly List<IntPtr> _allocations = [];
    private bool _disposed;

    public IntPtr ProcessHandle { get; }

    private RemoteProcessMemory(IntPtr processHandle)
    {
        ProcessHandle = processHandle;
    }

    public static RemoteProcessMemory? OpenFromWindow(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0)
            return null;

        IntPtr handle = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_VM_OPERATION | NativeMethods.PROCESS_VM_READ |
            NativeMethods.PROCESS_VM_WRITE | NativeMethods.PROCESS_QUERY_INFORMATION,
            false, pid);

        return handle == IntPtr.Zero ? null : new RemoteProcessMemory(handle);
    }

    public IntPtr Alloc(int size)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        IntPtr remote = NativeMethods.VirtualAllocEx(
            ProcessHandle,
            IntPtr.Zero,
            (UIntPtr)size,
            NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
            NativeMethods.PAGE_READWRITE);

        if (remote == IntPtr.Zero)
            throw new InvalidOperationException("VirtualAllocEx falhou no explorer.exe.");

        _allocations.Add(remote);
        return remote;
    }

    public void Write<T>(IntPtr remote, T value) where T : struct
    {
        byte[] bytes = NativeMethods.StructToBytes(value);
        if (!NativeMethods.WriteProcessMemory(ProcessHandle, remote, bytes, (UIntPtr)bytes.Length, out _))
            throw new InvalidOperationException("WriteProcessMemory falhou.");
    }

    public T Read<T>(IntPtr remote) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        byte[] buffer = new byte[size];
        if (!NativeMethods.ReadProcessMemory(ProcessHandle, remote, buffer, (UIntPtr)size, out _))
            throw new InvalidOperationException("ReadProcessMemory falhou.");
        return NativeMethods.BytesToStruct<T>(buffer);
    }

    public byte[] ReadBytes(IntPtr remote, int size)
    {
        byte[] buffer = new byte[size];
        if (!NativeMethods.ReadProcessMemory(ProcessHandle, remote, buffer, (UIntPtr)size, out _))
            throw new InvalidOperationException("ReadProcessMemory falhou.");
        return buffer;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (IntPtr alloc in _allocations)
            NativeMethods.VirtualFreeEx(ProcessHandle, alloc, UIntPtr.Zero, NativeMethods.MEM_RELEASE);

        _allocations.Clear();
        NativeMethods.CloseHandle(ProcessHandle);
    }
}

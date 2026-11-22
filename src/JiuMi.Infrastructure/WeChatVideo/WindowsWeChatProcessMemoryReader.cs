using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Windows read-only process memory reader using minimal <c>PROCESS_VM_READ | PROCESS_QUERY_INFORMATION</c>
/// (<c>0x0410</c>) access. Skips <c>MEM_IMAGE</c>, guard pages and non-readable pages.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsWeChatProcessMemoryReader : IWeChatProcessMemoryReader
{
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessQueryInformation = 0x0400;
    private const uint ReadOnlyAccess = ProcessVmRead | ProcessQueryInformation;

    private const uint MemCommit = 0x1000;
    private const uint MemImage = 0x1000000;

    private const uint PageNoAccess = 0x01;
    private const uint PageReadOnly = 0x02;
    private const uint PageReadWrite = 0x04;
    private const uint PageWriteCopy = 0x08;
    private const uint PageExecuteRead = 0x20;
    private const uint PageExecuteReadWrite = 0x40;
    private const uint PageExecuteWriteCopy = 0x80;
    private const uint PageGuard = 0x100;

    private const int ErrorAccessDenied = 5;
    private const int ErrorInvalidParameter = 87;

    private const int ChunkSize = 8 * 1024 * 1024;
    private const int ChunkOverlap = 64 * 1024;
    private const ulong MaxUserAddress = 0x00007FFFFFFFFFFF;
    private const int MaxCommandLineBytes = 32768;

    /// <inheritdoc />
    public WeChatProcessAccessStatus ProbeReadAccess(int processId)
    {
        using SafeProcessHandle handle = OpenProcess(ReadOnlyAccess, bInheritHandle: false, processId);
        return handle.IsInvalid
            ? MapOpenError(Marshal.GetLastWin32Error())
            : WeChatProcessAccessStatus.Accessible;
    }

    /// <inheritdoc />
    public WeChatProcessAccessStatus ScanReadableMemory(
        int processId,
        Func<ReadOnlySpan<byte>, bool> onChunk,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onChunk);
        cancellationToken.ThrowIfCancellationRequested();

        using SafeProcessHandle handle = OpenProcess(ReadOnlyAccess, bInheritHandle: false, processId);
        if (handle.IsInvalid)
        {
            return MapOpenError(Marshal.GetLastWin32Error());
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
        try
        {
            ulong address = 0;
            int mbiSize = Marshal.SizeOf<MemoryBasicInformation>();

            while (address < MaxUserAddress)
            {
                cancellationToken.ThrowIfCancellationRequested();

                nint queried = VirtualQueryEx(
                    handle,
                    (nint)address,
                    out MemoryBasicInformation mbi,
                    (nuint)mbiSize);
                if (queried == 0)
                {
                    break;
                }

                ulong regionBase = (ulong)mbi.BaseAddress;
                ulong regionSize = (ulong)mbi.RegionSize;
                if (regionSize == 0)
                {
                    break;
                }

                bool isReadable = mbi.State == MemCommit &&
                    mbi.Type != MemImage &&
                    (mbi.Protect & PageGuard) == 0 &&
                    (mbi.Protect & PageNoAccess) == 0 &&
                    IsReadableProtect(mbi.Protect);

                if (isReadable)
                {
                    ulong offset = 0;
                    while (offset < regionSize)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int toRead = (int)Math.Min((ulong)ChunkSize, regionSize - offset);
                        if (ReadProcessMemory(
                                handle,
                                (nint)(regionBase + offset),
                                buffer,
                                (nuint)toRead,
                                out nuint bytesRead) &&
                            bytesRead > 0)
                        {
                            if (!onChunk(buffer.AsSpan(0, (int)bytesRead)))
                            {
                                return WeChatProcessAccessStatus.Accessible;
                            }
                        }

                        if (offset + (ulong)toRead >= regionSize)
                        {
                            break;
                        }

                        offset += (ulong)(toRead - ChunkOverlap);
                    }
                }

                ulong next = regionBase + regionSize;
                if (next <= address)
                {
                    break;
                }

                address = next;
            }

            return WeChatProcessAccessStatus.Accessible;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Reads the command line of another process via <c>NtQueryInformationProcess</c> and
    /// <c>ReadProcessMemory</c> using the same minimal read-only handle.
    /// </summary>
    /// <param name="processId">The target process identifier.</param>
    /// <param name="commandLine">The UTF-16 command line when readable.</param>
    /// <returns><see langword="true"/> when the command line was read.</returns>
    public static bool TryReadCommandLine(int processId, out string? commandLine)
    {
        commandLine = null;

        using SafeProcessHandle handle = OpenProcess(ReadOnlyAccess, bInheritHandle: false, processId);
        if (handle.IsInvalid)
        {
            return false;
        }

        int pbiSize = Marshal.SizeOf<ProcessBasicInformation>();
        int status = NtQueryInformationProcess(
            handle,
            processInformationClass: 0,
            out ProcessBasicInformation pbi,
            (uint)pbiSize,
            out _);
        if (status != 0 || pbi.PebBaseAddress == 0)
        {
            return false;
        }

        int pointerSize = nint.Size;
        int processParametersOffset = pointerSize == 8 ? 0x20 : 0x10;
        int commandLineOffset = pointerSize == 8 ? 0x70 : 0x40;

        if (!TryReadPointer(handle, pbi.PebBaseAddress + processParametersOffset, out nint processParameters) ||
            processParameters == 0)
        {
            return false;
        }

        int unicodeStringSize = pointerSize == 8 ? 16 : 8;
        byte[] unicodeBytes = new byte[unicodeStringSize];
        if (!ReadProcessMemory(
                handle,
                processParameters + commandLineOffset,
                unicodeBytes,
                (nuint)unicodeBytes.Length,
                out nuint read) ||
            read < (nuint)unicodeBytes.Length)
        {
            return false;
        }

        ushort byteLength = BitConverter.ToUInt16(unicodeBytes, 0);
        if (byteLength == 0 || byteLength > MaxCommandLineBytes)
        {
            return false;
        }

        nint bufferAddress = pointerSize == 8
            ? (nint)BitConverter.ToInt64(unicodeBytes, 8)
            : BitConverter.ToInt32(unicodeBytes, 4);
        if (bufferAddress == 0)
        {
            return false;
        }

        byte[] cmdBytes = new byte[byteLength];
        if (!ReadProcessMemory(
                handle,
                bufferAddress,
                cmdBytes,
                (nuint)cmdBytes.Length,
                out nuint cmdRead) ||
            cmdRead == 0)
        {
            return false;
        }

        commandLine = Encoding.Unicode.GetString(cmdBytes, 0, (int)cmdRead);
        return true;
    }

    private static bool TryReadPointer(SafeProcessHandle handle, nint address, out nint value)
    {
        byte[] bytes = new byte[nint.Size];
        if (!ReadProcessMemory(handle, address, bytes, (nuint)bytes.Length, out nuint read) ||
            read < (nuint)bytes.Length)
        {
            value = 0;
            return false;
        }

        value = nint.Size == 8
            ? (nint)BitConverter.ToInt64(bytes, 0)
            : BitConverter.ToInt32(bytes, 0);
        return true;
    }

    private static WeChatProcessAccessStatus MapOpenError(int win32Error) => win32Error switch
    {
        ErrorAccessDenied => WeChatProcessAccessStatus.AccessDenied,
        ErrorInvalidParameter => WeChatProcessAccessStatus.NotFound,
        _ => WeChatProcessAccessStatus.Failed,
    };

    private static bool IsReadableProtect(uint protect)
    {
        uint baseProtect = protect & 0xFF;
        return baseProtect is PageReadOnly or PageReadWrite or PageWriteCopy
            or PageExecuteRead or PageExecuteReadWrite or PageExecuteWriteCopy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
    {
        public nint BaseAddress;
        public nint AllocationBase;
        public uint AllocationProtect;
        public nint RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public nint Reserved1;
        public nint PebBaseAddress;
        public nint Reserved2_0;
        public nint Reserved2_1;
        public nint UniqueProcessId;
        public nint Reserved3;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeProcessHandle OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint VirtualQueryEx(
        SafeProcessHandle hProcess,
        nint lpAddress,
        out MemoryBasicInformation lpBuffer,
        nuint dwLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(
        SafeProcessHandle hProcess,
        nint lpBaseAddress,
        [Out] byte[] lpBuffer,
        nuint nSize,
        out nuint lpNumberOfBytesRead);

    [DllImport("ntdll.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int NtQueryInformationProcess(
        SafeProcessHandle processHandle,
        int processInformationClass,
        out ProcessBasicInformation processInformation,
        uint processInformationLength,
        out uint returnLength);
}

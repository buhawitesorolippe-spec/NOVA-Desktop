using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;

namespace Nova.Desktop;

internal static class CloudCredentialStore
{
    private const uint GenericCredential = 1;
    private const uint PersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int MaximumCredentialBlobBytes = 2560;

    public static string? Read(string providerId)
    {
        var target = GetTarget(providerId);
        if (!CredRead(target, GenericCredential, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
                return null;
            throw new Win32Exception(error, "Der Cloud-API-Schlüssel konnte nicht aus dem Windows-Anmeldeinformationsspeicher gelesen werden.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
                return null;
            if (credential.CredentialBlobSize % sizeof(char) != 0)
                throw new InvalidDataException("Der gespeicherte Cloud-API-Schlüssel hat ein ungültiges Format.");
            return Marshal.PtrToStringUni(
                credential.CredentialBlob,
                checked((int)(credential.CredentialBlobSize / sizeof(char))));
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public static void Save(string providerId, string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        var bytes = Encoding.Unicode.GetBytes(apiKey);
        if (bytes.Length > MaximumCredentialBlobBytes)
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw new ArgumentException("Der API-Schlüssel überschreitet das Windows-Limit von 2.560 Bytes.", nameof(apiKey));
        }

        var blob = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new NativeCredential
            {
                Type = GenericCredential,
                TargetName = GetTarget(providerId),
                CredentialBlobSize = checked((uint)bytes.Length),
                CredentialBlob = blob,
                Persist = PersistLocalMachine,
                UserName = "NOVA API key"
            };
            if (!CredWrite(ref credential, 0))
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Der Cloud-API-Schlüssel konnte nicht im Windows-Anmeldeinformationsspeicher gespeichert werden.");
        }
        finally
        {
            Marshal.Copy(new byte[bytes.Length], 0, blob, bytes.Length);
            Marshal.FreeHGlobal(blob);
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public static void Delete(string providerId)
    {
        if (CredDelete(GetTarget(providerId), GenericCredential, 0))
            return;

        var error = Marshal.GetLastWin32Error();
        if (error != ErrorNotFound)
            throw new Win32Exception(error, "Der Cloud-API-Schlüssel konnte nicht aus dem Windows-Anmeldeinformationsspeicher gelöscht werden.");
    }

    private static string GetTarget(string providerId)
    {
        if (!CloudProviderCatalog.IsCloudProvider(providerId))
            throw new ArgumentException("Unbekannter Cloudanbieter.", nameof(providerId));
        return $"NOVA/Cloud/{providerId}";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Comment;
        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll", SetLastError = false)]
    private static extern void CredFree(IntPtr buffer);
}

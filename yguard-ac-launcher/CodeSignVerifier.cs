using System.Runtime.InteropServices;

namespace YGuardAC;

/// <summary>
/// Authenticode check via WinVerifyTrust. Returns true only when Windows
/// considers the file signature present and valid (trusted chain).
/// </summary>
internal static class CodeSignVerifier
{
    /// <summary>
    /// true = trusted Authenticode signature.
    /// false = unsigned, invalid, or unverifiable.
    /// </summary>
    public static bool HasValidSignature(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),
                pcwszFilePath = filePath,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero,
            };

            var guidAction = WINTRUST_ACTION_GENERIC_VERIFY_V2;
            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                pPolicyCallbackData = IntPtr.Zero,
                pSIPClientData = IntPtr.Zero,
                dwUIChoice = WTD_UI_NONE,
                fdwRevocationChecks = WTD_REVOKE_NONE,
                dwUnionChoice = WTD_CHOICE_FILE,
                pFile = IntPtr.Zero,
                dwStateAction = WTD_STATEACTION_IGNORE,
                hWVTStateData = IntPtr.Zero,
                pwszURLReference = IntPtr.Zero,
                dwProvFlags = WTD_CACHE_ONLY_URL_RETRIEVAL | WTD_SAFER_FLAG,
                dwUIContext = 0,
            };

            var pFile = Marshal.AllocHGlobal(Marshal.SizeOf(fileInfo));
            try
            {
                Marshal.StructureToPtr(fileInfo, pFile, false);
                data.pFile = pFile;

                var pData = Marshal.AllocHGlobal(Marshal.SizeOf(data));
                try
                {
                    Marshal.StructureToPtr(data, pData, false);
                    var status = WinVerifyTrust(IntPtr.Zero, ref guidAction, pData);
                    // 0 = ERROR_SUCCESS → signature valid and trusted.
                    return status == 0;
                }
                finally
                {
                    Marshal.FreeHGlobal(pData);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pFile);
            }
        }
        catch
        {
            return false;
        }
    }

    private static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 =
        new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private const uint WTD_UI_NONE = 2;
    private const uint WTD_REVOKE_NONE = 0;
    private const uint WTD_CHOICE_FILE = 1;
    private const uint WTD_STATEACTION_IGNORE = 0;
    private const uint WTD_CACHE_ONLY_URL_RETRIEVAL = 0x00001000;
    private const uint WTD_SAFER_FLAG = 0x00000100;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
    }

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false)]
    private static extern int WinVerifyTrust(
        IntPtr hwnd,
        ref Guid pgActionID,
        IntPtr pWinTrustData);
}

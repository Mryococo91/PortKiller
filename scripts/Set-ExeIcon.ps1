param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,
    [Parameter(Mandatory = $true)]
    [string]$IcoPath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ExePath)) {
    throw "Exe not found: $ExePath"
}
if (-not (Test-Path $IcoPath)) {
    throw "Ico not found: $IcoPath"
}

if (-not ("ExeIconWriter" -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

public static class ExeIconWriter
{
    private const uint RtIcon = 3;
    private const uint RtGroupIcon = 14;
    private const uint LoadLibraryAsDatafile = 2;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr BeginUpdateResource(string pFileName, bool bDeleteExistingResources);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateResource(
        IntPtr hUpdate,
        IntPtr lpType,
        IntPtr lpName,
        ushort wLanguage,
        byte[] lpData,
        uint cbData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(IntPtr hModule);

    private delegate bool EnumResNameProc(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam);
    private delegate bool EnumResLangProc(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, ushort wIDLanguage, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EnumResourceNames(IntPtr hModule, IntPtr lpszType, EnumResNameProc lpEnumFunc, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EnumResourceLanguages(IntPtr hModule, IntPtr lpType, IntPtr lpName, EnumResLangProc lpEnumFunc, IntPtr lParam);

    private sealed class IconRef
    {
        public IntPtr Type;
        public IntPtr Name;
        public ushort Language;
    }

    public static void SetIcon(string exePath, string icoPath)
    {
        byte[] ico = File.ReadAllBytes(icoPath);
        if (ico.Length < 6)
        {
            throw new InvalidDataException("ICO file is too small.");
        }

        ushort type = BitConverter.ToUInt16(ico, 2);
        ushort count = BitConverter.ToUInt16(ico, 4);
        if (type != 1 || count == 0)
        {
            throw new InvalidDataException("Invalid ICO file.");
        }

        var images = new byte[count][];
        var group = new byte[6 + (count * 14)];
        Array.Copy(ico, 0, group, 0, 6);

        for (int i = 0; i < count; i++)
        {
            int entry = 6 + (i * 16);
            int bytesInRes = BitConverter.ToInt32(ico, entry + 8);
            int imageOffset = BitConverter.ToInt32(ico, entry + 12);
            images[i] = new byte[bytesInRes];
            Array.Copy(ico, imageOffset, images[i], 0, bytesInRes);

            int groupEntry = 6 + (i * 14);
            Array.Copy(ico, entry, group, groupEntry, 12);
            BitConverter.GetBytes((ushort)(i + 1)).CopyTo(group, groupEntry + 12);
        }

        List<IconRef> existing = ListIconResources(exePath);

        IntPtr update = BeginUpdateResource(exePath, false);
        if (update == IntPtr.Zero)
        {
            throw new InvalidOperationException("BeginUpdateResource failed (code " + Marshal.GetLastWin32Error() + ").");
        }

        bool ok = true;
        foreach (IconRef item in existing)
        {
            ok = UpdateResource(update, item.Type, item.Name, item.Language, null, 0);
            if (!ok)
            {
                break;
            }
        }

        for (int i = 0; i < count && ok; i++)
        {
            ok = UpdateResource(
                update,
                (IntPtr)RtIcon,
                (IntPtr)(i + 1),
                0,
                images[i],
                (uint)images[i].Length);
        }

        if (ok)
        {
            ok = UpdateResource(
                update,
                (IntPtr)RtGroupIcon,
                (IntPtr)1,
                0,
                group,
                (uint)group.Length);
        }

        if (!EndUpdateResource(update, !ok))
        {
            throw new InvalidOperationException("EndUpdateResource failed (code " + Marshal.GetLastWin32Error() + ").");
        }

        if (!ok)
        {
            throw new InvalidOperationException("UpdateResource failed (code " + Marshal.GetLastWin32Error() + ").");
        }
    }

    private static List<IconRef> ListIconResources(string exePath)
    {
        var items = new List<IconRef>();
        IntPtr module = LoadLibraryEx(exePath, IntPtr.Zero, LoadLibraryAsDatafile);
        if (module == IntPtr.Zero)
        {
            throw new InvalidOperationException("LoadLibraryEx failed (code " + Marshal.GetLastWin32Error() + ").");
        }

        try
        {
            Collect(module, (IntPtr)RtGroupIcon, items);
            Collect(module, (IntPtr)RtIcon, items);
        }
        finally
        {
            FreeLibrary(module);
        }

        return items;
    }

    private static void Collect(IntPtr module, IntPtr type, List<IconRef> items)
    {
        EnumResourceNames(module, type, (hModule, lpType, lpName, lParam) =>
        {
            EnumResourceLanguages(hModule, lpType, lpName, (h, t, n, lang, lp) =>
            {
                items.Add(new IconRef { Type = t, Name = n, Language = lang });
                return true;
            }, IntPtr.Zero);
            return true;
        }, IntPtr.Zero);
    }
}
'@
}

[ExeIconWriter]::SetIcon((Resolve-Path $ExePath).Path, (Resolve-Path $IcoPath).Path)

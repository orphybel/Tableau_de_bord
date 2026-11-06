using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lanceur.Models;

namespace Lanceur.Services;

/// <summary>Récupère les icônes des programmes via le Shell Windows (48 ou 256 px), avec cache.</summary>
public static class IconService
{
    private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico" };

    public static void Clear() => Cache.Clear();

    public static ImageSource? Get(LaunchItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.IconPath))
        {
            var custom = GetForPath(item.IconPath);
            if (custom != null) return custom;
        }
        return GetForPath(item.Path);
    }

    public static ImageSource? GetForPath(string? raw)
    {
        var path = PathHelper.FindExisting(raw);
        if (path is null) return null;
        if (Cache.TryGetValue(path, out var cached)) return cached;

        ImageSource? img = null;
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            img = ImageExtensions.Contains(ext) && File.Exists(path) ? LoadImageFile(path) : LoadShellIcon(path);
        }
        catch { /* icône par défaut (monogramme) */ }

        if (img is null)
        {
            try { img = LoadAssociatedIcon(path); } catch { }
        }

        if (img != null) Cache[path] = img;
        return img;
    }

    private static ImageSource? LoadImageFile(string path)
    {
        using var fs = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        // Pour les .ico, on prend la plus grande taille disponible
        var frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
        frame.Freeze();
        return frame;
    }

    private static ImageSource? LoadAssociatedIcon(string path)
    {
        if (!File.Exists(path)) return null;
        using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
        if (icon is null) return null;
        var src = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        src.Freeze();
        return src;
    }

    private static ImageSource? LoadShellIcon(string path)
    {
        var info = new SHFILEINFO();
        if (SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_SYSICONINDEX) == IntPtr.Zero)
            return null;

        // 256 px si l'icône existe vraiment en grand, sinon 48 px
        var jumbo = FromImageList(SHIL_JUMBO, info.iIcon);
        if (jumbo != null && !IsSmallIconInLargeCanvas(jumbo))
            return jumbo;
        return FromImageList(SHIL_EXTRALARGE, info.iIcon) ?? jumbo;
    }

    private static BitmapSource? FromImageList(int listId, int index)
    {
        var iid = IID_IImageList;
        if (SHGetImageList(listId, ref iid, out var list) != 0 || list is null) return null;
        var hIcon = IntPtr.Zero;
        try
        {
            if (list.GetIcon(index, ILD_TRANSPARENT, ref hIcon) != 0 || hIcon == IntPtr.Zero) return null;
            var src = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        finally
        {
            if (hIcon != IntPtr.Zero) DestroyIcon(hIcon);
            Marshal.ReleaseComObject(list);
        }
    }

    /// <summary>Les programmes sans icône 256 px apparaissent en 48 px dans le coin d'un canevas 256 : on le détecte.</summary>
    private static bool IsSmallIconInLargeCanvas(BitmapSource src)
    {
        if (src.PixelWidth <= 48) return false;
        var bmp = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        int w = bmp.PixelWidth, h = bmp.PixelHeight, stride = w * 4;
        var px = new byte[stride * h];
        bmp.CopyPixels(px, stride, 0);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if ((x >= 48 || y >= 48) && px[y * stride + x * 4 + 3] > 0)
                    return false;
        return true;
    }

    // ---- Interop Shell ----

    private const uint SHGFI_SYSICONINDEX = 0x4000;
    private const int SHIL_EXTRALARGE = 0x2;
    private const int SHIL_JUMBO = 0x4;
    private const int ILD_TRANSPARENT = 0x1;
    private static readonly Guid IID_IImageList = new("46EB5926-582E-4017-9FDF-E8998DAA0950");

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll", EntryPoint = "SHGetImageList")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IImageList? ppv);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
        [PreserveSig] int Draw(IntPtr pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, ref IntPtr picon);
    }
}

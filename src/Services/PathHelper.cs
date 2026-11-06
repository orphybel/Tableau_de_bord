using System;
using System.Diagnostics;
using System.IO;

namespace Lanceur.Services;

public static class PathHelper
{
    /// <summary>Nettoie un chemin saisi : guillemets, espaces, variables d'environnement.</summary>
    public static string Expand(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var s = raw.Trim();
        if (s.Length >= 2 && s.StartsWith('"') && s.EndsWith('"')) s = s[1..^1];
        return Environment.ExpandEnvironmentVariables(s);
    }

    public static bool IsUrl(string s) =>
        Uri.TryCreate(s, UriKind.Absolute, out var u) && !u.IsFile && u.Scheme.Length > 1;

    /// <summary>
    /// Renvoie le chemin complet d'un fichier existant, en cherchant dans le PATH si besoin
    /// (ex. "notepad" ou "code.exe"). Renvoie null si introuvable.
    /// </summary>
    public static string? FindExisting(string? raw)
    {
        var p = Expand(raw);
        if (p.Length == 0 || IsUrl(p)) return null;
        try
        {
            if (Path.IsPathRooted(p))
                return File.Exists(p) || Directory.Exists(p) ? p : null;

            var candidates = Path.HasExtension(p) ? new[] { p } : new[] { p + ".exe", p };
            var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var dir in dirs)
                foreach (var c in candidates)
                {
                    var full = Path.Combine(dir.Trim(), c);
                    if (File.Exists(full)) return full;
                }
        }
        catch { /* chemin invalide */ }
        return null;
    }

    /// <summary>Nom lisible d'un programme : description du .exe, sinon nom du fichier.</summary>
    public static string FriendlyName(string path)
    {
        try
        {
            if (Directory.Exists(path))
                return new DirectoryInfo(path).Name;

            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                var desc = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
                if (!string.IsNullOrEmpty(desc) && desc.Length <= 40) return desc;
            }
        }
        catch { /* on retombe sur le nom du fichier */ }
        return Path.GetFileNameWithoutExtension(path);
    }
}

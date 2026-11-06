using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Lanceur.Models;

namespace Lanceur.Services;

public static class LaunchService
{
    private const int ErrorCancelled = 1223; // l'utilisateur a refusé l'élévation UAC

    /// <summary>Lance l'élément. Renvoie null si OK, "" si annulé par l'utilisateur, sinon un message d'erreur.</summary>
    public static string? Launch(LaunchItem item, bool forceAdmin = false)
    {
        var target = PathHelper.Expand(item.Path);
        if (target.Length == 0)
            return "Aucun programme n'est défini pour ce bouton.";

        if (!PathHelper.IsUrl(target) && Path.IsPathRooted(target) && !File.Exists(target) && !Directory.Exists(target))
            return $"Le fichier est introuvable :\n{target}";

        var psi = new ProcessStartInfo
        {
            FileName = target,
            Arguments = Environment.ExpandEnvironmentVariables(item.Arguments ?? ""),
            UseShellExecute = true,
        };

        var wd = PathHelper.Expand(item.WorkingDirectory);
        if (wd.Length > 0 && Directory.Exists(wd))
            psi.WorkingDirectory = wd;
        else if (File.Exists(target))
            psi.WorkingDirectory = Path.GetDirectoryName(target) ?? "";

        if (item.RunAsAdmin || forceAdmin)
            psi.Verb = "runas";

        try
        {
            using var _ = Process.Start(psi);
            return null;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return "";
        }
        catch (Exception ex)
        {
            return $"Windows n'a pas pu lancer « {item.Name} ».\n\n{ex.Message}";
        }
    }

    public static void OpenLocation(LaunchItem item)
    {
        var p = PathHelper.FindExisting(item.Path);
        if (p is null) return;
        if (Directory.Exists(p))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{p}\"") { UseShellExecute = true });
        else
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{p}\"") { UseShellExecute = true });
    }

    public static void OpenFolder(string folder)
    {
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }
}

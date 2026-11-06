using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Lanceur.Models;

namespace Lanceur.Services;

public static class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string AppDataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lanceur");

    /// <summary>
    /// Mode portable : si un fichier "lanceur.config.json" se trouve à côté du .exe, il est utilisé.
    /// Sinon la configuration est stockée dans %APPDATA%\Lanceur\config.json.
    /// </summary>
    public static string ConfigPath { get; } = ResolveConfigPath();

    public static bool IsPortable { get; private set; }

    /// <summary>Message renseigné si le fichier existant était illisible (une copie de sauvegarde a été faite).</summary>
    public static string? LoadWarning { get; private set; }

    private static string ResolveConfigPath()
    {
        var portable = Path.Combine(AppContext.BaseDirectory, "lanceur.config.json");
        if (File.Exists(portable))
        {
            IsPortable = true;
            return portable;
        }
        return Path.Combine(AppDataDir, "config.json");
    }

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return CreateDefault();

        try
        {
            var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), JsonOptions)
                      ?? throw new InvalidDataException("Fichier vide");
            return Normalize(cfg);
        }
        catch (Exception ex)
        {
            var backup = ConfigPath + $".illisible-{DateTime.Now:yyyyMMdd-HHmmss}.bak";
            try { File.Copy(ConfigPath, backup, true); } catch { /* on fait au mieux */ }
            LoadWarning = $"Le fichier de configuration était illisible ({ex.Message}).\n" +
                          $"Une copie a été conservée : {backup}\nUne configuration par défaut a été chargée.";
            return CreateDefault();
        }
    }

    public static void Save(AppConfig cfg) => WriteAtomic(ConfigPath, cfg);

    public static void Export(AppConfig cfg, string path) => WriteAtomic(path, cfg);

    public static AppConfig Import(string path)
    {
        var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException("Le fichier ne contient pas de configuration.");
        return Normalize(cfg);
    }

    private static void WriteAtomic(string path, AppConfig cfg)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(cfg, JsonOptions));
        File.Move(tmp, path, overwrite: true);
    }

    private static AppConfig Normalize(AppConfig cfg)
    {
        cfg.Groups ??= new ObservableCollection<LaunchGroup>();
        foreach (var g in cfg.Groups.ToList())
        {
            if (g is null) { cfg.Groups.Remove(g!); continue; }
            g.Items ??= new ObservableCollection<LaunchItem>();
            foreach (var it in g.Items.Where(i => i is null).ToList()) g.Items.Remove(it);
            foreach (var it in g.Items)
                if (string.IsNullOrWhiteSpace(it.Id)) it.Id = Guid.NewGuid().ToString("N");
        }
        if (cfg.Groups.Count == 0) cfg.Groups.Add(new LaunchGroup { Name = "Favoris" });
        cfg.SelectedGroupIndex = Math.Clamp(cfg.SelectedGroupIndex, 0, cfg.Groups.Count - 1);
        cfg.Title ??= "Mon tableau de bord";
        cfg.Theme ??= "Dark";
        cfg.AccentColor ??= "#6366F1";
        cfg.TileSize ??= "Medium";
        cfg.Hotkey ??= "Ctrl+Alt+Space";
        return cfg;
    }

    public static AppConfig CreateDefault()
    {
        var cfg = new AppConfig();
        cfg.Groups.Add(new LaunchGroup
        {
            Name = "Favoris",
            Items =
            {
                new LaunchItem { Name = "Bloc-notes", Path = @"%SystemRoot%\System32\notepad.exe", Color = "#3B82F6" },
                new LaunchItem { Name = "Calculatrice", Path = @"%SystemRoot%\System32\calc.exe", Color = "#22C55E" },
                new LaunchItem { Name = "Explorateur", Path = @"%SystemRoot%\explorer.exe", Color = "#EAB308" },
                new LaunchItem { Name = "Invite de commandes", Path = @"%SystemRoot%\System32\cmd.exe", WorkingDirectory = "%USERPROFILE%" },
            }
        });
        cfg.Groups.Add(new LaunchGroup
        {
            Name = "Outils système",
            Items =
            {
                new LaunchItem { Name = "Gestionnaire des tâches", Path = @"%SystemRoot%\System32\Taskmgr.exe" },
                new LaunchItem { Name = "Panneau de configuration", Path = @"%SystemRoot%\System32\control.exe" },
                new LaunchItem { Name = "Éditeur du Registre", Path = @"%SystemRoot%\regedit.exe", RunAsAdmin = true, Color = "#EF4444" },
            }
        });
        return cfg;
    }
}

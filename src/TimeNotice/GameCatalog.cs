// Phase 3 built-in catalog (SCOPE.md §5). Small verified list, 12 max.
// Priority: user GameExes mapping > this catalog > heuristic (Phase 4).
// Ignored entries here are Policy=Ignore and never count as gameplay, even
// if a user lists the same exe under Games (ignore wins).
// ponytail: static data table now, JSON-downloadable catalog later if asked.

namespace TimeNotice;

public sealed record CatalogEntry(string Exe, string Display, string Policy);

public static class GameCatalog
{
    // Keep under 12 entries per SCOPE §5. Exe spellings are the Valve/Epic/GOG/
    // Ubisoft/EA client process names plus a few widely-known game binaries.
    // If a name turns out wrong on the test PC, override locally via Settings
    // (Games / Ignored boxes) — no code change needed.
    public static readonly List<CatalogEntry> Entries = new()
    {
        new("steam.exe", "Steam", "Ignore"),
        new("epicgameslauncher.exe", "Epic Games Launcher", "Ignore"),
        new("GalaxyClient.exe", "GOG Galaxy", "Ignore"),
        new("UbisoftConnect.exe", "Ubisoft Connect", "Ignore"),
        new("eaapp.exe", "EA App", "Ignore"),
        // Multi-exe franchise listed explicitly (same title, two binaries).
        new("cs2.exe", "Counter-Strike 2", "Normal"),
        new("csgo.exe", "Counter-Strike (Legacy)", "Normal"),
        new("eldenring.exe", "Elden Ring", "Normal"),
        new("Balatro.exe", "Balatro", "Normal"),
    };

    public static List<string> DefaultIgnored() =>
        Entries.Where(e => e.Policy.Equals("Ignore", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Exe).ToList();

    public static CatalogEntry? Find(string? exe)
    {
        if (string.IsNullOrWhiteSpace(exe)) return null;
        return Entries.FirstOrDefault(e =>
            string.Equals(e.Exe, exe.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsIgnore(string? exe) =>
        Find(exe)?.Policy.Equals("Ignore", StringComparison.OrdinalIgnoreCase) == true;
}

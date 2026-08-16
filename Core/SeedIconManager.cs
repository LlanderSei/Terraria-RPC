using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TerrariaRPC.Core
{
  // ── Config model ────────────────────────────────────────────────────────────

  /// <summary>Variant set for seeds that depend on evil + hardmode.</summary>
  public class EvilHardmodeVariants
  {
    [JsonPropertyName("corrupt")]           public string Corrupt          { get; set; } = "";
    [JsonPropertyName("corrupt_hardmode")]  public string CorruptHardmode  { get; set; } = "";
    [JsonPropertyName("crimson")]           public string Crimson          { get; set; } = "";
    [JsonPropertyName("crimson_hardmode")]  public string CrimsonHardmode  { get; set; } = "";
  }

  /// <summary>Drunk only depends on hardmode (combines both evils in one icon).</summary>
  public class DrunkVariants
  {
    [JsonPropertyName("normal")]   public string Normal   { get; set; } = "";
    [JsonPropertyName("hardmode")] public string Hardmode { get; set; } = "";
  }

  /// <summary>Skyblock only has a single icon.</summary>
  public class SkyblockVariant
  {
    [JsonPropertyName("default")] public string Default { get; set; } = "";
  }

  public class SpecialSeedIcons
  {
    [JsonPropertyName("not_the_bees")]    public EvilHardmodeVariants NotTheBees     { get; set; } = new();
    [JsonPropertyName("drunk")]           public DrunkVariants         Drunk          { get; set; } = new();
    [JsonPropertyName("celebrationmk10")] public EvilHardmodeVariants CelebrationMk10 { get; set; } = new();
    [JsonPropertyName("the_constant")]    public EvilHardmodeVariants TheConstant    { get; set; } = new();
    [JsonPropertyName("for_the_worthy")]  public EvilHardmodeVariants ForTheWorthy   { get; set; } = new();
    [JsonPropertyName("no_traps")]        public EvilHardmodeVariants NoTraps        { get; set; } = new();
    [JsonPropertyName("remix")]           public EvilHardmodeVariants Remix          { get; set; } = new();
    [JsonPropertyName("skyblock")]        public SkyblockVariant       Skyblock       { get; set; } = new();
  }

  public class WorldIconConfig
  {
    [JsonPropertyName("defaultIcons")]      public EvilHardmodeVariants DefaultIcons     { get; set; } = new();
    [JsonPropertyName("secretSeedIcon")]    public string               SecretSeedIcon   { get; set; } = "";
    [JsonPropertyName("specialSeedIcons")]  public SpecialSeedIcons     SpecialSeedIcons { get; set; } = new();
    [JsonPropertyName("cyclIntervalSecs")]  public int                  CycleIntervalSecs { get; set; } = 5;
  }

  // ── Manager ─────────────────────────────────────────────────────────────────

  public class SeedIconManager
  {
    /// <summary>
    /// The canonical order in which Zenith (and other combinations) cycle through
    /// special seed icons. Skyblock is appended separately after this list.
    /// </summary>
    private static readonly string[] SpecialSeedOrder =
    {
      "Not The Bees", "Drunk", "Celebrationmk10", "The Constant",
      "For The Worthy", "No Traps", "Remix"
    };

    private static readonly HashSet<string> ZenithImplied = new(StringComparer.OrdinalIgnoreCase)
    {
      "Drunk", "Not The Bees", "For The Worthy", "Celebrationmk10",
      "The Constant", "Remix", "No Traps"
    };

    private readonly string configPath = "world_icons.json";
    private WorldIconConfig config = new();

    // Rotation state
    private List<string> rotationUrls = new();
    private int currentIndex = 0;
    private DateTime lastCycleTime = DateTime.MinValue;
    private string lastWorldSignature = "";

    public SeedIconManager()
    {
      LoadConfig();
    }

    // ── Config loading ─────────────────────────────────────────────────────

    private void LoadConfig()
    {
      if (File.Exists(configPath))
      {
        try
        {
          var json = File.ReadAllText(configPath);
          config = JsonSerializer.Deserialize<WorldIconConfig>(json) ?? new WorldIconConfig();
          Logger.Info($"Loaded world_icons.json");
        }
        catch (Exception ex)
        {
          Logger.Warn($"Failed to load world_icons.json: {ex.Message}. Using defaults.");
          config = new WorldIconConfig();
        }
      }
      else
      {
        // Write a skeleton config with all keys so the user can fill in their URLs
        config = MakeDefaultConfig();
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(configPath, JsonSerializer.Serialize(config, opts));
        Logger.Info($"Created default world_icons.json — fill in your image URLs.");
      }
    }

    private static WorldIconConfig MakeDefaultConfig() => new WorldIconConfig
    {
      CycleIntervalSecs = 3,
      SecretSeedIcon = "https://terraria.wiki.gg/images/Seed_Secret.png",
      DefaultIcons = new EvilHardmodeVariants
      {
        Corrupt         = "https://terraria.wiki.gg/images/IconCorruption.png",
        CorruptHardmode = "https://terraria.wiki.gg/images/IconHallowCorruption.png",
        Crimson         = "https://terraria.wiki.gg/images/IconCrimson.png",
        CrimsonHardmode = "https://terraria.wiki.gg/images/IconHallowCrimson.png"
      },
      SpecialSeedIcons = new SpecialSeedIcons
      {
        NotTheBees = new EvilHardmodeVariants
        {
          Corrupt         = "https://terraria.wiki.gg/images/IconCorruptionNotTheBees.png",
          CorruptHardmode = "https://terraria.wiki.gg/images/IconHallowCorruptionNotTheBees.png",
          Crimson         = "https://terraria.wiki.gg/images/IconCrimsonNotTheBees.png",
          CrimsonHardmode = "https://terraria.wiki.gg/images/IconHallowCrimsonNotTheBees.png"
        },
        Drunk = new DrunkVariants
        {
          Normal   = "https://terraria.wiki.gg/images/IconCorruptionCrimson.png",
          Hardmode = "https://terraria.wiki.gg/images/IconHallowCorruptionCrimson.png"
        },
        CelebrationMk10 = new EvilHardmodeVariants
        {
          Corrupt         = "https://terraria.wiki.gg/images/IconCorruptionAnniversary.png",
          CorruptHardmode = "https://terraria.wiki.gg/images/IconHallowCorruptionAnniversary.png",
          Crimson         = "https://terraria.wiki.gg/images/IconCrimsonAnniversary.png",
          CrimsonHardmode = "https://terraria.wiki.gg/images/IconHallowCrimsonAnniversary.png"
        },
        TheConstant = new EvilHardmodeVariants
        {
          Corrupt         = "https://terraria.wiki.gg/images/IconCorruptionDontStarve.png",
          CorruptHardmode = "https://terraria.wiki.gg/images/IconHallowCorruptionDontStarve.png",
          Crimson         = "https://terraria.wiki.gg/images/IconCrimsonDontStarve.png",
          CrimsonHardmode = "https://terraria.wiki.gg/images/IconHallowCrimsonDontStarve.png"
        },
        ForTheWorthy = new EvilHardmodeVariants
        {
          Corrupt         = "https://terraria.wiki.gg/images/IconCorruptionFTW.png",
          CorruptHardmode = "https://terraria.wiki.gg/images/IconHallowCorruptionFTW.png",
          Crimson         = "https://terraria.wiki.gg/images/IconCrimsonFTW.png",
          CrimsonHardmode = "https://terraria.wiki.gg/images/IconHallowCrimsonFTW.png"
        },
        NoTraps = new EvilHardmodeVariants
        {
          Corrupt         = "https://terraria.wiki.gg/images/IconCorruptionTraps.png",
          CorruptHardmode = "https://terraria.wiki.gg/images/IconHallowCorruptionTraps.png",
          Crimson         = "https://terraria.wiki.gg/images/IconCrimsonTraps.png",
          CrimsonHardmode = "https://terraria.wiki.gg/images/IconHallowCrimsonTraps.png"
        },
        Remix = new EvilHardmodeVariants
        {
          Corrupt         = "https://terraria.wiki.gg/images/IconCorruptionRemix.png",
          CorruptHardmode = "https://terraria.wiki.gg/images/IconHallowCorruptionRemix.png",
          Crimson         = "https://terraria.wiki.gg/images/IconCrimsonRemix.png",
          CrimsonHardmode = "https://terraria.wiki.gg/images/IconHallowCrimsonRemix.png"
        },
        Skyblock = new SkyblockVariant
        {
          Default = "https://terraria.wiki.gg/images/IconSkyblock.png"
        }
      }
    };

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the icon rotation list only when the world state signature changes.
    /// Safe to call every RPC tick — won't reset the cycling timer unnecessarily.
    /// </summary>
    public void UpdateWorldState(TerrariaGameState state)
    {
      // Build a compact signature of everything that affects the rotation
      string sig = $"{state.WorldEvil}|{state.WorldIsHardmode}|{string.Join(',', state.WorldSpecialSeeds)}|{state.WorldSecretSeedsAsNum}";

      if (sig == lastWorldSignature) return; // nothing changed, keep cycling

      lastWorldSignature = sig;
      rotationUrls = BuildRotation(state);
      currentIndex = 0;
      lastCycleTime = DateTime.Now;
    }

    /// <summary>Returns the URL of the icon to display right now, cycling on interval.</summary>
    public string GetCurrentIconUrl()
    {
      if (rotationUrls.Count == 0) return "";

      // Advance index if interval elapsed and there's more than one icon
      if (rotationUrls.Count > 1 &&
          DateTime.Now - lastCycleTime >= TimeSpan.FromSeconds(config.CycleIntervalSecs))
      {
        currentIndex = (currentIndex + 1) % rotationUrls.Count;
        lastCycleTime = DateTime.Now;
      }

      return rotationUrls[currentIndex];
    }

    // ── Rotation builder ───────────────────────────────────────────────────

    private List<string> BuildRotation(TerrariaGameState state)
    {
      bool isCrimson  = state.WorldEvil == "Crimson";
      bool isHardmode = state.WorldIsHardmode;

      // The resolved special seed list (already Zenith-collapsed by TerrariaMemoryReader)
      var specialSeeds = state.WorldSpecialSeeds;
      bool hasSpecial  = specialSeeds.Length > 0;
      bool hasSecret   = state.WorldSecretSeedsAsNum > 0;
      bool isZenith    = specialSeeds.Contains("Zenith");

      var urls = new List<string>();

      if (hasSpecial)
      {
        // Determine which seeds to iterate:
        // - Zenith: use ALL seeds in SpecialSeedOrder (it forces them all)
        // - Otherwise: iterate SpecialSeedOrder but only add if the seed is active
        IEnumerable<string> seedsToShow = isZenith
          ? SpecialSeedOrder
          : SpecialSeedOrder.Where(s => specialSeeds.Contains(s, StringComparer.OrdinalIgnoreCase));

        foreach (string seed in seedsToShow)
        {
          string url = ResolveSpecialSeedUrl(seed, isCrimson, isHardmode);
          if (!string.IsNullOrEmpty(url)) urls.Add(url);
        }

        // Skyblock is always last in the special seed block
        if (specialSeeds.Contains("Skyblock"))
        {
          string skUrl = config.SpecialSeedIcons.Skyblock.Default;
          if (!string.IsNullOrEmpty(skUrl)) urls.Add(skUrl);
        }
      }
      else
      {
        // No special seeds — use the default evil/hardmode icon
        string defUrl = ResolveDefaultIcon(isCrimson, isHardmode);
        if (!string.IsNullOrEmpty(defUrl)) urls.Add(defUrl);
      }

      // Secret seeds always go at the end of the rotation (after everything else)
      if (hasSecret && !string.IsNullOrEmpty(config.SecretSeedIcon))
        urls.Add(config.SecretSeedIcon);

      return urls;
    }

    private string ResolveDefaultIcon(bool isCrimson, bool isHardmode)
    {
      var d = config.DefaultIcons;
      return (isCrimson, isHardmode) switch
      {
        (true,  true)  => d.CrimsonHardmode,
        (true,  false) => d.Crimson,
        (false, true)  => d.CorruptHardmode,
        (false, false) => d.Corrupt
      };
    }

    private string ResolveSpecialSeedUrl(string seedName, bool isCrimson, bool isHardmode)
    {
      var sp = config.SpecialSeedIcons;

      // Drunk only cares about hardmode, not evil type
      if (seedName.Equals("Drunk", StringComparison.OrdinalIgnoreCase))
        return isHardmode ? sp.Drunk.Hardmode : sp.Drunk.Normal;

      // Skyblock handled separately
      if (seedName.Equals("Skyblock", StringComparison.OrdinalIgnoreCase))
        return sp.Skyblock.Default;

      // All other special seeds: 4 variants (evil × hardmode)
      EvilHardmodeVariants? variants = seedName.ToLower() switch
      {
        "not the bees"    => sp.NotTheBees,
        "celebrationmk10" => sp.CelebrationMk10,
        "the constant"    => sp.TheConstant,
        "for the worthy"  => sp.ForTheWorthy,
        "no traps"        => sp.NoTraps,
        "remix"           => sp.Remix,
        _                 => null
      };

      if (variants == null) return "";

      return (isCrimson, isHardmode) switch
      {
        (true,  true)  => variants.CrimsonHardmode,
        (true,  false) => variants.Crimson,
        (false, true)  => variants.CorruptHardmode,
        (false, false) => variants.Corrupt
      };
    }
  }
}

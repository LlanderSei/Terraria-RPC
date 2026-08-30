using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TerrariaRPC.Core
{
  // ── Config model ────────────────────────────────────────────────────────────

  public class EvilHardmodeVariants
  {
    [JsonPropertyName("corrupt")] public string Corrupt { get; set; } = "";
    [JsonPropertyName("corrupt_hardmode")] public string CorruptHardmode { get; set; } = "";
    [JsonPropertyName("crimson")] public string Crimson { get; set; } = "";
    [JsonPropertyName("crimson_hardmode")] public string CrimsonHardmode { get; set; } = "";
  }

  public class DrunkVariants
  {
    [JsonPropertyName("normal")] public string Normal { get; set; } = "";
    [JsonPropertyName("hardmode")] public string Hardmode { get; set; } = "";
  }

  public class SkyblockVariant
  {
    [JsonPropertyName("default")] public string Default { get; set; } = "";
  }

  public class SpecialSeedIcons
  {
    [JsonPropertyName("not_the_bees")] public EvilHardmodeVariants NotTheBees { get; set; } = new();
    [JsonPropertyName("drunk")] public DrunkVariants Drunk { get; set; } = new();
    [JsonPropertyName("celebrationmk10")] public EvilHardmodeVariants CelebrationMk10 { get; set; } = new();
    [JsonPropertyName("the_constant")] public EvilHardmodeVariants TheConstant { get; set; } = new();
    [JsonPropertyName("for_the_worthy")] public EvilHardmodeVariants ForTheWorthy { get; set; } = new();
    [JsonPropertyName("no_traps")] public EvilHardmodeVariants NoTraps { get; set; } = new();
    [JsonPropertyName("remix")] public EvilHardmodeVariants Remix { get; set; } = new();
    [JsonPropertyName("skyblock")] public SkyblockVariant Skyblock { get; set; } = new();
  }

  public class WorldIconConfig
  {
    [JsonPropertyName("defaultIcons")] public EvilHardmodeVariants DefaultIcons { get; set; } = new();
    [JsonPropertyName("secretSeedIcon")] public string SecretSeedIcon { get; set; } = "";
    [JsonPropertyName("specialSeedIcons")] public SpecialSeedIcons SpecialSeedIcons { get; set; } = new();
    [JsonPropertyName("cyclIntervalSecs")] public int CycleIntervalSecs { get; set; } = 5;
  }

  public class FullIconsConfig
  {
    [JsonPropertyName("worldIcons")]
    public WorldIconConfig WorldIcons { get; set; } = new();

    [JsonPropertyName("bossIcons")]
    public Dictionary<string, string> BossIcons { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("eventIcons")]
    public Dictionary<string, string> EventIcons { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("peacefulIcons")]
    public Dictionary<string, string> PeacefulIcons { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("weatherIcons")]
    public Dictionary<string, string> WeatherIcons { get; set; } = new(StringComparer.OrdinalIgnoreCase);
  }

  // ── Icon Manager ────────────────────────────────────────────────────────────

  public class IconManager
  {
    private static readonly string[] SpecialSeedOrder =
    {
      "Not The Bees", "Drunk", "Celebrationmk10", "The Constant",
      "For The Worthy", "No Traps", "Remix"
    };

    private readonly string primaryConfigPath = Path.Combine(AppContext.BaseDirectory, "icons.json");
    private readonly string legacyConfigPath = Path.Combine(AppContext.BaseDirectory, "world_icons.json");

    private FullIconsConfig fullConfig = new();

    // World rotation state
    private List<string> worldRotationUrls = new();
    private int worldCurrentIndex = 0;
    private DateTime worldLastCycleTime = DateTime.MinValue;
    private string lastWorldSignature = "";

    public IconManager()
    {
      LoadConfig();
    }

    private void LoadConfig()
    {
      if (File.Exists(primaryConfigPath))
      {
        try
        {
          var json = File.ReadAllText(primaryConfigPath);
          fullConfig = JsonSerializer.Deserialize<FullIconsConfig>(json) ?? new FullIconsConfig();
          MergeMissingDefaults(fullConfig, MakeDefaultConfig());
          Logger.Info($"Loaded icons.json");
        }
        catch (Exception ex)
        {
          Logger.Warn($"Failed to load icons.json: {ex.Message}. Using defaults.");
          fullConfig = MakeDefaultConfig();
        }
      }
      else if (File.Exists(legacyConfigPath))
      {
        try
        {
          var json = File.ReadAllText(legacyConfigPath);
          var legacyWorldConfig = JsonSerializer.Deserialize<WorldIconConfig>(json);
          fullConfig = MakeDefaultConfig();
          if (legacyWorldConfig != null)
          {
            fullConfig.WorldIcons = legacyWorldConfig;
          }
          SaveConfig();
          Logger.Info($"Migrated world_icons.json -> icons.json");
        }
        catch (Exception ex)
        {
          Logger.Warn($"Failed to migrate legacy world_icons.json: {ex.Message}");
          fullConfig = MakeDefaultConfig();
          SaveConfig();
        }
      }
      else
      {
        fullConfig = MakeDefaultConfig();
        SaveConfig();
        Logger.Info($"Created default icons.json");
      }
    }

    public void SaveConfig()
    {
      try
      {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(primaryConfigPath, JsonSerializer.Serialize(fullConfig, opts));
      }
      catch (Exception ex)
      {
        Logger.Warn($"Failed to save icons.json: {ex.Message}");
      }
    }

    private static void MergeMissingDefaults(FullIconsConfig current, FullIconsConfig defaults)
    {
      current.WorldIcons.SecretSeedIcon = string.IsNullOrEmpty(current.WorldIcons.SecretSeedIcon)
        ? defaults.WorldIcons.SecretSeedIcon
        : current.WorldIcons.SecretSeedIcon;
      current.WorldIcons.CycleIntervalSecs = current.WorldIcons.CycleIntervalSecs <= 0
        ? defaults.WorldIcons.CycleIntervalSecs
        : current.WorldIcons.CycleIntervalSecs;

      current.WorldIcons.DefaultIcons.Corrupt = string.IsNullOrEmpty(current.WorldIcons.DefaultIcons.Corrupt)
        ? defaults.WorldIcons.DefaultIcons.Corrupt
        : current.WorldIcons.DefaultIcons.Corrupt;
      current.WorldIcons.DefaultIcons.CorruptHardmode = string.IsNullOrEmpty(current.WorldIcons.DefaultIcons.CorruptHardmode)
        ? defaults.WorldIcons.DefaultIcons.CorruptHardmode
        : current.WorldIcons.DefaultIcons.CorruptHardmode;
      current.WorldIcons.DefaultIcons.Crimson = string.IsNullOrEmpty(current.WorldIcons.DefaultIcons.Crimson)
        ? defaults.WorldIcons.DefaultIcons.Crimson
        : current.WorldIcons.DefaultIcons.Crimson;
      current.WorldIcons.DefaultIcons.CrimsonHardmode = string.IsNullOrEmpty(current.WorldIcons.DefaultIcons.CrimsonHardmode)
        ? defaults.WorldIcons.DefaultIcons.CrimsonHardmode
        : current.WorldIcons.DefaultIcons.CrimsonHardmode;

      current.WorldIcons.SpecialSeedIcons.NotTheBees.Corrupt = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.NotTheBees.Corrupt)
        ? defaults.WorldIcons.SpecialSeedIcons.NotTheBees.Corrupt
        : current.WorldIcons.SpecialSeedIcons.NotTheBees.Corrupt;
      current.WorldIcons.SpecialSeedIcons.NotTheBees.CorruptHardmode = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.NotTheBees.CorruptHardmode)
        ? defaults.WorldIcons.SpecialSeedIcons.NotTheBees.CorruptHardmode
        : current.WorldIcons.SpecialSeedIcons.NotTheBees.CorruptHardmode;
      current.WorldIcons.SpecialSeedIcons.NotTheBees.Crimson = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.NotTheBees.Crimson)
        ? defaults.WorldIcons.SpecialSeedIcons.NotTheBees.Crimson
        : current.WorldIcons.SpecialSeedIcons.NotTheBees.Crimson;
      current.WorldIcons.SpecialSeedIcons.NotTheBees.CrimsonHardmode = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.NotTheBees.CrimsonHardmode)
        ? defaults.WorldIcons.SpecialSeedIcons.NotTheBees.CrimsonHardmode
        : current.WorldIcons.SpecialSeedIcons.NotTheBees.CrimsonHardmode;

      current.WorldIcons.SpecialSeedIcons.Drunk.Normal = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.Drunk.Normal)
        ? defaults.WorldIcons.SpecialSeedIcons.Drunk.Normal
        : current.WorldIcons.SpecialSeedIcons.Drunk.Normal;
      current.WorldIcons.SpecialSeedIcons.Drunk.Hardmode = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.Drunk.Hardmode)
        ? defaults.WorldIcons.SpecialSeedIcons.Drunk.Hardmode
        : current.WorldIcons.SpecialSeedIcons.Drunk.Hardmode;

      current.WorldIcons.SpecialSeedIcons.CelebrationMk10.Corrupt = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.CelebrationMk10.Corrupt)
        ? defaults.WorldIcons.SpecialSeedIcons.CelebrationMk10.Corrupt
        : current.WorldIcons.SpecialSeedIcons.CelebrationMk10.Corrupt;
      current.WorldIcons.SpecialSeedIcons.CelebrationMk10.CorruptHardmode = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.CelebrationMk10.CorruptHardmode)
        ? defaults.WorldIcons.SpecialSeedIcons.CelebrationMk10.CorruptHardmode
        : current.WorldIcons.SpecialSeedIcons.CelebrationMk10.CorruptHardmode;
      current.WorldIcons.SpecialSeedIcons.CelebrationMk10.Crimson = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.CelebrationMk10.Crimson)
        ? defaults.WorldIcons.SpecialSeedIcons.CelebrationMk10.Crimson
        : current.WorldIcons.SpecialSeedIcons.CelebrationMk10.Crimson;
      current.WorldIcons.SpecialSeedIcons.CelebrationMk10.CrimsonHardmode = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.CelebrationMk10.CrimsonHardmode)
        ? defaults.WorldIcons.SpecialSeedIcons.CelebrationMk10.CrimsonHardmode
        : current.WorldIcons.SpecialSeedIcons.CelebrationMk10.CrimsonHardmode;

      current.WorldIcons.SpecialSeedIcons.TheConstant.Corrupt = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.TheConstant.Corrupt)
        ? defaults.WorldIcons.SpecialSeedIcons.TheConstant.Corrupt
        : current.WorldIcons.SpecialSeedIcons.TheConstant.Corrupt;
      current.WorldIcons.SpecialSeedIcons.TheConstant.CorruptHardmode = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.TheConstant.CorruptHardmode)
        ? defaults.WorldIcons.SpecialSeedIcons.TheConstant.CorruptHardmode
        : current.WorldIcons.SpecialSeedIcons.TheConstant.CorruptHardmode;
      current.WorldIcons.SpecialSeedIcons.TheConstant.Crimson = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.TheConstant.Crimson)
        ? defaults.WorldIcons.SpecialSeedIcons.TheConstant.Crimson
        : current.WorldIcons.SpecialSeedIcons.TheConstant.Crimson;
      current.WorldIcons.SpecialSeedIcons.TheConstant.CrimsonHardmode = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.TheConstant.CrimsonHardmode)
        ? defaults.WorldIcons.SpecialSeedIcons.TheConstant.CrimsonHardmode
        : current.WorldIcons.SpecialSeedIcons.TheConstant.CrimsonHardmode;

      current.WorldIcons.SpecialSeedIcons.ForTheWorthy.Corrupt = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.ForTheWorthy.Corrupt)
        ? defaults.WorldIcons.SpecialSeedIcons.ForTheWorthy.Corrupt
        : current.WorldIcons.SpecialSeedIcons.ForTheWorthy.Corrupt;
      current.WorldIcons.SpecialSeedIcons.ForTheWorthy.CorruptHardmode = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.ForTheWorthy.CorruptHardmode)
        ? defaults.WorldIcons.SpecialSeedIcons.ForTheWorthy.CorruptHardmode
        : current.WorldIcons.SpecialSeedIcons.ForTheWorthy.CorruptHardmode;
      current.WorldIcons.SpecialSeedIcons.ForTheWorthy.Crimson = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.ForTheWorthy.Crimson)
        ? defaults.WorldIcons.SpecialSeedIcons.ForTheWorthy.Crimson
        : current.WorldIcons.SpecialSeedIcons.ForTheWorthy.Crimson;
      current.WorldIcons.SpecialSeedIcons.ForTheWorthy.CrimsonHardmode = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.ForTheWorthy.CrimsonHardmode)
        ? defaults.WorldIcons.SpecialSeedIcons.ForTheWorthy.CrimsonHardmode
        : current.WorldIcons.SpecialSeedIcons.ForTheWorthy.CrimsonHardmode;

      current.WorldIcons.SpecialSeedIcons.NoTraps.Corrupt = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.NoTraps.Corrupt)
        ? defaults.WorldIcons.SpecialSeedIcons.NoTraps.Corrupt
        : current.WorldIcons.SpecialSeedIcons.NoTraps.Corrupt;
      current.WorldIcons.SpecialSeedIcons.NoTraps.CorruptHardmode = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.NoTraps.CorruptHardmode)
        ? defaults.WorldIcons.SpecialSeedIcons.NoTraps.CorruptHardmode
        : current.WorldIcons.SpecialSeedIcons.NoTraps.CorruptHardmode;
      current.WorldIcons.SpecialSeedIcons.NoTraps.Crimson = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.NoTraps.Crimson)
        ? defaults.WorldIcons.SpecialSeedIcons.NoTraps.Crimson
        : current.WorldIcons.SpecialSeedIcons.NoTraps.Crimson;
      current.WorldIcons.SpecialSeedIcons.NoTraps.CrimsonHardmode = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.NoTraps.CrimsonHardmode)
        ? defaults.WorldIcons.SpecialSeedIcons.NoTraps.CrimsonHardmode
        : current.WorldIcons.SpecialSeedIcons.NoTraps.CrimsonHardmode;

      current.WorldIcons.SpecialSeedIcons.Remix.Corrupt = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.Remix.Corrupt)
        ? defaults.WorldIcons.SpecialSeedIcons.Remix.Corrupt
        : current.WorldIcons.SpecialSeedIcons.Remix.Corrupt;
      current.WorldIcons.SpecialSeedIcons.Remix.CorruptHardmode = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.Remix.CorruptHardmode)
        ? defaults.WorldIcons.SpecialSeedIcons.Remix.CorruptHardmode
        : current.WorldIcons.SpecialSeedIcons.Remix.CorruptHardmode;
      current.WorldIcons.SpecialSeedIcons.Remix.Crimson = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.Remix.Crimson)
        ? defaults.WorldIcons.SpecialSeedIcons.Remix.Crimson
        : current.WorldIcons.SpecialSeedIcons.Remix.Crimson;
      current.WorldIcons.SpecialSeedIcons.Remix.CrimsonHardmode = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.Remix.CrimsonHardmode)
        ? defaults.WorldIcons.SpecialSeedIcons.Remix.CrimsonHardmode
        : current.WorldIcons.SpecialSeedIcons.Remix.CrimsonHardmode;

      current.WorldIcons.SpecialSeedIcons.Skyblock.Default = string.IsNullOrEmpty(current.WorldIcons.SpecialSeedIcons.Skyblock.Default)
        ? defaults.WorldIcons.SpecialSeedIcons.Skyblock.Default
        : current.WorldIcons.SpecialSeedIcons.Skyblock.Default;

      foreach (var kvp in defaults.BossIcons)
      {
        if (!current.BossIcons.TryGetValue(kvp.Key, out var existing) || string.IsNullOrEmpty(existing))
          current.BossIcons[kvp.Key] = kvp.Value;
      }

      if (current.BossIcons.TryGetValue("Retinazer", out var retinazerIcon) &&
          retinazerIcon.EndsWith("/Retinazer.png", StringComparison.OrdinalIgnoreCase))
      {
        current.BossIcons["Retinazer"] = defaults.BossIcons.TryGetValue("Retinazer", out var defaultRetinazerIcon)
          ? defaultRetinazerIcon
          : "https://terraria.wiki.gg/images/Map_Icon_Retinazer_%28first_form%29.png";
      }

      foreach (var kvp in defaults.EventIcons)
      {
        if (!current.EventIcons.TryGetValue(kvp.Key, out var existing) || string.IsNullOrEmpty(existing))
          current.EventIcons[kvp.Key] = kvp.Value;
      }

      foreach (var kvp in defaults.PeacefulIcons)
      {
        if (!current.PeacefulIcons.TryGetValue(kvp.Key, out var existing) || string.IsNullOrEmpty(existing))
          current.PeacefulIcons[kvp.Key] = kvp.Value;
      }

      foreach (var kvp in defaults.WeatherIcons)
      {
        if (!current.WeatherIcons.TryGetValue(kvp.Key, out var existing) || string.IsNullOrEmpty(existing))
          current.WeatherIcons[kvp.Key] = kvp.Value;
      }
    }

    public static FullIconsConfig MakeDefaultConfig() => new FullIconsConfig
    {
      WorldIcons = new WorldIconConfig
      {
        CycleIntervalSecs = 3,
        SecretSeedIcon = "https://terraria.wiki.gg/images/Seed_Secret.png",
        DefaultIcons = new EvilHardmodeVariants
        {
          Corrupt = "https://terraria.wiki.gg/images/IconCorruption.png",
          CorruptHardmode = "https://terraria.wiki.gg/images/IconHallowCorruption.png",
          Crimson = "https://terraria.wiki.gg/images/IconCrimson.png",
          CrimsonHardmode = "https://terraria.wiki.gg/images/IconHallowCrimson.png"
        },
        SpecialSeedIcons = new SpecialSeedIcons
        {
          NotTheBees = new EvilHardmodeVariants
          {
            Corrupt = "https://terraria.wiki.gg/images/IconCorruptionNotTheBees.png",
            CorruptHardmode = "https://terraria.wiki.gg/images/IconHallowCorruptionNotTheBees.png",
            Crimson = "https://terraria.wiki.gg/images/IconCrimsonNotTheBees.png",
            CrimsonHardmode = "https://terraria.wiki.gg/images/IconHallowCrimsonNotTheBees.png"
          },
          Drunk = new DrunkVariants
          {
            Normal = "https://terraria.wiki.gg/images/IconCorruptionCrimson.png",
            Hardmode = "https://terraria.wiki.gg/images/IconHallowCorruptionCrimson.png"
          },
          CelebrationMk10 = new EvilHardmodeVariants
          {
            Corrupt = "https://terraria.wiki.gg/images/IconCorruptionAnniversary.png",
            CorruptHardmode = "https://terraria.wiki.gg/images/IconHallowCorruptionAnniversary.png",
            Crimson = "https://terraria.wiki.gg/images/IconCrimsonAnniversary.png",
            CrimsonHardmode = "https://terraria.wiki.gg/images/IconHallowCrimsonAnniversary.png"
          },
          TheConstant = new EvilHardmodeVariants
          {
            Corrupt = "https://terraria.wiki.gg/images/IconCorruptionDontStarve.png",
            CorruptHardmode = "https://terraria.wiki.gg/images/IconHallowCorruptionDontStarve.png",
            Crimson = "https://terraria.wiki.gg/images/IconCrimsonDontStarve.png",
            CrimsonHardmode = "https://terraria.wiki.gg/images/IconHallowCrimsonDontStarve.png"
          },
          ForTheWorthy = new EvilHardmodeVariants
          {
            Corrupt = "https://terraria.wiki.gg/images/IconCorruptionFTW.png",
            CorruptHardmode = "https://terraria.wiki.gg/images/IconHallowCorruptionFTW.png",
            Crimson = "https://terraria.wiki.gg/images/IconCrimsonFTW.png",
            CrimsonHardmode = "https://terraria.wiki.gg/images/IconHallowCrimsonFTW.png"
          },
          NoTraps = new EvilHardmodeVariants
          {
            Corrupt = "https://terraria.wiki.gg/images/IconCorruptionTraps.png",
            CorruptHardmode = "https://terraria.wiki.gg/images/IconHallowCorruptionTraps.png",
            Crimson = "https://terraria.wiki.gg/images/IconCrimsonTraps.png",
            CrimsonHardmode = "https://terraria.wiki.gg/images/IconHallowCrimsonTraps.png"
          },
          Remix = new EvilHardmodeVariants
          {
            Corrupt = "https://terraria.wiki.gg/images/IconCorruptionRemix.png",
            CorruptHardmode = "https://terraria.wiki.gg/images/IconHallowCorruptionRemix.png",
            Crimson = "https://terraria.wiki.gg/images/IconCrimsonRemix.png",
            CrimsonHardmode = "https://terraria.wiki.gg/images/IconHallowCrimsonRemix.png"
          },
          Skyblock = new SkyblockVariant
          {
            Default = "https://terraria.wiki.gg/images/IconSkyblock.png"
          }
        }
      },
      BossIcons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        { "King Slime", "https://terraria.wiki.gg/images/Map_Icon_King_Slime.png" },
        { "Eye of Cthulhu", "https://terraria.wiki.gg/images/Map_Icon_Eye_of_Cthulhu_%28first_form%29.png" },
        { "Eater of Worlds", "https://terraria.wiki.gg/images/Map_Icon_Eater_of_Worlds.png" },
        { "Brain of Cthulhu", "https://terraria.wiki.gg/images/Map_Icon_Brain_of_Cthulhu.png" },
        { "Queen Bee", "https://terraria.wiki.gg/images/Map_Icon_Queen_Bee.png" },
        { "Skeletron", "https://terraria.wiki.gg/images/Map_Icon_Skeletron.png" },
        { "Deerclops", "https://terraria.wiki.gg/images/Map_Icon_Deerclops.png" },
        { "Wall of Flesh", "https://terraria.wiki.gg/images/Map_Icon_Wall_of_Flesh.png" },
        { "Queen Slime", "https://terraria.wiki.gg/images/Map_Icon_Queen_Slime.png" },
        { "Retinazer", "https://terraria.wiki.gg/images/Map_Icon_Retinazer_%28first_form%29.png" },
        { "Spazmatism", "https://terraria.wiki.gg/images/Map_Icon_Spazmatism_%28first_form%29.png" },
        { "The Destroyer", "https://terraria.wiki.gg/images/Map_Icon_The_Destroyer.png" },
        { "Skeletron Prime", "https://terraria.wiki.gg/images/Map_Icon_Skeletron_Prime.png" },
        { "Plantera", "https://terraria.wiki.gg/images/Map_Icon_Plantera_%28first_form%29.png?82259e" },
        { "Golem", "https://terraria.wiki.gg/images/Map_Icon_Golem.png" },
        { "Empress of Light", "https://terraria.wiki.gg/images/Map_Icon_Empress_of_Light.png" },
        { "Duke Fishron", "https://terraria.wiki.gg/images/Map_Icon_Duke_Fishron.png" },
        { "Lunatic Cultist", "https://terraria.wiki.gg/images/Map_Icon_Lunatic_Cultist.png" },
        { "Moon Lord", "https://terraria.wiki.gg/images/Map_Icon_Moon_Lord.png" },
        { "Stardust Pillar", "https://terraria.wiki.gg/images/Map_Icon_Stardust_Pillar.png" },
        { "Solar Pillar", "https://terraria.wiki.gg/images/Map_Icon_Solar_Pillar.png" },
        { "Vortex Pillar", "https://terraria.wiki.gg/images/Map_Icon_Vortex_Pillar.png" },
        { "Nebula Pillar", "https://terraria.wiki.gg/images/Map_Icon_Nebula_Pillar.png" },
        { "Dreadnautilus", "https://terraria.wiki.gg/images/thumb/Dreadnautilus.png/73px-Dreadnautilus.png?" },
        { "Betsy", "https://terraria.wiki.gg/images/Map_Icon_Betsy.png" },
        { "Dark Mage", "https://terraria.wiki.gg/images/Map_Icon_Dark_Mage.png" },
        { "Ogre", "https://terraria.wiki.gg/images/Map_Icon_Ogre.png" },
        { "Flying Dutchman", "https://terraria.wiki.gg/images/Map_Icon_Flying_Dutchman.png" },
        { "Mourning Wood", "https://terraria.wiki.gg/images/Map_Icon_Mourning_Wood.png" },
        { "Pumpking", "https://terraria.wiki.gg/images/Map_Icon_Pumpking.png" },
        { "Everscream", "https://terraria.wiki.gg/images/Map_Icon_Everscream.png" },
        { "Santa-NK1", "https://terraria.wiki.gg/images/Map_Icon_Santa-NK1.png" },
        { "Ice Queen", "https://terraria.wiki.gg/images/Map_Icon_Ice_Queen.png" },
        { "Martian Saucer", "https://terraria.wiki.gg/images/Map_Icon_Martian_Saucer.png" }
      },
      EventIcons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        { "Goblin Invasion", "https://terraria.wiki.gg/images/Goblin_Army_Icon.png" },
        { "Frost Legion", "https://terraria.wiki.gg/images/Frost_Legion_Icon.png" },
        { "Pirate Invasion", "https://terraria.wiki.gg/images/Pirate_Invasion_Icon.png" },
        { "Martian Madness", "https://terraria.wiki.gg/images/Martian_Madness_Icon.png" },
        { "Solar Eclipse", "https://terraria.wiki.gg/images/Bestiary_Eclipse.png" },
        { "Blood Moon", "https://terraria.wiki.gg/images/Bestiary_Blood_Moon.png" },
        { "Pumpkin Moon", "https://terraria.wiki.gg/images/Pumpkin_Moon_Icon.png" },
        { "Frost Moon", "https://terraria.wiki.gg/images/Frost_Moon_Icon.png" },
        { "Slime Rain", "https://terraria.wiki.gg/images/Bestiary_Slime_Rain.png" },
        { "Old One's Army", "https://terraria.wiki.gg/images/Old_One%27s_Army_Icon.png" },
        { "The Torch God", "https://terraria.wiki.gg/images/Torch_God%27s_Favor.png" },
        { "Lunar Event", "https://files.catbox.moe/djvelz.png" },
        { "Celestial Pillars", "https://files.catbox.moe/djvelz.png" } // Custom Icon
      },
      PeacefulIcons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        { "Starfall", "https://terraria.wiki.gg/wiki/Special:FilePath/Fallen%20Star.png" },
        { "Party", "https://terraria.wiki.gg/images/Bestiary_Party.png" },
        { "Lantern Night", "https://terraria.wiki.gg/images/Release_Lantern.png" }
      },
      WeatherIcons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        { "Meteor Shower", "https://terraria.wiki.gg/images/Bestiary_Meteor.png" },
        { "Rain", "https://terraria.wiki.gg/images/Bestiary_Rain.png" },
        { "Thunderstorm", "https://files.catbox.moe/nuazde.png" },
        { "Sandstorm", "https://terraria.wiki.gg/images/Bestiary_Sandstorm.png" },
        { "Windy Day", "https://terraria.wiki.gg/images/Bestiary_Windy_Day.png" }
      }
    };

    // ── Public API ─────────────────────────────────────────────────────────

    public void UpdateWorldState(TerrariaGameState state)
    {
      string sig = $"{state.WorldEvil}|{state.WorldIsHardmode}|{string.Join(',', state.WorldSpecialSeeds)}|{state.WorldSecretSeedsAsNum}";

      if (sig == lastWorldSignature) return;

      lastWorldSignature = sig;
      worldRotationUrls = BuildRotation(state);
      worldCurrentIndex = 0;
      worldLastCycleTime = DateTime.Now;
    }

    public string GetCurrentWorldIconUrl()
    {
      if (worldRotationUrls.Count == 0) return "";

      if (worldRotationUrls.Count > 1 &&
          DateTime.Now - worldLastCycleTime >= TimeSpan.FromSeconds(fullConfig.WorldIcons.CycleIntervalSecs))
      {
        worldCurrentIndex = (worldCurrentIndex + 1) % worldRotationUrls.Count;
        worldLastCycleTime = DateTime.Now;
      }

      return worldRotationUrls[worldCurrentIndex];
    }

    public string GetBossIconUrl(string bossName)
    {
      if (string.IsNullOrEmpty(bossName)) return "";
      if (TryGetIconValue(fullConfig.BossIcons, bossName, out var url))
        return url;

      // Check if boss name contains pillar
      if (bossName.Contains("Pillar", StringComparison.OrdinalIgnoreCase))
      {
        foreach (var kvp in fullConfig.BossIcons)
        {
          if (bossName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase)) return kvp.Value;
        }
      }
      return "";
    }

    public string GetEventIconUrl(string eventName)
    {
      if (string.IsNullOrEmpty(eventName)) return "";
      if (TryGetIconValue(fullConfig.EventIcons, eventName, out var url))
        return url;
      return "";
    }

    public string GetPeacefulIconUrl(string eventName)
    {
      if (string.IsNullOrEmpty(eventName)) return "";
      if (TryGetIconValue(fullConfig.PeacefulIcons, eventName, out var url))
        return url;
      if (eventName.Contains("Starfall", StringComparison.OrdinalIgnoreCase) &&
          TryGetIconValue(fullConfig.PeacefulIcons, "Starfall", out url))
        return url;
      if (eventName.Contains("Party", StringComparison.OrdinalIgnoreCase) &&
          TryGetIconValue(fullConfig.PeacefulIcons, "Party", out url))
        return url;
      if (eventName.Contains("Lantern Night", StringComparison.OrdinalIgnoreCase) &&
          TryGetIconValue(fullConfig.PeacefulIcons, "Lantern Night", out url))
        return url;
      return "";
    }

    public string GetWeatherIconUrl(string weatherName)
    {
      if (string.IsNullOrEmpty(weatherName)) return "";
      if (TryGetIconValue(fullConfig.WeatherIcons, weatherName, out var url))
        return url;
      if (weatherName.Contains("Meteor Shower", StringComparison.OrdinalIgnoreCase) &&
          TryGetIconValue(fullConfig.WeatherIcons, "Meteor Shower", out url))
        return url;
      return "";
    }

    // ── World Rotation builder ──────────────────────────────────────────────

    public string FetchConfigValue(string source, string path)
    {
      if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(path))
      {
        Logger.Warn($"FetchConfigValue miss: source='{source ?? ""}' path='{path ?? ""}' (blank input)");
        return "";
      }

      if (string.Equals(source, "icons.json", StringComparison.OrdinalIgnoreCase))
      {
        string resolved = ResolveIconPath(path);
        Logger.Debug($"FetchConfigValue: source='{source}' path='{path}' => '{resolved}'");
        return resolved;
      }

      Logger.Warn($"FetchConfigValue miss: unsupported source='{source}' path='{path}'");
      return "";
    }

    private string ResolveIconPath(string path)
    {
      string[] parts = path.Split('.', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      if (parts.Length != 2)
      {
        Logger.Warn($"ResolveIconPath miss: invalid path='{path}'");
        return "";
      }

      Dictionary<string, string>? dictionary = parts[0].ToLowerInvariant() switch
      {
        "bossicons" => fullConfig.BossIcons,
        "eventicons" => fullConfig.EventIcons,
        "peacefulicons" => fullConfig.PeacefulIcons,
        "weathericons" => fullConfig.WeatherIcons,
        _ => null
      };

      if (dictionary == null)
      {
        Logger.Warn($"ResolveIconPath miss: unknown icon group='{parts[0]}' for path='{path}'");
        return "";
      }

      if (TryGetIconValue(dictionary, parts[1], out var value))
      {
        return value;
      }

      Logger.Warn($"ResolveIconPath miss: group='{parts[0]}' key='{parts[1]}' path='{path}'");
      return "";
    }

    private static bool TryGetIconValue(Dictionary<string, string> dictionary, string lookupKey, out string value)
    {
      if (dictionary.TryGetValue(lookupKey, out value) && !string.IsNullOrEmpty(value))
      {
        Logger.Debug($"TryGetIconValue hit: key='{lookupKey}'");
        return true;
      }

      string normalizedLookupKey = NormalizeIconKey(lookupKey);
      if (string.IsNullOrEmpty(normalizedLookupKey))
      {
        value = "";
        return false;
      }

      foreach (var kvp in dictionary)
      {
        if (NormalizeIconKey(kvp.Key) == normalizedLookupKey && !string.IsNullOrEmpty(kvp.Value))
        {
          value = kvp.Value;
          Logger.Debug($"TryGetIconValue normalized hit: lookupKey='{lookupKey}' matchedKey='{kvp.Key}'");
          return true;
        }
      }

      value = "";
      Logger.Debug($"TryGetIconValue miss: key='{lookupKey}' normalized='{normalizedLookupKey}'");
      return false;
    }

    private static string NormalizeIconKey(string key)
    {
      if (string.IsNullOrWhiteSpace(key))
      {
        return "";
      }

      return new string(key
        .Where(char.IsLetterOrDigit)
        .ToArray())
        .ToLowerInvariant();
    }

    private List<string> BuildRotation(TerrariaGameState state)
    {
      bool isCrimson = state.WorldEvil == "Crimson";
      bool isHardmode = state.WorldIsHardmode;

      var specialSeeds = state.WorldSpecialSeeds;
      bool hasSpecial = specialSeeds.Length > 0;
      bool hasSecret = state.WorldSecretSeedsAsNum > 0;
      bool isZenith = specialSeeds.Contains("Zenith");

      var urls = new List<string>();

      if (hasSpecial)
      {
        IEnumerable<string> seedsToShow = isZenith
          ? SpecialSeedOrder
          : SpecialSeedOrder.Where(s => specialSeeds.Contains(s, StringComparer.OrdinalIgnoreCase));

        foreach (string seed in seedsToShow)
        {
          string url = ResolveSpecialSeedUrl(seed, isCrimson, isHardmode);
          if (!string.IsNullOrEmpty(url)) urls.Add(url);
        }

        if (specialSeeds.Contains("Skyblock"))
        {
          string skUrl = fullConfig.WorldIcons.SpecialSeedIcons.Skyblock.Default;
          if (!string.IsNullOrEmpty(skUrl)) urls.Add(skUrl);
        }
      }
      else
      {
        string defUrl = ResolveDefaultIcon(isCrimson, isHardmode);
        if (!string.IsNullOrEmpty(defUrl)) urls.Add(defUrl);
      }

      if (hasSecret && !string.IsNullOrEmpty(fullConfig.WorldIcons.SecretSeedIcon))
        urls.Add(fullConfig.WorldIcons.SecretSeedIcon);

      return urls;
    }

    private string ResolveDefaultIcon(bool isCrimson, bool isHardmode)
    {
      var d = fullConfig.WorldIcons.DefaultIcons;
      return (isCrimson, isHardmode) switch
      {
        (true, true) => d.CrimsonHardmode,
        (true, false) => d.Crimson,
        (false, true) => d.CorruptHardmode,
        (false, false) => d.Corrupt
      };
    }

    private string ResolveSpecialSeedUrl(string seedName, bool isCrimson, bool isHardmode)
    {
      var sp = fullConfig.WorldIcons.SpecialSeedIcons;

      if (seedName.Equals("Drunk", StringComparison.OrdinalIgnoreCase))
        return isHardmode ? sp.Drunk.Hardmode : sp.Drunk.Normal;

      if (seedName.Equals("Skyblock", StringComparison.OrdinalIgnoreCase))
        return sp.Skyblock.Default;

      EvilHardmodeVariants? variants = seedName.ToLower() switch
      {
        "not the bees" => sp.NotTheBees,
        "celebrationmk10" => sp.CelebrationMk10,
        "the constant" => sp.TheConstant,
        "for the worthy" => sp.ForTheWorthy,
        "no traps" => sp.NoTraps,
        "remix" => sp.Remix,
        _ => null
      };

      if (variants == null) return "";

      return (isCrimson, isHardmode) switch
      {
        (true, true) => variants.CrimsonHardmode,
        (true, false) => variants.Crimson,
        (false, true) => variants.CorruptHardmode,
        (false, false) => variants.Corrupt
      };
    }
  }
}

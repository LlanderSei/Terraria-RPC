using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace TerrariaRPC.Core
{
  public class StatusTemplateEntry
  {
    public string StatusName { get; set; } = "";
    public string Image { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Enabled { get; set; } = true;
  }

  public class StatusDetailConfig
  {
    public string Image { get; set; } = "";
    public string Text { get; set; } = "";
  }

  public class StatusListConfig
  {
    public int SelectedIndex { get; set; } = 0;
    public List<StatusTemplateEntry> Templates { get; set; } = new();
  }

  public class SmallStatusListConfig : StatusListConfig
  {
    public StatusDetailConfig Static { get; set; } = new();
  }

  public class LargeStatusListConfig : StatusListConfig
  {
  }

  public class GeneralConfig
  {
    public string ClientId { get; set; } = "1537768004119691335";
    public int UpdateInterval { get; set; } = 2;
  }

  public class MainMenuConfig
  {
    public string Line1 { get; set; } = "{{!IsAttached ? \"Waiting for Terraria...\" :  Screen in \"MainMenu\" ? \"On Main Menu\" : Screen in [\"PlayerSelection\", \"WorldSelection\", \"EnteringWorld\"] ? \"Single Player\" : (Screen in [\"MultiplayerBrowser\", \"MultiplayerPlayerSelection\", \"MultiplayerIpSelection\", \"MultiplayerJoining\"] || RawMenuMode in [14] ? \"Multiplayer\" : \"In Menus\"}}";
    public string Line2 { get; set; } = "{{!IsAttached ? \"\" : Screen == \"PlayerSelection\" ? \"Choosing a player...\" : Screen == \"WorldSelection\" ? \"Selecting a world...\" : Screen == \"EnteringWorld\" ? \"Entering {{WorldName}}...\" : Screen == \"MultiplayerBrowser\" ? \"Selecting connection type...\" : Screen == \"MultiplayerPlayerSelection\" ? \"Choosing a player...\" : Screen == \"MultiplayerIpSelection\" ? \"Selecting an address to join...\" : Screen == \"MultiplayerJoining\" ? \"Joining world...\" : \"\"}}";
    public StatusDetailConfig SmallDetails { get; set; } = new();
    public StatusDetailConfig LargeDetails { get; set; } = new()
    {
      Image = "https://terraria.wiki.gg/images/Treetop_Forest_1.png"
    };
  }

  public class InGameConfig
  {
    public string Line1 { get; set; } = "{{WorldName}} - In {{Biome}}";
    public string Line2 { get; set; } = "ATK: {{PlayerDynamiWeaponDmg}} | DEF: {{PlayerDef}} | HP: {{PlayerHp}}/{{PlayerMaxHp}} | MP: {{PlayerMp}}/{{PlayerMaxMp}}";
    public BossAndEventPriorityConfig BossAndEventPriority { get; set; } = new();
    public SmallStatusListConfig SmallDetails { get; set; } = new();
    public LargeStatusListConfig LargeDetails { get; set; } = new();
  }

  public class BossAndEventPriorityConfig
  {
    public BossPriorityConfig BossPriority { get; set; } = new();
  }

  public class BossPriorityConfig
  {
    public bool PrioritizeLunarPillarsNearby { get; set; } = true;
    public bool PrioritizeTargetHitBoss { get; set; } = true;
    public bool PrioritizeNearestBoss { get; set; } = false;
    public bool PrioritizeHighestHealthBoss { get; set; } = false;
  }

  public class RpcConfig
  {
    public const string LegacyProgressiveEventSmallTextTemplate = "Clearing: {{ActiveProgressiveEvent}} ({{ActiveEventHasWaves ? \"Wave {{ActiveEventWaveNum}}: \" : \"\"}}{{ActiveEventHasProgress && !ActiveEventIsAtMaxWave ? \"{{ActiveEventProgression}}%\" : \"\"}}{{ActiveEventIsAtMaxWave && ActiveEventIsAtMaxProgression ? \"{{ActiveEventPoints}} pts\" : \"\"}})";
    public const string LegacyNonProgressiveEventSmallTextTemplate = "{{ActiveNonProgressiveEvent == \"Blood Moon\" ? \"The Blood Moon is rising...\" : ActiveNonProgressiveEvent == \"Solar Eclipse\" ? \"A Solar Eclipse is happening!\" : ActiveNonProgressiveEvent}}";
    public const string LegacyPeacefulEventSmallTextTemplate = "{{ActivePeacefulEvent}} is occuring.";
    public const string DefaultHeldItemSmallImageUrlTemplate = "{{PlayerHeldItem != \"\" ? \"https://terraria.wiki.gg/images/{{PlayerHeldItemWikiName}}.png\" : \"\"}}";
    public const string DefaultHeldItemSmallTextTemplate = "{{PlayerHeldItemPrefix != \"\" ? PlayerHeldItemPrefix + \" \" : \"\"}}{{PlayerHeldItem != \"\" ? PlayerHeldItem : \"\"}}";
    public GeneralConfig General { get; set; } = new();
    public MainMenuConfig MainMenu { get; set; } = new();
    public InGameConfig InGame { get; set; } = new();

    // Large Image settings
    [JsonIgnore]
    public int LargeImageStyleIndex
    {
      get => InGame.LargeDetails.SelectedIndex;
      set => InGame.LargeDetails.SelectedIndex = value;
    }

    [JsonIgnore]
    public string LargeImageCustomUrl
    {
      get => GetLargeStatus("Static Status").Image;
      set => GetLargeStatus("Static Status").Image = value;
    }

    [JsonIgnore]
    public string LargeImageCustomText
    {
      get => GetLargeStatus("Static Status").Text;
      set => GetLargeStatus("Static Status").Text = value;
    }

    [JsonIgnore]
    public string MainMenuLine1
    {
      get => MainMenu.Line1;
      set => MainMenu.Line1 = value;
    }

    [JsonIgnore]
    public string MainMenuLine2
    {
      get => MainMenu.Line2;
      set => MainMenu.Line2 = value;
    }

    [JsonIgnore]
    public string MainMenuSmallImageUrl
    {
      get => MainMenu.SmallDetails.Image;
      set => MainMenu.SmallDetails.Image = value;
    }

    [JsonIgnore]
    public string MainMenuSmallImageText
    {
      get => MainMenu.SmallDetails.Text;
      set => MainMenu.SmallDetails.Text = value;
    }

    [JsonIgnore]
    public string MainMenuLargeImageUrl
    {
      get => MainMenu.LargeDetails.Image;
      set => MainMenu.LargeDetails.Image = value;
    }

    [JsonIgnore]
    public string MainMenuLargeImageText
    {
      get => MainMenu.LargeDetails.Text;
      set => MainMenu.LargeDetails.Text = value;
    }

    [JsonIgnore]
    public string InGameLine1
    {
      get => InGame.Line1;
      set => InGame.Line1 = value;
    }

    [JsonIgnore]
    public string InGameLine2
    {
      get => InGame.Line2;
      set => InGame.Line2 = value;
    }

    // Small Image settings
    [JsonIgnore]
    public int SmallImageStyleIndex
    {
      get => InGame.SmallDetails.SelectedIndex;
      set => InGame.SmallDetails.SelectedIndex = value;
    }

    [JsonIgnore]
    public string SmallImageCustomUrl
    {
      get => InGame.SmallDetails.Static.Image;
      set => InGame.SmallDetails.Static.Image = value;
    }

    [JsonIgnore]
    public string SmallImageCustomText
    {
      get => InGame.SmallDetails.Static.Text;
      set => InGame.SmallDetails.Static.Text = value;
    }

    [JsonIgnore]
    public string HeldItemSmallImageUrlTemplate
    {
      get => GetSmallStatus("Held Item").Image;
      set => GetSmallStatus("Held Item").Image = value;
    }

    [JsonIgnore]
    public string HeldItemSmallTextTemplate
    {
      get => GetSmallStatus("Held Item").Text;
      set => GetSmallStatus("Held Item").Text = value;
    }

    [JsonIgnore]
    public bool SmallItemEnabled
    {
      get => GetSmallStatus("Held Item").Enabled;
      set => GetSmallStatus("Held Item").Enabled = value;
    }

    [JsonIgnore]
    public bool SmallBossEventEnabled
    {
      get => GetSmallStatus("Boss").Enabled || GetSmallStatus("Progressive Events").Enabled || GetSmallStatus("Non-Progressive Events").Enabled || GetSmallStatus("Peaceful Events").Enabled || GetSmallStatus("Weather").Enabled;
      set
      {
        if (value && InGame.SmallDetails.Templates.Count == 0)
        {
          InGame.SmallDetails.Templates = BuildDefaultSmallStatusTemplates();
        }
      }
    }

    [JsonIgnore]
    public bool PrioritizeLunarPillarsNearby
    {
      get => InGame.BossAndEventPriority.BossPriority.PrioritizeLunarPillarsNearby;
      set => InGame.BossAndEventPriority.BossPriority.PrioritizeLunarPillarsNearby = value;
    }

    [JsonIgnore]
    public bool PrioritizeTargetHitBoss
    {
      get => InGame.BossAndEventPriority.BossPriority.PrioritizeTargetHitBoss;
      set => InGame.BossAndEventPriority.BossPriority.PrioritizeTargetHitBoss = value;
    }

    [JsonIgnore]
    public bool PrioritizeNearestBoss
    {
      get => InGame.BossAndEventPriority.BossPriority.PrioritizeNearestBoss;
      set => InGame.BossAndEventPriority.BossPriority.PrioritizeNearestBoss = value;
    }

    [JsonIgnore]
    public bool PrioritizeHighestHealthBoss
    {
      get => InGame.BossAndEventPriority.BossPriority.PrioritizeHighestHealthBoss;
      set => InGame.BossAndEventPriority.BossPriority.PrioritizeHighestHealthBoss = value;
    }

    // Small Image Excludes
    [JsonIgnore]
    public bool ExcludeBoss { get; set; } = false;
    [JsonIgnore]
    public bool ExcludeEvents { get; set; } = false;
    [JsonIgnore]
    public bool ExcludeNonProgressiveEvents { get; set; } = false;
    [JsonIgnore]
    public bool ExcludePeacefulEvents { get; set; } = false;
    [JsonIgnore]
    public bool ExcludeWeather { get; set; } = false;

    // Small Image Includes to Rotation
    [JsonIgnore]
    public bool IncludeEvents { get; set; } = false;
    [JsonIgnore]
    public bool IncludeNonProgressiveEvents { get; set; } = false;
    [JsonIgnore]
    public bool IncludePeacefulEvents { get; set; } = false;
    [JsonIgnore]
    public bool IncludeWeather { get; set; } = false;

    // Small details templates
    [JsonIgnore]
    public string BossSmallTextTemplate { get => GetSmallStatus("Boss").Text; set => GetSmallStatus("Boss").Text = value; }
    [JsonIgnore]
    public string ProgressiveEventSmallTextTemplate { get => GetSmallStatus("Progressive Events").Text; set => GetSmallStatus("Progressive Events").Text = value; }
    [JsonIgnore]
    public string NonProgressiveEventSmallTextTemplate { get => GetSmallStatus("Non-Progressive Events").Text; set => GetSmallStatus("Non-Progressive Events").Text = value; }
    [JsonIgnore]
    public string PeacefulEventSmallTextTemplate { get => GetSmallStatus("Peaceful Events").Text; set => GetSmallStatus("Peaceful Events").Text = value; }
    [JsonIgnore]
    public string WeatherSmallTextTemplate { get => GetSmallStatus("Weather").Text; set => GetSmallStatus("Weather").Text = value; }

    // Discord settings
    [JsonIgnore]
    public string ClientId
    {
      get => General.ClientId;
      set => General.ClientId = value;
    }

    [JsonIgnore]
    public int UpdateInterval
    {
      get => General.UpdateInterval;
      set => General.UpdateInterval = value;
    }

    // Legacy aliases for migration/backward compatibility.
    [JsonIgnore]
    public string Line1
    {
      get => InGame.Line1;
      set => InGame.Line1 = value;
    }

    [JsonIgnore]
    public string Line2
    {
      get => InGame.Line2;
      set => InGame.Line2 = value;
    }

    public static List<StatusTemplateEntry> BuildDefaultSmallStatusTemplates()
    {
      return
      [
        new StatusTemplateEntry
        {
          StatusName = "Held Item",
          Image = DefaultHeldItemSmallImageUrlTemplate,
          Text = DefaultHeldItemSmallTextTemplate,
          Enabled = true
        },
        new StatusTemplateEntry
        {
          StatusName = "Boss",
          Image = "{{ActiveBoss != \"\" ? FetchConfigValue(\"icons.json\", \"bossIcons.\" + ActiveBoss) : \"\"}}",
          Text = "{{ActiveBoss != \"\" ? \"Fighting {{ActiveBoss}} ({{ActiveBossHasShield ? \"SP {{ActiveBossSp}}/{{ActiveBossMaxSp}}\" : \"{{ActiveBossHp}}/{{ActiveBossMaxHp}}\"}})\" : \"\"}}",
          Enabled = true
        },
        new StatusTemplateEntry
        {
          StatusName = "Progressive Events",
          Image = "{{ActiveProgressiveEvent != \"\" ? FetchConfigValue(\"icons.json\", \"eventIcons.\" + ActiveProgressiveEvent) : \"\"}}",
          Text = "{{ActiveProgressiveEvent != \"\" ? \"Clearing: {{ActiveProgressiveEvent}} ({{ActiveEventUsesPoints ? \"{{ActiveEventPoints}} pts.\" : \"{{ActiveEventHasWaves ? \"Wave {{ActiveEventWaveNum}}: \" : \"\"}}{{ActiveEventProgress}}%\"}})\" : \"\"}}",
          Enabled = true
        },
        new StatusTemplateEntry
        {
          StatusName = "Non-Progressive Events",
          Image = "{{ActiveNonProgressiveEvent != \"\" ? FetchConfigValue(\"icons.json\", \"eventIcons.\" + ActiveNonProgressiveEvent) : \"\"}}",
          Text = "{{ActiveNonProgressiveEvent != \"\" ? (ActiveNonProgressiveEvent == \"Blood Moon\" ? \"The Blood Moon is rising...\" : ActiveNonProgressiveEvent == \"Solar Eclipse\" ? \"A Solar Eclipse is happening!\" : ActiveNonProgressiveEvent == \"Slime Rain\" ? \"Slimes are falling from the sky!\" : ActiveNonProgressiveEvent) : \"\"}}",
          Enabled = true
        },
        new StatusTemplateEntry
        {
          StatusName = "Peaceful Events",
          Image = "{{ActivePeacefulEventValue != \"\" ? FetchConfigValue(\"icons.json\", \"peacefulIcons.\" + ActivePeacefulEventValue) : \"\"}}",
          Text = "{{ActivePeacefulEvent != \"\" ? ActivePeacefulEvent + \" is happening!\" : \"\"}}",
          Enabled = true
        },
        new StatusTemplateEntry
        {
          StatusName = "Weather",
          Image = "{{ActiveWeather != \"\" ? FetchConfigValue(\"icons.json\", \"weatherIcons.\" + ActiveWeather) : \"\"}}",
          Text = "{{ActiveWeather}}",
          Enabled = true
        },
        new StatusTemplateEntry
        {
          StatusName = "Spectating",
          Image = "{{SpectatedName != \"\" ? \"https://terraria.wiki.gg/images/Scrying_Orb.png\" : \"\"}}",
          Text = "{{SpectatedName != \"\" ? \"Spectating {{SpectatedName}}...\" : \"\"}}",
          Enabled = true
        }
      ];
    }

    private StatusTemplateEntry GetSmallStatus(string statusName)
    {
      InGame.SmallDetails.Templates ??= [];

      var existing = InGame.SmallDetails.Templates.Find(item => string.Equals(item.StatusName, statusName, StringComparison.OrdinalIgnoreCase));
      if (existing != null)
      {
        return existing;
      }

      var defaults = BuildDefaultSmallStatusTemplates();
      var defaultItem = defaults.Find(item => string.Equals(item.StatusName, statusName, StringComparison.OrdinalIgnoreCase));
      if (defaultItem != null)
      {
        InGame.SmallDetails.Templates.Add(defaultItem);
        return defaultItem;
      }

      var created = new StatusTemplateEntry { StatusName = statusName, Enabled = false };
      InGame.SmallDetails.Templates.Add(created);
      return created;
    }

    private StatusTemplateEntry GetLargeStatus(string statusName)
    {
      InGame.LargeDetails.Templates ??= [];

      var existing = InGame.LargeDetails.Templates.Find(item => string.Equals(item.StatusName, statusName, StringComparison.OrdinalIgnoreCase));
      if (existing != null)
      {
        return existing;
      }

      var defaults = BuildDefaultSmallStatusTemplates();
      var defaultItem = defaults.Find(item => string.Equals(item.StatusName, statusName, StringComparison.OrdinalIgnoreCase));
      if (defaultItem != null)
      {
        InGame.LargeDetails.Templates.Add(defaultItem);
        return defaultItem;
      }

      var created = new StatusTemplateEntry { StatusName = statusName, Enabled = false };
      InGame.LargeDetails.Templates.Add(created);
      return created;
    }
  }

  public static class ConfigManager
  {
    private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "config.json");
    public static RpcConfig CurrentConfig { get; private set; } = new RpcConfig();

    public static void LoadConfig()
    {
      if (File.Exists(ConfigPath))
      {
        try
        {
          var json = File.ReadAllText(ConfigPath);
          CurrentConfig = JsonSerializer.Deserialize<RpcConfig>(json) ?? new RpcConfig();
          EnsureDefaults(CurrentConfig);
          Console.WriteLine("Config loaded successfully.");
        }
        catch (Exception ex)
        {
          Console.WriteLine($"[WARNING] Failed to load config.json, using defaults. Error: {ex.Message}");
          CurrentConfig = new RpcConfig();
          EnsureDefaults(CurrentConfig);
        }
      }
      else
      {
        Console.WriteLine("Config not found. Creating default config.json.");
        CurrentConfig = new RpcConfig();
        EnsureDefaults(CurrentConfig);
        SaveConfig();
      }
    }

    private static void EnsureDefaults(RpcConfig config)
    {
      bool changed = false;

      config.MainMenu ??= new MainMenuConfig();
      config.InGame ??= new InGameConfig();
      config.General ??= new GeneralConfig();
      config.InGame.BossAndEventPriority ??= new BossAndEventPriorityConfig();
      config.InGame.BossAndEventPriority.BossPriority ??= new BossPriorityConfig();
      if (config.General.UpdateInterval <= 0)
      {
        config.General.UpdateInterval = 2;
        changed = true;
      }

      config.MainMenu.SmallDetails ??= new StatusDetailConfig();
      config.MainMenu.LargeDetails ??= new StatusDetailConfig();
      if (config.InGame.SmallDetails == null)
      {
        config.InGame.SmallDetails = new SmallStatusListConfig();
        changed = true;
      }

      if (config.InGame.LargeDetails == null)
      {
        config.InGame.LargeDetails = new LargeStatusListConfig();
        changed = true;
      }
      config.InGame.SmallDetails.Static ??= new StatusDetailConfig();
      if (config.InGame.SmallDetails.Templates.Count == 0)
      {
        config.InGame.SmallDetails.Templates = RpcConfig.BuildDefaultSmallStatusTemplates();
        changed = true;
      }
      else if (!config.InGame.SmallDetails.Templates.Any(item => string.Equals(item.StatusName, "Spectating", StringComparison.OrdinalIgnoreCase)))
      {
        config.InGame.SmallDetails.Templates.Add(new StatusTemplateEntry
        {
          StatusName = "Spectating",
          Image = "{{IsSpectating ? \"https://terraria.wiki.gg/images/Scrying_Orb.png\" : \"\"}}",
          Text = "{{SpectatedName != \"\" ? \"Spectating {{SpectatedName}}...\" : \"\"}}",
          Enabled = true
        });
        changed = true;
      }

      if (config.InGame.LargeDetails.Templates.Count == 0)
      {
        config.InGame.LargeDetails.Templates =
        [
          new StatusTemplateEntry
          {
            StatusName = "Default",
            Image = "",
            Text = "",
            Enabled = true
          },
          new StatusTemplateEntry
          {
            StatusName = "Static Status",
            Image = "",
            Text = "",
            Enabled = true
          }
        ];
        changed = true;
      }

      if (string.IsNullOrWhiteSpace(config.MainMenu.SmallDetails.Image) && string.IsNullOrWhiteSpace(config.MainMenu.SmallDetails.Text))
      {
        config.MainMenu.SmallDetails.Image = "{{Screen in [\"PlayerSelection\", \"MultiplayerPlayerSelection\"] ? \"https://terraria.wiki.gg/images/ColorCharacter.png?\" : Screen in [\"WorldSelection\", \"MultiplayerIpSelection\", \"EnteringWorld\", \"MultiplayerJoining\"] ? \"https://terraria.wiki.gg/images/World_Globe_%28old%29.png\" : \"\"}}";
        config.MainMenu.SmallDetails.Text = "";
        changed = true;
      }

      if (string.IsNullOrWhiteSpace(config.MainMenu.LargeDetails.Image))
      {
        config.MainMenu.LargeDetails.Image = "https://terraria.wiki.gg/images/Treetop_Forest_1.png";
        changed = true;
      }

      if (string.IsNullOrWhiteSpace(config.InGame.SmallDetails.Static.Image) && string.IsNullOrWhiteSpace(config.InGame.SmallDetails.Static.Text))
      {
        config.InGame.SmallDetails.Static.Image = "";
        config.InGame.SmallDetails.Static.Text = "";
      }

      var peacefulEvent = config.InGame.SmallDetails.Templates.Find(item => string.Equals(item.StatusName, "Peaceful Events", StringComparison.OrdinalIgnoreCase));
      if (peacefulEvent != null &&
          (string.IsNullOrWhiteSpace(peacefulEvent.Text) ||
           string.Equals(peacefulEvent.Text, RpcConfig.LegacyPeacefulEventSmallTextTemplate, StringComparison.Ordinal)))
      {
        peacefulEvent.Text = "{{ActivePeacefulEvent != \"\" ? ActivePeacefulEvent + \" is occuring.\" : \"\"}}";
        changed = true;
      }

      if (changed)
      {
        SaveConfig();
      }
    }

    public static void SaveConfig()
    {
      try
      {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(CurrentConfig, options);
        File.WriteAllText(ConfigPath, json);
        ActiveBossEventManager.ResetRotation();
        Console.WriteLine("Config saved.");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[ERROR] Failed to save config: {ex.Message}");
      }
    }
  }
}

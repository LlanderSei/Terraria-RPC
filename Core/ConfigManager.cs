using System;
using System.IO;
using System.Text.Json;

namespace TerrariaRPC.Core
{
    public class RpcConfig
    {
        public const string LegacyProgressiveEventSmallTextTemplate = "Clearing: {{ActiveProgressiveEvent}} ({{ActiveEventHasWaves ? \"Wave {{ActiveEventWaveNum}}: \" : \"\"}}{{ActiveEventHasProgress && !ActiveEventIsAtMaxWave ? \"{{ActiveEventProgression}}%\" : \"\"}}{{ActiveEventIsAtMaxWave && ActiveEventIsAtMaxProgression ? \"{{ActiveEventPoints}} pts\" : \"\"}})";
        public const string LegacyNonProgressiveEventSmallTextTemplate = "{{ActiveNonProgressiveEvent == \"Blood Moon\" ? \"The Blood Moon is rising...\" : ActiveNonProgressiveEvent == \"Solar Eclipse\" ? \"A Solar Eclipse is happening!\" : ActiveNonProgressiveEvent}}";

        public string Line1 { get; set; } = "{{WorldName}} - In {{Biome}}";
        public string Line2 { get; set; } = "ATK: {{PlayerAtk}} | DEF: {{PlayerDef}} | HP: {{PlayerHp}}/{{PlayerMaxHp}} | MP: {{PlayerMp}}/{{PlayerMaxMp}}";

        // Large Image settings
        public int LargeImageStyleIndex { get; set; } = 0; // 0 = Special Seed Icon, 1 = Custom
        public string LargeImageCustomUrl { get; set; } = "";
        public string LargeImageCustomText { get; set; } = "";

        // Small Image settings
        public int SmallImageStyleIndex { get; set; } = 0; // 0 = Rotation/Checkboxes, 1 = Custom
        public string SmallImageCustomUrl { get; set; } = "";
        public string SmallImageCustomText { get; set; } = "";

        // Small Image Rotation Checkbox options
        public bool SmallItemEnabled { get; set; } = true;
        public bool SmallBossEventEnabled { get; set; } = false;

        // Small Image Excludes
        public bool ExcludeBoss { get; set; } = false;
        public bool ExcludeEvents { get; set; } = false;
        public bool ExcludeNonProgressiveEvents { get; set; } = false;
        public bool ExcludePeacefulEvents { get; set; } = false;
        public bool ExcludeWeather { get; set; } = false;

        // Small Image Includes to Rotation
        public bool IncludeEvents { get; set; } = false;
        public bool IncludeNonProgressiveEvents { get; set; } = false;
        public bool IncludePeacefulEvents { get; set; } = false;
        public bool IncludeWeather { get; set; } = false;

        // Small details templates
        public string BossSmallTextTemplate { get; set; } = "Fighting: {{ActiveBoss}} ({{ActiveBossHasShield ? \"{{ActiveBossSp}}/{{ActiveBossMaxSp}}\" : \"{{ActiveBossHp}}/{{ActiveBossMaxHp}}\"}})";
        public string ProgressiveEventSmallTextTemplate { get; set; } = "Clearing: {{ActiveProgressiveEvent}}{{ActiveEventDetailText != \"\" ? \" (\" + ActiveEventDetailText + \")\" : \"\"}}";
        public string NonProgressiveEventSmallTextTemplate { get; set; } = "{{ActiveNonProgressiveEvent == \"Blood Moon\" ? \"The Blood Moon is rising...\" : ActiveNonProgressiveEvent == \"Solar Eclipse\" ? \"A Solar Eclipse is happening!\" : \"Clearing: {{ActiveNonProgressiveEvent}}\"}}";
        public string PeacefulEventSmallTextTemplate { get; set; } = "{{ActivePeacefulEvent}} is occuring.";
        public string WeatherSmallTextTemplate { get; set; } = "{{ActiveWeather}}";

        // Discord settings
        public string ClientId { get; set; } = "1537768004119691335";
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
                    MigrateConfig(CurrentConfig);
                    Console.WriteLine("Config loaded successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING] Failed to load config.json, using defaults. Error: {ex.Message}");
                    CurrentConfig = new RpcConfig();
                }
            }
            else
            {
                Console.WriteLine("Config not found. Creating default config.json.");
                CurrentConfig = new RpcConfig();
                SaveConfig();
            }
        }

        private static void MigrateConfig(RpcConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.ProgressiveEventSmallTextTemplate) ||
                string.Equals(config.ProgressiveEventSmallTextTemplate, RpcConfig.LegacyProgressiveEventSmallTextTemplate, StringComparison.Ordinal))
            {
                config.ProgressiveEventSmallTextTemplate = "Clearing: {{ActiveProgressiveEvent}}{{ActiveEventDetailText != \"\" ? \" (\" + ActiveEventDetailText + \")\" : \"\"}}";
                SaveConfig();
            }

            if (string.IsNullOrWhiteSpace(config.NonProgressiveEventSmallTextTemplate) ||
                string.Equals(config.NonProgressiveEventSmallTextTemplate, RpcConfig.LegacyNonProgressiveEventSmallTextTemplate, StringComparison.Ordinal))
            {
                config.NonProgressiveEventSmallTextTemplate = "{{ActiveNonProgressiveEvent == \"Blood Moon\" ? \"The Blood Moon is rising...\" : ActiveNonProgressiveEvent == \"Solar Eclipse\" ? \"A Solar Eclipse is happening!\" : \"Clearing: {{ActiveNonProgressiveEvent}}\"}}";
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
                Console.WriteLine("Config saved.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to save config: {ex.Message}");
            }
        }
    }
}

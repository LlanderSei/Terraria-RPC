using System;
using System.IO;
using System.Text.Json;

namespace TerrariaRPC.Core
{
    public class RpcConfig
    {
        public string Line1 { get; set; } = "{{WorldName}} - In {{Biome}}";
        public string Line2 { get; set; } = "ATK: {{PlayerAtk}} | DEF: {{PlayerDef}} | HP: {{PlayerHp}}/{{PlayerMaxHp}} | MP: {{PlayerMp}}/{{PlayerMaxMp}}";
        
        // Large Image settings
        public int LargeImageStyleIndex { get; set; } = 0; // 0 = Special Seed Icon, 1 = Custom
        public string LargeImageCustomUrl { get; set; } = "";
        public string LargeImageCustomText { get; set; } = "";
        
        // Small Image settings
        public int SmallImageStyleIndex { get; set; } = 0; // 0 = Item Icon, 1 = Custom
        public string SmallImageCustomUrl { get; set; } = "";
        public string SmallImageCustomText { get; set; } = "";

        // Discord settings
        public string ClientId { get; set; } = "123456789012345678";
    }

    public static class ConfigManager
    {
        private static readonly string ConfigPath = "config.json";
        public static RpcConfig CurrentConfig { get; private set; }

        public static void LoadConfig()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigPath);
                    CurrentConfig = JsonSerializer.Deserialize<RpcConfig>(json) ?? new RpcConfig();
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

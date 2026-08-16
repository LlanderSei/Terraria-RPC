using System;
using System.Collections.Generic;

namespace TerrariaRPC.Core
{
  public class PresenceTemplateEngine
  {
    public static string Format(string template, TerrariaGameState state)
    {
      if (string.IsNullOrEmpty(template)) return "";

      var replacements = new Dictionary<string, string>
      {
        // World
        { "{{WorldName}}",                      state.WorldName },
        { "{{Biome}}",                          state.Biome },
        { "{{WorldSize}}",                      state.WorldSize },
        { "{{WorldEvilType}}",                  state.WorldEvil },
        { "{{WorldDifficulty}}",                state.WorldRawDifficulty },
        { "{{WorldDifficultyWithFtwEscalation}}", state.WorldDifficulty },
        { "{{WorldIsHardmode}}",                state.WorldIsHardmode ? "Hardmode" : "Pre-hardmode" },
        { "{{WorldSpecialSeeds}}",              string.Join(", ", state.WorldSpecialSeeds) },
        { "{{WorldSecretSeeds}}",               string.Join(", ", state.WorldSecretSeeds) },
        { "{{WorldSecretSeedsAsNum}}",          state.WorldSecretSeedsAsNum.ToString() },

        // Player stats
        { "{{PlayerHp}}",                       state.PlayerHp.ToString() },
        { "{{PlayerMaxHp}}",                    state.PlayerMaxHp.ToString() },
        { "{{PlayerMp}}",                       state.PlayerMp.ToString() },
        { "{{PlayerMaxMp}}",                    state.PlayerMaxMp.ToString() },
        { "{{PlayerAtk}}",                      state.PlayerAtk },
        { "{{PlayerDef}}",                      state.PlayerDef.ToString() },
        { "{{PlayerItemHeld}}",                 state.PlayerItemHeld },
      };

      string result = template;
      foreach (var kvp in replacements)
        result = result.Replace(kvp.Key, kvp.Value);

      return result;
    }
  }
}

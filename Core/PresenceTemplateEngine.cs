using System;
using System.Collections.Generic;

namespace TerrariaRPC.Core
{
  public class PresenceTemplateEngine
  {
    public static string Format(string template, TerrariaGameState state)
    {
      if (string.IsNullOrEmpty(template)) return "";

      static string StatText(int value) => value > 0 ? value.ToString() : "N/A";

      string bossOrEventText = "";
      if (state.HasActiveBoss) bossOrEventText = state.ActiveBossText;
      else if (state.HasActiveEvent) bossOrEventText = state.ActiveEventText;
      else if (state.HasActiveNonProgressiveEvent) bossOrEventText = state.ActiveNonProgressiveEventName;
      else if (state.HasActivePeacefulEvent) bossOrEventText = state.ActivePeacefulEventName;
      else if (state.HasActiveWeather) bossOrEventText = state.ActiveWeatherName;

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
        { "{{PlayerHighestWeaponDmg}}",         StatText(state.PlayerHighestWeaponDmg) },
        { "{{PlayerHighestDps}}",               StatText(state.PlayerHighestDps) },
        { "{{PlayerDynamicWeaponDmg}}",         StatText(state.PlayerDynamicWeaponDmg) },
        { "{{PlayerDynamicDps}}",               StatText(state.PlayerDynamicDps) },
        { "{{PlayerDef}}",                      state.PlayerDef.ToString() },
        { "{{PlayerItemHeld}}",                 state.PlayerItemHeld },

        // Active Boss & Event
        { "{{ActiveBoss}}",                     state.ActiveBossName },
        { "{{ActiveBossHp}}",                   state.ActiveBossHp.ToString() },
        { "{{ActiveBossMaxHp}}",                state.ActiveBossMaxHp.ToString() },
        { "{{ActiveEvent}}",                    state.ActiveEventName },
        { "{{ActiveEventProgress}}",            state.ActiveEventProgress >= 0 ? state.ActiveEventProgress.ToString() : "" },
        { "{{ActiveEventWaveNum}}",             state.ActiveEventWaveNum > 0 ? state.ActiveEventWaveNum.ToString() : "" },
        { "{{ActiveBossOrEventText}}",          bossOrEventText }
      };

      string result = template;
      foreach (var kvp in replacements)
        result = result.Replace(kvp.Key, kvp.Value);

      return result;
    }
  }
}

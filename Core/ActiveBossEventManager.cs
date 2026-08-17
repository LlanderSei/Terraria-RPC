using System;
using System.Collections.Generic;
using System.Linq;

namespace TerrariaRPC.Core
{
  public enum EntityCategory
  {
    None,
    Boss,
    Event,
    NonProgressiveEvent,
    PeacefulEvent,
    Weather
  }

  public class ResolvedEntityInfo
  {
    public EntityCategory Category { get; set; } = EntityCategory.None;
    public string Name { get; set; } = "";
    public string DisplayText { get; set; } = "";
    public string IconUrl { get; set; } = "";
  }

  public class ActiveBossEventManager
  {
    private int _rotationIndex = 0;
    private DateTime _lastRotationTime = DateTime.MinValue;

    /// <summary>
    /// Evaluates available active entities in priority order:
    /// Boss > Events > Non-progressive Events > Peaceful Events > Weather.
    /// Skips any category marked as Excluded in RpcConfig.
    /// </summary>
    public ResolvedEntityInfo GetActiveBossesAndEvents(TerrariaGameState state, RpcConfig config, IconManager iconManager)
    {
      // 1. Boss (Highest Priority)
      if (!config.ExcludeBoss && state.HasActiveBoss)
      {
        return new ResolvedEntityInfo
        {
          Category = EntityCategory.Boss,
          Name = state.ActiveBossName,
          DisplayText = state.ActiveBossText,
          IconUrl = iconManager.GetBossIconUrl(state.ActiveBossName)
        };
      }

      // 2. Progressive Events (Invasion, Slime Rain, etc.)
      if (!config.ExcludeEvents && state.HasActiveEvent)
      {
        return new ResolvedEntityInfo
        {
          Category = EntityCategory.Event,
          Name = state.ActiveEventName,
          DisplayText = state.ActiveEventText,
          IconUrl = iconManager.GetEventIconUrl(state.ActiveEventName)
        };
      }

      // 3. Non-Progressive Events (Blood Moon, Solar Eclipse)
      if (!config.ExcludeNonProgressiveEvents && state.HasActiveNonProgressiveEvent)
      {
        return new ResolvedEntityInfo
        {
          Category = EntityCategory.NonProgressiveEvent,
          Name = state.ActiveNonProgressiveEventName,
          DisplayText = state.ActiveNonProgressiveEventName,
          IconUrl = iconManager.GetEventIconUrl(state.ActiveNonProgressiveEventName)
        };
      }

      // 4. Peaceful Events (Party, Lantern Night)
      if (!config.ExcludePeacefulEvents && state.HasActivePeacefulEvent)
      {
        return new ResolvedEntityInfo
        {
          Category = EntityCategory.PeacefulEvent,
          Name = state.ActivePeacefulEventName,
          DisplayText = state.ActivePeacefulEventName,
          IconUrl = iconManager.GetPeacefulIconUrl(state.ActivePeacefulEventName)
        };
      }

      // 5. Weather Events (Rain, Thunderstorm, Sandstorm, Windy Day)
      if (!config.ExcludeWeather && state.HasActiveWeather)
      {
        return new ResolvedEntityInfo
        {
          Category = EntityCategory.Weather,
          Name = state.ActiveWeatherName,
          DisplayText = state.ActiveWeatherName,
          IconUrl = iconManager.GetWeatherIconUrl(state.ActiveWeatherName)
        };
      }

      return new ResolvedEntityInfo();
    }

    /// <summary>
    /// Gets the current Small Image URL and Hover Text based on rotation settings.
    /// </summary>
    public (string IconUrl, string HoverText) GetSmallIconAndText(TerrariaGameState state, RpcConfig config, IconManager iconManager, string itemIconUrl)
    {
      // Custom URL override mode
      if (config.SmallImageStyleIndex == 1)
      {
        return (config.SmallImageCustomUrl, config.SmallImageCustomText);
      }

      // Rotation mode based on checkboxes
      var slots = new List<(string IconUrl, string HoverText)>();

      // Slot A: Holding Item
      if (config.SmallItemEnabled && !string.IsNullOrEmpty(state.PlayerItemHeld))
      {
        string text = string.IsNullOrEmpty(state.PlayerItemPrefix)
          ? state.PlayerItemHeld
          : $"{state.PlayerItemPrefix} {state.PlayerItemHeld}";
        slots.Add((itemIconUrl, text));
      }

      // Boss / Event processing if enabled
      if (config.SmallBossEventEnabled)
      {
        // Primary Top Priority Entity
        var primary = GetActiveBossesAndEvents(state, config, iconManager);
        if (primary.Category != EntityCategory.None)
        {
          slots.Add((primary.IconUrl, primary.DisplayText));
        }

        // Additional explicitly Included categories (for cycling alongside primary)
        // 1. Events include
        if (config.IncludeEvents && !config.ExcludeEvents && state.HasActiveEvent && primary.Category != EntityCategory.Event)
        {
          string icon = iconManager.GetEventIconUrl(state.ActiveEventName);
          slots.Add((icon, state.ActiveEventText));
        }

        // 2. Non-progressive events include
        if (config.IncludeNonProgressiveEvents && !config.ExcludeNonProgressiveEvents && state.HasActiveNonProgressiveEvent && primary.Category != EntityCategory.NonProgressiveEvent)
        {
          string icon = iconManager.GetEventIconUrl(state.ActiveNonProgressiveEventName);
          slots.Add((icon, state.ActiveNonProgressiveEventName));
        }

        // 3. Peaceful events include
        if (config.IncludePeacefulEvents && !config.ExcludePeacefulEvents && state.HasActivePeacefulEvent && primary.Category != EntityCategory.PeacefulEvent)
        {
          string icon = iconManager.GetPeacefulIconUrl(state.ActivePeacefulEventName);
          slots.Add((icon, state.ActivePeacefulEventName));
        }

        // 4. Weather include
        if (config.IncludeWeather && !config.ExcludeWeather && state.HasActiveWeather && primary.Category != EntityCategory.Weather)
        {
          string icon = iconManager.GetWeatherIconUrl(state.ActiveWeatherName);
          slots.Add((icon, state.ActiveWeatherName));
        }
      }

      if (slots.Count == 0)
      {
        return ("", "");
      }

      // Advance index if cycling interval elapsed
      if (slots.Count > 1 && (DateTime.Now - _lastRotationTime).TotalSeconds >= 4.0)
      {
        _rotationIndex = (_rotationIndex + 1) % slots.Count;
        _lastRotationTime = DateTime.Now;
      }

      if (_rotationIndex >= slots.Count) _rotationIndex = 0;

      return slots[_rotationIndex];
    }
  }
}

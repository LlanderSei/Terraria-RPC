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
    private long _lastRotationSequence = -1;
    private int _rotationCursor;
    private (string IconUrl, string HoverText) _lastRotationSnapshot = ("", "");
    private static string ResolveBossText(TerrariaGameState state, RpcConfig config, IconManager iconManager)
    {
      return string.IsNullOrWhiteSpace(config.BossSmallTextTemplate)
        ? state.ActiveBossText
        : PresenceTemplateEngine.Format(config.BossSmallTextTemplate, state, iconManager);
    }

    private static string ResolveProgressiveEventText(TerrariaGameState state, RpcConfig config, IconManager iconManager)
    {
      return string.IsNullOrWhiteSpace(config.ProgressiveEventSmallTextTemplate)
        ? state.ActiveEventText
        : PresenceTemplateEngine.Format(config.ProgressiveEventSmallTextTemplate, state, iconManager);
    }

    private static string ResolveNonProgressiveEventText(TerrariaGameState state, RpcConfig config, IconManager iconManager)
    {
      return string.IsNullOrWhiteSpace(config.NonProgressiveEventSmallTextTemplate)
        ? state.ActiveNonProgressiveEventText
        : PresenceTemplateEngine.Format(config.NonProgressiveEventSmallTextTemplate, state, iconManager);
    }

    private static string ResolvePeacefulEventText(TerrariaGameState state, RpcConfig config, IconManager iconManager)
    {
      return string.IsNullOrWhiteSpace(config.PeacefulEventSmallTextTemplate)
        ? $"{state.ActivePeacefulEventName} is occuring."
        : PresenceTemplateEngine.Format(config.PeacefulEventSmallTextTemplate, state, iconManager);
    }

    private static string ResolveWeatherText(TerrariaGameState state, RpcConfig config, IconManager iconManager)
    {
      return string.IsNullOrWhiteSpace(config.WeatherSmallTextTemplate)
        ? state.ActiveWeatherName
        : PresenceTemplateEngine.Format(config.WeatherSmallTextTemplate, state, iconManager);
    }

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
          DisplayText = ResolveBossText(state, config, iconManager),
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
          DisplayText = ResolveProgressiveEventText(state, config, iconManager),
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
          DisplayText = ResolveNonProgressiveEventText(state, config, iconManager),
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
          DisplayText = ResolvePeacefulEventText(state, config, iconManager),
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
          DisplayText = ResolveWeatherText(state, config, iconManager),
          IconUrl = iconManager.GetWeatherIconUrl(state.ActiveWeatherName)
        };
      }

      return new ResolvedEntityInfo();
    }

    /// <summary>
    /// Gets the current Small Image URL and Hover Text based on rotation settings.
    /// </summary>
    public (string IconUrl, string HoverText) GetSmallIconAndText(TerrariaGameState state, RpcConfig config, IconManager iconManager, string itemIconUrl, long presenceSequence)
    {
      // Custom URL override mode
      if (config.SmallImageStyleIndex == 1)
      {
        return (PresenceTemplateEngine.Format(config.SmallImageCustomUrl, state, iconManager), PresenceTemplateEngine.Format(config.SmallImageCustomText, state, iconManager));
      }

      if (presenceSequence == _lastRotationSequence)
      {
        return _lastRotationSnapshot;
      }

      var templates = config.InGame.SmallDetails.Templates;
      if (templates.Count == 0)
      {
        _lastRotationSequence = presenceSequence;
        _lastRotationSnapshot = ("", "");
        return _lastRotationSnapshot;
      }

      // Keep a cursor over the configured list. Each update evaluates forward
      // from that cursor and skips empty statuses until one produces content.
      _rotationCursor %= templates.Count;
      for (int checkedCount = 0; checkedCount < templates.Count; checkedCount++)
      {
        int index = (_rotationCursor + checkedCount) % templates.Count;
        var template = templates[index];
        if (!template.Enabled)
        {
          continue;
        }

        string iconUrl = PresenceTemplateEngine.Format(template.Image, state, iconManager).Trim();
        string hoverText = PresenceTemplateEngine.Format(template.Text, state, iconManager).Trim();
        if (string.IsNullOrWhiteSpace(iconUrl) && string.IsNullOrWhiteSpace(hoverText))
        {
          continue;
        }

        _rotationCursor = (index + 1) % templates.Count;
        _lastRotationSequence = presenceSequence;
        _lastRotationSnapshot = (iconUrl, hoverText);
        return _lastRotationSnapshot;
      }

      _lastRotationSequence = presenceSequence;
      _lastRotationSnapshot = ("", "");
      return _lastRotationSnapshot;
    }
  }
}

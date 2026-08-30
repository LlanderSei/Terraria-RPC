using System;
using DiscordRPC;

namespace TerrariaRPC.Core
{
    public class DiscordRpcManager : IDisposable
    {
        private DiscordRpcClient client;
        private IconManager iconManager;
        private ActiveBossEventManager bossEventManager = new ActiveBossEventManager();
        private string currentClientId = "";
        private readonly DateTime sessionStartUtc;

        public DiscordRpcManager(IconManager iconManager)
        {
            this.iconManager = iconManager;
            sessionStartUtc = DateTime.UtcNow;
        }

        private void EnsureClient(string clientId)
        {
            if (client == null || currentClientId != clientId)
            {
                client?.Dispose();
                currentClientId = clientId;
                client = new DiscordRpcClient(clientId);
                client.Initialize();
                Logger.Info($"Discord RPC client initialised (ClientId: {clientId})");
            }
        }

    public void UpdatePresence(TerrariaGameState state, RpcConfig config)
    {
        EnsureClient(config.ClientId);
        iconManager.UpdateWorldState(state);

            bool isInGame = state.Screen == GameScreen.InGameSinglePlayer || state.Screen == GameScreen.InGameMultiplayer;
            bool isMenuContext = !isInGame;

            string title = isMenuContext
                ? PresenceTemplateEngine.Format(config.MainMenuLine1, state, iconManager)
                : PresenceTemplateEngine.Format(config.InGameLine1, state, iconManager);

            string subtitle1 = isMenuContext
                ? PresenceTemplateEngine.Format(config.MainMenuLine2, state, iconManager)
                : PresenceTemplateEngine.Format(config.InGameLine2, state, iconManager);

            string largeIconUrl = config.LargeImageStyleIndex == 1
                ? ResolveImageUrlTemplate(config.LargeImageCustomUrl, state, iconManager)
                : (isInGame ? iconManager.GetCurrentWorldIconUrl() : ResolveImageUrlTemplate(config.MainMenuLargeImageUrl, state, iconManager, "https://terraria.wiki.gg/images/Treetop_Forest_1.png"));

            string largeImageText = "";
            if (isInGame)
            {
                if (config.LargeImageStyleIndex == 1)
                {
                    largeImageText = PresenceTemplateEngine.Format(config.LargeImageCustomText, state, iconManager);
                }
                else if (!string.IsNullOrEmpty(state.WorldDifficulty))
                {
                    bool hasSpecial = state.WorldSpecialSeeds.Length > 0;
                    bool hasSecret = state.WorldSecretSeedsAsNum > 0;

                    string diffPart = $"{state.WorldDifficulty} Mode";

                    if (hasSpecial && hasSecret)
                    {
                        string specialStr = string.Join(", ", state.WorldSpecialSeeds);
                        string secretStr = state.WorldSecretSeedsAsNum == 1
                            ? "+1 secret seed"
                            : $"+{state.WorldSecretSeedsAsNum} secret seeds";
                        largeImageText = $"{diffPart} | {specialStr} {secretStr}";
                    }
                    else if (hasSpecial)
                    {
                        largeImageText = $"{diffPart} | {string.Join(", ", state.WorldSpecialSeeds)}";
                    }
                    else if (hasSecret)
                    {
                        string secretStr = state.WorldSecretSeedsAsNum == 1
                            ? "+1 secret seed"
                            : $"+{state.WorldSecretSeedsAsNum} secret seeds";
                        largeImageText = $"{diffPart} | {secretStr}";
                    }
                    else
                    {
                        largeImageText = diffPart;
                    }
                }
            }
            else
            {
                largeImageText = PresenceTemplateEngine.Format(config.MainMenuLargeImageText, state, iconManager);
            }

            // Discord enforces a 128-char limit on image tooltip text
            if (largeImageText.Length > 128)
                largeImageText = largeImageText[..125] + "...";

            string smallIconUrl = "";
            string smallImageText = "";

            if (isInGame)
            {
                string heldItemWikiName = (state.PlayerItemHeld ?? "").Replace(" ", "_");
                string itemIconUrl = !string.IsNullOrEmpty(heldItemWikiName) ? $"https://terraria.wiki.gg/images/{heldItemWikiName}.png" : "";

                var (url, text) = bossEventManager.GetSmallIconAndText(state, config, iconManager, itemIconUrl);
                smallIconUrl = url;
                smallImageText = text;
            }
            else
            {
                smallIconUrl = ResolveImageUrlTemplate(config.MainMenuSmallImageUrl, state, iconManager);
                smallImageText = PresenceTemplateEngine.Format(config.MainMenuSmallImageText, state, iconManager);
            }

            if (smallImageText.Length > 128)
                smallImageText = smallImageText[..125] + "...";

            var presence = new RichPresence()
            {
                Details = title,
                State = subtitle1,
                Timestamps = new Timestamps(sessionStartUtc),
                Assets = new Assets()
                {
                    LargeImageKey = string.IsNullOrEmpty(largeIconUrl) ? null : largeIconUrl,
                    LargeImageText = string.IsNullOrEmpty(largeImageText) ? null : largeImageText,
                    SmallImageKey = string.IsNullOrEmpty(smallIconUrl) ? null : smallIconUrl,
                    SmallImageText = string.IsNullOrEmpty(smallImageText) ? null : smallImageText
                }
            };

            client.SetPresence(presence);
            client.Invoke();
            Logger.Info($"Presence sent → Details:\"{title}\" State:\"{subtitle1}\" SmallIcon:\"{smallIconUrl}\" SmallText:\"{smallImageText}\"");
        }

        public void Dispose()
        {
            client?.Dispose();
        }

        private static string ResolveImageUrlTemplate(string template, TerrariaGameState state, IconManager iconManager, string fallback = "")
        {
            string resolved = PresenceTemplateEngine.Format(template, state, iconManager).Trim();
            return string.IsNullOrWhiteSpace(resolved) ? fallback : resolved;
        }
    }
}

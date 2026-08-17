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

        public DiscordRpcManager(IconManager iconManager)
        {
            this.iconManager = iconManager;
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

            string title = "";
            string subtitle1 = "";

            if (!state.IsAttached)
            {
                title = "Waiting for Terraria...";
                subtitle1 = "";
            }
            else
            {
                switch (state.Screen)
                {
                    case GameScreen.InGameSinglePlayer:
                    case GameScreen.InGameMultiplayer:
                        title = PresenceTemplateEngine.Format(config.Line1, state);
                        subtitle1 = PresenceTemplateEngine.Format(config.Line2, state);
                        break;

                    case GameScreen.MainMenu:
                        title = "On Main Menu";
                        subtitle1 = "";
                        break;

                    case GameScreen.PlayerSelection:
                        title = "Single Player";
                        subtitle1 = "Choosing a player...";
                        break;

                    case GameScreen.WorldSelection:
                        title = "Single Player";
                        subtitle1 = "Selecting a world...";
                        break;

                    case GameScreen.EnteringWorld:
                        title = "Single Player";
                        subtitle1 = $"Entering {state.WorldName}...";
                        break;

                    case GameScreen.MultiplayerBrowser:
                        title = "Multiplayer";
                        subtitle1 = "Selecting connection type...";
                        break;

                    case GameScreen.MultiplayerPlayerSelection:
                        title = "Multiplayer";
                        subtitle1 = "Choosing a player...";
                        break;

                    case GameScreen.MultiplayerIpSelection:
                        title = "Multiplayer";
                        subtitle1 = "Selecting an address to join...";
                        break;

                    case GameScreen.MultiplayerJoining:
                        title = "Multiplayer";
                        subtitle1 = "Joining world...";
                        break;

                    default:
                        title = "In Menus";
                        subtitle1 = "";
                        break;
                }
            }

            bool isInGame = state.Screen == GameScreen.InGameSinglePlayer || state.Screen == GameScreen.InGameMultiplayer;

            string largeIconUrl = config.LargeImageStyleIndex == 1
                ? config.LargeImageCustomUrl
                : (isInGame ? iconManager.GetCurrentWorldIconUrl() : "https://terraria.wiki.gg/images/Treetop_Forest_1.png");

            string largeImageText = "";
            if (isInGame)
            {
                if (config.LargeImageStyleIndex == 1)
                {
                    largeImageText = PresenceTemplateEngine.Format(config.LargeImageCustomText, state);
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

            // Discord enforces a 128-char limit on image tooltip text
            if (largeImageText.Length > 128)
                largeImageText = largeImageText[..125] + "...";

            string smallIconUrl = "";
            string smallImageText = "";

            if (isInGame)
            {
                string heldItemWikiName = state.PlayerItemHeld.Replace(" ", "_");
                string itemIconUrl = !string.IsNullOrEmpty(heldItemWikiName) ? $"https://terraria.wiki.gg/images/{heldItemWikiName}.png" : "";

                var (url, text) = bossEventManager.GetSmallIconAndText(state, config, iconManager, itemIconUrl);
                smallIconUrl = url;
                smallImageText = text;
            }

            if (smallImageText.Length > 128)
                smallImageText = smallImageText[..125] + "...";

            var presence = new RichPresence()
            {
                Details = title,
                State = subtitle1,
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
    }
}

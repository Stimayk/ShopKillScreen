using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Newtonsoft.Json.Linq;
using ShopAPI;

namespace ShopKillScreen
{
    public class ShopKillScreen : BasePlugin
    {
        public override string ModuleName => "[SHOP] KillScreen";
        public override string ModuleDescription => "";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.0.1";

        private IShopApi? SHOP_API;
        private const string CategoryName = "KillScreen";
        public static JObject? JsonKillScreen { get; private set; }
        private readonly PlayerKillScreen[] playerKillScreen = new PlayerKillScreen[65];

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            SHOP_API = IShopApi.Capability.Get();
            if (SHOP_API == null) return;

            LoadConfig();
            InitializeShopItems();
            SetupTimersAndListeners();
        }

        private void LoadConfig()
        {
            string configPath = Path.Combine(ModuleDirectory, "../../configs/plugins/Shop/KillScreen.json");
            if (File.Exists(configPath))
            {
                JsonKillScreen = JObject.Parse(File.ReadAllText(configPath));
            }
        }

        private void InitializeShopItems()
        {
            if (JsonKillScreen == null || SHOP_API == null) return;

            SHOP_API.CreateCategory(CategoryName, "Ёффект при убийстве");

            foreach (var item in JsonKillScreen.Properties().Where(p => p.Value is JObject))
            {
                Task.Run(async () =>
                {
                    int itemId = await SHOP_API.AddItem(
                        item.Name,
                        (string)item.Value["name"]!,
                        CategoryName,
                        (int)item.Value["price"]!,
                        (int)item.Value["sellprice"]!,
                        (int)item.Value["duration"]!
                    );
                    SHOP_API.SetItemCallbacks(itemId, OnClientBuyItem, OnClientSellItem, OnClientToggleItem);
                }).Wait();
            }
        }

        private void SetupTimersAndListeners()
        {
            RegisterListener<Listeners.OnClientDisconnect>(playerSlot => playerKillScreen[playerSlot] = null!);

            RegisterEventHandler<EventPlayerDeath>((@event, info) =>
            {
                var attacker = @event.Attacker;
                if (attacker is null || !attacker.IsValid) return HookResult.Continue;
                if (@event.Userid is not null && attacker.PlayerName == @event.Userid.PlayerName) return HookResult.Continue;

                if (playerKillScreen[attacker.Slot] != null)
                {
                    var attackerPawn = attacker.PlayerPawn.Value;

                    if (attackerPawn == null) return HookResult.Continue;

                    attackerPawn.HealthShotBoostExpirationTime = Server.CurrentTime + 1.0f;
                    Utilities.SetStateChanged(attackerPawn, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");
                }
                return HookResult.Continue;
            });
        }

        public HookResult OnClientBuyItem(CCSPlayerController player, int itemId, string categoryName, string uniqueName, int buyPrice, int sellPrice, int duration, int count)
        {
            playerKillScreen[player.Slot] = new PlayerKillScreen(itemId);
            return HookResult.Continue;
        }

        public HookResult OnClientToggleItem(CCSPlayerController player, int itemId, string uniqueName, int state)
        {
            if (state == 1)
            {
                playerKillScreen[player.Slot] = new PlayerKillScreen(itemId);
            }
            else if (state == 0)
            {
                OnClientSellItem(player, itemId, uniqueName, 0);
            }
            return HookResult.Continue;
        }

        public HookResult OnClientSellItem(CCSPlayerController player, int itemId, string uniqueName, int sellPrice)
        {
            playerKillScreen[player.Slot] = null!;
            return HookResult.Continue;
        }

        public record class PlayerKillScreen(int ItemID);
    }
}
using GenericModConfigMenu;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace ICaughtThisContinued
{
    public class ModEntry : Mod
    {
        private ModConfig Config = null!;

        private enum CreditResult
        {
            Credited,
            AlreadyCredited,
            NotFish,
            NoChange
        }

        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Player.InventoryChanged += OnInventoryChanged;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
            {
                return;
            }

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            configMenu.AddTextOption(
                mod: this.ModManifest,
                getValue: () => this.Config.Mode.ToString(),
                setValue: value =>
                {
                    if (Enum.TryParse(value, out TriggerMode mode))
                    {
                        this.Config.Mode = mode;
                    }
                },
                name: () => Translate("config.mode.name"),
                tooltip: () => Translate("config.mode.tooltip"),
                allowedValues: Enum.GetNames<TriggerMode>(),
                formatAllowedValue: value => Translate($"config.mode.{value.ToLowerInvariant()}")
            );

            configMenu.AddKeybind(
                mod: this.ModManifest,
                getValue: () => this.Config.TriggerKey,
                setValue: value => this.Config.TriggerKey = value,
                name: () => Translate("config.trigger-key.name"),
                tooltip: () => Translate("config.trigger-key.tooltip")
            );
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || this.Config.Mode != TriggerMode.Manual) return;
            if (e.Button != this.Config.TriggerKey) return;

            if (Game1.player.CurrentItem is StardewValley.Object obj)
            {
                CreditResult result = TryCreditFish(Game1.player, obj, "source.manual");
                if (result == CreditResult.AlreadyCredited)
                {
                    ShowHudMessage("hud.already-credited", new { fishName = obj.DisplayName }, HUDMessage.error_type);
                }
                else if (result == CreditResult.NotFish)
                {
                    ShowHudMessage("hud.not-fish", new { itemName = obj.DisplayName }, HUDMessage.error_type);
                }
                else if (result == CreditResult.NoChange)
                {
                    ShowHudMessage("hud.no-change", new { fishName = obj.DisplayName }, HUDMessage.error_type);
                }
            }
            else
            {
                ShowHudMessage("hud.not-holding-item", null, HUDMessage.error_type);
            }
        }

        private void OnInventoryChanged(object? sender, InventoryChangedEventArgs e)
        {
            if (!Context.IsWorldReady || !e.IsLocalPlayer || this.Config.Mode != TriggerMode.Automatic) return;

            foreach (Item item in e.Added)
            {
                if (item is StardewValley.Object obj)
                {
                    TryCreditFish(e.Player, obj, "source.automatic");
                }
            }

            foreach (ItemStackSizeChange change in e.QuantityChanged)
            {
                if (change.NewSize > change.OldSize && change.Item is StardewValley.Object obj)
                {
                    TryCreditFish(e.Player, obj, "source.automatic");
                }
            }
        }

        private CreditResult TryCreditFish(Farmer player, StardewValley.Object fish, string sourceKey)
        {
            if (!TryGetCreditableFishId(fish, out string fishId))
            {
                return CreditResult.NotFish;
            }

            string source = Translate(sourceKey);
            string fishName = fish.DisplayName;

            // Use the same normalized ID format as vanilla to avoid double-counting.
            if (player.fishCaught.ContainsKey(fishId))
            {
                Monitor.Log(Translate("log.already-credited", new { source, fishName }), LogLevel.Trace);
                return CreditResult.AlreadyCredited;
            }

            Monitor.Log(Translate("log.credit-needed", new { source, fishName }), LogLevel.Debug);
            return HandleFishAction(player, fishId, source, fishName);
        }

        private static bool TryGetCreditableFishId(StardewValley.Object item, out string fishId)
        {
            fishId = item.QualifiedItemId;

            var metadata = ItemRegistry.GetMetadata(fishId);
            if (!metadata.Exists())
            {
                return false;
            }

            fishId = metadata.QualifiedItemId;

            if (ItemContextTagManager.HasBaseTag(fishId, "trash_item") || fishId == "(O)167")
            {
                return false;
            }

            return metadata.GetParsedData()?.ObjectType == "Fish" || fishId == "(O)372";
        }

        private CreditResult HandleFishAction(Farmer player, string fishId, string source, string fishName)
        {
            // Delegate collection updates and achievement checks to the vanilla method.
            player.caughtFish(fishId, 9, false, 1);

            if (player.fishCaught.ContainsKey(fishId))
            {
                Monitor.Log(Translate("log.credit-success", new { source, fishName }), LogLevel.Info);
                ShowHudMessage("hud.credit-success", new { fishName }, HUDMessage.achievement_type);
                return CreditResult.Credited;
            }
            else
            {
                Monitor.Log(Translate("log.credit-no-change", new { source, fishName }), LogLevel.Trace);
                return CreditResult.NoChange;
            }
        }

        private void ShowHudMessage(string key, object? tokens, int type)
        {
            Game1.addHUDMessage(new HUDMessage(Translate(key, tokens), type));
        }

        private string Translate(string key, object? tokens = null)
        {
            return tokens is null
                ? Helper.Translation.Get(key).ToString()
                : Helper.Translation.Get(key, tokens).ToString();
        }
    }
}

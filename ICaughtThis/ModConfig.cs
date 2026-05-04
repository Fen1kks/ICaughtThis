using StardewModdingAPI;

namespace ICaughtThisContinued
{
    public enum TriggerMode
    {
        Manual,
        Automatic
    }

    public class ModConfig
    {
        public TriggerMode Mode { get; set; } = TriggerMode.Automatic;
        
        public SButton TriggerKey { get; set; } = SButton.F9;
    }
}

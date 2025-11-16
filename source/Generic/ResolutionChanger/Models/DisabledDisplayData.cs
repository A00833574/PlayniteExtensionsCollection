using static WinApi.Structs;

namespace DisplayHelper.Models
{
    public class DisabledDisplayData
    {
        public string DisplayName { get; }
        public DEVMODE DevMode { get; }

        public DisabledDisplayData(string displayName, DEVMODE devMode)
        {
            DisplayName = displayName;
            DevMode = devMode;
        }
    }
}

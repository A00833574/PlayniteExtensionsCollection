using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static WinApi.Structs;

namespace DisplayHelper.Models
{
    public class DisplayConfigChangeData
    {
        public readonly DEVMODE DevMode;
        public readonly string TargetDisplayName;
        public readonly string PrimaryDisplayName;
        public readonly bool RestoreResolutionValues;
        public readonly bool RestoreRefreshRate;
        public bool RestorePrimaryDisplay => TargetDisplayName != PrimaryDisplayName;
        public readonly List<DisabledDisplayData> DisabledDisplays;
        public bool HasDisabledDisplays => DisabledDisplays?.Any() == true;

        public DisplayConfigChangeData(DEVMODE devMode, string targetDisplayName, string primaryDisplayName, bool restoreResolutionValues, bool restoreRefreshRate, List<DisabledDisplayData> disabledDisplays = null)
        {
            DevMode = devMode;
            TargetDisplayName = targetDisplayName;
            PrimaryDisplayName = primaryDisplayName;
            RestoreResolutionValues = restoreResolutionValues;
            RestoreRefreshRate = restoreRefreshRate;
            DisabledDisplays = disabledDisplays?.ToList() ?? new List<DisabledDisplayData>();
        }
    }
}

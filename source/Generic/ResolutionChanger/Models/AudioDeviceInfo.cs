using Playnite.SDK;
using Playnite.SDK.Data;

namespace DisplayHelper.Models
{
    public class AudioDeviceInfo : ObservableObject
    {
        public string Id { get; }
        public string Name { get; }
        public bool IsDefault { get; }
        public string DisplayName => IsDefault ? $"{Name} ({ResourceProvider.GetString("LOCDisplayHelper_AudioDeviceDefaultLabel")})" : Name;

        public AudioDeviceInfo(string id, string name, bool isDefault)
        {
            Id = id;
            Name = name;
            IsDefault = isDefault;
        }
    }
}

using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;

namespace SelectionCopyFlash
{
    internal readonly struct SelectionCopyFlashSettings
    {
        public SelectionCopyFlashSettings(int durationMilliseconds, int brightnessPercent)
        {
            DurationMilliseconds = durationMilliseconds;
            BrightnessPercent = brightnessPercent;
        }

        public int DurationMilliseconds { get; }
        public int BrightnessPercent { get; }
    }

    internal static class SelectionCopyFlashSettingsStore
    {
        private const string CollectionName = "SelectionCopyFlash";
        private const string DurationPropertyName = "DurationMilliseconds";
        private const string BrightnessPropertyName = "BrightnessPercent";

        public const int DefaultDurationMilliseconds = 200;
        public const int MinDurationMilliseconds = 40;
        public const int MaxDurationMilliseconds = 1000;
        public const int DefaultBrightnessPercent = 45;
        public const int MinBrightnessPercent = 0;
        public const int MaxBrightnessPercent = 100;

        public static SelectionCopyFlashSettings GetSettings()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var settings = new SelectionCopyFlashSettings(DefaultDurationMilliseconds, DefaultBrightnessPercent);
            var serviceProvider = ServiceProvider.GlobalProvider;
            if (serviceProvider == null)
            {
                return settings;
            }

            try
            {
                var settingsManager = new ShellSettingsManager(serviceProvider);
                var store = settingsManager.GetReadOnlySettingsStore(SettingsScope.UserSettings);

                if (!store.CollectionExists(CollectionName))
                {
                    return settings;
                }

                var duration = store.PropertyExists(CollectionName, DurationPropertyName)
                    ? store.GetInt32(CollectionName, DurationPropertyName)
                    : DefaultDurationMilliseconds;

                var brightness = store.PropertyExists(CollectionName, BrightnessPropertyName)
                    ? store.GetInt32(CollectionName, BrightnessPropertyName)
                    : DefaultBrightnessPercent;

                return new SelectionCopyFlashSettings(
                    ClampDuration(duration),
                    ClampBrightness(brightness));
            }
            catch
            {
                return settings;
            }
        }

        public static void SaveSettings(SelectionCopyFlashSettings settings)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var serviceProvider = ServiceProvider.GlobalProvider;
            if (serviceProvider == null)
            {
                return;
            }

            try
            {
                var settingsManager = new ShellSettingsManager(serviceProvider);
                var store = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

                if (!store.CollectionExists(CollectionName))
                {
                    store.CreateCollection(CollectionName);
                }

                store.SetInt32(CollectionName, DurationPropertyName, ClampDuration(settings.DurationMilliseconds));
                store.SetInt32(CollectionName, BrightnessPropertyName, ClampBrightness(settings.BrightnessPercent));
            }
            catch
            {
                // Best effort only. The extension falls back to defaults if settings cannot be persisted.
            }
        }

        public static int ClampDuration(int value)
        {
            if (value < MinDurationMilliseconds)
            {
                return MinDurationMilliseconds;
            }

            if (value > MaxDurationMilliseconds)
            {
                return MaxDurationMilliseconds;
            }

            return value;
        }

        public static int ClampBrightness(int value)
        {
            if (value < MinBrightnessPercent)
            {
                return MinBrightnessPercent;
            }

            if (value > MaxBrightnessPercent)
            {
                return MaxBrightnessPercent;
            }

            return value;
        }
    }
}

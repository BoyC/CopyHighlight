using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace SelectionCopyFlash
{
    public sealed class SelectionCopyFlashOptionsPage : DialogPage
    {
        private int _durationMilliseconds = SelectionCopyFlashSettingsStore.DefaultDurationMilliseconds;
        private int _brightnessPercent = SelectionCopyFlashSettingsStore.DefaultBrightnessPercent;

        [Category("Animation")]
        [DisplayName("Duration (ms)")]
        [Description("How long the flash lasts, in milliseconds.")]
        [DefaultValue(SelectionCopyFlashSettingsStore.DefaultDurationMilliseconds)]
        public int DurationMilliseconds
        {
            get => _durationMilliseconds;
            set => _durationMilliseconds = SelectionCopyFlashSettingsStore.ClampDuration(value);
        }

        [Category("Animation")]
        [DisplayName("Brightness (%)")]
        [Description("How much brighter the flash starts than the selection color, from 0 to 100.")]
        [DefaultValue(SelectionCopyFlashSettingsStore.DefaultBrightnessPercent)]
        public int BrightnessPercent
        {
            get => _brightnessPercent;
            set => _brightnessPercent = SelectionCopyFlashSettingsStore.ClampBrightness(value);
        }

        public override void ResetSettings()
        {
            DurationMilliseconds = SelectionCopyFlashSettingsStore.DefaultDurationMilliseconds;
            BrightnessPercent = SelectionCopyFlashSettingsStore.DefaultBrightnessPercent;
        }

        public override void LoadSettingsFromStorage()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var settings = SelectionCopyFlashSettingsStore.GetSettings();
            DurationMilliseconds = settings.DurationMilliseconds;
            BrightnessPercent = settings.BrightnessPercent;
        }

        public override void SaveSettingsToStorage()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            SelectionCopyFlashSettingsStore.SaveSettings(
                new SelectionCopyFlashSettings(DurationMilliseconds, BrightnessPercent));
        }
    }
}

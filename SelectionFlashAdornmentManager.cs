using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;

namespace SelectionCopyFlash
{
    internal sealed class SelectionFlashAdornmentManager
    {
        public const string LayerName = "SelectionCopyFlashLayer";

        [Export(typeof(AdornmentLayerDefinition))]
        [Name(LayerName)]
        [Order(After = PredefinedAdornmentLayers.Selection, Before = PredefinedAdornmentLayers.Text)]
#pragma warning disable CS0169 // Discovered by MEF via attributes.
        private static AdornmentLayerDefinition selectionCopyFlashAdornmentLayer;
#pragma warning restore CS0169

        private readonly IWpfTextView _view;
        private readonly IEditorFormatMapService _formatMapService;
        private readonly IAdornmentLayer _layer;
        private readonly IAdornmentLayer _selectionLayer;
        private readonly object _tag = new object();
        private readonly Stopwatch _animationStopwatch = new Stopwatch();
        private readonly List<SolidColorBrush> _activeBrushes = new List<SolidColorBrush>();

        private Color _baseVisibleSelectionColor;
        private Color _flashStartVisibleColor;
        private TimeSpan _animationDuration;
        private bool _isAnimating;
        private bool _removeOnNextRenderingPass;

        public SelectionFlashAdornmentManager(IWpfTextView view, IEditorFormatMapService formatMapService)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _formatMapService = formatMapService ?? throw new ArgumentNullException(nameof(formatMapService));
            _layer = _view.GetAdornmentLayer(LayerName);
            _selectionLayer = _view.GetAdornmentLayer(PredefinedAdornmentLayers.Selection);

            _view.Closed += OnViewClosed;
        }

        public void Flash()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_view.Selection.IsEmpty || _view.TextViewLines == null)
            {
                return;
            }

            CancelActiveFlash();

            var settings = SelectionCopyFlashSettingsStore.GetSettings();
            _baseVisibleSelectionColor = GetVisibleSelectionColor();
            _flashStartVisibleColor = GetFlashStartColor(
                _baseVisibleSelectionColor,
                settings.BrightnessPercent / 100.0);
            _animationDuration = TimeSpan.FromMilliseconds(settings.DurationMilliseconds);
            _removeOnNextRenderingPass = false;

            if (!AddFlashAdornments(BuildOverlayColor(_baseVisibleSelectionColor, _flashStartVisibleColor, 0.0)))
            {
                return;
            }

            _animationStopwatch.Restart();
            CompositionTarget.Rendering += OnRendering;
            _isAnimating = true;
        }

        private bool AddFlashAdornments(Color initialOverlayColor)
        {
            var selectionLayerVisual = _selectionLayer as Visual;
            if (selectionLayerVisual == null)
            {
                return false;
            }

            var addedAny = false;
            foreach (var element in _selectionLayer.Elements)
            {
                if (!(element.Adornment is Visual selectionVisual))
                {
                    continue;
                }

                var selectionBounds = VisualTreeHelper.GetDescendantBounds(selectionVisual);
                if (selectionBounds.IsEmpty || selectionBounds.Width <= 0.0 || selectionBounds.Height <= 0.0)
                {
                    continue;
                }

                Rect layerBounds;
                try
                {
                    layerBounds = selectionVisual
                        .TransformToAncestor(selectionLayerVisual)
                        .TransformBounds(selectionBounds);
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                if (layerBounds.IsEmpty || layerBounds.Width <= 0.0 || layerBounds.Height <= 0.0)
                {
                    continue;
                }

                var fill = new SolidColorBrush(initialOverlayColor);
                var flash = new Rectangle
                {
                    Width = layerBounds.Width,
                    Height = layerBounds.Height,
                    Fill = fill,
                    OpacityMask = new VisualBrush(selectionVisual)
                    {
                        Viewbox = selectionBounds,
                        ViewboxUnits = BrushMappingMode.Absolute,
                        Stretch = Stretch.Fill
                    },
                    IsHitTestVisible = false,
                    SnapsToDevicePixels = true
                };

                Canvas.SetLeft(flash, layerBounds.Left);
                Canvas.SetTop(flash, layerBounds.Top);

                if (_layer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, _tag, flash, null))
                {
                    _activeBrushes.Add(fill);
                    addedAny = true;
                }
            }

            return addedAny;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (!_isAnimating || _activeBrushes.Count == 0)
            {
                CancelActiveFlash();
                return;
            }

            if (_removeOnNextRenderingPass)
            {
                CancelActiveFlash();
                return;
            }

            var durationMilliseconds = _animationDuration.TotalMilliseconds;
            var progress = durationMilliseconds <= 0.0
                ? 1.0
                : _animationStopwatch.Elapsed.TotalMilliseconds / durationMilliseconds;

            if (progress <= 0.0)
            {
                ApplyColorToActiveBrushes(BuildOverlayColor(_baseVisibleSelectionColor, _flashStartVisibleColor, 0.0));
                return;
            }

            if (progress >= 1.0)
            {
                ApplyColorToActiveBrushes(Color.FromArgb(
                    0,
                    _baseVisibleSelectionColor.R,
                    _baseVisibleSelectionColor.G,
                    _baseVisibleSelectionColor.B));

                // Leave the transparent adornment alive for one more render pass so the
                // fully-restored selection state is actually presented before we remove it.
                _removeOnNextRenderingPass = true;
                return;
            }

            var desiredVisibleColor = InterpolatePerceptually(
                _flashStartVisibleColor,
                _baseVisibleSelectionColor,
                progress);

            var overlayColor = BuildOverlayColor(_baseVisibleSelectionColor, desiredVisibleColor, progress);
            ApplyColorToActiveBrushes(overlayColor);
        }

        private void ApplyColorToActiveBrushes(Color color)
        {
            foreach (var brush in _activeBrushes)
            {
                brush.Color = color;
            }
        }

        private void CancelActiveFlash()
        {
            if (_isAnimating)
            {
                CompositionTarget.Rendering -= OnRendering;
                _isAnimating = false;
            }

            _animationStopwatch.Reset();
            _removeOnNextRenderingPass = false;
            _activeBrushes.Clear();
            _layer.RemoveAdornmentsByTag(_tag);
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            CancelActiveFlash();
            _view.Closed -= OnViewClosed;
        }

        private Color GetVisibleSelectionColor()
        {
            var selectionColor = GetSelectionColor();
            var backgroundColor = GetViewBackgroundColor();
            var visibleColor = Composite(selectionColor, backgroundColor);

            return Color.FromArgb(byte.MaxValue, visibleColor.R, visibleColor.G, visibleColor.B);
        }

        private Color GetSelectionColor()
        {
            var formatMap = _formatMapService.GetEditorFormatMap(_view);
            var key = _view.Selection.IsActive ? "Selected Text" : "Inactive Selected Text";
            var properties = formatMap.GetProperties(key);

            if (TryGetColorFromProperties(properties, EditorFormatDefinition.BackgroundBrushId, out var brushColor))
            {
                return brushColor;
            }

            if (TryGetColorFromProperties(properties, EditorFormatDefinition.BackgroundColorId, out var directColor))
            {
                return directColor;
            }

            return _view.Selection.IsActive
                ? SystemColors.HighlightColor
                : SystemColors.InactiveSelectionHighlightBrush.Color;
        }

        private Color GetViewBackgroundColor()
        {
            if (_view.Background is SolidColorBrush viewBackgroundBrush)
            {
                return Color.FromArgb(
                    byte.MaxValue,
                    viewBackgroundBrush.Color.R,
                    viewBackgroundBrush.Color.G,
                    viewBackgroundBrush.Color.B);
            }

            try
            {
                var formatMap = _formatMapService.GetEditorFormatMap(_view);
                var properties = formatMap.GetProperties("Plain Text");

                if (TryGetColorFromProperties(properties, EditorFormatDefinition.BackgroundBrushId, out var brushColor))
                {
                    return Color.FromArgb(byte.MaxValue, brushColor.R, brushColor.G, brushColor.B);
                }

                if (TryGetColorFromProperties(properties, EditorFormatDefinition.BackgroundColorId, out var directColor))
                {
                    return Color.FromArgb(byte.MaxValue, directColor.R, directColor.G, directColor.B);
                }
            }
            catch
            {
                // Best effort only. Fall through to a framework default below.
            }

            return SystemColors.WindowColor;
        }

        private static bool TryGetColorFromProperties(ResourceDictionary properties, object key, out Color color)
        {
            if (properties != null && properties.Contains(key))
            {
                if (properties[key] is SolidColorBrush brush)
                {
                    color = brush.Color;
                    return true;
                }

                if (properties[key] is Color directColor)
                {
                    color = directColor;
                    return true;
                }
            }

            color = default(Color);
            return false;
        }

        private static Color GetFlashStartColor(Color endColor, double brightenAmount)
        {
            brightenAmount = Clamp01(brightenAmount);
            var flashColor = Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
            return InterpolatePerceptually(endColor, flashColor, brightenAmount);
        }

        private static Color BuildOverlayColor(Color baseVisibleColor, Color desiredVisibleColor, double progress)
        {
            progress = Clamp01(progress);
            if (progress >= 1.0)
            {
                return Color.FromArgb(0, desiredVisibleColor.R, desiredVisibleColor.G, desiredVisibleColor.B);
            }

            var overlayAlpha = 1.0 - progress;
            overlayAlpha = Math.Max(overlayAlpha, GetMinimumOverlayAlpha(baseVisibleColor.R, desiredVisibleColor.R));
            overlayAlpha = Math.Max(overlayAlpha, GetMinimumOverlayAlpha(baseVisibleColor.G, desiredVisibleColor.G));
            overlayAlpha = Math.Max(overlayAlpha, GetMinimumOverlayAlpha(baseVisibleColor.B, desiredVisibleColor.B));
            overlayAlpha = Clamp01(overlayAlpha);

            if (overlayAlpha <= 0.0)
            {
                return Color.FromArgb(0, desiredVisibleColor.R, desiredVisibleColor.G, desiredVisibleColor.B);
            }

            byte SolveOverlayComponent(byte baseComponent, byte desiredVisibleComponent)
            {
                var baseValue = baseComponent / 255.0;
                var desiredValue = desiredVisibleComponent / 255.0;
                var overlayValue = (desiredValue - ((1.0 - overlayAlpha) * baseValue)) / overlayAlpha;
                return ToByte(overlayValue);
            }

            return Color.FromArgb(
                ToByte(overlayAlpha),
                SolveOverlayComponent(baseVisibleColor.R, desiredVisibleColor.R),
                SolveOverlayComponent(baseVisibleColor.G, desiredVisibleColor.G),
                SolveOverlayComponent(baseVisibleColor.B, desiredVisibleColor.B));
        }

        private static double GetMinimumOverlayAlpha(byte baseComponent, byte desiredVisibleComponent)
        {
            var baseValue = baseComponent / 255.0;
            var desiredValue = desiredVisibleComponent / 255.0;
            var minimum = 0.0;

            if (desiredValue < baseValue && baseValue > 0.0)
            {
                minimum = Math.Max(minimum, 1.0 - (desiredValue / baseValue));
            }

            if (desiredValue > baseValue && baseValue < 1.0)
            {
                minimum = Math.Max(minimum, (desiredValue - baseValue) / (1.0 - baseValue));
            }

            return Clamp01(minimum);
        }

        private static Color Composite(Color foreground, Color background)
        {
            var foregroundAlpha = foreground.A / 255.0;
            var backgroundAlpha = background.A / 255.0;
            var outputAlpha = foregroundAlpha + (backgroundAlpha * (1.0 - foregroundAlpha));

            if (outputAlpha <= 0.0)
            {
                return Colors.Transparent;
            }

            byte Blend(byte foregroundComponent, byte backgroundComponent)
            {
                var foregroundValue = foregroundComponent / 255.0;
                var backgroundValue = backgroundComponent / 255.0;
                var outputValue =
                    ((foregroundValue * foregroundAlpha) + (backgroundValue * backgroundAlpha * (1.0 - foregroundAlpha))) /
                    outputAlpha;

                return ToByte(outputValue);
            }

            return Color.FromArgb(
                ToByte(outputAlpha),
                Blend(foreground.R, background.R),
                Blend(foreground.G, background.G),
                Blend(foreground.B, background.B));
        }

        private static Color InterpolatePerceptually(Color from, Color to, double progress)
        {
            progress = Clamp01(progress);

            var fromLab = OkLab.FromColor(from);
            var toLab = OkLab.FromColor(to);

            var resultLab = new OkLab(
                Lerp(fromLab.L, toLab.L, progress),
                Lerp(fromLab.A, toLab.A, progress),
                Lerp(fromLab.B, toLab.B, progress));

            var resultColor = resultLab.ToColor();
            var alpha = ToByte(Lerp(from.A / 255.0, to.A / 255.0, progress));
            return Color.FromArgb(alpha, resultColor.R, resultColor.G, resultColor.B);
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0)
            {
                return 0.0;
            }

            if (value > 1.0)
            {
                return 1.0;
            }

            return value;
        }

        private static double Lerp(double start, double end, double progress)
        {
            return start + ((end - start) * progress);
        }

        private static byte ToByte(double value)
        {
            return (byte)Math.Round(255.0 * Clamp01(value));
        }

        private readonly struct OkLab
        {
            public OkLab(double l, double a, double b)
            {
                L = l;
                A = a;
                B = b;
            }

            public double L { get; }
            public double A { get; }
            public double B { get; }

            public static OkLab FromColor(Color color)
            {
                var r = SrgbToLinear(color.R / 255.0);
                var g = SrgbToLinear(color.G / 255.0);
                var b = SrgbToLinear(color.B / 255.0);

                var l = (0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b);
                var m = (0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b);
                var s = (0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b);

                var lRoot = CubeRoot(l);
                var mRoot = CubeRoot(m);
                var sRoot = CubeRoot(s);

                return new OkLab(
                    (0.2104542553 * lRoot) + (0.7936177850 * mRoot) - (0.0040720468 * sRoot),
                    (1.9779984951 * lRoot) - (2.4285922050 * mRoot) + (0.4505937099 * sRoot),
                    (0.0259040371 * lRoot) + (0.7827717662 * mRoot) - (0.8086757660 * sRoot));
            }

            public Color ToColor()
            {
                var lRoot = L + (0.3963377774 * A) + (0.2158037573 * B);
                var mRoot = L - (0.1055613458 * A) - (0.0638541728 * B);
                var sRoot = L - (0.0894841775 * A) - (1.2914855480 * B);

                var l = lRoot * lRoot * lRoot;
                var m = mRoot * mRoot * mRoot;
                var s = sRoot * sRoot * sRoot;

                var r = (4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s);
                var g = (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s);
                var b = (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s);

                return Color.FromArgb(
                    byte.MaxValue,
                    LinearToSrgbByte(r),
                    LinearToSrgbByte(g),
                    LinearToSrgbByte(b));
            }

            private static double SrgbToLinear(double value)
            {
                return value <= 0.04045
                    ? value / 12.92
                    : Math.Pow((value + 0.055) / 1.055, 2.4);
            }

            private static byte LinearToSrgbByte(double value)
            {
                value = value <= 0.0
                    ? 0.0
                    : value >= 1.0
                        ? 1.0
                        : value;

                var srgb = value <= 0.0031308
                    ? 12.92 * value
                    : (1.055 * Math.Pow(value, 1.0 / 2.4)) - 0.055;

                return (byte)Math.Round(255.0 * srgb);
            }

            private static double CubeRoot(double value)
            {
                return value < 0.0
                    ? -Math.Pow(-value, 1.0 / 3.0)
                    : Math.Pow(value, 1.0 / 3.0);
            }
        }
    }
}

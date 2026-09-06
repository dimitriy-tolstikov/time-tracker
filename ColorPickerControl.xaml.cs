using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimeTracker
{
    /// <summary>
    /// Простой селектор цвета: HSV-слайдеры, hex-поле и палитра типовых цветов.
    /// Выбранный цвет доступен через двустороннее свойство <see cref="ColorHex"/>.
    /// </summary>
    public partial class ColorPickerControl : UserControl
    {
        private static readonly string[] PaletteColors =
        {
            "#E53935", "#D81B60", "#8E24AA", "#5E35B1", "#3949AB", "#1E88E5",
            "#039BE5", "#00ACC1", "#00897B", "#43A047", "#7CB342", "#C0CA33",
            "#FDD835", "#FFB300", "#FB8C00", "#F4511E", "#6D4C41", "#757575"
        };

        /// <summary>Защита от рекурсии при синхронизации слайдеров и hex-строки.</summary>
        private bool _updating;

        public static readonly DependencyProperty ColorHexProperty =
            DependencyProperty.Register(
                nameof(ColorHex),
                typeof(string),
                typeof(ColorPickerControl),
                new FrameworkPropertyMetadata(
                    "#888888",
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnColorHexChanged));

        /// <summary>Цвет в формате "#RRGGBB".</summary>
        public string ColorHex
        {
            get => (string)GetValue(ColorHexProperty);
            set => SetValue(ColorHexProperty, value);
        }

        public ColorPickerControl()
        {
            InitializeComponent();
            Palette.ItemsSource = PaletteColors;
            UpdateSlidersFromHex();
        }

        private static void OnColorHexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((ColorPickerControl)d).UpdateSlidersFromHex();

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updating)
                return;

            var color = HsvToColor(
                HueSlider.Value,
                SaturationSlider.Value / 100.0,
                ValueSlider.Value / 100.0);
            ColorHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private void PaletteColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string hex)
                ColorHex = hex;
        }

        private void UpdateSlidersFromHex()
        {
            if (_updating)
                return;

            Color color;
            try
            {
                color = (Color)ColorConverter.ConvertFromString(ColorHex);
            }
            catch (FormatException)
            {
                // Пользователь ещё вводит hex — ждём валидное значение.
                return;
            }

            var (h, s, v) = RgbToHsv(color);
            _updating = true;
            HueSlider.Value = h;
            SaturationSlider.Value = s * 100.0;
            ValueSlider.Value = v * 100.0;
            _updating = false;
        }

        private static (double H, double S, double V) RgbToHsv(Color color)
        {
            double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double h = 0;
            if (delta > 0)
            {
                if (max == r) h = 60.0 * (((g - b) / delta) % 6.0);
                else if (max == g) h = 60.0 * ((b - r) / delta + 2.0);
                else h = 60.0 * ((r - g) / delta + 4.0);
            }
            if (h < 0)
                h += 360.0;

            double s = max > 0 ? delta / max : 0;
            return (h, s, max);
        }

        private static Color HsvToColor(double h, double s, double v)
        {
            double c = v * s;
            double x = c * (1.0 - Math.Abs(h / 60.0 % 2.0 - 1.0));
            double m = v - c;

            double r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromRgb(
                (byte)Math.Round((r + m) * 255.0),
                (byte)Math.Round((g + m) * 255.0),
                (byte)Math.Round((b + m) * 255.0));
        }
    }
}

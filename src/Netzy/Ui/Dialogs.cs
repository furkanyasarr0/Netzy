using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Netzy.Core;

namespace Netzy.Ui
{
    /// <summary>Koyu temalı, logolu iletişim pencereleri. Yükseklik içeriğe göre ayarlanır; uzun metin kesilmez.</summary>
    internal static class Dialogs
    {
        static ImageSource _logo;

        /// <summary>
        /// Logo ve pencere simgesi. logo.ico'daki PNG sıkıştırmalı kareleri WPF çözemez; bu yüzden PNG kullanılır
        /// (logo.ico yalnızca EXE simgesidir). 1024 px'lik görsel belleğe küçültülerek yüklenir.
        /// </summary>
        public static ImageSource Logo
        {
            get
            {
                if (_logo != null) return _logo;
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri("pack://application:,,,/Assets/logo.png");
                img.DecodePixelWidth = 128;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return _logo = img;
            }
        }

        public static Brush Res(string key) => (Brush)Application.Current.FindResource(key);

        public static void DarkTitleBar(Window w)
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            int on = 1;
            // 20: Windows 10 20H1+ / 11, 19: eski Windows 10 sürümleri
            if (Native.DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)) != 0)
                Native.DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int));
        }

        public static bool Confirm(Window owner, string title, string message) => Show(owner, title, message, confirm: true);

        public static void Message(Window owner, string title, string message) => Show(owner, title, message, confirm: false);

        static bool Show(Window owner, string title, string message, bool confirm)
        {
            var w = NewWindow(owner, title, 480);
            w.SizeToContent = SizeToContent.Height;
            w.ResizeMode = ResizeMode.NoResize;
            bool result = false;

            var stack = new StackPanel();
            stack.Children.Add(Header(title, 30));
            stack.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Res("SoftTextBrush"),
                FontSize = 12.5,
                LineHeight = 19,
                Margin = new Thickness(0, 12, 0, 18),
            });
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            if (confirm) row.Children.Add(Button(Loc.T("btn.no"), "DialogSecondaryButton", w.Close));
            var yes = Button(Loc.T(confirm ? "btn.yes" : "btn.ok"), "DialogPrimaryButton", () => { result = true; w.Close(); });
            yes.IsDefault = true;
            row.Children.Add(yes);
            stack.Children.Add(row);

            w.Content = new Border { Style = (Style)Application.Current.FindResource("Card"), Margin = new Thickness(14), Padding = new Thickness(20, 18, 20, 18), Child = stack };
            w.Loaded += (s, e) => yes.Focus();
            w.ShowDialog();
            return result;
        }

        /// <summary>Gizlilik penceresi. İlk açılışta "Kabul ediyorum" dışında kapatılırsa false döner.</summary>
        public static bool Privacy(Window owner, bool firstRun)
        {
            bool accepted = !firstRun;
            var text = new TextBlock { Text = Loc.T("privacy.body"), TextWrapping = TextWrapping.Wrap, Foreground = Res("SoftTextBrush"), FontSize = 12.5, LineHeight = 20 };
            var buttons = new List<Button>();
            Window w = null;
            if (firstRun) buttons.Add(Button(Loc.T("btn.accept"), "DialogPrimaryButton", () => { accepted = true; w.Close(); }));
            w = InfoWindow(owner, Loc.T(firstRun ? "privacy.welcome" : "privacy.title"), Scroll(text), buttons, closeLabel: firstRun ? null : Loc.T("btn.close"));
            w.ShowDialog();
            return accepted;
        }

        public static void Report(Window owner)
        {
            var box = new TextBox
            {
                Text = RunLog.LatestText() ?? Loc.T("report.empty"),
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11.5,
                Background = Brushes.Transparent,
                Foreground = Res("SoftTextBrush"),
                BorderThickness = new Thickness(0),
            };
            Button copy = null;
            copy = Button(Loc.T("btn.copy"), "DialogSecondaryButton", () =>
            {
                for (int i = 0; i < 5; i++)
                {
                    try { Clipboard.SetText(box.Text); copy.Content = Loc.T("btn.copied"); return; }
                    catch (COMException) { System.Threading.Thread.Sleep(50); }   // pano başka bir programda meşgul
                }
            });
            var folder = Button(Loc.T("btn.openfolder"), "DialogSecondaryButton", () => Elevation.OpenFolder(Paths.Logs));
            InfoWindow(owner, Loc.T("report.title"), box, new List<Button> { copy, folder }, Loc.T("btn.close")).ShowDialog();
        }

        public static void Operations(Window owner)
        {
            var stack = new StackPanel();
            AddSteps(stack, Loc.T("ops.quick"), Repairs.Steps(deep: false));
            AddSteps(stack, Loc.T("ops.deep"), Repairs.Steps(deep: true));
            InfoWindow(owner, Loc.T("ops.title"), Scroll(stack), new List<Button>(), Loc.T("btn.close")).ShowDialog();
        }

        static void AddSteps(StackPanel stack, string title, List<RepairStep> steps)
        {
            stack.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Res("AccentBrush"), Margin = new Thickness(0, stack.Children.Count == 0 ? 0 : 14, 0, 6) });
            foreach (var s in steps)
            {
                var item = new StackPanel();
                item.Children.Add(new TextBlock { Text = Loc.T("step." + s.Key), FontWeight = FontWeights.SemiBold, FontSize = 12, Foreground = Res("TextBrush") });
                item.Children.Add(new TextBlock { Text = Loc.T("step." + s.Key + ".desc"), TextWrapping = TextWrapping.Wrap, FontSize = 11.5, Foreground = Res("MutedBrush"), Margin = new Thickness(0, 2, 0, 0) });
                stack.Children.Add(new Border { Background = Res("CardBrush"), BorderBrush = Res("LineBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 3, 0, 3), Child = item });
            }
        }

        static Window InfoWindow(Window owner, string title, UIElement body, List<Button> buttons, string closeLabel)
        {
            var w = NewWindow(owner, title, 600);
            w.Height = 560;
            w.MinWidth = 460;
            w.MinHeight = 380;

            var grid = new Grid { Margin = new Thickness(22, 18, 22, 18) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.Children.Add(Header(title, 34));
            var inset = new Border
            {
                Background = Res("InsetBrush"),
                BorderBrush = Res("LineBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 8, 12),
                Margin = new Thickness(0, 14, 0, 14),
                Child = body,
            };
            Grid.SetRow(inset, 1);
            grid.Children.Add(inset);

            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            foreach (var b in buttons) row.Children.Add(b);
            if (closeLabel != null) row.Children.Add(Button(closeLabel, buttons.Count == 0 ? "DialogPrimaryButton" : "DialogSecondaryButton", w.Close));
            Grid.SetRow(row, 2);
            grid.Children.Add(row);

            w.Content = grid;
            return w;
        }

        static Window NewWindow(Window owner, string title, double width)
        {
            var w = new Window
            {
                Title = "Netzy — " + title,
                Width = width,
                Background = Res("BgBrush"),
                Foreground = Res("TextBrush"),
                FontFamily = new FontFamily("Segoe UI"),
                Icon = Logo,
                UseLayoutRounding = true,
                ShowInTaskbar = owner == null,
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            };
            if (owner != null && owner.IsVisible) w.Owner = owner;
            TextOptions.SetTextFormattingMode(w, TextFormattingMode.Display);
            w.SourceInitialized += (s, e) => DarkTitleBar(w);
            w.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) w.Close(); };
            return w;
        }

        static UIElement Header(string title, double logoSize)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new Image { Source = Logo, Width = logoSize, Height = logoSize, Margin = new Thickness(0, 0, 10, 0) });
            RenderOptions.SetBitmapScalingMode(row.Children[0], BitmapScalingMode.HighQuality);
            row.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Foreground = Res("TextBrush") });
            return row;
        }

        static ScrollViewer Scroll(UIElement content) =>
            new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 8, 0) };

        static Button Button(string text, string style, Action onClick)
        {
            var b = new Button { Content = text, Style = (Style)Application.Current.FindResource(style) };
            b.Click += (s, e) => onClick();
            return b;
        }
    }
}

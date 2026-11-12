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

        /// <summary>İşlem geçmişi: solda tüm kayıtlar (en yeni üstte), sağda seçilen kaydın adımları, hataları ve ayrıntılı kaydı.</summary>
        public static void ShowHistory(Window owner)
        {
            List<HistoryEntry> entries;
            string error = null;
            try { entries = Core.History.List(); }
            catch (Exception e) when (IsHistoryError(e))
            {
                entries = new List<HistoryEntry>();
                error = Loc.T("history.unavailable", e.Message);
            }

            var list = new ListBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                ItemContainerStyle = (Style)Application.Current.FindResource("HistoryItem"),
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
            foreach (var e in entries) list.Items.Add(HistoryRow(e));

            var detail = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            var logBox = new TextBox
            {
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = Res("CardBrush"),
                Foreground = Res("SoftTextBrush"),
                BorderBrush = Res("LineBrush"),
                Padding = new Thickness(8, 6, 8, 6),
                Visibility = Visibility.Collapsed,
            };
            var right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.Children.Add(new ScrollViewer { Content = detail, MaxHeight = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Grid.SetRow(logBox, 1);
            right.Children.Add(logBox);

            HistoryEntry current = null;
            void ShowEmpty(string text)
            {
                current = null;
                detail.Children.Clear();
                detail.Children.Add(new TextBlock { Text = text, Foreground = Res("MutedBrush"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
                logBox.Visibility = Visibility.Collapsed;
            }
            void ShowEntry(HistoryEntry e)
            {
                try { current = Core.History.Get(e.Id) ?? e; }
                catch (Exception ex) when (IsHistoryError(ex)) { current = e; }
                RenderHistoryDetail(detail, current);
                logBox.Text = current.Log;
                logBox.Visibility = string.IsNullOrEmpty(current.Log) ? Visibility.Collapsed : Visibility.Visible;
            }
            list.SelectionChanged += (s, a) =>
            {
                if (list.SelectedItem is FrameworkElement row && row.Tag is HistoryEntry e) ShowEntry(e);
            };

            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            body.ColumnDefinitions.Add(new ColumnDefinition());
            body.Children.Add(list);
            Grid.SetColumn(right, 2);
            body.Children.Add(right);

            Window w = null;
            Button copy = null;
            copy = Button(Loc.T("btn.copy"), "DialogSecondaryButton", () =>
            {
                if (current == null) return;
                for (int i = 0; i < 5; i++)
                {
                    try { Clipboard.SetText(HistoryText(current)); copy.Content = Loc.T("btn.copied"); return; }
                    catch (COMException) { System.Threading.Thread.Sleep(50); }   // pano başka bir programda meşgul
                }
            });
            var clear = Button(Loc.T("history.clear"), "DialogSecondaryButton", () =>
            {
                if (list.Items.Count == 0 || !Confirm(w, Loc.T("history.clear"), Loc.T("history.clear.body"))) return;
                try
                {
                    Core.History.Clear();
                    list.Items.Clear();
                    ShowEmpty(Loc.T("history.empty"));
                }
                catch (Exception ex) when (IsHistoryError(ex)) { ShowEmpty(Loc.T("history.unavailable", ex.Message)); }
            });

            w = InfoWindow(owner, Loc.T("history.title"), body, new List<Button> { copy, clear }, Loc.T("btn.close"));
            w.Width = 920;
            w.Height = 620;
            w.MinWidth = 760;
            if (list.Items.Count > 0) list.SelectedIndex = 0;
            else ShowEmpty(error ?? Loc.T("history.empty"));
            w.ShowDialog();
        }

        static bool IsHistoryError(Exception e) =>
            e is SqliteException || e is DllNotFoundException || e is EntryPointNotFoundException || e is System.IO.IOException || e is UnauthorizedAccessException;

        static UIElement HistoryRow(HistoryEntry e)
        {
            var top = new DockPanel();
            var dot = new TextBlock { Text = "●", FontSize = 10, Foreground = Res(e.Ok ? "GreenBrush" : "RedBrush"), Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(dot, Dock.Left);
            top.Children.Add(dot);
            var count = new TextBlock { Text = e.StepTotal > 0 ? $"{e.StepOk}/{e.StepTotal}" : "", FontSize = 11, Foreground = Res("MutedBrush"), VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(count, Dock.Right);
            top.Children.Add(count);
            top.Children.Add(new TextBlock { Text = Loc.T("run." + e.Kind), FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Res("TextBrush") });

            var row = new StackPanel { Tag = e };
            row.Children.Add(top);
            row.Children.Add(new TextBlock
            {
                Text = e.Time.ToString("g", Loc.Culture) + (e.Summary != null ? " · " + e.Summary : ""),
                FontSize = 11,
                Foreground = Res("MutedBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(17, 2, 0, 0),
            });
            return row;
        }

        static void RenderHistoryDetail(StackPanel detail, HistoryEntry e)
        {
            detail.Children.Clear();
            detail.Children.Add(new TextBlock { Text = Loc.T("run." + e.Kind), FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Res("TextBrush") });
            detail.Children.Add(new TextBlock
            {
                Text = e.Time.ToString("F", Loc.Culture) + "  ·  " + (e.Ok ? "✓ " + Loc.T("status.ok") : "✗ " + Loc.T("status.error")),
                FontSize = 11.5,
                Foreground = Res(e.Ok ? "GreenBrush" : "RedBrush"),
                Margin = new Thickness(0, 2, 0, 6),
            });
            if (e.Summary != null)
                detail.Children.Add(new TextBlock { Text = e.Summary, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Res(e.Ok ? "SoftTextBrush" : "AmberBrush"), Margin = new Thickness(0, 0, 0, 6) });
            if (e.Kind == "quick" || e.Kind == "deep")
                detail.Children.Add(new TextBlock { Text = Format.Metrics(e.Before, e.After), FontSize = 12, Foreground = Res("AccentBrush"), Margin = new Thickness(0, 0, 0, 6) });
            if (e.Steps.Count == 0) return;
            detail.Children.Add(new TextBlock { Text = Loc.T("history.steps"), FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Res("MutedBrush"), Margin = new Thickness(0, 4, 0, 3) });
            foreach (var s in e.Steps)
                detail.Children.Add(new TextBlock
                {
                    Text = (s.Ok ? "✓  " : "✗  ") + Loc.T(s.Key) + (s.Detail != null ? " — " + s.Detail : ""),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Foreground = Res(s.Ok ? "SoftTextBrush" : "RedBrush"),
                    Margin = new Thickness(0, 1, 0, 1),
                });
        }

        static string HistoryText(HistoryEntry e)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{Loc.T("run." + e.Kind)} — {e.Time.ToString("F", Loc.Culture)} — {(e.Ok ? "OK" : Loc.T("status.error"))}");
            if (e.Summary != null) sb.AppendLine(e.Summary);
            if (e.Kind == "quick" || e.Kind == "deep") sb.AppendLine(Format.Metrics(e.Before, e.After));
            foreach (var s in e.Steps) sb.AppendLine($"{(s.Ok ? "OK " : "ERR")} {Loc.T(s.Key)}{(s.Detail != null ? " — " + s.Detail : "")}");
            sb.AppendLine().Append(e.Log);
            return sb.ToString();
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

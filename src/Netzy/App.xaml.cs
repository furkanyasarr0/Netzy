using System;
using System.Net;
using System.Windows;
using System.Windows.Threading;
using Netzy.Core;
using Netzy.Ui;

namespace Netzy
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            Config.Load();

            var cli = Cli.Parse(e.Args);
            Loc.Set(cli.Lang ?? Config.Lang ?? Loc.SystemDefault());
            if (cli.Kind != CliKind.Gui)
            {
                Shutdown(Cli.Run(cli));
                return;
            }

            DispatcherUnhandledException += OnUnhandled;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (!Config.TermsAccepted)
            {
                if (!Dialogs.Privacy(null, firstRun: true))
                {
                    Shutdown();
                    return;
                }
                Config.TermsAccepted = true;
                Config.Save();
            }

            var main = new MainWindow();
            MainWindow = main;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            main.Show();
        }

        void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var log = new RunLog("Unhandled error");
            log.Line(e.Exception.ToString());
            log.Save();
            e.Handled = true;
            try { Dialogs.Message(MainWindow, Loc.T("error.title"), e.Exception.Message); }
            catch (Exception) { MessageBox.Show(e.Exception.Message, "Netzy", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }
}

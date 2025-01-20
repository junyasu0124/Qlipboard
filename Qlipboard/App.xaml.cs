using Microsoft.UI.Xaml;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Qlipboard;

public partial class App : Microsoft.UI.Xaml.Application
{
  private Window? window;
  private readonly ContextMenuStrip menu = new();
  private readonly NotifyIcon notifyIcon;

  public App()
  {
    InitializeComponent();

    menu.Items.Add("Exit", null, null);
    menu.Items[0].Click += (sender, e) => window?.Close();
    notifyIcon = new NotifyIcon
    {
      Icon = new Icon(AppDomain.CurrentDomain.BaseDirectory + "NotifyIcon.ico"),
      ContextMenuStrip = menu,
      Visible = true
    };
  }

  protected override void OnLaunched(LaunchActivatedEventArgs args)
  {
    window = new MainWindow();
    window.Activate();
  }
}

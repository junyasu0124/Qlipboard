using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Windows.ApplicationModel.DataTransfer;

namespace Qlipboard;

public partial class App : Microsoft.UI.Xaml.Application
{
  private Window? window;
  private readonly ContextMenuStrip menu = new();
  private readonly NotifyIcon notifyIcon;

  private bool isActivated = true;

  public App()
  {
    InitializeComponent();

    menu.Items.Add("Inactivate", null, (sender, e) =>
    {
      isActivated = !isActivated;
      if (isActivated)
      {
        Windows.Storage.ApplicationData.Current.LocalSettings.Values["Inactivated"] = false;
        Hook.HookStart();
        menu.Items[0].Text = "Inactivate";
        notifyIcon!.Icon = new Icon(AppDomain.CurrentDomain.BaseDirectory + "NotifyIcon.ico");
      }
      else
      {
        Windows.Storage.ApplicationData.Current.LocalSettings.Values["Inactivated"] = true;
        Hook.HookEnd();
        menu.Items[0].Text = "Activate";
        notifyIcon!.Icon = new Icon(AppDomain.CurrentDomain.BaseDirectory + "NotifyIconBlack.ico");
      }
    });
    if (Windows.Storage.ApplicationData.Current.LocalSettings.Values["Inactivated"] is bool inactivated && inactivated)
    {
      isActivated = false;
      menu.Items[0].Text = "Activate";
    }
    else
    {
      Hook.HookStart();
    }

    menu.Items.Add("Settings", null, (sender, e) =>
    {
      window ??= new MainWindow();
      window.Activate();
    });
    menu.Items.Add("Exit", null, (sender, e) => Current.Exit());
    notifyIcon = new NotifyIcon
    {
      Icon = new Icon(AppDomain.CurrentDomain.BaseDirectory + (isActivated ? "NotifyIcon.ico" : "NotifyIconBlack.ico")),
      ContextMenuStrip = menu,
      Visible = true
    };
  }

  protected override void OnLaunched(LaunchActivatedEventArgs args)
  {
    //window = new MainWindow();
    //window.Activate();
  }
}

static class Hook
{
  delegate int delegateHookCallback(int nCode, IntPtr wParam, IntPtr lParam);
  [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  static extern IntPtr SetWindowsHookEx(int idHook, delegateHookCallback lpfn, IntPtr hMod, uint dwThreadId);

  [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  static extern bool UnhookWindowsHookEx(IntPtr hhk);

  [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  static extern IntPtr GetModuleHandle(string lpModuleName);

  private static IntPtr hookPtr = IntPtr.Zero;

  private static bool isCtrlPressed = false;
  private static bool isShiftPressed = false;
  private static bool hasShifted = false;

  public static void HookStart()
  {
    using var curProcess = Process.GetCurrentProcess();
    if (curProcess.MainModule == null)
      return;
    using var curModule = curProcess.MainModule;
    // フックを行う
    // 第1引数	フックするイベントの種類
    //   13はキーボードフックを表す
    // 第2引数 フック時のメソッドのアドレス
    //   フックメソッドを登録する
    // 第3引数	インスタンスハンドル
    //   現在実行中のハンドルを渡す
    // 第4引数	スレッドID
    //   0を指定すると、すべてのスレッドでフックされる
    hookPtr = SetWindowsHookEx(
        13,
        HookCallback,
        GetModuleHandle(curModule.ModuleName),
        0
    );
  }

  private static int HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
  {
    // フックしたキー
    var key = (Keys)(short)Marshal.ReadInt32(lParam);

    if (key == Keys.LControlKey)
    {
      isCtrlPressed = checked((int)wParam) switch
      {
        256 => true,
        257 => false,
        _ => isCtrlPressed
      };
    }
    else if (key == Keys.LShiftKey)
    {
      isShiftPressed = checked((int)wParam) switch
      {
        256 => true,
        257 => false,
        _ => isShiftPressed
      };
    }
    else if (isCtrlPressed && isShiftPressed && key == Keys.V && checked((int)wParam) == 256)
    {
      DispatcherQueue.GetForCurrentThread().TryEnqueue(async () =>
      {
        if (Windows.ApplicationModel.DataTransfer.Clipboard.IsHistoryEnabled() == false)
          return;

        var historyItems = await Windows.ApplicationModel.DataTransfer.Clipboard.GetHistoryItemsAsync();
        if (historyItems.Status != ClipboardHistoryItemsResultStatus.Success)
          return;

        var historyList = historyItems.Items;
        if (historyList.Count <= 0)
          return;
        if (hasShifted == false)
        {
          hasShifted = true;
          Windows.ApplicationModel.DataTransfer.Clipboard.SetHistoryItemAsContent(historyList[1]);
        }
        else
        {
          hasShifted = false;
          Windows.ApplicationModel.DataTransfer.Clipboard.SetHistoryItemAsContent(historyList[0]);
        }
      });
      return 1;
    }

    return 0;
  }

  public static void HookEnd()
  {
    UnhookWindowsHookEx(hookPtr);
    hookPtr = IntPtr.Zero;
  }
}

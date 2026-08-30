using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TerrariaRPC.Forms;

public partial class ConfirmationDialog : Window
{
  public ConfirmationDialog()
  {
    InitializeComponent();
    InitializeDialog("Confirm", "");
  }

  public ConfirmationDialog(string title, string message)
  {
    InitializeComponent();
    InitializeDialog(title, message);
  }

  private void InitializeDialog(string title, string message)
  {
    Title = title;
    this.FindControl<TextBlock>("HeaderText")!.Text = title;
    this.FindControl<TextBlock>("BodyText")!.Text = message;
  }

  protected override void OnOpened(EventArgs e)
  {
    base.OnOpened(e);
    if (OperatingSystem.IsWindows())
    {
      try
      {
        var handle = TryGetPlatformHandle()?.Handle;
        if (handle.HasValue && handle.Value != IntPtr.Zero)
        {
          const int GWL_STYLE = -16;
          const int WS_MINIMIZEBOX = 0x00020000;
          const int WS_MAXIMIZEBOX = 0x00010000;
          int style = GetWindowLong(handle.Value, GWL_STYLE);
          style &= ~(WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
          SetWindowLong(handle.Value, GWL_STYLE, style);
        }
      }
      catch
      {
      }
    }
  }

  public void OnConfirmClick(object sender, RoutedEventArgs e) => Close(true);

  public void OnCancelClick(object sender, RoutedEventArgs e) => Close(false);

  [DllImport("user32.dll")]
  private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

  [DllImport("user32.dll")]
  private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}

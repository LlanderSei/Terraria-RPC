using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TerrariaRPC.Forms;

public partial class StatusTemplateDialog : Window
{
  public bool WasSaved { get; private set; }

  public string StatusName
  {
    get => this.FindControl<TextBox>("StatusNameBox")?.Text ?? "";
    private set
    {
      var box = this.FindControl<TextBox>("StatusNameBox");
      box?.Text = value;
    }
  }

  public string ImageTemplate
  {
    get => this.FindControl<TextBox>("ImageTemplateBox")?.Text ?? "";
    private set
    {
      var box = this.FindControl<TextBox>("ImageTemplateBox");
      box?.Text = value;
    }
  }

  public string TextTemplate
  {
    get => this.FindControl<TextBox>("TextTemplateBox")?.Text ?? "";
    private set
    {
      var box = this.FindControl<TextBox>("TextTemplateBox");
      box?.Text = value;
    }
  }

  public StatusTemplateDialog()
  {
    InitializeComponent();
    InitializeDialog("Configure Status", "", "", "");
  }

  public StatusTemplateDialog(string statusName, string imageTemplate, string textTemplate)
  {
    InitializeComponent();
    string normalizedStatusName = string.IsNullOrWhiteSpace(statusName) ? "Status" : statusName.Trim();
    InitializeDialog($"Configure {normalizedStatusName} Status", normalizedStatusName, imageTemplate, textTemplate);
  }

  public StatusTemplateDialog(string title, string statusName, string imageTemplate, string textTemplate)
  {
    InitializeComponent();
    InitializeDialog(title, statusName, imageTemplate, textTemplate);
  }

  private void InitializeDialog(string title, string statusName, string imageTemplate, string textTemplate)
  {
    Title = title;
    this.FindControl<TextBlock>("HeaderText")!.Text = title;
    StatusName = statusName;
    ImageTemplate = imageTemplate;
    TextTemplate = textTemplate;
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

  public void OnSaveClick(object sender, RoutedEventArgs e)
  {
    WasSaved = true;
    Close();
  }

  public void OnCancelClick(object sender, RoutedEventArgs e) => Close();

  [DllImport("user32.dll")]
  private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

  [DllImport("user32.dll")]
  private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}

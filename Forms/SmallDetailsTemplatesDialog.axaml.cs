using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TerrariaRPC.Core;

namespace TerrariaRPC.Forms;

public partial class SmallDetailsTemplatesDialog : Window
{
  public SmallDetailsTemplatesDialog()
  {
    InitializeComponent();
    LoadValues();
  }

  private void LoadValues()
  {
    var config = ConfigManager.CurrentConfig;
    this.FindControl<TextBox>("BossTemplateBox")!.Text = config.BossSmallTextTemplate;
    this.FindControl<TextBox>("ProgressiveEventTemplateBox")!.Text = config.ProgressiveEventSmallTextTemplate;
    this.FindControl<TextBox>("NonProgressiveEventTemplateBox")!.Text = config.NonProgressiveEventSmallTextTemplate;
    this.FindControl<TextBox>("PeacefulEventTemplateBox")!.Text = config.PeacefulEventSmallTextTemplate;
    this.FindControl<TextBox>("WeatherTemplateBox")!.Text = config.WeatherSmallTextTemplate;
  }

  public async void OnVariablesClick(object sender, RoutedEventArgs e)
  {
    var dialog = new VariablesDialog();
    await dialog.ShowDialog(this);
  }

  public void OnSaveClick(object sender, RoutedEventArgs e)
  {
    var config = ConfigManager.CurrentConfig;
    config.BossSmallTextTemplate = this.FindControl<TextBox>("BossTemplateBox")!.Text ?? "";
    config.ProgressiveEventSmallTextTemplate = this.FindControl<TextBox>("ProgressiveEventTemplateBox")!.Text ?? "";
    config.NonProgressiveEventSmallTextTemplate = this.FindControl<TextBox>("NonProgressiveEventTemplateBox")!.Text ?? "";
    config.PeacefulEventSmallTextTemplate = this.FindControl<TextBox>("PeacefulEventTemplateBox")!.Text ?? "";
    config.WeatherSmallTextTemplate = this.FindControl<TextBox>("WeatherTemplateBox")!.Text ?? "";
    ConfigManager.SaveConfig();
    Close();
  }

  public void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}

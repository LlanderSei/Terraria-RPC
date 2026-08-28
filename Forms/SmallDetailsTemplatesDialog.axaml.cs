using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using TerrariaRPC.Core;

namespace TerrariaRPC.Forms;

public partial class SmallDetailsTemplatesDialog : Window
{
  private enum ActivePane
  {
    Priority,
    Templates
  }

  private ActivePane _activePane = ActivePane.Templates;
  private bool _suppressPriorityNormalization;

  public SmallDetailsTemplatesDialog()
  {
    InitializeComponent();
    LoadValues();
    SetActivePane(ActivePane.Templates);
  }

  private void LoadValues()
  {
    var config = ConfigManager.CurrentConfig;
    this.FindControl<TextBox>("BossTemplateBox")!.Text = config.BossSmallTextTemplate;
    this.FindControl<TextBox>("ProgressiveEventTemplateBox")!.Text = config.ProgressiveEventSmallTextTemplate;
    this.FindControl<TextBox>("NonProgressiveEventTemplateBox")!.Text = config.NonProgressiveEventSmallTextTemplate;
    this.FindControl<TextBox>("PeacefulEventTemplateBox")!.Text = config.PeacefulEventSmallTextTemplate;
    this.FindControl<TextBox>("WeatherTemplateBox")!.Text = config.WeatherSmallTextTemplate;

    this.FindControl<CheckBox>("PrioritizeLunarPillarsNearbyBox")!.IsChecked = config.PrioritizeLunarPillarsNearby;
    this.FindControl<CheckBox>("PrioritizeTargetHitBossBox")!.IsChecked = config.PrioritizeTargetHitBoss;
    this.FindControl<CheckBox>("PrioritizeNearestBossBox")!.IsChecked = config.PrioritizeNearestBoss;
    this.FindControl<CheckBox>("PrioritizeHighestHealthBossBox")!.IsChecked = config.PrioritizeHighestHealthBoss;

    NormalizeBossPrioritySelection();
  }

  private void SetActivePane(ActivePane pane)
  {
    _activePane = pane;

    var priorityButton = this.FindControl<ToggleButton>("PriorityPaneButton");
    var templateButton = this.FindControl<ToggleButton>("TemplatePaneButton");
    var priorityPane = this.FindControl<Panel>("PriorityPane");
    var templatePane = this.FindControl<Panel>("TemplatePane");

    if (priorityButton != null) priorityButton.IsChecked = pane == ActivePane.Priority;
    if (templateButton != null) templateButton.IsChecked = pane == ActivePane.Templates;
    if (priorityPane != null) priorityPane.IsVisible = pane == ActivePane.Priority;
    if (templatePane != null) templatePane.IsVisible = pane == ActivePane.Templates;
  }

  private void NormalizeBossPrioritySelection()
  {
    if (_suppressPriorityNormalization)
    {
      return;
    }

    var hitBox = this.FindControl<CheckBox>("PrioritizeTargetHitBossBox");
    var nearestBox = this.FindControl<CheckBox>("PrioritizeNearestBossBox");
    var highestBox = this.FindControl<CheckBox>("PrioritizeHighestHealthBossBox");

    if (hitBox == null || nearestBox == null || highestBox == null)
    {
      return;
    }

    if (hitBox.IsChecked != true && nearestBox.IsChecked != true && highestBox.IsChecked != true)
    {
      _suppressPriorityNormalization = true;
      hitBox.IsChecked = true;
      _suppressPriorityNormalization = false;
    }
  }

  private void EnforceSingleBossPriority(CheckBox selectedBox)
  {
    if (_suppressPriorityNormalization)
    {
      return;
    }

    var hitBox = this.FindControl<CheckBox>("PrioritizeTargetHitBossBox");
    var nearestBox = this.FindControl<CheckBox>("PrioritizeNearestBossBox");
    var highestBox = this.FindControl<CheckBox>("PrioritizeHighestHealthBossBox");

    if (hitBox == null || nearestBox == null || highestBox == null)
    {
      return;
    }

    _suppressPriorityNormalization = true;

    if (selectedBox.IsChecked == true)
    {
      if (selectedBox != hitBox) hitBox.IsChecked = false;
      if (selectedBox != nearestBox) nearestBox.IsChecked = false;
      if (selectedBox != highestBox) highestBox.IsChecked = false;
    }

    _suppressPriorityNormalization = false;
    NormalizeBossPrioritySelection();
  }

  public void OnPriorityPaneClick(object sender, RoutedEventArgs e)
  {
    SetActivePane(ActivePane.Priority);
  }

  public void OnTemplatePaneClick(object sender, RoutedEventArgs e)
  {
    SetActivePane(ActivePane.Templates);
  }

  public void OnBossPriorityChanged(object sender, RoutedEventArgs e)
  {
    if (sender is CheckBox selectedBox)
    {
      EnforceSingleBossPriority(selectedBox);
    }
    else
    {
      NormalizeBossPrioritySelection();
    }
  }

  public async void OnVariablesClick(object sender, RoutedEventArgs e)
  {
    var dialog = new VariablesDialog();
    await dialog.ShowDialog(this);
  }

  public void OnSaveClick(object sender, RoutedEventArgs e)
  {
    NormalizeBossPrioritySelection();

    var config = ConfigManager.CurrentConfig;
    config.BossSmallTextTemplate = this.FindControl<TextBox>("BossTemplateBox")!.Text ?? "";
    config.ProgressiveEventSmallTextTemplate = this.FindControl<TextBox>("ProgressiveEventTemplateBox")!.Text ?? "";
    config.NonProgressiveEventSmallTextTemplate = this.FindControl<TextBox>("NonProgressiveEventTemplateBox")!.Text ?? "";
    config.PeacefulEventSmallTextTemplate = this.FindControl<TextBox>("PeacefulEventTemplateBox")!.Text ?? "";
    config.WeatherSmallTextTemplate = this.FindControl<TextBox>("WeatherTemplateBox")!.Text ?? "";

    config.PrioritizeLunarPillarsNearby = this.FindControl<CheckBox>("PrioritizeLunarPillarsNearbyBox")!.IsChecked ?? false;
    config.PrioritizeTargetHitBoss = this.FindControl<CheckBox>("PrioritizeTargetHitBossBox")!.IsChecked ?? false;
    config.PrioritizeNearestBoss = this.FindControl<CheckBox>("PrioritizeNearestBossBox")!.IsChecked ?? false;
    config.PrioritizeHighestHealthBoss = this.FindControl<CheckBox>("PrioritizeHighestHealthBossBox")!.IsChecked ?? false;

    if (!config.PrioritizeTargetHitBoss && !config.PrioritizeNearestBoss && !config.PrioritizeHighestHealthBoss)
    {
      config.PrioritizeTargetHitBoss = true;
    }

    ConfigManager.SaveConfig();
    Close();
  }

  public void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}

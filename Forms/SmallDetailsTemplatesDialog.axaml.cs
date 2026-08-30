using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TerrariaRPC.Core;

namespace TerrariaRPC.Forms;

public partial class SmallDetailsTemplatesDialog : Window
{
  private bool _suppressPriorityNormalization;

  public SmallDetailsTemplatesDialog()
  {
    InitializeComponent();
    LoadValues();
  }

  private void LoadValues()
  {
    var config = ConfigManager.CurrentConfig;

    var bossPriority = config.InGame.BossAndEventPriority.BossPriority;
    this.FindControl<CheckBox>("PrioritizeLunarPillarsNearbyBox")!.IsChecked = bossPriority.PrioritizeLunarPillarsNearby;
    this.FindControl<CheckBox>("PrioritizeTargetHitBossBox")!.IsChecked = bossPriority.PrioritizeTargetHitBoss;
    this.FindControl<CheckBox>("PrioritizeNearestBossBox")!.IsChecked = bossPriority.PrioritizeNearestBoss;
    this.FindControl<CheckBox>("PrioritizeHighestHealthBossBox")!.IsChecked = bossPriority.PrioritizeHighestHealthBoss;

    NormalizeBossPrioritySelection();
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

  public void OnSaveClick(object sender, RoutedEventArgs e)
  {
    NormalizeBossPrioritySelection();

    var config = ConfigManager.CurrentConfig;
    var bossPriority = config.InGame.BossAndEventPriority.BossPriority;
    bossPriority.PrioritizeLunarPillarsNearby = this.FindControl<CheckBox>("PrioritizeLunarPillarsNearbyBox")!.IsChecked ?? false;
    bossPriority.PrioritizeTargetHitBoss = this.FindControl<CheckBox>("PrioritizeTargetHitBossBox")!.IsChecked ?? false;
    bossPriority.PrioritizeNearestBoss = this.FindControl<CheckBox>("PrioritizeNearestBossBox")!.IsChecked ?? false;
    bossPriority.PrioritizeHighestHealthBoss = this.FindControl<CheckBox>("PrioritizeHighestHealthBossBox")!.IsChecked ?? false;

    if (!bossPriority.PrioritizeTargetHitBoss && !bossPriority.PrioritizeNearestBoss && !bossPriority.PrioritizeHighestHealthBoss)
    {
      bossPriority.PrioritizeTargetHitBoss = true;
    }

    ConfigManager.SaveConfig();
    Close();
  }

  public void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}

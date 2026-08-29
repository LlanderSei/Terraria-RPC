using Avalonia.Interactivity;
using TerrariaRPC.Core;

namespace TerrariaRPC.Forms;

public partial class MainWindow
{
  public async void OnCustomizeHeldItemStatusClick(object sender, RoutedEventArgs e)
  {
    var config = ConfigManager.CurrentConfig;
    var dialog = new StatusTemplateDialog(
      "Configure Holding Item's Icon & Name Status",
      config.HeldItemSmallImageUrlTemplate,
      config.HeldItemSmallTextTemplate);

    await dialog.ShowDialog(this);

    if (!dialog.WasSaved)
    {
      return;
    }

    config.HeldItemSmallImageUrlTemplate = dialog.ImageTemplate;
    config.HeldItemSmallTextTemplate = dialog.TextTemplate;
    ConfigManager.SaveConfig();
  }
}

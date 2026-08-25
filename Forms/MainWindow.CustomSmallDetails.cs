using Avalonia.Interactivity;

namespace TerrariaRPC.Forms;

public partial class MainWindow
{
  public async void OnCustomizeSmallDetailsClick(object sender, RoutedEventArgs e)
  {
    var dialog = new SmallDetailsTemplatesDialog();
    await dialog.ShowDialog(this);
  }
}

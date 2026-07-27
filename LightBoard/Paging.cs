using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LightBoard;

public class PagePreview(int number, ImageSource preview)
{
    public int Number { get; set; } = number;
    public ImageSource Preview { get; set; } = preview;
}

public partial class MainWindow
{
    public ObservableCollection<PagePreview> PagePreviews { get; set; } = 
    [
        new PagePreview(1, new BitmapImage())
    ];
}

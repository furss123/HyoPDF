using System.Windows.Controls;
using HyoPDF.UI.Services;

namespace HyoPDF.UI.Views;

public partial class ToastHost : UserControl
{
    public ToastHost(IToastService toastService)
    {
        InitializeComponent();
        DataContext = toastService;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Threading.Tasks;
using AvaloniaP2.Views;

namespace AvaloniaP2.Views
{
    public partial class CreateServiceDialog : Window
    {
        private TextBox _serviceNameTextBox;

        public string ServiceName { get; private set; }

        public CreateServiceDialog()
        {
            InitializeComponent();

            _serviceNameTextBox = this.FindControl<TextBox>("ServiceNameTextBox");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            ServiceName = _serviceNameTextBox.Text;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

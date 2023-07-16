using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Threading.Tasks;
using AvaloniaP2.Views;

namespace AvaloniaP2.Views
{
    // файл с классом называется не *.axaml.cs, и IDE думает что это разные классы. Странно как-то, но имей ввиду
    public partial class CreateServiceDialog : Window
    {
        private TextBox _serviceNameTextBox;

        public string ServiceName { get; private set; }

        public CreateServiceDialog()
        {
            InitializeComponent();

            // ServiceNameTextBox. и т.д., этот компонент уже в code-generate классе
            _serviceNameTextBox = this.FindControl<TextBox>("ServiceNameTextBox");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // подобную логику лучше во ViewModel и дальше Binding'ами
            ServiceName = _serviceNameTextBox.Text;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

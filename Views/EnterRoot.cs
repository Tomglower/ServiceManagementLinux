using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Threading.Tasks;
using AvaloniaP2.Views;

namespace AvaloniaP2.Views
{
    public partial class EnterRoot : Window
    {
        private TextBox Root_pass;

        public string RootPassword { get; private set; }

        public EnterRoot()
        {
            InitializeComponent();

            // авалония сама генерирует поля для именованных компонентов. Можешь смело обращаться к Rootpass и т.д.
            Root_pass = this.FindControl<TextBox>("Rootpass");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            RootPassword = Root_pass.Text;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AvaloniaP2.Views
{
    public partial class ChangeParamService : Window
    {
        private TextBox _UnitBox;
        private TextBox _ServiceBox;
        private ComboBox _TargetComboBox;

        public string UnitData { get; set; }
        public string ServiceData { get; set; }

        public List<string> Targets { get; set; }
        public string SelectedTarget { get; set; }

        public ChangeParamService()
        {
            InitializeComponent();
            DataContext = this;

            _UnitBox = this.FindControl<TextBox>("UnitBox");
            _ServiceBox = this.FindControl<TextBox>("ServiceBox");
            _TargetComboBox = this.FindControl<ComboBox>("TargetComboBox");

            _UnitBox.Text = "[Unit]\nDescription=";
            _ServiceBox.Text = "[Service]\n";

            Targets = GetList();
            SelectedTarget = Targets.FirstOrDefault();

            foreach (string target in Targets)
            {
                _TargetComboBox.Items.Add(target);
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            UnitData = _UnitBox.Text;
            ServiceData = _ServiceBox.Text;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private List<string> GetList()
        {
            List<string> customParameters = new List<string>();

            Process process = new Process();
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = "-c \"systemctl list-units --type=target | tail -n +2 | head -n -5\"";
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            string[] lines = output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string target = parts[0];
                customParameters.Add(target);
            }

            return customParameters;
        }
    }
}

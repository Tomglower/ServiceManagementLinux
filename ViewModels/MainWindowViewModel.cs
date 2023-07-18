using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ReactiveUI;
using System.Reactive;
using System.IO;
using System.Text;
using MsBox.Avalonia;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using AvaloniaP2.Views;
using System.Windows.Input;
using System.Collections.ObjectModel;
using log4net;
using System.Reactive.Linq;

namespace AvaloniaP2.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public string ServiceName;
        private string _sudopass;
        private ObservableCollection<ServiceViewModel> servicesList;
        private ObservableCollection<ServiceViewModel> filteredServicesList;
        private string searchText;
        public ICommand OpenFileCommand { get; }
        public ICommand SudoUpdate { get; }
        public ICommand CreateServiceCommand { get; }
        public ICommand StartServiceCommand { get; }
        public ICommand StopServiceCommand { get; }
        public ICommand RestartServiceCommand { get; }
        private static readonly ILog log = LogManager.GetLogger(typeof(MainWindowViewModel));
        private ServiceViewModel serviceViewModel;

        public ServiceViewModel ServiceViewModel
        {
            get => serviceViewModel;
            set => this.RaiseAndSetIfChanged(ref serviceViewModel, value);
        }
        public ObservableCollection<ServiceViewModel> ServicesList
        {
            get => servicesList;
            set => this.RaiseAndSetIfChanged(ref servicesList, value);
        }
        public ObservableCollection<ServiceViewModel> FilteredServicesList
        {
            get => filteredServicesList;
            set => this.RaiseAndSetIfChanged(ref filteredServicesList, value);
        }
        public string SearchText
        {
            get => searchText;
            set
            {
                if (searchText != value)
                {
                    searchText = value;
                    Poisk();
                }
            }
        }
        public MainWindowViewModel()
        {
            Initialize();
            OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFile);
            SudoUpdate = ReactiveCommand.CreateFromTask(RootPassword);
            CreateServiceCommand = ReactiveCommand.CreateFromTask(CreateServiceDialog);
        }
        private async Task Initialize()
        {
            ServiceViewModel = new ServiceViewModel();
            ParseServiceList();
            Poisk();
            log4net.Config.XmlConfigurator.Configure(new FileInfo("log4net.config"));
            foreach (var service in servicesList)
            {
                service.StartServiceCommand = ReactiveCommand.CreateFromTask<string>(StartService);
                service.StopServiceCommand = ReactiveCommand.CreateFromTask<string>(StopService);
                service.RestartServiceCommand = ReactiveCommand.CreateFromTask<string>(RestartService);
            }
        }
        private async Task RootPassword()
        {
            // ты пишешь View логику внутри ViewModel слоя, что нарушает паттерн MVVM и несет за собой ряд неудобств.
            // ViewModel созданы для бизнес логики и всё взаимодействие с View происходит в основном через абстракцию.
            // По хорошему все ViewModel'и у тебя должны быть написаны так, чтобы они могли быть библиотекой, не имеющей 
            // никакого отношения к фреймворку для отрисовки (например, Avalonia)
            var dialog = new EnterRoot();
            // вот, соответсвенно, и первая проблема, с которой ты сам и столкнулся. Ты никак не можешь получить окно
            // внутри ViewModel слоя. И тебе приходится плясать со статикой. И получается так, что ты создаешь вьюмодель,
            // пихаешь её внутрь окна, окно инициализируется, и только потом, по нажатию на кнопку, ты ищешь это же окно
            // где-то в недрах статики Avalonia. Если бы этот метод был привязан не к кнопке, а, например, вызывался до
            // того, как ты открыл MainWindow окно, то ты бы наткнулся на null reference exception
            // и ничего бы тебе это не дало
            await dialog.ShowDialog((Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
            _sudopass = dialog.RootPassword;
            Console.WriteLine(_sudopass);
            // с помощью ReactiveUi можно решить эту проблему с помощью уже написанной абстракции:
            // погугли ReactiveUi Interactions

            // P.S. за async хвалю
        }
        private async Task CreateServiceDialog()
        {
            var dialog = new CreateServiceDialog();
            await dialog.ShowDialog((Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);

            string serviceName = dialog.ServiceName;
            Console.WriteLine(serviceName);
            if (!string.IsNullOrEmpty(serviceName))
            {
                if (await CreateServiceFile(serviceName))
                {
                    var box = MessageBoxManager.GetMessageBoxStandard("Ok", $"File {serviceName}.service Created");
                    await OpenServiceDialog(serviceName);
                }
                else
                {
                    var box1 = MessageBoxManager.GetMessageBoxStandard("Ok", $"File {serviceName}.service not created");
                }
            }
        }

        private async Task AddTextToService(string unit, string service, string target, string name)
        {
            var filePath = Path.Combine("/etc/systemd/system", name + ".service");
            string targetFull = "[Install]\nWantedBy=" + target;
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "/bin/bash";
            startInfo.Arguments = $"-c \"echo '{_sudopass}' | echo '{unit} \n{service} \n{targetFull}' |sudo tee {filePath} \"";
            startInfo.RedirectStandardOutput = true;
            process.StartInfo = startInfo;
            process.Start();
            await process.WaitForExitAsync();
            EnableService(name);
            servicesList.Clear();
            ParseServiceList();
            ServicesList = servicesList;
            ChangeLogs("Created Service: " + filePath);
            Poisk();

        }

        private void EnableService(string name)
        {
            Process process = new Process();
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = $"-c \"echo '{_sudopass}' | sudo systemctl enable {name}\"";
            process.Start();
            process.WaitForExit();
        }

        private async Task OpenServiceDialog(string name)
        {
            var targets = GetList();
            var changeParamService = new ChangeParamService(targets);

            await changeParamService.ShowDialog((Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
            var unit = changeParamService.UnitData;
            var service = changeParamService.ServiceData;
            var target = changeParamService.SelectedTarget;
            await AddTextToService(unit, service, target, name);
        }

        public List<string> GetList()
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

            return customParameters ?? new List<string>();
        }

        private async Task<bool> CreateServiceFile(string name)
        {
            if (!string.IsNullOrEmpty(_sudopass))
            {
                //string directoryPath = "/lib/systemd/system";
                string directoryPath = "/etc/systemd/system";
                string file = name + ".service";
                Process process = new Process();
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = $"-c \"echo '{_sudopass}' | sudo -S touch \\\"{directoryPath}/{file}\\\"\"";
                process.Start();
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Root Password", "Enter your root password");
                await box.ShowAsync();

            }
            return false;
        }



        private async Task StartService(string unit)
        {
            if (!string.IsNullOrEmpty(_sudopass))
            {

                Process process = new Process();
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = $"-c \"echo '{_sudopass}' | sudo -S systemctl start {unit}\"";
                process.Start();
                process.WaitForExitAsync();
                UpdateService(unit);
                FilteredServicesList = new ObservableCollection<ServiceViewModel>(filteredServicesList);
                this.RaisePropertyChanged(nameof(FilteredServicesList));
                ChangeLogs("Start " + unit);
            }
            else
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Root Password", "Enter your root password");
                await box.ShowAsync();

            }
        }
        private async Task StopService(string unit)
        {
            if (!string.IsNullOrEmpty(_sudopass))
            {
                Process process = new Process();
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = $"-c \"echo '{_sudopass}' | sudo -S systemctl stop {unit}\"";
                process.Start();
                process.WaitForExitAsync();

                UpdateService(unit);
                FilteredServicesList = new ObservableCollection<ServiceViewModel>(filteredServicesList);
                this.RaisePropertyChanged(nameof(FilteredServicesList));
                ChangeLogs("Stop " + unit);
            }
            else
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Root Password", "Enter your root password");
                await box.ShowAsync();

            }

        }
        private async Task RestartService(string unit)
        {
            if (!string.IsNullOrEmpty(_sudopass))
            {
                Process process = new Process();
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = $"-c \"echo '{_sudopass}' | sudo -S systemctl restart {unit}\"";
                process.Start();
                process.WaitForExitAsync();

                UpdateService(unit);
                FilteredServicesList = new ObservableCollection<ServiceViewModel>(filteredServicesList);
                this.RaisePropertyChanged(nameof(FilteredServicesList));
                ChangeLogs("Restart " + unit);
            }
            else
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Root Password", "Enter your root password");
                await box.ShowAsync();


            }

        }
        private void UpdateService(string unit)
        {
            Process process = new Process();
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = $"-c \"echo '{_sudopass}' | sudo -S systemctl is-active {unit}\"";
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            ServiceViewModel service = ServicesList.FirstOrDefault(s => s.Unit == unit);
            if (service != null)
            {
                service.Active = output.Trim();
            }

        }


        private string GetServiceList()
        {
            Process process = new Process();
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = "-c \"systemctl list-units -a | tail -n +2 | head -n -5\"";
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }

        private void ParseServiceList()
        {
            servicesList = new ObservableCollection<ServiceViewModel>();
            filteredServicesList = new ObservableCollection<ServiceViewModel>();
            ServicesList = servicesList;
            string[] lines = GetServiceList().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string[] parts = line.Split(new[] { ' ', '\t', '●', '?' }, StringSplitOptions.RemoveEmptyEntries);

                string unit = parts[0];
                string load = parts[1];
                string active = parts[2];
                string sub = parts[3];

                servicesList.Add(new ServiceViewModel
                {
                    Unit = unit,
                    Load = load,
                    Active = active,
                    Sub = sub
                });
            }

        }

        private void Poisk()
        {
            if (string.IsNullOrEmpty(SearchText))
            {
                FilteredServicesList.Clear();
                foreach (var service in ServicesList)
                {
                    FilteredServicesList.Add(service);
                }
            }
            else
            {
                FilteredServicesList.Clear();
                foreach (var service in ServicesList.Where(service => service.Unit.Contains(SearchText)))
                {
                    FilteredServicesList.Add(service);
                }
            }
        }


        /* private void UpdateSudo()
         {
             string pass = _sudopass;
         }*/
        private void ChangeLogs(string text)
        {
            log.Info($"{text} | {DateTime.Now}");
            /*string logs = text + " | " + DateTime.Now + "\n";
            using (FileStream fstream = new FileStream("logs.txt", FileMode.Append, FileAccess.Write))
            {
                byte[] input = Encoding.Default.GetBytes(logs);
                fstream.Write(input, 0, input.Length);
            }*/

        }

        private async Task OpenFile()
        {
            string file = "logs.txt";
            Console.WriteLine("", file);
            if (File.Exists(file))
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = file,
                    UseShellExecute = true
                };
                Process.Start(processStartInfo);
            }
            else
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Error", "File does not exist");
                var result = await box.ShowAsync();
            }
        }
    }


}

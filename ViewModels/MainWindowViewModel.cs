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
using System.Runtime;

namespace AvaloniaP2.ViewModels;
public class MainWindowViewModel : ViewModelBase
{
    public string ServiceName;
    private string sudopass;
    private List<ServiceViewModel> servicesList;
    private List<ServiceViewModel> filteredServicesList;
    private string searchText;
    public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }
    public ReactiveCommand<Unit, Unit> SudoUpdate { get; }
    public ReactiveCommand<Unit, Unit> CreateServiceCommand { get; }


    public List<ServiceViewModel> ServicesList
    {
        get => servicesList;
        set => this.RaiseAndSetIfChanged(ref servicesList, value);
    }
    public List<ServiceViewModel> FilteredServicesList
    {
        get => filteredServicesList;
        set => this.RaiseAndSetIfChanged(ref filteredServicesList, value);
    }
    /* public string sudopass
     {
         get => sudopass;
         set => this.RaiseAndSetIfChanged(ref sudopass, value);
     }*/
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
        servicesList = new List<ServiceViewModel>();
        ParserService();
        ServicesList = servicesList;
        Poisk();
        UpdateButton();
        OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFile);
        SudoUpdate = ReactiveCommand.CreateFromTask(RootPassword);
        CreateServiceCommand = ReactiveCommand.CreateFromTask(CreateServiceDialog);
    }
    private async Task RootPassword()
    {
        var dialog = new EnterRoot();
        await dialog.ShowDialog((Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
        sudopass = dialog.RootPassword;
        Console.WriteLine(sudopass);
    }
    private async Task CreateServiceDialog()
    {
        var dialog = new CreateServiceDialog();
        await dialog.ShowDialog((Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);

        string serviceName = dialog.ServiceName;
        if (!string.IsNullOrEmpty(serviceName))
        {
            if (await CreateServiceFile(serviceName))
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Ok", "File " + serviceName + ".service" + " Created");
                var result = await box.ShowAsync();
                await OpenServiceDialog(serviceName);
            }
            else
            {
                var box1 = MessageBoxManager.GetMessageBoxStandard("Ok", "File " + serviceName + ".service" + " not created");
                var result1 = await box1.ShowAsync();
            }
        }
    }
    private async Task AddTextToService(string unit, string service, string target, string name)
    {
        //string filePath = "/lib/systemd/system/" + name + ".service";
        string filePath = "/etc/systemd/system/" + name + ".service";
        string targetFull = "[Install]\nWantedBy=" + target;
        Process process = new Process();
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = "/bin/bash";
        startInfo.Arguments = $"-c \"echo '{sudopass}' | echo '{unit} \n{service} \n{targetFull}' |sudo tee {filePath} \"";
        startInfo.RedirectStandardOutput = true;
        process.StartInfo = startInfo;
        process.Start();
        process.WaitForExit();
        ServiceEnable(name);
        servicesList.Clear();
        ParserService();
        ServicesList = servicesList;
        ChangeLogs("Created Service: " + filePath);
        Poisk();
        UpdateButton();
    }
    private void ServiceEnable(string name)
    {
        Process process = new Process();
        process.StartInfo.FileName = "/bin/bash";
        process.StartInfo.Arguments = $"-c \"echo '{sudopass}' | sudo systemctl enable {name}\"";
        process.Start();
        process.WaitForExit();
    }

    private async Task OpenServiceDialog(string name)
    {
        var changeParamService = new ChangeParamService();
        await changeParamService.ShowDialog((Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
        var unit = changeParamService.UnitData;
        var service = changeParamService.ServiceData;
        var target = changeParamService.SelectedTarget;
        AddTextToService(unit, service, target, name);
    }
    private async Task<bool> CreateServiceFile(string name)
    {
        if (!string.IsNullOrEmpty(sudopass))
        {
            //string directoryPath = "/lib/systemd/system";
            string directoryPath = "/etc/systemd/system";
            string file = name + ".service";
            Process process = new Process();
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = $"-c \"echo '{sudopass}' | sudo -S touch \\\"{directoryPath}/{file}\\\"\"";
            process.Start();
            process.WaitForExit();
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
            var result = await box.ShowAsync();
            UpdateButton();
        }
        return false;

    }


    private async Task StartService(string unit)
    {
        if (!string.IsNullOrEmpty(sudopass))
        {

            Process process = new Process();
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = $"-c \"echo '{sudopass}' | sudo -S systemctl start {unit}\"";
            process.Start();
            process.WaitForExit();
            UpdateService(unit);
            FilteredServicesList = new List<ServiceViewModel>(filteredServicesList);
            this.RaisePropertyChanged(nameof(FilteredServicesList));
            ChangeLogs("Start " + unit);
        }
        else
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Root Password", "Enter your root password");
            var result = await box.ShowAsync();
            UpdateButton();
        }
    }
    private async Task StopService(string unit)
    {
        if (!string.IsNullOrEmpty(sudopass))
        {
            Process process = new Process();
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = $"-c \"echo '{sudopass}' | sudo -S systemctl stop {unit}\"";
            process.Start();
            process.WaitForExit();
            UpdateButton();
            UpdateService(unit);
            FilteredServicesList = new List<ServiceViewModel>(filteredServicesList);
            this.RaisePropertyChanged(nameof(FilteredServicesList));
            ChangeLogs("Stop " + unit);
        }
        else
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Root Password", "Enter your root password");
            var result = await box.ShowAsync();
            UpdateButton();

        }

    }
    private async Task RestartService(string unit)
    {
        if (!string.IsNullOrEmpty(sudopass))
        {
            Process process = new Process();
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = $"-c \"echo '{sudopass}' | sudo -S systemctl restart {unit}\"";
            process.Start();
            process.WaitForExit();
            UpdateButton();
            UpdateService(unit);
            FilteredServicesList = new List<ServiceViewModel>(filteredServicesList);
            this.RaisePropertyChanged(nameof(FilteredServicesList));
            ChangeLogs("Restart " + unit);
        }
        else
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Root Password", "Enter your root password");
            var result = await box.ShowAsync();
            UpdateButton();

        }

    }
    private void UpdateService(string unit)
    {
        Process process = new Process();
        process.StartInfo.FileName = "/bin/bash";
        process.StartInfo.Arguments = $"-c \"echo '{sudopass}' | sudo -S systemctl is-active {unit}\"";
        process.StartInfo.RedirectStandardOutput = true;
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        ServiceViewModel service = ServicesList.FirstOrDefault(s => s.Unit == unit);
        service.Active = output.Trim();

    }
    private void UpdateButton()
    {
        foreach (var service in servicesList)
        {
            service.StartServiceCommand = ReactiveCommand.CreateFromTask<string>(StartService);
            service.StopServiceCommand = ReactiveCommand.CreateFromTask<string>(StopService);
            service.RestartServiceCommand = ReactiveCommand.CreateFromTask<string>(RestartService);
        }
    }


    private string GetSystem()
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

    private void ParserService()
    {
        string[] lines = GetSystem().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            string[] parts = line.Split(new[] { ' ', '\t', '●', '?' }, StringSplitOptions.RemoveEmptyEntries);

            string Unit = parts[0];
            string Load = parts[1];
            string Active = parts[2];
            string Sub = parts[3];

            servicesList.Add(new ServiceViewModel
            {
                Unit = Unit,
                Load = Load,
                Active = Active,
                Sub = Sub
            });

        }
    }

    private void Poisk()
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            FilteredServicesList = null;
            FilteredServicesList = ServicesList;
        }
        else
        {
            FilteredServicesList = null;
            FilteredServicesList = ServicesList.Where(service => service.Unit.Contains(SearchText)).ToList();
        }
    }

    /* private void UpdateSudo()
     {
         string pass = sudopass;
     }*/
    private void ChangeLogs(string text)
    {
        string logs = text + " | " + DateTime.Now + "\n";
        using (FileStream fstream = new FileStream("logs.txt", FileMode.Append, FileAccess.Write))
        {
            byte[] input = Encoding.Default.GetBytes(logs);
            fstream.Write(input, 0, input.Length);
        }


    }

    private async Task OpenFile()
    {
        string file = "logs.txt";

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
            var box = MessageBoxManager.GetMessageBoxStandard("Error", "File not exist");
            var result = await box.ShowAsync();

        }
    }


}


public class ServiceViewModel
{
    public string Unit { get; set; }
    public string Load { get; set; }
    public string Active { get; set; }
    public string Sub { get; set; }

    public ReactiveCommand<string, Unit> StartServiceCommand { get; set; }
    public ReactiveCommand<string, Unit> StopServiceCommand { get; set; }
    public ReactiveCommand<string, Unit> RestartServiceCommand { get; set; }

}

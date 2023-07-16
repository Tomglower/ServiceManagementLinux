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
    // в c# принято называть приватные переменные начиная с _ 
    private string sudopass;
    private List<ServiceViewModel> servicesList;
    // можно использовать ObservableCollection, затем очищать/заполнять её элементами. Avalonia сама подпишется на 
    // нужные события и будет перерисовывать ItemsControl'ы
    private List<ServiceViewModel> filteredServicesList;
    private string searchText;
    
    // здесь конкретный тип по-хорошему нужно заменить на интерфейс ICommand, тогда твоё взаимодействие с ReactiveUi и
    // Avalonia сведётся к абстракции, что будет соответствовать MVVM-паттерну и исключит участие
    // Framework'о-зависимого кода в твоей бизнес-логике:
    // public ICommand OpenFileCommand { get; } и т.д.
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
        // вызов долгоиграющего метода в конструкторе, что плохо
        // конструктор подразумевает, что ты выделяешь память под объект, а у тебя внутри вызывается метод, который и
        // вовсе может выполняться вечно.
        // Лучше такие методы выносить куда-нибудь в Initialize(), который будет вызываться после инициализации самого 
        // объекта. А ещё лучше, если такие методы будут async
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
        sudopass = dialog.RootPassword;
        Console.WriteLine(sudopass);
        // с помощью ReactiveUi можно решить эту проблему с помощью уже написанной абстракции:
        // погугли ReactiveUi Interactions
        
        // P.S. за async хвалю
    }
    private async Task CreateServiceDialog()
    {
        // то же самое, что и в предыдущем методе
        var dialog = new CreateServiceDialog();
        await dialog.ShowDialog((Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);

        string serviceName = dialog.ServiceName;
        if (!string.IsNullOrEmpty(serviceName))
        {
            if (await CreateServiceFile(serviceName))
            {
                // старайся не вылезать за пределы ограничителя строки (в IDE рисуется вертикальной полоской где-то тут,
                // используй переносы, это хороший тон и зачастую реально выручает, когда нужно редактировать сразу 
                // несколько файлов
                // также не забывай про интерполяцию строк: $"File {serviceName}.service Created"
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
    // объявляешь как async таску, но в самом методе не используешь прерываний. Можешь стоклнуться с такими багами,
    // которые замучаешься потом искать
    private async Task AddTextToService(string unit, string service, string target, string name)
    {
        // используй Path.Combine метод, да и вообще статический класс Path для работы с путями. Он решит для тебя
        // сразу ряд нюансов. Например поле Path.PathSeparator само отследит, какой сепаратор используется для
        // разделения вложенности папок в системе (Unix-like - '/', Windows - '\' и т.д.)
        // Также почитай про статические методы File и Directory. Тоже много полезного имеют
        
        //string filePath = "/lib/systemd/system/" + name + ".service";
        // не забывай про ключевое слово var, удобно же :) 
        string filePath = "/etc/systemd/system/" + name + ".service";
        string targetFull = "[Install]\nWantedBy=" + target;
        Process process = new Process();
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = "/bin/bash";
        startInfo.Arguments = $"-c \"echo '{sudopass}' | echo '{unit} \n{service} \n{targetFull}' |sudo tee {filePath} \"";
        startInfo.RedirectStandardOutput = true;
        process.StartInfo = startInfo;
        process.Start();
        // можешь использовать await process.WaitForExitAsync(), т.к. у тебя async метод
        process.WaitForExit();
        ServiceEnable(name);
        servicesList.Clear();
        ParserService();
        ServicesList = servicesList;
        ChangeLogs("Created Service: " + filePath);
        Poisk();
        UpdateButton();
    }
    // правильнее было бы назвать метод EnableService(). Следи за семантикой для более легкой читабельности кода
    private void ServiceEnable(string name)
    {
        Process process = new Process();
        process.StartInfo.FileName = "/bin/bash";
        process.StartInfo.Arguments = $"-c \"echo '{sudopass}' | sudo systemctl enable {name}\"";
        process.Start();
        process.WaitForExit();
    }

    // тоже async без прерываний (await)
    private async Task OpenServiceDialog(string name)
    {
        var changeParamService = new ChangeParamService();
        await changeParamService.ShowDialog((Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
        var unit = changeParamService.UnitData;
        var service = changeParamService.ServiceData;
        var target = changeParamService.SelectedTarget;
        // забыл await
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
            // WaitForExitAsync()
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
            // result нигде не используешь в итоге
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
            // WaitForExitAsync
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
            // WaitForExitAsync
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
            // копипасты - зло))
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
        // FirstOrDefault возвращает nullable, и т.к. у тебя включены в настройках проекта nullable-типы, то их нужно
        // соблюдать. Почитать подробнее про C# 8.0 nullable
        ServiceViewModel service = ServicesList.FirstOrDefault(s => s.Unit == unit);
        service.Active = output.Trim();

    }
    private void UpdateButton()
    {
        // пихаешь View логику ещё и в Model слой. Model уж точно никак не должны знать ни о View, ни о
        // ViewModel уровнях. Старайся избегать таких моментов, ибо Model у тебя могут быть вообще из другого проекта
        // Замучаешься конвертировать. За это должны отвечать отдельные ViewModel с их отдельными View, например,
        // Service -> ServiceViewModel -> ServiceView
        // С такой связкой и цикл не понадобился бы, твой ItemsControl сам бы создавал нужные ему View и прокладывал
        // туда логику из ViewModel
        foreach (var service in servicesList)
        {
            service.StartServiceCommand = ReactiveCommand.CreateFromTask<string>(StartService);
            service.StopServiceCommand = ReactiveCommand.CreateFromTask<string>(StopService);
            service.RestartServiceCommand = ReactiveCommand.CreateFromTask<string>(RestartService);
        }
    }


    // не очень понятное название метода. Следи за семантикой
    private string GetSystem()
    {
        // тоже самое, что и в подобных методах выше. 
        Process process = new Process();
        process.StartInfo.FileName = "/bin/bash";
        process.StartInfo.Arguments = "-c \"systemctl list-units -a | tail -n +2 | head -n -5\"";
        process.StartInfo.RedirectStandardOutput = true;
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
        // много повторяющегося кода. Можно вынести запуск процессов в 1 статичный класс и переиспользовать метод,
        // например: 
        // var output = await ProcessExecutor
        //     .ExecuteAsync("/bin/bash", "-c \"systemctl list-units -a | tail -n +2 | head -n -5\"")
    }

    // тоже придирка к названию метода
    private void ParserService()
    {
        string[] lines = GetSystem().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            string[] parts = line.Split(new[] { ' ', '\t', '●', '?' }, StringSplitOptions.RemoveEmptyEntries);

            // имена переменных не по code-style, старайся его придерживаться. локальные переменные называем с маленькой
            // буквы в camelCase стиле
            // важное уточнение: у компаний может быть свой code-style. У кого-то есть вообще свои кастомные линтеры
            // но у языков программирования тоже могут быть рекомендации и общие договорёности. У C# есть, у Java такие
            // договорености вообще перетикают в ошибки прекомпилятора. В TypeScript ты настраиваешь свой Linter и т.д.
            // Так что это не совсем ошибка, но имей ввиду.
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

    // почему не Search()?)
    private void Poisk()
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            // как я писал выше, если бы использовал ObservableCollection и очищал/заполнял её, не пришлось бы плодить
            // экземпляры коллекции, тем более такой жирной, как List.
            // коллекции сильно насилуют алокатор и сборщика мусора. В dotnet есть проблемы со скоростью выделения
            // памяти. Мы, конечно, не космолеты на Марс отправляем, но всё же по хорошему следить за этим.
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
        // я бы советовал использовать готовые штуки, потипу NLog, Log4NET, ELMAH и т.д.д., как душе угодно.
        // они уже оптимизированы и реализуют много кейсов, потипу уровня логирования и т.д.
        string logs = text + " | " + DateTime.Now + "\n";
        using (FileStream fstream = new FileStream("logs.txt", FileMode.Append, FileAccess.Write))
        {
            byte[] input = Encoding.Default.GetBytes(logs);
            fstream.Write(input, 0, input.Length);
        }


    }

    // вообще не понял, для чего предназначен этот метод
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

// плохой тон плодить классы внутри 1 файла. неудобно искать и вообще нарушает иерархию. В Java такое вообще считается
// ошибкой компиляции.
// исключением являются приватные классы, но я никогда не видел их обоснованного использования.
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

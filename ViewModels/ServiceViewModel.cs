using System.Windows.Input;

public class ServiceViewModel
{
    public string Unit { get; set; }
    public string Load { get; set; }
    public string Active { get; set; }
    public string Sub { get; set; }

    public ICommand StartServiceCommand { get; set; }
    public ICommand StopServiceCommand { get; set; }
    public ICommand RestartServiceCommand { get; set; }
}

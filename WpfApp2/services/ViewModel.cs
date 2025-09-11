using System.ComponentModel;
using System.Runtime.CompilerServices;

public class CellDoubleViewModel : INotifyPropertyChanged
{
    private bool reverse;
    private double sensitivity;
    private double value;

    public bool Reverse
    {
        get => reverse;
        set { reverse = value; OnPropertyChanged(); }
    }

    public double Sensitivity
    {
        get => sensitivity;
        set { sensitivity = value; OnPropertyChanged(); }
    }

    public double Value
    {
        get => value;
        set { this.value = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void LoadFromConfig(Configs.CellDouble config)
    {
        Reverse = config.Reverse;
        Sensitivity = config.Sensitivity;
        Value = config.Default;
    }

    public void SaveToConfig(Configs.CellDouble config)
    {
        config.Reverse = Reverse;
        config.Sensitivity = Sensitivity;
        config.Default = Value;
    }
}

public class CellBoolViewModel : INotifyPropertyChanged
{
    private int aixs;
    private int needType;

    public int Aixs
    {
        get => aixs;
        set { aixs = value; OnPropertyChanged(); }
    }

    public int NeedType
    {
        get => needType;
        set { needType = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void LoadFromConfig(Configs.CellBool config)
    {
        Aixs = config.Aixs;
        NeedType = config.NeedType;
    }

    public void SaveToConfig(Configs.CellBool config)
    {
        config.Aixs = Aixs;
        config.NeedType = NeedType;
    }
}

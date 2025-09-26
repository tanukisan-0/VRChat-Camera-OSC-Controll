using Serilog;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using TextBox = System.Windows.Controls.TextBox;
using WpfMessageBox = System.Windows.MessageBox;

namespace WpfApp
{

    public partial class MainWindow : Window
    {
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private int _Send_Port;
        private int _Receive_Port;
        private string _IPv4_Address;
        private bool _isMainTaskRunning = false;

        private Dictionary<int, string> MappingNumber = new Dictionary<int, string>
        {
            {0,"なし"},
            {1,"左スティックX軸"},
            {2,"左スティックY軸"},
            {3,"左トリガー"},
            {5,"左グリップ"},
            {101,"右スティックX軸"},
            {102,"右スティックY軸"},
            {103,"右トリガー"},
            {105,"右グリップ"}
        };



        public Configs _config = new Configs();
        public OscService _OscService;
        private MainController _MainController;

        public MainWindow()
        {
            InitializeComponent();

            RenderOptions.ProcessRenderMode = RenderMode.Default;

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/osc_log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Zoom_ComboBox.ItemsSource = MappingNumber;
            Zoom_ComboBox.DisplayMemberPath = "Value";
            Zoom_ComboBox.SelectedValuePath = "Key";

            Exposure_ComboBox.ItemsSource = MappingNumber;
            Exposure_ComboBox.DisplayMemberPath = "Value";
            Exposure_ComboBox.SelectedValuePath = "Key";

            FocalDistance_ComboBox.ItemsSource = MappingNumber;
            FocalDistance_ComboBox.DisplayMemberPath = "Value";
            FocalDistance_ComboBox.SelectedValuePath = "Key";

            Aperture_ComboBox.ItemsSource = MappingNumber;
            Aperture_ComboBox.DisplayMemberPath = "Value";
            Aperture_ComboBox.SelectedValuePath = "Key";

            NotifysInitialize();
            
            Loaded += MainWindow_Loaded;
        }

        private void NotifysInitialize()
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = new Icon("ICON.ico"), // プロジェクトにアイコンを追加
                Visible = false,
                Text = "VRChat Camera OSC Controll"
            };

            _notifyIcon.Text = "VRChat Camera OSC Control";

            // 右クリックメニュー作成
            var contextMenu = new ContextMenuStrip();
            var showItem = new ToolStripMenuItem("表示する");
            showItem.Click += (s, e) => ShowFromTray();
            var exitItem = new ToolStripMenuItem("終了する");
            exitItem.Click += (s, e) => CloseApp();

            contextMenu.Items.Add(showItem);
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            _notifyIcon.DoubleClick += (s, e) => ShowFromTray();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            base.OnClosing(e);
            Console.WriteLine("アプリケーションを終了します。");
        }

        private void CloseApp()
        {
            _notifyIcon.Dispose();
            Close();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("UIのロードが完了しました。");
            LoadConfig();

            Applying_The_Settings();

            _MainController = new MainController(_config.Config!);
        }

        private void LoadConfig()
        {
            try
            {
                _config.ReadConfig();
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"設定の読み込み中にエラーが発生しました: {ex.Message}");
            }
        }

        public void Applying_The_Settings()
        {
            Send_Port_Text.Text = Convert.ToString(_config.Config?.Ports.Send_Port);
            Receive_Port_Text.Text = Convert.ToString(_config.Config?.Ports.Receive_Port);
            IPv4_Address_Text.Text = _config.Config?.Ports.IPv4_Address;

            Zoom_ComboBox.SelectedValue = _config.Config.Mapping.Zoom.Axis;
            Exposure_ComboBox.SelectedValue = _config.Config.Mapping.Exposure.Axis;
            FocalDistance_ComboBox.SelectedValue = _config.Config.Mapping.FocalDistance.Axis;
            Aperture_ComboBox.SelectedValue = _config.Config.Mapping.Aperture.Axis;

            Zoom_Sensitivity.Value = _config.Config.Mapping.Zoom.Sensitivity;
            Zoom_Invert_CheckBox.IsChecked = _config.Config.Mapping.Zoom.Reverse;

            Exposure_Sensitivity.Value = _config.Config.Mapping.Exposure.Sensitivity;
            Exposure_Invert_CheckBox.IsChecked = _config.Config.Mapping.Exposure.Reverse;

            Aperture_Sensitivity.Value = _config.Config.Mapping.Aperture.Sensitivity;
            Aperture_Invert_CheckBox.IsChecked = _config.Config.Mapping.Aperture.Reverse;

            FocalDistance_Sensitivity.Value = _config.Config.Mapping.FocalDistance.Sensitivity;
            FocalDistance_Invert_CheckBox.IsChecked = _config.Config.Mapping.FocalDistance.Reverse;

            Stop_With_Both_Hands_Grip_CheckBox.IsChecked = _config.Config.anotherSettings.StopWithBothHandsGrip;

            UpdateFrequency.Value = _config.Config.anotherSettings.UpdateFrequency;
        }

        /// <summary>
        /// タスクトレイの処理
        /// </summary>

        private void TrayButton_Click(object sender, RoutedEventArgs e)
        {
            HideToTray();
        }

        private void HideToTray()
        {
            Hide();
            _notifyIcon.Visible = true;

        }

        private void ShowFromTray()
        {
            this.Show();
            WindowState = WindowState.Normal;
            this.Activate();
            _notifyIcon.Visible = false;
        }

        /// <summary>
        /// OSCの入出力
        /// </summary>

        private void Send_Port_TextBox(object sender, TextChangedEventArgs e)
        {
            if (_config.IsReadConfigCompleted)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                if (sender is TextBox textBox)
                {
                    temp.Ports.Send_Port = Convert.ToInt32(textBox.Text);
                    _config.WriteConfig(temp);
                }
                else
                {
                    Log.Information("sender is not textbox");
                }
            }

            Log.Information($"From:Send_Port_TextBox:Event{e}");
        }

        private void Receive_Port_TextBox(object sender, TextChangedEventArgs e)
        {
            if (_config.IsReadConfigCompleted)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                if (sender is TextBox textBox)
                {
                    temp.Ports.Receive_Port = Convert.ToInt32(textBox.Text);
                    _config.WriteConfig(temp);
                }
                else
                {
                    Log.Information("sender is not textbox");
                }
            }

            Log.Information($"From:Receive_Port_TextBox:Event{e}");
        }

        private void IPv4_Address_TextBox(object sender, TextChangedEventArgs e)
        {
            if (_config.IsReadConfigCompleted)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                if (sender is TextBox textBox)
                {
                    temp.Ports.IPv4_Address = textBox.Text;
                    _config.WriteConfig(temp);
                }
                else
                {
                    Log.Information("sender is not textbox");
                }
            }
            Log.Information($"From:IPv4_Address_TextBox:Event{e}");
        }

        /// <summary>
        /// ツール系の処理
        /// </summary>

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // ダブルクリックで最大化/復元
                WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                // ドラッグ可能にする
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ===== Zoom =====
        private void Zoom_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_config.IsReadConfigCompleted)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                temp.Mapping.Zoom.Sensitivity = e.NewValue;
                _config.WriteConfig(temp);
            }
        }

        private void Zoom_Invert_CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_config.IsReadConfigCompleted)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                temp.Mapping.Zoom.Reverse = Zoom_Invert_CheckBox.IsChecked ?? false;
                _config.WriteConfig(temp);
            }
        }

        private void Zoom_ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Zoom_ComboBox.SelectedValue is int axis)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                temp.Mapping.Zoom.Axis = axis;
                _config.WriteConfig(temp);
            }
        }

        //Expose
        private void Exposure_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_config.IsReadConfigCompleted)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                temp.Mapping.Exposure.Sensitivity = e.NewValue;
                _config.WriteConfig(temp);
            }
        }

        private void Exposure_Invert_CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_config.IsReadConfigCompleted)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                temp.Mapping.Exposure.Reverse = Exposure_Invert_CheckBox.IsChecked ?? false;
                _config.WriteConfig(temp);
            }
        }

        private void Exposure_ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Exposure_ComboBox.SelectedValue is int axis)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                temp.Mapping.Exposure.Axis = axis;
                _config.WriteConfig(temp);
            }
        }

        //Focal Distance
        private void FocalDistance_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_config.IsReadConfigCompleted)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                temp.Mapping.FocalDistance.Sensitivity = e.NewValue;
                _config.WriteConfig(temp);
            }
        }

        private void FocalDistance_Invert_CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_config.IsReadConfigCompleted)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                temp.Mapping.FocalDistance.Reverse = FocalDistance_Invert_CheckBox.IsChecked ?? false;
                _config.WriteConfig(temp);
            }
        }

        private void FocalDistance_ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FocalDistance_ComboBox.SelectedValue is int axis)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                temp.Mapping.FocalDistance.Axis = axis;
                _config.WriteConfig(temp);
            }
        }

        //Aperture
        private void Aperture_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_config.IsReadConfigCompleted)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                temp.Mapping.Aperture.Sensitivity = e.NewValue;
                _config.WriteConfig(temp);
            }
        }

        private void Aperture_Invert_CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_config.IsReadConfigCompleted)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                temp.Mapping.Aperture.Reverse = Aperture_Invert_CheckBox.IsChecked ?? false;
                _config.WriteConfig(temp);
            }
        }

        private void Aperture_ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Aperture_ComboBox.SelectedValue is int axis)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                temp.Mapping.Aperture.Axis = axis;
                _config.WriteConfig(temp);
            }
        }

        //another
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _MainController.StopWorkLoop();
            _MainController.ShutdownServices();
            _MainController = new MainController(_config.Config!);
            Log.Information("初期化されました。");
        }

        private void StopWithBothHandsGripCheckBox(object sender, RoutedEventArgs e)
        {
            if (_config.IsReadConfigCompleted)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                temp.anotherSettings.StopWithBothHandsGrip = Stop_With_Both_Hands_Grip_CheckBox.IsChecked ?? false;
                _config.WriteConfig(temp);
            }
        }

        private void ShowLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow licenseWindow = new AboutWindow();
            licenseWindow.Owner = this; // 親ウィンドウを設定するとモーダル風に扱える
            licenseWindow.ShowDialog(); // モーダル表示
        }

        private void UpdateFrequency_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_config.IsReadConfigCompleted)
            {
                Configs.JsonConfigsStructure temp = _config.Config!;
                temp.anotherSettings.UpdateFrequency = e.NewValue;
                _config.WriteConfig(temp);
            }
        }
    }
}

public class MainViewModel
{
    private readonly Configs configs = new Configs();

    public CellDoubleViewModel Zoom { get; } = new CellDoubleViewModel();
    public CellDoubleViewModel Exposure { get; } = new CellDoubleViewModel();
    public CellDoubleViewModel FocalDistance { get; } = new CellDoubleViewModel();
    public CellDoubleViewModel Aperture { get; } = new CellDoubleViewModel();
    public CellBoolViewModel ChangeControll { get; } = new CellBoolViewModel();

    public MainViewModel()
    {
        configs.ReadConfig();
        if (configs.Config != null)
        {
            Zoom.LoadFromConfig(configs.Config.Mapping.Zoom);
            Exposure.LoadFromConfig(configs.Config.Mapping.Exposure);
            FocalDistance.LoadFromConfig(configs.Config.Mapping.FocalDistance);
            Aperture.LoadFromConfig(configs.Config.Mapping.Aperture);
            ChangeControll.LoadFromConfig(configs.Config.Mapping.Change_Controll);
        }
    }

    public void Save()
    {
        if (configs.Config != null)
        {
            Zoom.SaveToConfig(configs.Config.Mapping.Zoom);
            Exposure.SaveToConfig(configs.Config.Mapping.Exposure);
            FocalDistance.SaveToConfig(configs.Config.Mapping.FocalDistance);
            Aperture.SaveToConfig(configs.Config.Mapping.Aperture);
            ChangeControll.SaveToConfig(configs.Config.Mapping.Change_Controll);

            configs.WriteConfig(configs.Config);
        }
    }
}
using BuildSoft.VRChat.Osc;
using Serilog;
using System.Windows.Threading;
using System.Diagnostics;
using Valve.VR;


public class MainController
{
    public Configs.JsonConfigsStructure Configs;
    private OscService? _OscService;
    private SteamVRSystem? _SteamVRSystem;
    private readonly DispatcherTimer _checkTimer;      // SteamVR/VRChat監視用 (5秒間隔)
    private CancellationTokenSource? _workCts;
    private Task? _workTask;
    private bool _isInitialized = false;
    private Dictionary<string, object?>? tempCameraParametar;
    private bool ToggleStop = false;
    
    public MainController(Configs.JsonConfigsStructure _config)
    {

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/osc_log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Configs = _config;

        // 5秒ごとにSteamVRとVRChatを監視
        _checkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _checkTimer.Tick += CheckProcesses;
        _checkTimer.Start();
    }

    private void CheckProcesses(object? sender, EventArgs e)
    {
        bool steamVR = Process.GetProcessesByName("vrserver").Length > 0;
        bool vrchat = Process.GetProcessesByName("VRChat").Length > 0;

        if (steamVR && vrchat)
        {
            if (!_isInitialized)
            {
                Log.Information("SteamVR と VRChat が起動しました。初期化を開始します...");
                InitializeServices();
                StartWorkLoop();   // 60FPS処理を開始
                _isInitialized = true;
            }
        }
        else
        {
            if (_isInitialized)
            {
                Log.Warning("SteamVR または VRChat が終了しました。サービスを停止します...");
                StopWorkLoop();    // 60FPS処理を停止
                ShutdownServices();
                _isInitialized = false;
            }
        }
    }

    private void InitializeServices()
    {
        _SteamVRSystem = new SteamVRSystem();
        _OscService = new OscService(Configs);
        _OscService.AdaptOscConfig();

        _SteamVRSystem.ControllerPollerStart();
        _OscService.AvatarParameterPollerStart();
    }

    public void ShutdownServices()
    {
        try
        {
            Log.Information("サービスを停止中...");

            _SteamVRSystem?.ControllerPollerStop();
            _OscService?.AvatarParameterPollerStop();

            OpenVR.Shutdown();

            Log.Information("サービスを正常に停止しました。");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "サービス停止処理中に例外が発生しました。");
        }
    }

    // 60FPSで動かす別処理
    private void StartWorkLoop()
    {
        _workCts = new CancellationTokenSource();
        _workTask = Task.Run(async () =>
        {
            while (!_workCts.Token.IsCancellationRequested)
            {
                var interval = TimeSpan.FromMilliseconds(1000.0 / _OscService!._targetFps * Configs.anotherSettings.UpdateFrequency);
                //Log.Information($"{_OscService!._targetFps}");
                try
                {
                    WorkLoopTickInternal(); // UI スレッド不要の処理
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "処理中にエラーが発生しました");
                }

                try
                {
                    await Task.Delay(interval, _workCts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }, _workCts.Token);

        Log.Information("処理を別スレッドで開始しました。");
    }

    public void StopWorkLoop()
    {
        if (_workCts != null)
        {
            _workCts.Cancel();
            try { _workTask?.Wait(); } catch { }
            _workCts.Dispose();
            _workCts = null;
            _workTask = null;
        }

        Log.Information("60FPS 処理を停止しました。");
    }

    private void WorkLoopTickInternal()
    {
        if (_SteamVRSystem == null) return;

        bool SendCount = (int)_OscService!.CameraParametars["Mode"]! == 1 && _SteamVRSystem.ControllerInputs.LeftController.rAxis2.x > 0.75 && _SteamVRSystem.ControllerInputs.RightController.rAxis2.x > 0.75;
        if (!SendCount)
            tempCameraParametar = _OscService!.CameraParametars;

        // Zoom, Exposure, FocalDistance, Aperture などの Input Axis を取得
        var zoomAix = new OutputDouble(_SteamVRSystem, Configs.Mapping.Zoom);
        var exposureAix = new OutputDouble(_SteamVRSystem, Configs.Mapping.Exposure);
        var focalDistanceAix = new OutputDouble(_SteamVRSystem, Configs.Mapping.FocalDistance);
        var apertureAix = new OutputDouble(_SteamVRSystem, Configs.Mapping.Aperture);

        // 値を更新
        zoomAix.UpdateAixs();
        exposureAix.UpdateAixs();
        focalDistanceAix.UpdateAixs();
        apertureAix.UpdateAixs();

        if (SendCount)
        {
            if (Math.Abs(Convert.ToDouble(zoomAix.value)) > 0.025)
                OscParameter.SendValue("/usercamera/Zoom",
                    Convert.ToSingle(Math.Clamp(Convert.ToSingle(tempCameraParametar!["Zoom"]) +
                    Convert.ToSingle(zoomAix.ValueManipulation()) * (60 / _OscService!._targetFps) * Configs.anotherSettings.UpdateFrequency / 2,zoomAix.Min,zoomAix.Max)));

            if (Math.Abs(Convert.ToDouble(exposureAix.value)) > 0.025)
                OscParameter.SendValue("/usercamera/Exposure",
                    Convert.ToSingle(Math.Clamp(Convert.ToSingle(tempCameraParametar!["Exposure"]) +
                    Convert.ToSingle(exposureAix.ValueManipulation()) * (60 / _OscService!._targetFps) * Configs.anotherSettings.UpdateFrequency / 2,exposureAix.Min,exposureAix.Max)));

            if (Math.Abs(Convert.ToDouble(focalDistanceAix.value)) > 0.025)
                OscParameter.SendValue("/usercamera/FocalDistance",
                    Convert.ToSingle(Math.Clamp(Convert.ToSingle(tempCameraParametar!["FocalDistance"]) +
                    Convert.ToSingle(focalDistanceAix.ValueManipulation()) * (60 / _OscService!._targetFps) * Configs.anotherSettings.UpdateFrequency / 2,focalDistanceAix.Min,focalDistanceAix.Max)));

            if (Math.Abs(Convert.ToDouble(apertureAix.value)) > 0.025)
                OscParameter.SendValue("/usercamera/Aperture",
                    Convert.ToSingle(Math.Clamp(Convert.ToSingle(tempCameraParametar!["Aperture"]) +
                    Convert.ToSingle(apertureAix.ValueManipulation()) * (60 / _OscService!._targetFps) * Configs.anotherSettings.UpdateFrequency / 2,apertureAix.Min,apertureAix.Max)));

            if (Configs.anotherSettings.StopWithBothHandsGrip)
            {
                OscParameter.SendValue("/avatar/parameters/VRChatCameraStop", true);
            }
            else
            {
                OscParameter.SendValue("/avatar/parameters/VRChatCameraStop", false);
            }
        }
        else
        {
            OscParameter.SendValue("/avatar/parameters/VRChatCameraStop", false);
            _OscService.PollerOnOff = new bool[3] { true, true, true };
        }
    }


    private class OutputDouble
    {
        private SteamVRSystem _steamVRSystem;

        public int Aixs { get; set; }
        public int NeedType { get; set; }  // 0 = Anything, 1 = Bool, 2 = float range
        public bool Reverse { get; set; }
        public object? value { get; set; }
        public double Sensitivity { get; set; }
        public double Default { get; set; }
        public double Max { get; set; }
        public double Min { get; set; }

        public OutputDouble(SteamVRSystem steamVR, Configs.CellDouble config)
        {
            _steamVRSystem = steamVR;
            Aixs = config.Axis;
            NeedType = config.NeedType;
            Sensitivity = config.Sensitivity;
            Default = config.Default;
            Max = config.Max;
            Min = config.Min;
            value = SelectAixs();
            Reverse = config.Reverse;
        }

        public void UpdateAixs()
        {
            value = SelectAixs();
        }

        public object? ValueManipulation()
        {
            if (NeedType == 1)
            {
                if (value is float fValue)
                {
                    if (fValue > 0.75)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else if (NeedType == 2)
            {
                if (value is float fValue)
                {
                    return (Max - Min) * (Sensitivity / 100) * fValue / 10 * (Reverse ? -1 : 1);
                }
            }
            return null;
        }

        private object? SelectAixs()
        {
            var inputs = _steamVRSystem.ControllerInputs;

            return Aixs switch
            {
                0 => null,
                1 => inputs.LeftController.rAxis0.x * (1 - Math.Abs(inputs.LeftController.rAxis0.y)), //スティックのX
                2 => inputs.LeftController.rAxis0.y * (1 - Math.Abs(inputs.LeftController.rAxis0.x)), //スティックのY
                3 => inputs.LeftController.rAxis1.x, //トリガー
                4 => inputs.LeftController.rAxis1.y, //None クエストのコントローラーだけだと何が入るかわからない。
                5 => inputs.LeftController.rAxis2.x, //グリップ
                6 => inputs.LeftController.rAxis2.y,
                7 => inputs.LeftController.rAxis3.x,
                8 => inputs.LeftController.rAxis3.y,
                9 => inputs.LeftController.rAxis4.x,
                10 => inputs.LeftController.rAxis4.y, //None
                11 => inputs.LeftController.ulButtonPressed, //未調査 ボタン

                51 => inputs.RightController.rAxis0.x * (1 - Math.Abs(inputs.RightController.rAxis0.y)),
                52 => inputs.RightController.rAxis0.y * (1 - Math.Abs(inputs.RightController.rAxis0.x)),
                53 => inputs.RightController.rAxis1.x,
                54 => inputs.RightController.rAxis1.y,
                55 => inputs.RightController.rAxis2.x,
                56 => inputs.RightController.rAxis2.y,
                57 => inputs.RightController.rAxis3.x,
                58 => inputs.RightController.rAxis3.y,
                59 => inputs.RightController.rAxis4.x,
                60 => inputs.RightController.rAxis4.y,
                61 => inputs.RightController.ulButtonPressed,

                _ => null
            };
        }
    }
}
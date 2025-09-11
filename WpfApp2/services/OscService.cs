using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildSoft.VRChat.Osc;
using BuildSoft.VRChat.Osc.Avatar;
using Serilog;

public class OscService
{
    public OscAvatarConfig? AvatarConfig;
    public OscAvatarParameterContainer? AvatarContainer;
    public IReadOnlyDictionary<string, object?>? CommonParameters;
    public readonly Dictionary<string, object?> CameraParametars = new Dictionary<string, object?>();
    public bool[] PollerOnOff = new bool[3] { true, true, true };
    private Configs.JsonConfigsStructure _config;
    private DateTime _lastLogTime = DateTime.MinValue;

    private CancellationTokenSource? _cts;
    private Task? _pollerTask;
    public readonly int _targetFps;

    // 🔹 推定FPS（他のクラスから読み取れる）
    public static double EstimatedFps { get; private set; } = 60;

    private DateTime _lastReceiveTime = DateTime.Now;

    public OscService(Configs.JsonConfigsStructure config, int targetFps = 60)
    {
        _config = config;
        _targetFps = targetFps;

        // 初期化
        foreach (var kv in CameraParametersAddress)
        {
            CameraParametars[kv.Key] = kv.Value;
        }

        RegisterAvatarChangeHandler();
    }

    private Dictionary<string, object?> CameraParametersAddress = new Dictionary<string, object?>
    {
        { "Mode", 0 },
        { "Pose", 0 },
        { "ShowUIInCamera", false },
        { "Lock", false },
        { "LocalPlayer", false },
        { "RemotePlayer", false },
        { "Environment", false },
        { "GreenScreen", false },
        { "SmoothMovement", false },
        { "LookAtMe", false },
        { "AutoLevelRoll", false },
        { "AutoLevelPitch", false },
        { "Flying", false },
        { "TriggerTakesPhotos", false },
        { "DollyPathsStayVisible", false },
        { "CameraEars", false },
        { "ShowFocus", false },
        { "Streaming", false },
        { "RollWhileFlying", false },
        { "OrientationIsLandscape", false },
        { "Zoom", (float)45 },
        { "Exposure", (float)0 },
        { "FocalDistance", (float)1.5 },
        { "Aperture", (float)15 },
        { "Hue", (float)120 },
        { "Saturation", (float)100 },
        { "Lightness", (float)60 },
        { "LookAtMeXOffset", (float)0 },
        { "LookAtMeYOffset", (float)0 },
        { "FlySpeed", (float)3 },
        { "TurnSpeed", (float)1 },
        { "SmoothingStrength", (float)5 },
        { "PhotoRate", (float)1 },
        { "Duration", (float)2 }
    };

    public void AvatarParameterPollerStart()
    {
        _cts = new CancellationTokenSource();
        _pollerTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    GetAvatarParametars();
                    GetUtilityParametars();
                    GetCameraParametars();
                    UpdateEstimatedFps();   // 🔹 FPS計測を追加
                    Measuring_time();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "AvatarParameterPoller 内で例外発生");
                }

                await Task.Delay(1000 / _targetFps, _cts.Token);
            }
        }, _cts.Token);
    }

    public void AvatarParameterPollerStop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            try { _pollerTask?.Wait(); } catch { }
            _cts.Dispose();
            _cts = null;
            _pollerTask = null;
        }
    }

    public void UpdateConfig(Configs.JsonConfigsStructure config) => _config = config;

    public async void AdaptOscConfig()
    {
        OscConnectionSettings.ReceivePort = _config.Ports.Receive_Port;
        OscConnectionSettings.SendPort = _config.Ports.Send_Port;
        OscConnectionSettings.VrcIPAddress = _config.Ports.IPv4_Address;
        try
        {
            OscUtility.Initialize();
            if (OscUtility.IsFailedAutoInitialization)
            {
                Log.Information("OSCの初期化に失敗しました。1秒後に再度試行します");
                await Task.Delay(1000);
                AdaptOscConfig();
            }
        }
        catch (Exception ex)
        {
            Log.Information($"FORM:Setting:{ex}");
        }
    }

    private void RegisterAvatarChangeHandler()
    {
        OscAvatarUtility.AvatarChanged += async (s, e) =>
        {
            Log.Information("アバターが変更されました。新しい設定をロードします。");

            try
            {
                AvatarConfig = await OscAvatarConfig.WaitAndCreateAtCurrentAsync();
                if (AvatarConfig != null)
                {
                    AvatarContainer = new OscAvatarParameterContainer(AvatarConfig.Parameters.Items);
                    Log.Information($"新しい AvatarConfig を取得しました: {AvatarConfig.Id}");
                }
                else
                {
                    Log.Warning("AvatarConfig の取得に失敗しました（null）");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AvatarConfig の再取得に失敗しました");
            }
        };
    }

    private void GetAvatarParametars()
    {
        if (AvatarContainer == null) return;
        // ここで必要な処理を追加
    }

    private void GetUtilityParametars()
    {
        if (AvatarContainer == null || !PollerOnOff[1]) return;

        CommonParameters = OscAvatarUtility.CommonParameters;
        if ((DateTime.Now - _lastLogTime).TotalSeconds > 5)
        {
            foreach (var kv in CommonParameters)
            {
                Log.Information($"{kv.Key} : {kv.Value}");
            }
        }
    }

    private void GetCameraParametars()
    {
        if (!PollerOnOff[2]) return;

        foreach (var kv in CameraParametersAddress)
        {
            string Address = "/usercamera/" + kv.Key;
            var temp = OscParameter.GetValue(Address);
            CameraParametars[kv.Key] = (temp == null) ? kv.Value : temp;

            if ((DateTime.Now - _lastLogTime).TotalSeconds > 5)
            {
                if (temp != null)
                    Log.Information($"{kv.Key} : {CameraParametars[kv.Key]}");
                else
                    Log.Information($"nullです。デフォルト値が設定されています。 {Address} : {temp}");
            }
        }
    }

    private void Measuring_time()
    {
        if ((DateTime.Now - _lastLogTime).TotalSeconds > 5)
        {
            _lastLogTime = DateTime.Now;
        }
    }

    // 🔹 FPS推定処理
    private void UpdateEstimatedFps()
    {
        var now = DateTime.Now;
        var delta = (now - _lastReceiveTime).TotalSeconds;
        _lastReceiveTime = now;

        if (delta > 0)
        {
            double currentFps = 1.0 / delta;
            EstimatedFps = (EstimatedFps * 0.9) + (currentFps * 0.1); // 移動平均で安定化
        }
    }
}

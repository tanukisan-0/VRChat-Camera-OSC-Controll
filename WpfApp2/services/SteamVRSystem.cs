
using System.Runtime.InteropServices;
using System.Windows.Threading;

using Valve.VR;
using Serilog;

public class SteamVRSystem : IDisposable
{
    private CVRSystem? VRSystem;
    private readonly DispatcherTimer VRControllerPoller;
    private readonly int waitTime = 2000;
    private readonly int maxRetry = 5;

    public ControllerInputsStructure ControllerInputs { get; private set; } = new ControllerInputsStructure();

    public class ControllerInputsStructure
    {
        public VRControllerState_t LeftController { get; set; }
        public VRControllerState_t RightController { get; set; }
    }

    public bool Is_Running => VRSystem != null;

    public SteamVRSystem(int targetFps = 60)
    {
        VRControllerPoller = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / targetFps)
        };
        VRControllerPoller.Tick += VRControllerPoller_Tick;

        Initialization_VR(waitTime, maxRetry);
    }

    public void ControllerPollerStart() => VRControllerPoller.Start();
    public void ControllerPollerStop() => VRControllerPoller.Stop();

    /// <summary>
    /// SteamVR 初期化
    /// </summary>
    private void Initialization_VR(int waitTime, int maxRetry)
    {
        for (int attempt = 1; attempt <= maxRetry; attempt++)
        {
            try
            {
                EVRInitError error = EVRInitError.None;
                var system = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);

                if (error == EVRInitError.None && system != null)
                {
                    VRSystem = system;
                    Log.Information("OpenVR 初期化に成功しました");
                    return;
                }
                else
                {
                    Log.Warning($"OpenVRの初期化に失敗しました: {error} ({attempt}/{maxRetry})");
                    OpenVR.Shutdown();
                    System.Threading.Thread.Sleep(waitTime);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Initialization_VR で例外発生");
            }
        }

        Log.Error("OpenVR 初期化に失敗し、リトライ回数を超えました");
        VRSystem = null;
    }

    /// <summary>
    /// DispatcherTimer Tick 内で呼び出す
    /// </summary>
    private void VRControllerPoller_Tick(object? sender, EventArgs e)
    {
        try
        {
            CheckVRSystem();
            GetControllerInputs();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "VRControllerPoller_Tick 内で例外発生。SteamVR が落ちている可能性があります");
            VRSystem = null;
        }
    }

    /// <summary>
    /// SteamVR が落ちている場合は再初期化
    /// </summary>
    private void CheckVRSystem()
    {
        if (VRSystem == null)
        {
            Log.Information("SteamVR が見つかりません。再初期化を試みます...");
            Initialization_VR(waitTime, 1);
        }
    }

    /// <summary>
    /// コントローラ入力取得
    /// </summary>
    private void GetControllerInputs()
    {
        if (VRSystem == null) return;

        try
        {
            for (uint deviceIndex = 0; deviceIndex < OpenVR.k_unMaxTrackedDeviceCount; deviceIndex++)
            {
                var deviceClass = VRSystem.GetTrackedDeviceClass(deviceIndex);

                if (deviceClass != ETrackedDeviceClass.Controller)
                    continue;

                VRControllerState_t state = new VRControllerState_t();
                uint size = (uint)Marshal.SizeOf(typeof(VRControllerState_t));

                if (VRSystem.GetControllerState(deviceIndex, ref state, size))
                {
                    var role = VRSystem.GetControllerRoleForTrackedDeviceIndex(deviceIndex);

                    if (role == ETrackedControllerRole.LeftHand)
                        ControllerInputs.LeftController = state;
                    else if (role == ETrackedControllerRole.RightHand)
                        ControllerInputs.RightController = state;

                    // ログは必要に応じて出力
                    // Log.Information($"Controller[{deviceIndex}] Role:{role} Buttons:{state.ulButtonPressed:X}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetControllerInputs で例外発生");
            VRSystem = null;
        }
    }

    /// <summary>
    /// VRイベント処理
    /// </summary>
    private void ProcessVREvents()
    {
        if (VRSystem == null) return;

        VREvent_t vrEvent = new VREvent_t();
        while (VRSystem.PollNextEvent(ref vrEvent, (uint)Marshal.SizeOf(typeof(VREvent_t))))
        {
            Log.Information($"VR Event: {vrEvent.eventType}");
        }
    }

    /// <summary>
    /// 安全に終了する
    /// </summary>
    public void Dispose()
    {
        ControllerPollerStop();

        if (VRSystem != null)
        {
            OpenVR.Shutdown();
            VRSystem = null;
            Log.Information("OpenVR をシャットダウンしました");
        }
    }
}

using System;
using System.IO;
using Newtonsoft.Json;
using Serilog;

public class Configs
{
    public bool IsReadConfigCompleted { get; private set; } = false;
    public JsonConfigsStructure? Config { get; private set; }

    /// <summary>
    /// JSON構造体
    /// </summary>
    public class JsonConfigsStructure
    {
        public OSCConfig Ports { get; set; } = new OSCConfig();
        public OutputMapping Mapping { get; set; } = new OutputMapping();
        public string ParameterName { get; set; } = "";
        public bool ParameterToggleOrPush { get; set; } = false; // true = Toggle
        public CollarConfig Collars = new CollarConfig("FFFFFF", "FFFFFF");
        public AnotherSettings anotherSettings = new AnotherSettings();
    }

    public class CollarConfig
    {
        public string MainCollar { get; set; }
        public string SubCollar { get; set; }
        public CollarConfig(string _MainCollar, string _SubCollar)
        {
            MainCollar = _MainCollar;
            SubCollar = _SubCollar;
        }
    }

    public class OSCConfig
    {
        public int Receive_Port { get; set; } = 9001;
        public int Send_Port { get; set; } = 9000;
        public string IPv4_Address { get; set; } = "127.0.0.1";
    }

    public class OutputMapping
    {
        public CellDouble Zoom { get; set; } = new CellDouble(1, 2, 50, 45, 150, 20);
        public CellDouble Exposure { get; set; } = new CellDouble(1, 2, 50, 0, 4, -10);
        public CellDouble FocalDistance { get; set; } = new CellDouble(1, 2, 50, 1.5, 10, 0);
        public CellDouble Aperture { get; set; } = new CellDouble(1, 2, 50, 15, 32, 1.4);
        public CellBool Change_Controll { get; set; } = new CellBool(1, 0);
    }

    public class CellDouble
    {
        public int Axis { get; set; }
        public int NeedType { get; set; } // 0 = Anything, 1 = Bool, 2 = float range
        public bool Reverse { get; set; }
        public double Sensitivity { get; set; }
        public double Default { get; set; }
        public double Max { get; set; }
        public double Min { get; set; }

        public CellDouble() { } // JSON用

        public CellDouble(int aixs, int needType, double sensitivity, double @default, double max, double min, bool reverse = false)
        {
            Axis = aixs;
            NeedType = needType;
            Sensitivity = sensitivity;
            Default = @default;
            Max = max;
            Min = min;
            Reverse = reverse;
        }
    }

    public class CellBool
    {
        public int Aixs { get; set; }
        public int NeedType { get; set; } // 0 = Anything, 1 = Bool, 2 = float range

        public CellBool() { } // JSON用

        public CellBool(int aixs, int needType)
        {
            Aixs = aixs;
            NeedType = needType;
        }
    }

    public class AnotherSettings
    {
        public bool StopWithBothHandsGrip { get; set; } = false;
        public double UpdateFrequency { get; set; } = 2;
    }
    /// <summary>
    /// 設定ファイルを読み込む
    /// </summary>
    public void ReadConfig()
    {
        IsReadConfigCompleted = false;
        try
        {
            Log.Information("設定ファイルを探します...");

            string directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configs");
            string filePath = Path.Combine(directoryPath, "config.json");

            if (File.Exists(filePath))
            {
                Log.Information("設定ファイルが見つかりました。");
                string jsonString = File.ReadAllText(filePath);

                Config = JsonConvert.DeserializeObject<JsonConfigsStructure>(jsonString);

                if (Config != null)
                {
                    Log.Information("設定ファイルを読み込みました。");
                    IsReadConfigCompleted = true;
                }
                else
                {
                    Log.Warning("設定ファイルを読み込めませんでした。デフォルト設定を作成します。");
                    Config = new JsonConfigsStructure();
                    WriteConfig(Config);
                    IsReadConfigCompleted = true;
                }
            }
            else
            {
                Log.Warning("設定ファイルが見つかりません。デフォルト設定を作成します。");
                Config = new JsonConfigsStructure();
                WriteConfig(Config);
                IsReadConfigCompleted = true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ReadConfig で例外発生");
            Config = new JsonConfigsStructure();
            IsReadConfigCompleted = true;
        }
    }

    /// <summary>
    /// 設定ファイルを書き込む
    /// </summary>
    public void WriteConfig(JsonConfigsStructure config)
    {
        Config = config;

        try
        {
            string directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configs");
            string filePath = Path.Combine(directoryPath, "config.json");

            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            string jsonString = JsonConvert.SerializeObject(Config, Formatting.Indented);
            File.WriteAllText(filePath, jsonString);

            Log.Information($"設定を保存しました: {filePath}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "WriteConfig で例外発生");
        }
    }
}

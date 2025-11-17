// ============================================================================
// C# 使用範例: MesMiddleware.LabViewBridge.dll
// ============================================================================
// 此檔案展示如何在 C# 程式中使用 LabViewBridge DLL (模擬 LabVIEW 行為)
// ============================================================================

using System;
using System.Threading;
using MesMiddleware.LabViewBridge;

namespace MesMiddleware.LabViewBridge.Examples
{
    /// <summary>
    /// 範例 1: 簡單的檢驗資料上傳
    /// </summary>
    public class Example1_SimpleInspectionUpload
    {
        public static void Run()
        {
            Console.WriteLine("=== 範例 1: 簡單的檢驗資料上傳 ===\n");

            // 1. 建立 Bridge 實例
            using (var bridge = new MesMiddlewareBridge())
            {
                // 2. 初始化
                bool initSuccess = bridge.Initialize();
                if (!initSuccess)
                {
                    Console.WriteLine("初始化失敗: " + bridge.LastError);
                    return;
                }
                Console.WriteLine("✓ 初始化成功");

                // 3. 建立 JSON 檢驗資料
                string inspectionJson = @"{
                    ""RowNo"": ""ROW_001"",
                    ""ProcName"": ""盲孔檢驗"",
                    ""DevName"": ""AOI-MACHINE-01"",
                    ""UserName"": ""操作員A"",
                    ""WorkClass"": ""日班"",
                    ""TraceCode"": ""TRACE123456"",
                    ""LotNo"": null,
                    ""ParamData"": [
                        {
                            ""Name"": ""孔徑_X"",
                            ""Value"": ""0.25"",
                            ""Unit"": ""mm"",
                            ""Status"": ""Pass""
                        },
                        {
                            ""Name"": ""孔徑_Y"",
                            ""Value"": ""0.26"",
                            ""Unit"": ""mm"",
                            ""Status"": ""Pass""
                        }
                    ],
                    ""Benchmarks"": [
                        {
                            ""Name"": ""孔徑_USL"",
                            ""Value"": ""0.30"",
                            ""Unit"": ""mm""
                        },
                        {
                            ""Name"": ""孔徑_LSL"",
                            ""Value"": ""0.20"",
                            ""Unit"": ""mm""
                        }
                    ],
                    ""OtherData"": [
                        {
                            ""Key"": ""Temperature"",
                            ""Value"": ""25.5""
                        }
                    ],
                    ""InspectionTime"": """ + DateTime.UtcNow.ToString("o") + @"""
                }";

                // 4. 寫入檢驗資料
                bool writeSuccess = bridge.WriteInspectionData(inspectionJson);
                if (writeSuccess)
                {
                    Console.WriteLine("✓ 檢驗資料上傳成功");
                }
                else
                {
                    Console.WriteLine("✗ 檢驗資料上傳失敗: " + bridge.LastError);
                }

                Console.WriteLine("\n按任意鍵繼續...");
                Console.ReadKey();
            }
        }
    }

    /// <summary>
    /// 範例 2: 接收設備指令並回覆確認
    /// </summary>
    public class Example2_ReceiveCommands
    {
        private static MesMiddlewareBridge _bridge;

        public static void Run()
        {
            Console.WriteLine("=== 範例 2: 接收設備指令並回覆確認 ===\n");

            _bridge = new MesMiddlewareBridge();

            try
            {
                // 1. 初始化
                if (!_bridge.Initialize())
                {
                    Console.WriteLine("初始化失敗: " + _bridge.LastError);
                    return;
                }
                Console.WriteLine("✓ 初始化成功");

                // 2. 註冊指令接收回調
                _bridge.RegisterEquipmentCommandCallback(OnCommandReceived);
                Console.WriteLine("✓ 已註冊指令回調");

                // 3. 啟動監控
                if (!_bridge.StartMonitoring())
                {
                    Console.WriteLine("啟動監控失敗: " + _bridge.LastError);
                    return;
                }
                Console.WriteLine("✓ 監控已啟動");
                Console.WriteLine("\n正在等待中介軟體發送指令...");
                Console.WriteLine("(請確保中介軟體服務正在執行)");
                Console.WriteLine("\n按 'Q' 鍵退出\n");

                // 4. 主迴圈 (等待使用者按 Q 退出)
                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Q)
                            break;
                    }

                    Thread.Sleep(100);
                }

                // 5. 清理
                Console.WriteLine("\n正在停止監控...");
                _bridge.StopMonitoring();
                Console.WriteLine("✓ 監控已停止");
            }
            finally
            {
                _bridge?.Dispose();
            }
        }

        /// <summary>
        /// 指令接收回調函數
        /// </summary>
        private static void OnCommandReceived(string jsonCommand)
        {
            Console.WriteLine("\n【收到指令】");
            Console.WriteLine(jsonCommand);
            Console.WriteLine();

            try
            {
                // 在實際應用中,應該解析 JSON 並執行對應操作
                // 這裡簡化為直接回覆成功確認

                // 假設從 JSON 中提取 CommandId (簡化範例,實際應使用 JSON 解析器)
                string commandId = ExtractCommandId(jsonCommand);

                // 建立確認 JSON
                string ackJson = string.Format(@"{{
                    ""CommandId"": ""{0}"",
                    ""Status"": ""Success"",
                    ""Message"": ""指令已成功執行"",
                    ""AcknowledgedAt"": ""{1}""
                }}", commandId, DateTime.UtcNow.ToString("o"));

                // 回傳確認
                bool ackSuccess = _bridge.WriteCommandAcknowledgment(ackJson);
                if (ackSuccess)
                {
                    Console.WriteLine("✓ 已回傳指令確認");
                }
                else
                {
                    Console.WriteLine("✗ 回傳確認失敗: " + _bridge.LastError);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("✗ 處理指令時發生錯誤: " + ex.Message);
            }
        }

        /// <summary>
        /// 簡化的 CommandId 提取 (實際應使用 JSON 解析器)
        /// </summary>
        private static string ExtractCommandId(string json)
        {
            // 簡化範例: 尋找 "CommandId": "guid" 模式
            int startIndex = json.IndexOf("\"CommandId\"");
            if (startIndex < 0) return Guid.Empty.ToString();

            startIndex = json.IndexOf("\"", startIndex + 13); // 跳過 "CommandId": "
            int endIndex = json.IndexOf("\"", startIndex + 1);

            if (startIndex < 0 || endIndex < 0) return Guid.Empty.ToString();

            return json.Substring(startIndex + 1, endIndex - startIndex - 1);
        }
    }

    /// <summary>
    /// 範例 3: 雙向通訊完整流程
    /// </summary>
    public class Example3_BidirectionalCommunication
    {
        private static MesMiddlewareBridge _bridge;
        private static int _uploadCount = 0;

        public static void Run()
        {
            Console.WriteLine("=== 範例 3: 雙向通訊完整流程 ===\n");

            _bridge = new MesMiddlewareBridge();

            try
            {
                // 1. 初始化
                if (!_bridge.Initialize())
                {
                    Console.WriteLine("初始化失敗: " + _bridge.LastError);
                    return;
                }
                Console.WriteLine("✓ 初始化成功");

                // 2. 註冊回調
                _bridge.RegisterEquipmentCommandCallback(OnCommandReceived);
                Console.WriteLine("✓ 已註冊指令回調");

                // 3. 啟動監控
                if (!_bridge.StartMonitoring())
                {
                    Console.WriteLine("啟動監控失敗: " + _bridge.LastError);
                    return;
                }
                Console.WriteLine("✓ 監控已啟動");

                Console.WriteLine("\n操作說明:");
                Console.WriteLine("  [U] - 上傳檢驗資料");
                Console.WriteLine("  [Q] - 退出程式");
                Console.WriteLine("\n同時等待中介軟體發送指令...\n");

                // 4. 主迴圈
                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Q)
                            break;
                        else if (key.Key == ConsoleKey.U)
                            UploadInspectionData();
                    }

                    Thread.Sleep(100);
                }

                // 5. 清理
                Console.WriteLine("\n正在停止監控...");
                _bridge.StopMonitoring();
                Console.WriteLine("✓ 監控已停止");
            }
            finally
            {
                _bridge?.Dispose();
            }
        }

        private static void UploadInspectionData()
        {
            _uploadCount++;
            string inspectionJson = string.Format(@"{{
                ""RowNo"": ""ROW_{0:D3}"",
                ""ProcName"": ""盲孔檢驗"",
                ""DevName"": ""AOI-MACHINE-01"",
                ""UserName"": ""操作員A"",
                ""WorkClass"": ""日班"",
                ""TraceCode"": ""TRACE{0:D6}"",
                ""ParamData"": [
                    {{
                        ""Name"": ""孔徑"",
                        ""Value"": ""{1:F2}"",
                        ""Unit"": ""mm"",
                        ""Status"": ""Pass""
                    }}
                ],
                ""Benchmarks"": [],
                ""OtherData"": [],
                ""InspectionTime"": ""{2}""
            }}", _uploadCount, 0.25 + (_uploadCount * 0.01), DateTime.UtcNow.ToString("o"));

            bool success = _bridge.WriteInspectionData(inspectionJson);
            if (success)
            {
                Console.WriteLine("✓ 檢驗資料 #{0} 上傳成功", _uploadCount);
            }
            else
            {
                Console.WriteLine("✗ 上傳失敗: " + _bridge.LastError);
            }
        }

        private static void OnCommandReceived(string jsonCommand)
        {
            Console.WriteLine("\n【收到指令】");
            Console.WriteLine(jsonCommand);

            try
            {
                string commandId = ExtractCommandId(jsonCommand);
                string ackJson = string.Format(@"{{
                    ""CommandId"": ""{0}"",
                    ""Status"": ""Success"",
                    ""Message"": ""指令已成功執行"",
                    ""AcknowledgedAt"": ""{1}""
                }}", commandId, DateTime.UtcNow.ToString("o"));

                _bridge.WriteCommandAcknowledgment(ackJson);
                Console.WriteLine("✓ 已回傳確認\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine("✗ 處理指令失敗: " + ex.Message + "\n");
            }
        }

        private static string ExtractCommandId(string json)
        {
            int startIndex = json.IndexOf("\"CommandId\"");
            if (startIndex < 0) return Guid.Empty.ToString();

            startIndex = json.IndexOf("\"", startIndex + 13);
            int endIndex = json.IndexOf("\"", startIndex + 1);

            if (startIndex < 0 || endIndex < 0) return Guid.Empty.ToString();

            return json.Substring(startIndex + 1, endIndex - startIndex - 1);
        }
    }

    /// <summary>
    /// 主程式進入點
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "MesMiddleware LabViewBridge - C# 使用範例";

            while (true)
            {
                Console.Clear();
                Console.WriteLine("╔══════════════════════════════════════════════════╗");
                Console.WriteLine("║  MesMiddleware.LabViewBridge - C# 使用範例      ║");
                Console.WriteLine("╚══════════════════════════════════════════════════╝");
                Console.WriteLine();
                Console.WriteLine("請選擇範例:");
                Console.WriteLine("  [1] 簡單的檢驗資料上傳");
                Console.WriteLine("  [2] 接收設備指令並回覆確認");
                Console.WriteLine("  [3] 雙向通訊完整流程");
                Console.WriteLine("  [Q] 退出");
                Console.WriteLine();
                Console.Write("請選擇 (1/2/3/Q): ");

                var key = Console.ReadKey();
                Console.WriteLine("\n");

                switch (key.Key)
                {
                    case ConsoleKey.D1:
                    case ConsoleKey.NumPad1:
                        Example1_SimpleInspectionUpload.Run();
                        break;

                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        Example2_ReceiveCommands.Run();
                        break;

                    case ConsoleKey.D3:
                    case ConsoleKey.NumPad3:
                        Example3_BidirectionalCommunication.Run();
                        break;

                    case ConsoleKey.Q:
                        Console.WriteLine("再見!");
                        return;

                    default:
                        Console.WriteLine("無效的選擇,請重試...");
                        Thread.Sleep(1000);
                        break;
                }
            }
        }
    }
}

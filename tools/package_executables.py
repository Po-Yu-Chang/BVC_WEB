#!/usr/bin/env python3
"""
打包所有執行檔和操作手冊到發布資料夾
"""
import shutil
import os
from pathlib import Path

def package_executables():
    """打包執行檔"""
    base_dir = Path(__file__).parent

    # 發布資料夾
    release_dir = base_dir / "Release_Package"
    release_dir.mkdir(exist_ok=True)

    print(f"[INFO] Release folder: {release_dir}")

    # 清空舊檔案
    for item in release_dir.iterdir():
        if item.is_file():
            item.unlink()
        elif item.is_dir():
            shutil.rmtree(item)

    # 執行檔路徑
    executables = [
        {
            "name": "Monitor",
            "source": base_dir / "src/MesMiddleware.Monitor/bin/Debug/net9.0-windows",
            "exe": "MesMiddleware.Monitor.exe",
            "description": "中介軟體主程式"
        },
        {
            "name": "DeviceSimulator",
            "source": base_dir / "src/MesMiddleware.DeviceSimulator/bin/Debug/net9.0-windows",
            "exe": "MesMiddleware.DeviceSimulator.exe",
            "description": "設備端模擬器"
        },
        {
            "name": "Simulator",
            "source": base_dir / "src/MesMiddleware.Simulator/bin/Debug/net9.0-windows",
            "exe": "MesMiddleware.Simulator.exe",
            "description": "MES Cloud模擬器"
        }
    ]

    copied_files = []

    # 複製每個執行檔及其依賴
    for app in executables:
        source_dir = app["source"]
        if not source_dir.exists():
            print(f"[WARNING] Source not found: {source_dir}")
            print(f"[INFO] Please build {app['name']} first: dotnet build -c Debug")
            continue

        # 建立子資料夾
        app_folder = release_dir / app["name"]
        app_folder.mkdir(exist_ok=True)

        print(f"\n[INFO] Copying {app['description']} ({app['name']})...")

        # 複製所有檔案
        copied_count = 0
        for item in source_dir.rglob("*"):
            if item.is_file():
                # 排除不需要的檔案
                if any(exclude in item.name for exclude in [".pdb", ".xml", ".deps.json", ".runtimeconfig.dev.json"]):
                    continue

                relative_path = item.relative_to(source_dir)
                dest_file = app_folder / relative_path
                dest_file.parent.mkdir(parents=True, exist_ok=True)

                shutil.copy2(item, dest_file)
                copied_count += 1

        print(f"  - Copied {copied_count} files to {app_folder}")
        copied_files.append({
            "name": app["name"],
            "exe": app["exe"],
            "description": app["description"],
            "folder": app_folder
        })

    # 複製操作手冊
    manual_source = base_dir / "LabVIEW整合操作手冊.docx"
    if manual_source.exists():
        manual_dest = release_dir / "LabVIEW整合操作手冊.docx"
        shutil.copy2(manual_source, manual_dest)
        print(f"\n[INFO] Copied manual: {manual_dest}")

    # 建立 README.txt
    readme_path = release_dir / "README.txt"
    with open(readme_path, 'w', encoding='utf-8') as f:
        f.write("=" * 80 + "\n")
        f.write("MES 中介軟體 Release Package\n")
        f.write("=" * 80 + "\n\n")

        f.write("【檔案說明】\n\n")

        for app in copied_files:
            f.write(f"📁 {app['name']}/\n")
            f.write(f"   - 說明: {app['description']}\n")
            f.write(f"   - 執行檔: {app['exe']}\n")
            f.write(f"   - 啟動方式: 進入資料夾，雙擊 {app['exe']}\n")
            f.write("\n")

        f.write("📄 LabVIEW整合操作手冊.docx\n")
        f.write("   - LabVIEW 開發人員整合參考文件\n")
        f.write("   - 包含 HTTP API 規格、資料格式、整合步驟\n")
        f.write("\n")

        f.write("=" * 80 + "\n")
        f.write("【啟動順序】\n")
        f.write("=" * 80 + "\n\n")

        f.write("情境 1: 完整測試環境 (離線)\n")
        f.write("   1. 啟動 Simulator\\MesMiddleware.Simulator.exe (MES Cloud 模擬器)\n")
        f.write("   2. 啟動 Monitor\\MesMiddleware.Monitor.exe (中介軟體)\n")
        f.write("   3. 啟動 DeviceSimulator\\MesMiddleware.DeviceSimulator.exe (設備模擬器)\n")
        f.write("   4. 在 DeviceSimulator 中測試發送資料\n\n")

        f.write("情境 2: 連接真實 MES Cloud\n")
        f.write("   1. 啟動 Monitor\\MesMiddleware.Monitor.exe\n")
        f.write("   2. 在 Monitor 設定檔 (appsettings.json) 中設定 MES Cloud URL\n")
        f.write("   3. 使用 DeviceSimulator 或 LabVIEW 發送資料\n\n")

        f.write("情境 3: LabVIEW 整合開發\n")
        f.write("   1. 啟動 Monitor\\MesMiddleware.Monitor.exe\n")
        f.write("   2. 參考「LabVIEW整合操作手冊.docx」建立 LabVIEW VI\n")
        f.write("   3. 使用 HTTP POST 發送 JSON 到 http://localhost:5100/api/inspection/submit\n")
        f.write("   4. 檢查 Monitor UI 確認資料接收成功\n\n")

        f.write("=" * 80 + "\n")
        f.write("【詳細操作說明】\n")
        f.write("=" * 80 + "\n\n")

        f.write("請開啟「LabVIEW整合操作手冊.docx」查看完整說明，包含:\n")
        f.write("  - HTTP API 規格\n")
        f.write("  - JSON 資料格式\n")
        f.write("  - LabVIEW 整合步驟\n")
        f.write("  - 錯誤處理機制\n")
        f.write("  - 測試驗證方法\n")
        f.write("  - 常見問題 FAQ\n\n")

        f.write("=" * 80 + "\n")
        f.write("【技術支援】\n")
        f.write("=" * 80 + "\n\n")
        f.write("如有問題請聯繫 MES 系統管理員\n\n")

    print(f"\n[INFO] Created README.txt: {readme_path}")

    # 建立每個執行檔的操作說明
    create_individual_readme(release_dir, copied_files)

    print(f"\n{'='*80}")
    print(f"[SUCCESS] Package completed!")
    print(f"{'='*80}")
    print(f"Output folder: {release_dir}")
    print(f"\nContents:")
    for app in copied_files:
        print(f"  - {app['name']}/ ({app['description']})")
    print(f"  - LabVIEW整合操作手冊.docx")
    print(f"  - README.txt")
    print(f"{'='*80}\n")

def create_individual_readme(release_dir, apps):
    """為每個執行檔建立獨立的操作說明"""

    # Monitor 說明
    monitor_readme = release_dir / "Monitor" / "操作說明.txt"
    with open(monitor_readme, 'w', encoding='utf-8') as f:
        f.write("=" * 80 + "\n")
        f.write("MesMiddleware.Monitor.exe - 中介軟體主程式\n")
        f.write("=" * 80 + "\n\n")

        f.write("【功能說明】\n")
        f.write("  - 接收設備端 (LabVIEW/DeviceSimulator) 的檢測資料\n")
        f.write("  - 上傳資料到 MES Cloud API\n")
        f.write("  - 管理離線佇列 (自動重試失敗的上傳)\n")
        f.write("  - 監聽端口: http://localhost:5100\n\n")

        f.write("【啟動方式】\n")
        f.write("  1. 雙擊 MesMiddleware.Monitor.exe\n")
        f.write("  2. 等待左上角燈號變成綠色 (表示正常運行)\n")
        f.write("  3. 可以最小化到系統列執行\n\n")

        f.write("【介面說明】\n")
        f.write("  - 狀態頁籤: 顯示連線狀態、統計數字 (總數/成功/佇列中)\n")
        f.write("  - 歷史記錄頁籤: 顯示最近 100 筆上傳記錄\n")
        f.write("  - 佇列頁籤: 顯示待重試的離線資料\n")
        f.write("  - 追溯碼驗證頁籤: 查詢特定 TraceCode 的上傳狀態\n\n")

        f.write("【配置檔案】\n")
        f.write("  - 檔案: appsettings.json\n")
        f.write("  - 重要設定:\n")
        f.write("    * WebApi.BaseUrl: MES Cloud API 位址\n")
        f.write("    * WebApi.Username: MES Cloud 帳號\n")
        f.write("    * WebApi.Password: MES Cloud 密碼\n\n")

        f.write("【日誌檔案】\n")
        f.write("  - 位置: logs/monitor-YYYYMMDD.log\n")
        f.write("  - 保留 7 天\n\n")

        f.write("【故障排除】\n")
        f.write("  Q: 燈號顯示紅色?\n")
        f.write("  A: 檢查 MES Cloud 連線設定 (appsettings.json)\n\n")

        f.write("  Q: 設備發送資料但 Monitor 沒收到?\n")
        f.write("  A: 檢查防火牆是否阻擋 5100 端口\n\n")

    print(f"[INFO] Created {monitor_readme}")

    # DeviceSimulator 說明
    simulator_readme = release_dir / "DeviceSimulator" / "操作說明.txt"
    with open(simulator_readme, 'w', encoding='utf-8') as f:
        f.write("=" * 80 + "\n")
        f.write("MesMiddleware.DeviceSimulator.exe - 設備端模擬器\n")
        f.write("=" * 80 + "\n\n")

        f.write("【功能說明】\n")
        f.write("  - 模擬生產設備發送檢測資料\n")
        f.write("  - 用於測試 Monitor 中介軟體\n")
        f.write("  - 支援手動/批量/自動發送模式\n")
        f.write("  - 目標: http://localhost:5100 (Monitor)\n\n")

        f.write("【啟動方式】\n")
        f.write("  1. 先啟動 Monitor.exe\n")
        f.write("  2. 雙擊 MesMiddleware.DeviceSimulator.exe\n")
        f.write("  3. 點擊「測試連接」按鈕確認綠燈\n\n")

        f.write("【操作模式】\n\n")

        f.write("  模式 1: 手動發送 (左側面板)\n")
        f.write("    1. 點擊「生成 TraceCode」按鈕\n")
        f.write("    2. 點擊「生成 LotNo」按鈕\n")
        f.write("    3. 點擊「隨機結果」按鈕\n")
        f.write("    4. 點擊「發送數據」發送到 Monitor\n\n")

        f.write("  模式 2: 批量發送 (中間面板)\n")
        f.write("    1. 調整數量滑桿 (1-100 筆)\n")
        f.write("    2. 調整間隔滑桿 (0-1000 毫秒)\n")
        f.write("    3. 點擊「批量發送」按鈕\n")
        f.write("    4. 等待發送完成提示\n\n")

        f.write("  模式 3: 自動發送 (右側面板)\n")
        f.write("    1. 調整間隔滑桿 (1-60 秒)\n")
        f.write("    2. 點擊「啟動自動發送」按鈕\n")
        f.write("    3. 按鈕變紅色並顯示「停止自動發送」\n")
        f.write("    4. 再次點擊停止\n\n")

        f.write("【發送歷史】\n")
        f.write("  - 底部 DataGrid 顯示最近 100 條記錄\n")
        f.write("  - 欄位: 時間、TraceCode、狀態、耗時(ms)、錯誤訊息\n")
        f.write("  - 點擊「清空」按鈕清除歷史\n\n")

        f.write("【故障排除】\n")
        f.write("  Q: 連接測試顯示紅燈?\n")
        f.write("  A: 確認 Monitor.exe 是否正在運行\n\n")

        f.write("  Q: 發送失敗顯示「網絡錯誤」?\n")
        f.write("  A: Monitor 可能已停止，重新啟動 Monitor.exe\n\n")

    print(f"[INFO] Created {simulator_readme}")

    # MES Simulator 說明
    mes_sim_readme = release_dir / "Simulator" / "操作說明.txt"
    with open(mes_sim_readme, 'w', encoding='utf-8') as f:
        f.write("=" * 80 + "\n")
        f.write("MesMiddleware.Simulator.exe - MES Cloud 模擬器\n")
        f.write("=" * 80 + "\n\n")

        f.write("【功能說明】\n")
        f.write("  - 模擬 MES Cloud API 伺服器\n")
        f.write("  - 用於離線測試 (無需連接真實 MES Cloud)\n")
        f.write("  - 記錄所有上傳請求和資料\n")
        f.write("  - 監聽端口: http://localhost:5200\n\n")

        f.write("【啟動方式】\n")
        f.write("  1. 雙擊 MesMiddleware.Simulator.exe\n")
        f.write("  2. 查看「請求記錄」頁籤確認運行中\n")
        f.write("  3. 可以最小化到系統列執行\n\n")

        f.write("【配合 Monitor 使用】\n")
        f.write("  1. 修改 Monitor 的 appsettings.json:\n")
        f.write("     \"WebApi\": {\n")
        f.write("       \"BaseUrl\": \"http://localhost:5200\"\n")
        f.write("     }\n")
        f.write("  2. 重新啟動 Monitor.exe\n")
        f.write("  3. Monitor 會將資料上傳到 Simulator 而非真實 MES Cloud\n\n")

        f.write("【介面說明】\n")
        f.write("  - 請求記錄頁籤: 顯示所有 HTTP 請求\n")
        f.write("  - 追溯資料頁籤: 顯示所有上傳的檢測資料\n")
        f.write("  - 設備登入頁籤: 顯示登入記錄\n\n")

        f.write("【測試功能】\n")
        f.write("  - 模擬成功: 預設行為，回傳 200 OK\n")
        f.write("  - 模擬失敗: 勾選後回傳 500 錯誤，測試 Monitor 重試機制\n")
        f.write("  - 模擬延遲: 設定回應延遲，測試逾時處理\n\n")

        f.write("【應用場景】\n")
        f.write("  1. 開發階段: 離線開發和測試\n")
        f.write("  2. 功能測試: 驗證資料格式和 API 規格\n")
        f.write("  3. 重試測試: 勾選「模擬失敗」測試 Monitor 重試機制\n")
        f.write("  4. 資料檢視: 查看上傳的完整 JSON 資料\n\n")

    print(f"[INFO] Created {mes_sim_readme}")

if __name__ == "__main__":
    package_executables()

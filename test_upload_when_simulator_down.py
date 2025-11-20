#!/usr/bin/env python3
"""
測試當 Simulator 關閉時，資料是否會進入佇列
"""
import requests
import json
import time

# Monitor API endpoint
MONITOR_URL = "http://localhost:5100/api/inspection/submit"

# 測試資料
test_data = {
    "rowNo": "99",
    "traceCode": "TEST-SIMULATOR-DOWN-001",
    "lotNo": "LOT-TEST-001",
    "procName": "TEST-PROC",
    "devName": "TEST-DEVICE",
    "userName": "TEST-USER",
    "workClass": "A",
    "inspectionTime": "2025-01-20T10:30:00",
    "paramData": [
        {
            "code": "Result",
            "name": "檢測結果",
            "value": "PASS",
            "unit": "",
            "desc": "測試結果"
        }
    ],
    "benchmarks": [],
    "otherData": []
}

def test_upload():
    """發送測試資料到 Monitor"""
    print("=" * 60)
    print("測試：當 Simulator 關閉時上傳資料")
    print("=" * 60)
    print("\n請確保：")
    print("  1. Monitor 正在運行 (http://localhost:5100)")
    print("  2. Simulator 已經關閉 (http://localhost:5200)")
    print("\n按 Enter 繼續測試...")
    input()

    print("\n[INFO] 發送測試資料到 Monitor...")
    print(f"[INFO] URL: {MONITOR_URL}")
    print(f"[INFO] TraceCode: {test_data['traceCode']}")

    try:
        response = requests.post(
            MONITOR_URL,
            json=test_data,
            headers={"Content-Type": "application/json"},
            timeout=10
        )

        print(f"\n[RESPONSE] Status Code: {response.status_code}")
        print(f"[RESPONSE] Body: {response.text}")

        if response.status_code == 200:
            print("\n[SUCCESS] Monitor 接收資料成功！")
            print("\n請檢查：")
            print("  1. Monitor 日誌是否顯示「Failed to upload inspection data」")
            print("  2. 資料是否已存入 SQLite 佇列（UploadQueue 表格）")
            print("\n等待 3 秒後檢查資料庫...")
            time.sleep(3)

            # 檢查資料庫
            import sqlite3
            from pathlib import Path

            db_path = Path("src/MesMiddleware.Monitor/bin/Debug/net9.0-windows/monitor.db")
            if db_path.exists():
                conn = sqlite3.connect(str(db_path))
                cursor = conn.cursor()

                cursor.execute("SELECT COUNT(*) FROM UploadQueue WHERE TraceCode = ?", (test_data['traceCode'],))
                count = cursor.fetchone()[0]

                cursor.execute("SELECT * FROM UploadQueue WHERE TraceCode = ? ORDER BY Id DESC LIMIT 1", (test_data['traceCode'],))
                queue_item = cursor.fetchone()

                conn.close()

                print("\n" + "=" * 60)
                print("資料庫檢查結果:")
                print("=" * 60)
                print(f"佇列中的測試資料: {count} 筆")
                if queue_item:
                    print(f"\n最新佇列項目:")
                    print(f"  - ID: {queue_item[0]}")
                    print(f"  - TraceCode: {queue_item[1]}")
                    print(f"  - Status: {queue_item[16]}")
                    print(f"  - RetryCount: {queue_item[13]}")
                    print(f"  - LastError: {queue_item[15]}")
                else:
                    print("\n[WARNING] 找不到測試資料！資料沒有存入佇列！")
            else:
                print(f"\n[ERROR] 找不到資料庫: {db_path}")

        else:
            print(f"\n[ERROR] Monitor 回應異常")

    except requests.exceptions.ConnectionError:
        print("\n[ERROR] 無法連接到 Monitor，請確認 Monitor 是否正在運行")
    except Exception as e:
        print(f"\n[ERROR] 發生錯誤: {e}")

if __name__ == "__main__":
    test_upload()

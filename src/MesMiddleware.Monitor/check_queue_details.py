#!/usr/bin/env python3
"""
檢查佇列詳細資料
"""
import sqlite3
from pathlib import Path
from datetime import datetime

# 資料庫路徑
DB_PATH = Path(__file__).parent / "bin" / "Debug" / "net9.0-windows" / "monitor.db"

def check_queue_details():
    """檢查佇列詳細資料"""
    if not DB_PATH.exists():
        print(f"[ERROR] 資料庫不存在: {DB_PATH}")
        return

    try:
        conn = sqlite3.connect(str(DB_PATH))
        cursor = conn.cursor()

        # 查詢 UploadQueue
        cursor.execute("SELECT * FROM UploadQueue ORDER BY Id")
        queue_items = cursor.fetchall()

        print("=" * 80)
        print("UploadQueue 佇列詳細資料:")
        print("=" * 80)

        if not queue_items:
            print("佇列是空的")
        else:
            for item in queue_items:
                print(f"\n[佇列項目 #{item[0]}]")
                print(f"  TraceCode: {item[1]}")
                print(f"  LotNo: {item[2]}")
                print(f"  RowNo: {item[3]}")
                print(f"  Status: {item[16]}")
                print(f"  RetryCount: {item[13]}")
                print(f"  CreatedAt: {item[12]}")
                print(f"  NextRetryAt: {item[15]}")
                print(f"  LastError: {item[17]}")

                # 計算距離下次重試的時間
                if item[15]:
                    try:
                        next_retry = datetime.fromisoformat(item[15].replace('Z', '+00:00'))
                        now = datetime.utcnow()
                        diff = (next_retry - now).total_seconds()
                        if diff > 0:
                            print(f"  距離下次重試: {diff:.1f} 秒")
                        else:
                            print(f"  [WARNING] 已超過重試時間 {abs(diff):.1f} 秒！")
                    except:
                        pass

        print("\n" + "=" * 80)

        cursor.close()
        conn.close()

    except sqlite3.Error as e:
        print(f"[ERROR] 資料庫錯誤: {e}")

if __name__ == "__main__":
    check_queue_details()

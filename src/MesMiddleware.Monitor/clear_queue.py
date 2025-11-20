#!/usr/bin/env python3
"""
清除 Monitor SQLite 資料庫中的佇列資料
"""
import sqlite3
import os
from pathlib import Path

# 資料庫路徑
DB_PATH = Path(__file__).parent / "bin" / "Debug" / "net9.0-windows" / "monitor.db"

def clear_queue_data():
    """清除佇列資料"""
    if not DB_PATH.exists():
        print(f"[ERROR] 資料庫不存在: {DB_PATH}")
        return

    print(f"[INFO] 資料庫路徑: {DB_PATH}")

    try:
        # 連接資料庫
        conn = sqlite3.connect(str(DB_PATH))
        cursor = conn.cursor()

        # 檢查佇列資料（刪除前）
        cursor.execute("SELECT COUNT(*) FROM UploadQueue")
        queue_count_before = cursor.fetchone()[0]

        cursor.execute("SELECT COUNT(*) FROM UploadHistory")
        history_count_before = cursor.fetchone()[0]

        print(f"\n[BEFORE] 刪除前統計:")
        print(f"   - UploadQueue (佇列): {queue_count_before} 筆")
        print(f"   - UploadHistory (歷史): {history_count_before} 筆")

        # 清除佇列資料
        cursor.execute("DELETE FROM UploadQueue")
        queue_deleted = cursor.rowcount

        # 清除歷史資料
        cursor.execute("DELETE FROM UploadHistory")
        history_deleted = cursor.rowcount

        # 重置自動增長 ID
        cursor.execute("DELETE FROM sqlite_sequence WHERE name='UploadQueue'")
        cursor.execute("DELETE FROM sqlite_sequence WHERE name='UploadHistory'")

        # 提交變更
        conn.commit()

        # 檢查清除後的資料（確認）
        cursor.execute("SELECT COUNT(*) FROM UploadQueue")
        queue_count_after = cursor.fetchone()[0]

        cursor.execute("SELECT COUNT(*) FROM UploadHistory")
        history_count_after = cursor.fetchone()[0]

        print(f"\n[SUCCESS] 清除完成:")
        print(f"   - UploadQueue 刪除: {queue_deleted} 筆 (剩餘: {queue_count_after} 筆)")
        print(f"   - UploadHistory 刪除: {history_deleted} 筆 (剩餘: {history_count_after} 筆)")
        print(f"   - 自動增長 ID 已重置\n")

        # 關閉連接
        cursor.close()
        conn.close()

    except sqlite3.Error as e:
        print(f"[ERROR] 資料庫錯誤: {e}")
    except Exception as e:
        print(f"[ERROR] 未預期的錯誤: {e}")

if __name__ == "__main__":
    print("=" * 60)
    print("  清除 Monitor SQLite 佇列資料")
    print("=" * 60)
    clear_queue_data()
    print("=" * 60)

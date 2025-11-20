#!/usr/bin/env python3
"""
檢查 Monitor SQLite 資料庫表格結構
"""
import sqlite3
from pathlib import Path

# 資料庫路徑
DB_PATH = Path(__file__).parent / "bin" / "Debug" / "net9.0-windows" / "monitor.db"

def check_tables():
    """檢查表格結構"""
    if not DB_PATH.exists():
        print(f"[ERROR] 資料庫不存在: {DB_PATH}")
        return

    print(f"[INFO] 資料庫路徑: {DB_PATH}\n")

    try:
        conn = sqlite3.connect(str(DB_PATH))
        cursor = conn.cursor()

        # 列出所有表格
        cursor.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")
        tables = cursor.fetchall()

        print("=" * 60)
        print("資料庫表格列表:")
        print("=" * 60)
        for table in tables:
            table_name = table[0]
            print(f"\n[TABLE] {table_name}")

            # 顯示表格結構
            cursor.execute(f"PRAGMA table_info({table_name})")
            columns = cursor.fetchall()

            print("  欄位:")
            for col in columns:
                col_id, col_name, col_type, not_null, default_val, pk = col
                pk_str = " [PRIMARY KEY]" if pk else ""
                null_str = " NOT NULL" if not_null else ""
                print(f"    - {col_name}: {col_type}{null_str}{pk_str}")

            # 顯示資料筆數
            cursor.execute(f"SELECT COUNT(*) FROM {table_name}")
            count = cursor.fetchone()[0]
            print(f"  資料筆數: {count}")

        print("\n" + "=" * 60)

        cursor.close()
        conn.close()

    except sqlite3.Error as e:
        print(f"[ERROR] 資料庫錯誤: {e}")

if __name__ == "__main__":
    check_tables()

#!/usr/bin/env python3
"""
檢查 Monitor 日誌最近 50 行
"""
from pathlib import Path
import os

# 日誌目錄
LOG_DIR = Path(__file__).parent / "logs"

def check_logs():
    """顯示最新的日誌內容"""
    if not LOG_DIR.exists():
        print(f"[ERROR] 日誌目錄不存在: {LOG_DIR}")
        return

    # 找到最新的日誌檔案
    log_files = sorted(LOG_DIR.glob("middleware-monitor-*.log"), key=os.path.getmtime, reverse=True)

    if not log_files:
        print("[ERROR] 找不到日誌檔案")
        return

    latest_log = log_files[0]
    print(f"[INFO] 最新日誌: {latest_log.name}\n")
    print("=" * 80)
    print("最近 50 行日誌:")
    print("=" * 80)

    try:
        with open(latest_log, 'r', encoding='utf-8') as f:
            lines = f.readlines()
            # 顯示最後 50 行
            for line in lines[-50:]:
                print(line.rstrip())
    except Exception as e:
        print(f"[ERROR] 讀取日誌失敗: {e}")

if __name__ == "__main__":
    check_logs()

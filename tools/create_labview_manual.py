#!/usr/bin/env python3
"""
建立 LabVIEW 人員參考手冊 (Word 格式)
"""
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_PARAGRAPH_ALIGNMENT
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
import os
from pathlib import Path

def add_heading_with_color(doc, text, level, color_rgb=(0, 112, 192)):
    """新增帶顏色的標題"""
    heading = doc.add_heading(text, level=level)
    for run in heading.runs:
        run.font.color.rgb = RGBColor(*color_rgb)
        run.font.bold = True
    return heading

def add_code_block(doc, code, language="json"):
    """新增程式碼區塊"""
    para = doc.add_paragraph()
    para.paragraph_format.left_indent = Inches(0.5)
    para.paragraph_format.space_before = Pt(6)
    para.paragraph_format.space_after = Pt(6)

    # 設定單色背景
    shading_elm = OxmlElement('w:shd')
    shading_elm.set(qn('w:fill'), 'F0F0F0')
    para._element.get_or_add_pPr().append(shading_elm)

    run = para.add_run(code)
    run.font.name = 'Consolas'
    run.font.size = Pt(9)
    run.font.color.rgb = RGBColor(0, 0, 0)

    return para

def add_table_with_header(doc, headers, rows):
    """新增帶標題的表格"""
    table = doc.add_table(rows=1 + len(rows), cols=len(headers))
    table.style = 'Light Grid Accent 1'

    # 設定標題行
    header_cells = table.rows[0].cells
    for i, header in enumerate(headers):
        header_cells[i].text = header
        # 標題行加粗
        for paragraph in header_cells[i].paragraphs:
            for run in paragraph.runs:
                run.font.bold = True

    # 設定資料行
    for row_idx, row_data in enumerate(rows, start=1):
        cells = table.rows[row_idx].cells
        for col_idx, value in enumerate(row_data):
            cells[col_idx].text = value

    return table

def create_labview_manual():
    """建立 LabVIEW 操作手冊"""
    doc = Document()

    # 設定文件屬性
    doc.core_properties.title = "MES 中介軟體 LabVIEW 整合手冊"
    doc.core_properties.author = "Claude Code"
    doc.core_properties.subject = "LabVIEW Web Service 整合指南"

    # ==================== 封面 ====================
    title = doc.add_heading('MES 中介軟體', level=0)
    title.alignment = WD_PARAGRAPH_ALIGNMENT.CENTER

    subtitle = doc.add_paragraph('LabVIEW Web Service 整合操作手冊')
    subtitle.alignment = WD_PARAGRAPH_ALIGNMENT.CENTER
    subtitle_run = subtitle.runs[0]
    subtitle_run.font.size = Pt(18)
    subtitle_run.font.color.rgb = RGBColor(0, 112, 192)

    doc.add_paragraph()
    version_para = doc.add_paragraph('版本: v2.0 (Web API 架構)')
    version_para.alignment = WD_PARAGRAPH_ALIGNMENT.CENTER

    date_para = doc.add_paragraph('日期: 2025-01-20')
    date_para.alignment = WD_PARAGRAPH_ALIGNMENT.CENTER

    doc.add_page_break()

    # ==================== 目錄 ====================
    add_heading_with_color(doc, '目錄', 1)
    doc.add_paragraph('1. 系統架構說明', style='List Number')
    doc.add_paragraph('2. HTTP API 規格', style='List Number')
    doc.add_paragraph('3. LabVIEW 整合步驟', style='List Number')
    doc.add_paragraph('4. 資料格式說明', style='List Number')
    doc.add_paragraph('5. 錯誤處理機制', style='List Number')
    doc.add_paragraph('6. 測試與驗證', style='List Number')
    doc.add_paragraph('7. 常見問題 FAQ', style='List Number')
    doc.add_paragraph('8. 附錄：執行檔說明', style='List Number')

    doc.add_page_break()

    # ==================== 第1章：系統架構 ====================
    add_heading_with_color(doc, '第1章 系統架構說明', 1)

    doc.add_paragraph('MES 中介軟體採用 Web API 架構，LabVIEW 設備透過 HTTP POST 方式上傳檢測資料。')

    add_heading_with_color(doc, '1.1 整體架構', 2, (192, 0, 0))
    doc.add_paragraph()

    # 架構圖文字描述
    arch_para = doc.add_paragraph()
    arch_para.add_run('[LabVIEW 設備] ').bold = True
    arch_para.add_run('→ HTTP POST → ')
    arch_para.add_run('[Monitor 中介軟體] ').bold = True
    arch_para.add_run('→ HTTP POST → ')
    arch_para.add_run('[MES Cloud API]').bold = True

    doc.add_paragraph()

    add_heading_with_color(doc, '1.2 資料流程', 2, (192, 0, 0))
    doc.add_paragraph('1. LabVIEW 檢測設備完成檢測', style='List Number')
    doc.add_paragraph('2. 組裝 JSON 資料格式', style='List Number')
    doc.add_paragraph('3. 透過 HTTP POST 發送到 Monitor（預設: http://localhost:5100）', style='List Number')
    doc.add_paragraph('4. Monitor 驗證資料格式', style='List Number')
    doc.add_paragraph('5. Monitor 上傳到 MES Cloud', style='List Number')
    doc.add_paragraph('6. 若上傳失敗，存入 SQLite 佇列自動重試（無限重試）', style='List Number')

    doc.add_page_break()

    # ==================== 第2章：HTTP API 規格 ====================
    add_heading_with_color(doc, '第2章 HTTP API 規格', 1)

    add_heading_with_color(doc, '2.1 端點資訊', 2, (192, 0, 0))

    # API 端點表格
    api_table_data = [
        ('端點 URL', 'http://localhost:5100/api/inspection/submit'),
        ('HTTP 方法', 'POST'),
        ('Content-Type', 'application/json'),
        ('字元編碼', 'UTF-8'),
        ('逾時設定', '30 秒（建議）')
    ]

    table = doc.add_table(rows=len(api_table_data), cols=2)
    table.style = 'Light Grid Accent 1'

    for row_idx, (key, value) in enumerate(api_table_data):
        cells = table.rows[row_idx].cells
        cells[0].text = key
        cells[1].text = value
        # 第一欄加粗
        for para in cells[0].paragraphs:
            for run in para.runs:
                run.font.bold = True

    doc.add_paragraph()

    add_heading_with_color(doc, '2.2 LabVIEW HTTP 設定', 2, (192, 0, 0))

    doc.add_paragraph('在 LabVIEW 中使用 "HTTP Client" 或 "POST VI" 發送請求：')

    labview_steps = [
        '使用 "HTTP Client POST.vi"',
        '設定 URL = "http://localhost:5100/api/inspection/submit"',
        '設定 Header:',
        '  - Content-Type: application/json',
        '  - Accept: application/json',
        '組裝 JSON String（使用 Flatten To JSON.vi）',
        'POST Request 執行',
        '檢查 Status Code = 200 表示成功'
    ]

    for step in labview_steps:
        doc.add_paragraph(step, style='List Bullet')

    doc.add_page_break()

    # ==================== 第3章：LabVIEW 整合步驟 ====================
    add_heading_with_color(doc, '第3章 LabVIEW 整合步驟', 1)

    add_heading_with_color(doc, '3.1 前置準備', 2, (192, 0, 0))
    doc.add_paragraph('1. 確認 Monitor.exe 正在執行（綠燈表示正常）')
    doc.add_paragraph('2. 確認網路連線正常')
    doc.add_paragraph('3. 準備檢測資料（TraceCode, LotNo, 參數值等）')

    doc.add_paragraph()

    add_heading_with_color(doc, '3.2 LabVIEW VI 結構建議', 2, (192, 0, 0))

    doc.add_paragraph('建議的 LabVIEW 程式結構：')

    vi_structure = """
[主迴圈]
  ├─ [檢測邏輯]
  │   ├─ 讀取感測器數值
  │   ├─ 計算檢測結果 (OK/NG)
  │   └─ 產生 TraceCode/LotNo
  │
  ├─ [組裝 JSON]
  │   ├─ 建立 Cluster (對應 JSON 結構)
  │   ├─ 使用 "Flatten To JSON.vi"
  │   └─ 轉換為 String
  │
  ├─ [HTTP POST]
  │   ├─ 呼叫 "HTTP Client POST.vi"
  │   ├─ 傳入 URL, Headers, Body
  │   └─ 讀取 Response Status Code
  │
  └─ [錯誤處理]
      ├─ Status Code = 200 → 成功
      ├─ Status Code ≠ 200 → 失敗（Monitor 會自動重試）
      └─ Timeout/Network Error → 記錄錯誤
"""
    add_code_block(doc, vi_structure)

    doc.add_page_break()

    # ==================== 第4章：資料格式說明 ====================
    add_heading_with_color(doc, '第4章 資料格式說明', 1)

    add_heading_with_color(doc, '4.1 完整 JSON 範例', 2, (192, 0, 0))

    json_example = '''{
  "rowNo": "1",
  "traceCode": "TRACE-20250120-0001",
  "lotNo": "LOT-202501-042",
  "procName": "鑽孔",
  "devName": "鑽孔機-01",
  "userName": "操作員A",
  "workClass": "日班",
  "inspectionTime": "2025-01-20T10:30:00",
  "paramData": [
    {
      "code": "DIAMETER",
      "name": "孔徑",
      "value": "9.87",
      "unit": "mm",
      "desc": "孔徑測量值"
    },
    {
      "code": "DEPTH",
      "name": "深度",
      "value": "4.72",
      "unit": "mm",
      "desc": "深度測量值"
    }
  ],
  "benchmarks": [
    {
      "code": "DIAMETER",
      "name": "孔徑規格",
      "upperLimit": "10.0",
      "lowerLimit": "9.5",
      "unit": "mm"
    }
  ],
  "otherData": [
    {
      "key": "檢測結果",
      "value": "OK"
    },
    {
      "key": "工單號",
      "value": "WO-202501-042"
    }
  ]
}'''

    add_code_block(doc, json_example)

    doc.add_paragraph()

    add_heading_with_color(doc, '4.2 欄位說明', 2, (192, 0, 0))

    field_data = [
        ('欄位名稱', '型別', '必填', '說明', '範例'),
        ('rowNo', 'string', '是', '站別編號', '"1"'),
        ('traceCode', 'string', '擇一', '追溯碼', '"TRACE-20250120-0001"'),
        ('lotNo', 'string', '擇一', '批號', '"LOT-202501-042"'),
        ('procName', 'string', '是', '製程名稱', '"鑽孔"'),
        ('devName', 'string', '是', '設備名稱', '"鑽孔機-01"'),
        ('userName', 'string', '是', '作業員', '"操作員A"'),
        ('workClass', 'string', '是', '班別', '"日班"'),
        ('inspectionTime', 'string', '否', 'ISO 8601 時間', '"2025-01-20T10:30:00"'),
        ('paramData', 'array', '否', '檢測參數陣列', '見下方說明'),
        ('benchmarks', 'array', '否', '基準值陣列', '見下方說明'),
        ('otherData', 'array', '否', '其他資料陣列', '見下方說明')
    ]

    add_table_with_header(doc, field_data[0], field_data[1:])

    doc.add_paragraph()
    doc.add_paragraph('⚠️ 重要：traceCode 和 lotNo 至少提供一個！', style='Intense Quote')

    doc.add_page_break()

    add_heading_with_color(doc, '4.3 paramData 陣列格式', 2, (192, 0, 0))

    param_fields = [
        ('欄位', '型別', '必填', '說明'),
        ('code', 'string', '是', '參數代碼（例: "DIAMETER"）'),
        ('name', 'string', '是', '參數名稱（例: "孔徑"）'),
        ('value', 'string', '是', '測量值（例: "9.87"）'),
        ('unit', 'string', '否', '單位（例: "mm"）'),
        ('desc', 'string', '否', '描述（例: "孔徑測量值"）')
    ]

    add_table_with_header(doc, param_fields[0], param_fields[1:])

    doc.add_paragraph()

    add_heading_with_color(doc, '4.4 benchmarks 陣列格式', 2, (192, 0, 0))

    benchmark_fields = [
        ('欄位', '型別', '必填', '說明'),
        ('code', 'string', '是', '基準代碼'),
        ('name', 'string', '是', '基準名稱'),
        ('upperLimit', 'string', '是', '上限值'),
        ('lowerLimit', 'string', '是', '下限值'),
        ('unit', 'string', '否', '單位')
    ]

    add_table_with_header(doc, benchmark_fields[0], benchmark_fields[1:])

    doc.add_paragraph()

    add_heading_with_color(doc, '4.5 otherData 陣列格式', 2, (192, 0, 0))

    other_fields = [
        ('欄位', '型別', '必填', '說明'),
        ('key', 'string', '是', '鍵值（例: "檢測結果"）'),
        ('value', 'string', '是', '數值（例: "OK"）')
    ]

    add_table_with_header(doc, other_fields[0], other_fields[1:])

    doc.add_page_break()

    # ==================== 第5章：錯誤處理 ====================
    add_heading_with_color(doc, '第5章 錯誤處理機制', 1)

    add_heading_with_color(doc, '5.1 HTTP 狀態碼', 2, (192, 0, 0))

    status_codes = [
        ('狀態碼', '說明', 'LabVIEW 處理方式'),
        ('200 OK', '成功接收', '正常繼續'),
        ('400 Bad Request', '資料格式錯誤', '檢查 JSON 格式，修正後重試'),
        ('500 Internal Server Error', 'Monitor 內部錯誤', 'Monitor 會自動重試，無需處理'),
        ('503 Service Unavailable', 'Monitor 未啟動', '啟動 Monitor.exe')
    ]

    add_table_with_header(doc, status_codes[0], status_codes[1:])

    doc.add_paragraph()

    add_heading_with_color(doc, '5.2 LabVIEW 端錯誤處理建議', 2, (192, 0, 0))

    doc.add_paragraph('🔸 Timeout 錯誤', style='List Bullet')
    doc.add_paragraph('   - 原因：Monitor 未啟動或網路問題')
    doc.add_paragraph('   - 處理：記錄錯誤，下次檢測時重試')

    doc.add_paragraph('🔸 Status Code ≠ 200', style='List Bullet')
    doc.add_paragraph('   - 原因：資料格式錯誤')
    doc.add_paragraph('   - 處理：檢查 Response Body 錯誤訊息')

    doc.add_paragraph('🔸 JSON 組裝錯誤', style='List Bullet')
    doc.add_paragraph('   - 原因：Cluster 結構不正確')
    doc.add_paragraph('   - 處理：使用 DeviceSimulator.exe 產生範例資料對照')

    doc.add_paragraph()

    add_heading_with_color(doc, '5.3 Monitor 自動重試機制', 2, (192, 0, 0))

    doc.add_paragraph('✅ Monitor 收到資料後，若上傳 MES Cloud 失敗：')
    doc.add_paragraph('   1. 自動存入 SQLite 離線佇列')
    doc.add_paragraph('   2. 每 2 秒自動重試')
    doc.add_paragraph('   3. 無重試次數限制（直到成功）')
    doc.add_paragraph('   4. 應用程式重啟後繼續重試')

    doc.add_paragraph()
    doc.add_paragraph('📌 LabVIEW 端只需確保資料成功送達 Monitor（Status Code = 200），後續上傳由 Monitor 保證。')

    doc.add_page_break()

    # ==================== 第6章：測試與驗證 ====================
    add_heading_with_color(doc, '第6章 測試與驗證', 1)

    add_heading_with_color(doc, '6.1 使用 DeviceSimulator 測試', 2, (192, 0, 0))

    doc.add_paragraph('在整合 LabVIEW 前，建議先使用 DeviceSimulator.exe 測試：')
    doc.add_paragraph('1. 啟動 Monitor.exe', style='List Number')
    doc.add_paragraph('2. 啟動 DeviceSimulator.exe', style='List Number')
    doc.add_paragraph('3. 點擊「生成 TraceCode」和「生成 LotNo」', style='List Number')
    doc.add_paragraph('4. 點擊「發送數據」', style='List Number')
    doc.add_paragraph('5. 檢查「發送歷史」顯示「成功」', style='List Number')
    doc.add_paragraph('6. 複製 JSON 格式到 LabVIEW 作為參考', style='List Number')

    doc.add_paragraph()

    add_heading_with_color(doc, '6.2 LabVIEW 整合測試步驟', 2, (192, 0, 0))

    doc.add_paragraph('步驟 1：單筆資料測試', style='Heading 3')
    doc.add_paragraph('   - 在 LabVIEW 中組裝一筆完整資料')
    doc.add_paragraph('   - POST 到 Monitor')
    doc.add_paragraph('   - 確認 Status Code = 200')

    doc.add_paragraph('步驟 2：批量資料測試', style='Heading 3')
    doc.add_paragraph('   - 連續發送 10 筆資料')
    doc.add_paragraph('   - 檢查 Monitor UI 統計數字正確')

    doc.add_paragraph('步驟 3：錯誤情境測試', style='Heading 3')
    doc.add_paragraph('   - 故意發送格式錯誤的 JSON')
    doc.add_paragraph('   - 確認收到 400 Bad Request')

    doc.add_paragraph('步驟 4：斷線重連測試', style='Heading 3')
    doc.add_paragraph('   - 關閉 Monitor.exe')
    doc.add_paragraph('   - 發送資料（預期 Timeout）')
    doc.add_paragraph('   - 重新啟動 Monitor.exe')
    doc.add_paragraph('   - 再次發送（預期成功）')

    doc.add_page_break()

    # ==================== 第7章：FAQ ====================
    add_heading_with_color(doc, '第7章 常見問題 FAQ', 1)

    faq_items = [
        ('Q1: LabVIEW 中如何組裝 JSON？',
         'A: 使用 "Flatten To JSON.vi"（需安裝 JSON Toolkit）或手動串接 String。'),

        ('Q2: 如何確認 Monitor 是否正在運行？',
         'A: 開啟 Monitor.exe，查看左上角燈號（綠燈 = 正常，紅燈 = 異常）。'),

        ('Q3: 如何查看 Monitor 收到的資料？',
         'A: Monitor UI 的「歷史記錄」頁籤會顯示最近 100 筆資料。'),

        ('Q4: 發送資料後 Status Code = 400，如何除錯？',
         'A: 檢查 Response Body 的錯誤訊息，通常是欄位缺失或格式錯誤。'),

        ('Q5: 網路連線正常但 POST 失敗？',
         'A: 確認 URL 正確（http://localhost:5100/api/inspection/submit），注意不要有空格。'),

        ('Q6: 能否使用 GET 方法發送？',
         'A: 不行，必須使用 POST 方法，Content-Type 必須是 application/json。'),

        ('Q7: 如何查看 Monitor 日誌？',
         'A: Monitor.exe 執行目錄下的 logs/ 資料夾（例: logs/monitor-20250120.log）。'),

        ('Q8: LabVIEW 發送成功，但 MES Cloud 沒收到？',
         'A: 檢查 Simulator.exe 是否運行，或聯繫 MES 系統管理員。Monitor 會自動重試。')
    ]

    for question, answer in faq_items:
        doc.add_paragraph(question, style='Heading 3')
        doc.add_paragraph(answer)
        doc.add_paragraph()

    doc.add_page_break()

    # ==================== 第8章：附錄 ====================
    add_heading_with_color(doc, '第8章 附錄：執行檔說明', 1)

    add_heading_with_color(doc, '8.1 Monitor.exe（中介軟體主程式）', 2, (192, 0, 0))

    doc.add_paragraph('📦 檔案名稱：MesMiddleware.Monitor.exe')
    doc.add_paragraph('🎯 功能：接收設備資料、上傳 MES Cloud、管理離線佇列')
    doc.add_paragraph('🔌 監聽端口：http://localhost:5100')

    doc.add_paragraph()
    doc.add_paragraph('啟動步驟：', style='Heading 3')
    doc.add_paragraph('1. 雙擊 Monitor.exe')
    doc.add_paragraph('2. 等待左上角燈號變綠（表示正常）')
    doc.add_paragraph('3. 可最小化到系統列執行')

    doc.add_paragraph()
    doc.add_paragraph('介面說明：', style='Heading 3')
    doc.add_paragraph('🟢 狀態頁籤：顯示連線狀態、統計數字')
    doc.add_paragraph('📋 歷史記錄頁籤：顯示最近 100 筆上傳記錄')
    doc.add_paragraph('⏳ 佇列頁籤：顯示待重試的離線資料')

    doc.add_paragraph()

    add_heading_with_color(doc, '8.2 DeviceSimulator.exe（設備端模擬器）', 2, (192, 0, 0))

    doc.add_paragraph('📦 檔案名稱：MesMiddleware.DeviceSimulator.exe')
    doc.add_paragraph('🎯 功能：模擬設備發送檢測資料，用於測試')
    doc.add_paragraph('🔗 目標：http://localhost:5100（Monitor）')

    doc.add_paragraph()
    doc.add_paragraph('啟動步驟：', style='Heading 3')
    doc.add_paragraph('1. 先啟動 Monitor.exe')
    doc.add_paragraph('2. 雙擊 DeviceSimulator.exe')
    doc.add_paragraph('3. 點擊「測試連接」確認綠燈')

    doc.add_paragraph()
    doc.add_paragraph('功能說明：', style='Heading 3')
    doc.add_paragraph('🖊️ 手動發送：自定義單筆資料')
    doc.add_paragraph('📦 批量發送：一次發送 1-100 筆測試資料')
    doc.add_paragraph('🔄 自動發送：定時自動發送（間隔 1-60 秒）')
    doc.add_paragraph('📊 發送歷史：查看發送記錄和錯誤訊息')

    doc.add_paragraph()

    add_heading_with_color(doc, '8.3 Simulator.exe（MES Cloud 模擬器）', 2, (192, 0, 0))

    doc.add_paragraph('📦 檔案名稱：MesMiddleware.Simulator.exe')
    doc.add_paragraph('🎯 功能：模擬 MES Cloud API，用於離線測試')
    doc.add_paragraph('🔌 監聽端口：http://localhost:5200')

    doc.add_paragraph()
    doc.add_paragraph('啟動步驟：', style='Heading 3')
    doc.add_paragraph('1. 雙擊 Simulator.exe')
    doc.add_paragraph('2. 查看「請求記錄」頁籤確認運行中')
    doc.add_paragraph('3. 可設定「模擬失敗」測試重試機制')

    doc.add_paragraph()
    doc.add_paragraph('應用場景：', style='Heading 3')
    doc.add_paragraph('✅ 離線測試（無需連接真實 MES Cloud）')
    doc.add_paragraph('✅ 測試重試機制（勾選「模擬失敗」）')
    doc.add_paragraph('✅ 查看上傳的完整 JSON 資料')

    doc.add_page_break()

    # ==================== 快速參考卡 ====================
    add_heading_with_color(doc, '快速參考卡', 1)

    doc.add_paragraph('📌 重要資訊速查：')
    doc.add_paragraph()

    reference_data = [
        ('項目', '內容'),
        ('API 端點', 'http://localhost:5100/api/inspection/submit'),
        ('HTTP 方法', 'POST'),
        ('Content-Type', 'application/json'),
        ('必填欄位', 'rowNo, procName, devName, userName, workClass'),
        ('擇一必填', 'traceCode 或 lotNo（至少一個）'),
        ('成功狀態碼', '200 OK'),
        ('Monitor 端口', '5100'),
        ('Simulator 端口', '5200'),
        ('日誌位置', 'logs/monitor-YYYYMMDD.log'),
        ('重試機制', '自動、無限次、每 2 秒')
    ]

    add_table_with_header(doc, reference_data[0], reference_data[1:])

    doc.add_paragraph()
    doc.add_paragraph('📞 技術支援：請聯繫 MES 系統管理員')

    # 儲存文件
    output_path = Path(__file__).parent / "LabVIEW整合操作手冊.docx"
    doc.save(output_path)
    print(f"[OK] Manual created: {output_path}")
    return output_path

if __name__ == "__main__":
    create_labview_manual()

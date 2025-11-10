# 思方云追溯系统

# 追溯数据上传接口文档

**文件编号**: CIMFORCE-WPT-20240902
**修订日期**: 2025 年 09 月 18 日
**公司**: 深圳市易美科软件有限公司

> **说明**: 本文档涉及技术资料,仅供我公司客户内部使用,不得对外泄露,否则将承担相应之法律责任。本文档仅供本次项目使用,不得以任何方式把本文档另做他用。

---

## 1、设备登录

### 简要描述

- 设备登录验证并获取 TOKEN。

### 请求 URL

- `http://{ip}:{port}/CimforceTraceMgrDev/api/prtmac/prtmacuserlogin`

### 请求方式

- POST
- 登录后获取 token 长期有效(IP 和设备名称变换时需要重新获取)
- 后续接口需要在头部传递 Token 参数

### 请求 Header 参数说明

| 参数名 | 必选 | 类型 | 说明 |
|--------|------|------|------|
| Referrer | 是 | string | 当前设备 IP(实际内网 IP) |

### 请求 BODY 参数说明

| 参数名 | 必选 | 类型 | 说明 |
|--------|------|------|------|
| PrtMacNo | 是 | string | 机台编号(由 WPT 系统分配) |

### 请求 BODY 参数实例

```json
{
  "PrtMacNo": "S63"
}
```

### 返回参数说明

| 参数名 | 类型 | 说明 |
|--------|------|------|
| success | bool | 请求状态 true 成功 false 失败 |
| msg | string | 请求失败时错误描述 |
| data | object | 成功时返回数据结构 |
| code | string | 返回编码(200=成功;其他代码=失败) |

### 返回参数 data 说明

| 参数名 | 类型 | 说明 |
|--------|------|------|
| PrtMacNo | string | 机台号 |
| IpAddr | string | 机台注册 IP |
| Token | string | 后续接口需在请求头部追加 accessToken=当前值 |
| SysUserId | string | 绑定用户 Id |

### 返回失败示例

```json
{
  "success": false,
  "data": null,
  "msg": "本机台(S63)IP(192.168.1.10)与注册电脑 IP 不匹配!",
  "code": "300"
}
```

### 返回成功示例

```json
{
  "success": true,
  "data": {
    "PrtMacNo": "S63",
    "IpAddr": "192.168.1.231",
    "Token": "autoprtaed02b44469a0e81a63dec431",
    "SysUserId": null
  },
  "msg": "success",
  "code": "200"
}
```

---

## 2、数据上传

### 简要描述

- 设备主动上传追溯数据。

### 请求 URL

- `http://{ip}:{port}/CimforceTraceMgrDev/api/v1/MesTrace/TraceData/AddData3`

### 请求方式

- POST
- 请求头部需加参数 accessToken=Token (token 为设备登录接口成功后返回的值)

### 请求 Header 参数说明

| 参数名 | 必选 | 类型 | 说明 |
|--------|------|------|------|
| accessToken | 是 | string | token 为设备登录接口成功后返回的值 |

### 请求 BODY 参数说明

| 参数名 | 类型 | 说明 |
|--------|------|------|
| isVerifyLot | bool | 是否要求MES收到数据后进行混批验证并返回结果(True=要求,False=不要求) |
| data | 集合 | 溯码结果数据列表,每一行表示一条追溯结果,至少一笔。非空。通常一个单板或面次上传一条。 |
| data[n]->rowNo | Int | 数据行号,从 1 开始。非空。 |
| data[n]->procName | string | 站点(工步)编码或名称。非空。设备中自定义。 |
| data[n]->devName | string | 设备编号或名称。非空。设备中自定义。 |
| data[n]->userName | string | 作业员编号或名称。非空。 |
| data[n]->workClass | string | 作业员班次。非空。 |
| data[n]->traceCode | string | 追溯二维码。traceCode 和 lotNo 至少一个非空。(读到单板二维码时传入二维码内容,无二维码或读二维码 NG 时传入空字符串) |
| data[n]->lotNo | string | LOT 号。traceCode 和 lotNo 至少一个非空。(有扫 LOT 号则传入所扫的 LOT 号,未扫 LOT 号可传入机台生成的作业板序流水号) |
| data[n]->partNumber | string | 产品型号。可为空。 |
| data[n]->remark | string | 备注信息,可为空。例如保存机台生成的作业板序流水号。 |
| data[n]->paramData | 集合,可多条 | 生产过程汇总数据(例如:单板检测结论、前 20 大类缺陷汇总等。可参考对应设备类型的报文示例) |
| data[n]->benchmarks | 集合,可多条 | 生产过程详细数据或判断标准(例如:测试条件和参数等。可参考对应设备类型的报文示例) |
| data[n]->otherData | 集合,可多条 | 其他辅助信息(例如:设备作业时间等,可参考对应设备类型的报文示例)。 |

### paramData 参数

| 参数 | 类型 | 说明 |
|------|------|------|
| code | string | 序号 |
| name | string | 标准名 |
| value | string | 标准值 |
| unit | string | 单位 |
| desc | string | 描述 |

### benchmarks 参数

| 参数 | 类型 | 说明 |
|------|------|------|
| code | string | 序号 |
| name | string | 标准名 |
| value | string | 标准值 |
| unit | string | 单位 |
| desc | string | 描述 |

### otherData 参数

| 参数 | 类型 | 说明 |
|------|------|------|
| code | string | 项目代码 |
| name | string | 项目名称 |
| value | string | 值 |
| unit | string | 单位 |
| desc | string | 描述 |

### 参考推荐的参数代码、名称规范

| 代码 | 名称 | 含义 |
|------|------|------|
| Result | 单板检测结果 | OK、NG |
| DefectQty | NG/缺陷总数 | |
| OkQty | OK 总数 | |
| CheckQty | 检测总数 | |
| {Defect_Qty_01,02,03...NN} | 实际的{缺陷名称汇总数量} | 该缺陷名称汇总数量。可多条,不超过 99 条。 |
| {Defect_Pos_01,02,03...NN} | 实际的{缺陷坐标及图片路劲} | 缺陷坐标及图片路劲。可多条,不超过 99 条。 |
| {Check_Param_01,02,03...NN} | 实际的{测试参数名称} | 测试参数或条件。可多条,不超过 99 条。 |
| LayersType | 检测面次 | TOP 或 BOT |
| SourceType | 数据分类 | 1=机检/2=复检 |
| CheckTime | 检测时间 | 检测时间 |

### 请求 BODY 参数实例

```json
{
  "isVerifyLot": false,
  "data": [
    {
      "rowNo": 1,
      "procName": "ET",
      "devName": "W4-MXDCJ-001",
      "userName": "215123",
      "workClass": "A",
      "traceCode": "09O12380010001",
      "lotNo": "02029156-00800-N",
      "partNumber": "4FD890011A01",
      "remark": "",
      "paramData": [
        {
          "code": "Result",
          "name": "单板检测结果",
          "value": "PASS",
          "unit": "",
          "desc": "单板检测结果"
        },
        {
          "code": "CheckQty",
          "name": "检测数量",
          "value": "190",
          "unit": "个",
          "desc": "检测数量"
        },
        {
          "code": "DefectQty",
          "name": "NG 数量",
          "value": "190",
          "unit": "个",
          "desc": "NG 数量"
        },
        {
          "code": "OkQty",
          "name": "OK 数量",
          "value": "694",
          "unit": "个",
          "desc": "OK 数量"
        }
      ],
      "benchmarks": [
        {
          "code": "Defect_Qty_01",
          "name": "开路数量",
          "value": "310",
          "unit": "",
          "desc": "开路数量"
        },
        {
          "code": "Defect_Qty_02",
          "name": "短路数量",
          "value": "320",
          "unit": "",
          "desc": "短路数量"
        },
        {
          "code": "Defect_Pos_01",
          "name": "开路-坐标及图片路径",
          "value": "X=325.0;Y=229.0",
          "unit": "",
          "desc": "170.18.1.101\\D41029\\0"
        },
        {
          "code": "Defect_Pos_02",
          "name": "短路-坐标及图片路径",
          "value": "X=25.0;Y=28.0",
          "unit": "",
          "desc": "170.18.1.101\\D41029\\1"
        },
        {
          "code": "Check_Param_01",
          "name": "Open Voltage",
          "value": "100",
          "unit": "V",
          "Desc": "Open Voltage"
        },
        {
          "code": "Check_Param_02",
          "name": "Open Resist",
          "value": "100",
          "unit": "V",
          "Desc": "Open Resist"
        }
      ],
      "otherData": [
        {
          "code": "Layers",
          "name": "面次",
          "value": "L2",
          "unit": "",
          "desc": "面次"
        },
        {
          "code": "SourceType",
          "name": "数据分类",
          "value": "1",
          "unit": "",
          "desc": "1=机测;2=复检"
        },
        {
          "code": "CheckTime",
          "name": "测试时间",
          "value": "2024-08-29 8:30",
          "unit": "",
          "Desc": "测试时间"
        }
      ]
    }
  ]
}
```

### 返回参数说明

| 参数名 | 类型 | 说明 |
|--------|------|------|
| success | bool | 请求状态 true 成功 false 失败 |
| msg | string | 请求失败时错误描述 |
| data | object | 成功时返回数据结构 |
| code | string | 返回编码(200=成功;其他代码=失败) |

### 返回失败示例

```json
{
  "success": false,
  "data": null,
  "msg": "工单[202401-01-000]不存在",
  "code": "-1"
}
```

### 返回成功示例

```json
{
  "success": true,
  "data": null,
  "msg": "上传成功",
  "code": "200"
}
```

**备注**: 此接口支持各类型设备(例如 AOI、AVI、VRS、ET、验孔机等)上报追溯数据,可参考特定设备类型的上报数据报文示例(JSON 示例)。

---

## 3、追溯码校验

### 简要描述

- 验证追溯码与 LOT 号是否一致,即判断是否混批。

### 请求 URL

- `http://{ip}:{port}/CimforceTraceMgrDev/api/transcode/checkcode`

### 请求方式

- POST

### 请求 Header 参数说明

| 参数名 | 必选 | 类型 | 说明 |
|--------|------|------|------|
| accessToken | 是 | string | token 为设备登录接口成功后返回的值 |

### 请求 BODY 参数说明

| 参数名 | 类型 | 说明 |
|--------|------|------|
| woType | Int | 验证类型 固定传 1 必填 |
| woNum | string | 工单编号 必填 |
| codes | string 集合 | 待验证编码 可以上传多个 |
| PrtMacNo | string | 设备编号 |

### 请求 BODY 参数示例

```json
{
  "woType": 1,
  "woNum": "229033-0-1-1",
  "codes": ["0000000001", "0000000002", "0000000003"],
  "PrtMacNo": "W5-JSDEV-001"
}
```

### 返回参数说明

| 参数名 | 类型 | 说明 |
|--------|------|------|
| success | bool | 请求状态 true 成功 false 失败 |
| msg | string | 请求失败时错误描述 |
| data | object | 成功时返回数据结构 |
| code | string | 返回编码(200=成功;其他代码=失败) |

### 返回成功示例

```json
{
  "success": true,
  "data": null,
  "msg": "验证成功",
  "code": "200"
}
```

### 返回失败示例

```json
{
  "success": false,
  "data": null,
  "msg": "验证失败:编码[0000000001]不属于工单。编码[0000000002]不属于工单。",
  "code": "0"
}
```

---

*文档结束*

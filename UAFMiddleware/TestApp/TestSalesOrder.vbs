' Sage 100 BOI Sales Order Test - VBScript
' Run this directly on the workstation: cscript TestSalesOrder.vbs
Option Explicit

Dim oScript, oSession, oSO, oLines
Dim retVal, nextOrderNo

' Configuration - CHANGE THESE!
Const ServerPath = "\\uaf-erp\Sage Premium 2022\MAS90\Home"
Const Company = "TST"
Const Username = "dyl"
Const Password = "apex"
Const ARDivisionNo = "01"
Const CustomerNo = "A0075"
Const ItemCode = "14202"
Const Quantity = 1
Const WarehouseCode = "000"

WScript.Echo "==================================="
WScript.Echo "Sage 100 BOI Sales Order Test (VBS)"
WScript.Echo "==================================="
WScript.Echo ""

On Error Resume Next

' Step 1: Create ProvideX.Script
WScript.Echo "[1] Creating ProvideX.Script..."
Set oScript = CreateObject("ProvideX.Script")
If Err.Number <> 0 Then
    WScript.Echo "    ERROR: " & Err.Description
    WScript.Quit 1
End If
WScript.Echo "    SUCCESS"

' Step 2: Initialize
WScript.Echo "[2] Initializing with path: " & ServerPath
oScript.Init(ServerPath)
If Err.Number <> 0 Then
    WScript.Echo "    ERROR: " & Err.Description
    WScript.Quit 1
End If
WScript.Echo "    SUCCESS"

' Step 3: Create Session
WScript.Echo "[3] Creating SY_Session..."
Set oSession = oScript.NewObject("SY_Session")
If Err.Number <> 0 Then
    WScript.Echo "    ERROR: " & Err.Description
    WScript.Quit 1
End If
WScript.Echo "    SUCCESS"

' Step 4: Authenticate
WScript.Echo "[4] Authenticating user: " & Username
retVal = oSession.nSetUser(Username, Password)
WScript.Echo "    nSetUser returned: " & retVal
If retVal = 0 Then
    WScript.Echo "    ERROR: " & oSession.sLastErrorMsg
    WScript.Quit 1
End If
WScript.Echo "    SUCCESS"

' Step 5: Set Company
WScript.Echo "[5] Setting company: " & Company
retVal = oSession.nSetCompany(Company)
WScript.Echo "    nSetCompany returned: " & retVal
If retVal = 0 Then
    WScript.Echo "    ERROR: " & oSession.sLastErrorMsg
    WScript.Quit 1
End If
WScript.Echo "    SUCCESS"

' Step 6: Set Module
WScript.Echo "[6] Setting module to S/O..."
retVal = oSession.nSetModule("S/O")
WScript.Echo "    SUCCESS"

' Step 6b: Set Program Context
WScript.Echo "[6b] Setting program context..."
Dim taskId
taskId = oSession.nLookupTask("SO_SalesOrder_ui")
WScript.Echo "    Task ID: " & taskId
If taskId <> 0 Then
    retVal = oSession.nSetProgram(taskId)
    WScript.Echo "    nSetProgram returned: " & retVal
End If

' Step 7: Create SO_SalesOrder_bus
WScript.Echo "[7] Creating SO_SalesOrder_bus..."
Set oSO = oScript.NewObject("SO_SalesOrder_bus", oSession)
If Err.Number <> 0 Then
    WScript.Echo "    ERROR: " & Err.Description
    WScript.Quit 1
End If
If oSO Is Nothing Then
    WScript.Echo "    ERROR: Object is Nothing"
    WScript.Quit 1
End If
WScript.Echo "    SUCCESS"

' Step 7b: Get Lines Object IMMEDIATELY (before setting key - per Tek-Tips example)
WScript.Echo "[7b] Getting oLines (before nSetKey)..."
Set oLines = oSO.oLines
If Err.Number <> 0 Then
    WScript.Echo "    ERROR: " & Err.Description
    Err.Clear
End If
WScript.Echo "    SUCCESS"

' Step 8: Get Next Order Number
WScript.Echo "[8] Getting next sales order number..."
nextOrderNo = ""
retVal = oSO.nGetNextSalesOrderNo(nextOrderNo)
WScript.Echo "    Next order number: " & nextOrderNo

' Step 9: Set Key using nSetKeyValue + nSetKey() pattern
WScript.Echo "[9] Setting key using nSetKeyValue pattern..."
retVal = oSO.nSetKeyValue("SalesOrderNo$", nextOrderNo)
WScript.Echo "    nSetKeyValue returned: " & retVal
retVal = oSO.nSetKey()
WScript.Echo "    nSetKey() returned: " & retVal
If retVal = 0 Then
    WScript.Echo "    ERROR: " & oSO.sLastErrorMsg
    WScript.Quit 1
End If

' Step 10: Set Header Fields
WScript.Echo "[10] Setting header fields..."
retVal = oSO.nSetValue("ARDivisionNo$", ARDivisionNo)
WScript.Echo "    ARDivisionNo$ = " & ARDivisionNo & ", result: " & retVal
If retVal = 0 Then WScript.Echo "        Warning: " & oSO.sLastErrorMsg

retVal = oSO.nSetValue("CustomerNo$", CustomerNo)
WScript.Echo "    CustomerNo$ = " & CustomerNo & ", result: " & retVal
If retVal = 0 Then WScript.Echo "        Warning: " & oSO.sLastErrorMsg

retVal = oSO.nSetValue("CustomerPONo$", "VBS-TEST-002")
WScript.Echo "    CustomerPONo$ = VBS-TEST-002, result: " & retVal

' Step 11: Re-get Lines Object (in case it changed after nSetKey)
WScript.Echo "[11] Re-getting oLines after header setup..."
Set oLines = oSO.oLines
If Err.Number <> 0 Then
    WScript.Echo "    ERROR: " & Err.Description
    WScript.Quit 1
End If
WScript.Echo "    SUCCESS"

' Step 12: Add Line using direct oSO.oLines notation
WScript.Echo "[12] Adding line using oSO.oLines.nAddLine()..."
retVal = oSO.oLines.nAddLine()
WScript.Echo "    nAddLine returned: " & retVal
If Err.Number <> 0 Then
    WScript.Echo "    ERROR: " & Err.Description
    Err.Clear
End If

' Check current line state
WScript.Echo "[12b] Checking line state after nAddLine..."
WScript.Echo "    EditState: " & oSO.oLines.nEditState
If Err.Number <> 0 Then
    WScript.Echo "    nEditState error: " & Err.Description
    Err.Clear
End If

' Step 13: Try multiple approaches for ItemCode$
WScript.Echo "[13] Setting ItemCode$ = " & ItemCode
WScript.Echo "    [13a] Trying nSetValue..."
retVal = oSO.oLines.nSetValue("ItemCode$", CStr(ItemCode))
WScript.Echo "        nSetValue result: [" & retVal & "]"
If Err.Number <> 0 Then
    WScript.Echo "        COM Error: " & Err.Description
    Err.Clear
End If

WScript.Echo "    [13b] Trying SetValue (no n prefix)..."
retVal = oSO.oLines.SetValue("ItemCode$", CStr(ItemCode))
WScript.Echo "        SetValue result: [" & retVal & "]"
If Err.Number <> 0 Then
    WScript.Echo "        COM Error: " & Err.Description
    Err.Clear
End If

WScript.Echo "    [13c] Trying nSetKeyValue..."
retVal = oSO.oLines.nSetKeyValue("ItemCode$", CStr(ItemCode))
WScript.Echo "        nSetKeyValue result: [" & retVal & "]"
If Err.Number <> 0 Then
    WScript.Echo "        COM Error: " & Err.Description
    Err.Clear
End If

' Check what columns the lines object actually has
WScript.Echo "    [13d] Checking columns via nGetValue..."
Dim testVal
testVal = ""
retVal = oSO.oLines.nGetValue("ItemCode$", testVal)
WScript.Echo "        nGetValue ItemCode$ result: " & retVal & ", value: [" & testVal & "]"
If Err.Number <> 0 Then
    WScript.Echo "        COM Error: " & Err.Description
    Err.Clear
End If

' Step 14: Set QuantityOrdered
WScript.Echo "[14] Setting QuantityOrdered = " & Quantity
retVal = oSO.oLines.nSetValue("QuantityOrdered", CDbl(Quantity))
WScript.Echo "    Result: [" & retVal & "]"
If Err.Number <> 0 Then
    WScript.Echo "    COM Error: " & Err.Description
    Err.Clear
End If

' Step 15: Set WarehouseCode$
WScript.Echo "[15] Setting WarehouseCode$ = " & WarehouseCode
retVal = oSO.oLines.nSetValue("WarehouseCode$", WarehouseCode)
WScript.Echo "    Result: [" & retVal & "]"
If Err.Number <> 0 Then
    WScript.Echo "    COM Error: " & Err.Description
    Err.Clear
End If

' Step 16: Write Line
WScript.Echo "[16] Writing line (oSO.oLines.nWrite)..."
retVal = oSO.oLines.nWrite()
WScript.Echo "    nWrite returned: [" & retVal & "]"
If Err.Number <> 0 Then
    WScript.Echo "    COM Error: " & Err.Description
    Err.Clear
End If
If retVal = 0 Or retVal = "" Then
    WScript.Echo "    sLastErrorMsg: " & oSO.oLines.sLastErrorMsg
    If Err.Number <> 0 Then Err.Clear
End If

' Step 17: Write Order
WScript.Echo "[17] Writing order (oSO.nWrite)..."
retVal = oSO.nWrite()
WScript.Echo "    nWrite returned: " & retVal
If Err.Number <> 0 Then
    WScript.Echo "    COM Error: " & Err.Description
End If
If retVal = 0 Then
    WScript.Echo "    sLastErrorMsg: " & oSO.sLastErrorMsg
ElseIf retVal = 1 Then
    WScript.Echo ""
    WScript.Echo "========================================="
    WScript.Echo "SUCCESS! Sales Order " & nextOrderNo & " created!"
    WScript.Echo "========================================="
End If

' Cleanup
Set oLines = Nothing
Set oSO = Nothing
Set oSession = Nothing
Set oScript = Nothing

WScript.Echo ""
WScript.Echo "Done."


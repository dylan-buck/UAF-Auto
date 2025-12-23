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

' Step 6c: Verify customer exists using AR_Customer_svc
WScript.Echo "[6c] Verifying customer " & ARDivisionNo & "-" & CustomerNo & " exists..."
Dim oCustomer, custExists
Set oCustomer = oScript.NewObject("AR_Customer_svc", oSession)
If Err.Number <> 0 Then
    WScript.Echo "    Could not create AR_Customer_svc: " & Err.Description
    Err.Clear
Else
    ' Try to find the customer
    retVal = oCustomer.nSetKeyValue("ARDivisionNo$", ARDivisionNo)
    WScript.Echo "    Set ARDivisionNo$: " & retVal
    retVal = oCustomer.nSetKeyValue("CustomerNo$", CustomerNo)
    WScript.Echo "    Set CustomerNo$: " & retVal
    retVal = oCustomer.nSetKey()
    WScript.Echo "    nSetKey returned: " & retVal & " (1=exists, 0=not found)"
    If retVal = 1 Then
        WScript.Echo "    Customer FOUND!"
        Dim custName
        custName = ""
        oCustomer.nGetValue "CustomerName$", custName
        WScript.Echo "    Customer Name: " & custName
    Else
        WScript.Echo "    Customer NOT FOUND: " & oCustomer.sLastErrorMsg
    End If
    Set oCustomer = Nothing
End If

' Step 6d: Verify item exists using CI_Item_svc
WScript.Echo "[6d] Verifying item " & ItemCode & " exists..."
Dim oItem
Set oItem = oScript.NewObject("CI_Item_svc", oSession)
If Err.Number <> 0 Then
    WScript.Echo "    Could not create CI_Item_svc: " & Err.Description
    Err.Clear
Else
    retVal = oItem.nSetKeyValue("ItemCode$", ItemCode)
    WScript.Echo "    Set ItemCode$: " & retVal
    retVal = oItem.nSetKey()
    WScript.Echo "    nSetKey returned: " & retVal & " (1=exists, 0=not found)"
    If retVal = 1 Then
        WScript.Echo "    Item FOUND!"
        Dim itemDesc
        itemDesc = ""
        oItem.nGetValue "ItemCodeDesc$", itemDesc
        WScript.Echo "    Item Description: " & itemDesc
    Else
        WScript.Echo "    Item NOT FOUND: " & oItem.sLastErrorMsg
    End If
    Set oItem = Nothing
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

' Step 11: Get lines object reference (we know oSO.oLines returns IPvxDispatch)
WScript.Echo "[11] Getting lines object via oSO.oLines..."
Set oLines = oSO.oLines
If Err.Number <> 0 Then
    WScript.Echo "    ERROR: " & Err.Description
    Err.Clear
Else
    WScript.Echo "    Got object type: " & TypeName(oLines)
End If

' Step 12: Add Line using direct oSO.oLines notation
WScript.Echo "[12] Adding line using oSO.oLines.nAddLine()..."
retVal = oSO.oLines.nAddLine()
WScript.Echo "    nAddLine returned: " & retVal
If Err.Number <> 0 Then
    WScript.Echo "    ERROR: " & Err.Description
    Err.Clear
End If
WScript.Echo "    oSO.oLines.sLastErrorMsg: [" & oSO.oLines.sLastErrorMsg & "]"
If Err.Number <> 0 Then Err.Clear

' Check current line state
WScript.Echo "[12b] Checking line state after nAddLine..."
WScript.Echo "    EditState: " & oSO.oLines.nEditState
If Err.Number <> 0 Then
    WScript.Echo "    nEditState error: " & Err.Description
    Err.Clear
End If

' Step 13: Set ItemCode$ and check oLines.sLastErrorMsg (KEY INSIGHT from Sage City!)
WScript.Echo "[13] Setting ItemCode$ = " & ItemCode
retVal = oSO.oLines.nSetValue("ItemCode$", CStr(ItemCode))
WScript.Echo "    nSetValue result: [" & retVal & "]"
If Err.Number <> 0 Then
    WScript.Echo "    COM Error: " & Err.Description
    Err.Clear
End If
' CRITICAL: Check oLines.sLastErrorMsg for the real error!
WScript.Echo "    oSO.oLines.sLastErrorMsg: [" & oSO.oLines.sLastErrorMsg & "]"
If Err.Number <> 0 Then Err.Clear
WScript.Echo "    oSO.sLastErrorMsg: [" & oSO.sLastErrorMsg & "]"
If Err.Number <> 0 Then Err.Clear

' Step 14: Set QuantityOrdered
WScript.Echo "[14] Setting QuantityOrdered = " & Quantity
retVal = oSO.oLines.nSetValue("QuantityOrdered", CDbl(Quantity))
WScript.Echo "    Result: [" & retVal & "]"
If Err.Number <> 0 Then
    WScript.Echo "    COM Error: " & Err.Description
    Err.Clear
End If
WScript.Echo "    oSO.oLines.sLastErrorMsg: [" & oSO.oLines.sLastErrorMsg & "]"
If Err.Number <> 0 Then Err.Clear

' Step 15: Set WarehouseCode$ (note: forum example used 'WarehouseCode' without $)
WScript.Echo "[15] Setting WarehouseCode$ = " & WarehouseCode
retVal = oSO.oLines.nSetValue("WarehouseCode$", WarehouseCode)
WScript.Echo "    Result: [" & retVal & "]"
If Err.Number <> 0 Then
    WScript.Echo "    COM Error: " & Err.Description
    Err.Clear
End If
WScript.Echo "    oSO.oLines.sLastErrorMsg: [" & oSO.oLines.sLastErrorMsg & "]"
If Err.Number <> 0 Then Err.Clear

' Step 16: Write Line
WScript.Echo "[16] Writing line (oSO.oLines.nWrite)..."
retVal = oSO.oLines.nWrite()
WScript.Echo "    nWrite returned: [" & retVal & "]"
If Err.Number <> 0 Then
    WScript.Echo "    COM Error: " & Err.Description
    Err.Clear
End If
WScript.Echo "    oSO.oLines.sLastErrorMsg: [" & oSO.oLines.sLastErrorMsg & "]"
If Err.Number <> 0 Then Err.Clear
WScript.Echo "    oSO.sLastErrorMsg: [" & oSO.sLastErrorMsg & "]"
If Err.Number <> 0 Then Err.Clear

' Step 17: Write Order
WScript.Echo "[17] Writing order (oSO.nWrite)..."
retVal = oSO.nWrite()
WScript.Echo "    nWrite returned: [" & retVal & "]"
If Err.Number <> 0 Then
    WScript.Echo "    COM Error: " & Err.Description
    Err.Clear
End If
WScript.Echo "    oSO.sLastErrorMsg: [" & oSO.sLastErrorMsg & "]"
If Err.Number <> 0 Then Err.Clear
WScript.Echo "    oSO.oLines.sLastErrorMsg: [" & oSO.oLines.sLastErrorMsg & "]"
If Err.Number <> 0 Then Err.Clear

If retVal = 1 Then
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


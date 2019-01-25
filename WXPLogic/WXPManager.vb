Imports System.IO
Imports System.Text
Imports WXPModel
Imports System.Data.Objects

Public Class WXPManager

    Private fileName As String
    Private context As wxp_dbEntities = New wxp_dbEntities
    Public wxpRows As List(Of WXPRow)
    Private currentDataset As Dataset
    Private items As List(Of Item)
    Private varIDs As List(Of Integer)
    Private headers As List(Of Header)

    Private Const BULK_ROW_COUNT_FOR_ROWS As Integer = 500
    Private Const ROW_COUNT_FOR_EF As Integer = 1000
    Private Const DATA_MARKER As String = "DATA"

    Public Sub LoadWXP(fileName As String)

        Me.fileName = fileName
        Me.LoadDataset()

        Me.LoadWXPRows(New StreamReader(fileName, Encoding.Default, True))

        Me.PopulateModel()
        Me.wxpRows.Clear()
        Me.wxpRows = Nothing

        Me.PopulateFacts(New StreamReader(fileName, Encoding.Default, True))

    End Sub

    Public Sub LoadWXPRows(stream As StreamReader)

        Me.wxpRows = New List(Of WXPRow)

        Dim line As String = String.Empty
        Dim rowIndex As Integer = 0
        Dim headerRowIndex As Long = 0
        Dim respondentRowIndex As Long = 0
        Dim originalRows As List(Of WXPRow) = New List(Of WXPRow)

        Do Until stream.EndOfStream
            line = stream.ReadLine
            rowIndex += 1

            If line.Length >= 4 AndAlso line.Substring(0, 4) = DATA_MARKER Then
                headerRowIndex = rowIndex + 1
                respondentRowIndex = rowIndex + 3
            End If

            Dim type As WXPDBRowTypes
            If rowIndex = headerRowIndex Then
                type = WXPDBRowTypes.ColumnHeader
                Me.PopulateHeaders(line)
            ElseIf respondentRowIndex > 0 And rowIndex >= respondentRowIndex Then
                type = WXPDBRowTypes.CaseData
            ElseIf Me.RowBelongsToMetaData(line) Then
                type = WXPDBRowTypes.MetaData
            Else
                type = WXPDBRowTypes.ControlData
            End If

            Dim row As WXPRow = New WXPRow
            row.Initialize(line, rowIndex, type)
            row.Parse()
            row.Source = Nothing
            Me.wxpRows.Add(row)

            Dim originalRow As WXPRow = New WXPRow
            originalRow.Initialize(line, rowIndex, type)
            originalRows.Add(originalRow)
            
            If originalRows.Count = BULK_ROW_COUNT_FOR_ROWS Then
                Me.LoadOriginalData(originalRows)
                originalRows = New List(Of WXPRow)
            End If

        Loop
        Me.LoadOriginalData(originalRows)
        stream.Dispose()

    End Sub

    Private Function RowBelongsToMetaData(line As String) As Boolean

        If line.Length < 2 Then Return False
        If line.Substring(0, 1) = "*" Then line = line.Substring(1)
        If line.Substring(0, 1) = "L" Then Return True

        Select Case line.Substring(0, 2)
            Case "AL", "AR", "BD", "RW", "TH"
                Return True
            Case Else
                Return False
        End Select

    End Function

    Public Sub LoadDataset()

        'Dataset
        Me.currentDataset = New Dataset()
        Me.currentDataset.Name = Me.fileName
        Me.context.Datasets.AddObject(currentDataset)
        Me.context.SaveChanges()

    End Sub

    Public Sub LoadOriginalData(ByRef originalRows As List(Of WXPRow))

        Dim bulkHelper As SQLBulkHelper = New SQLBulkHelper
        Dim table As DataTable = bulkHelper.GetEmptyDataTable(WXPDBDataTables.OriginalRows)

        For Each row In originalRows

            Dim dataRow As DataRow = table.NewRow
            dataRow("ID") = 0
            dataRow("RowIndex") = row.RowIndex
            dataRow("Source") = row.Source
            row.Source = Nothing
            dataRow("RowTypeID") = CInt(row.RowType)
            dataRow("DatasetID") = Me.currentDataset.ID
            table.Rows.Add(dataRow)
        Next

        bulkHelper.BulkInsert(table)
        table.Rows.Clear()


    End Sub

    Public Sub PopulateModel()

        'Folders/Variables/Items

        Dim currentFolder As Folder = New Folder
        Dim folderExists As Boolean = False
        Dim currentVariable As Variable = New Variable
        Dim ParentFolders(20) As Folder
        Dim ParentItems(20) As Item
        Dim rowCount As Integer = 0

        For Each row As WXPRow In Me.wxpRows

            rowCount += 1

            'Create folders
            If row.Prefix = "L" And row.Label <> String.Empty Then
                folderExists = True
                currentFolder = New Folder
                currentFolder.RowIndex = row.RowIndex
                If row.Level = "I" Then
                    currentFolder.FolderLevel = 0
                    currentFolder.IsUserDefined = True
                Else
                    currentFolder.FolderLevel = CInt(row.Level)
                    currentFolder.IsUserDefined = False
                End If
                currentFolder.Label = row.Label
                currentFolder.Dataset = currentDataset
                If currentFolder.FolderLevel > 0 Then
                    currentFolder.ParentFolder = ParentFolders(row.Level - 1)
                End If
                context.Folders.AddObject(currentFolder)
                ParentFolders(currentFolder.FolderLevel) = currentFolder
            ElseIf row.Prefix = "TH" Then
                'Create Variable
                currentVariable = New Variable
                currentVariable.RowIndex = row.RowIndex
                currentVariable.Label = row.Label
                currentVariable.IsDeleted = row.StartsWithAsterix
                If folderExists Then currentVariable.Folder = currentFolder
                currentVariable.Dataset = currentDataset
                If row.ColumnIndex = "HOLE" Then
                    currentVariable.VariableTypeID = WXPDBVariableTypes.Grid
                ElseIf row.Label.Length >= 6 AndAlso row.Label.Substring(0, 6) = "Weight" Then
                    currentVariable.VariableTypeID = WXPDBVariableTypes.Weight
                    currentVariable.ColumnIndex = CLng(row.ColumnIndex)
                Else
                    currentVariable.VariableTypeID = WXPDBVariableTypes.Regular
                    currentVariable.ColumnIndex = CLng(row.ColumnIndex)
                End If
                context.Variables.AddObject(currentVariable)

            ElseIf row.Prefix = "BD" Then
                'Create item (base & variable base)
                Dim item As FilterItem = New FilterItem
                item.RowIndex = row.RowIndex
                item.Label = row.Label
                item.ColumnIndex = CLng(row.ColumnIndex)
                If row.ItemIndex = "*" Then
                    item.FilterItemTypeID = WXPDBFilterItemTypes.VariableFilter
                Else
                    item.FilterItemTypeID = WXPDBFilterItemTypes.ItemFilter
                    item.ItemIndex = Me.ConvertItemIndex(row.ItemIndex)
                End If
                item.Variable = currentVariable
                item.Dataset = currentDataset
                context.FilterItems.AddObject(item)
            ElseIf row.Prefix = "RW" Then
                'Create item (categorical & pointer)
                Dim item As Item = New Item
                item.RowIndex = row.RowIndex
                item.ColumnIndex = CLng(row.ColumnIndex)

                If row.Label = String.Empty Then
                    item.ItemTypeID = WXPDBItemTypes.Pointer
                Else
                    item.ItemTypeID = WXPDBItemTypes.Categorical
                    item.ItemIndex = Me.ConvertItemIndex(row.ItemIndex)
                    item.Label = row.Label

                End If

                If row.FactorAvg.Length > 0 Then
                    item.FactorAvg = CDbl(row.FactorAvg.Replace(".", ","))
                End If
                If row.FactorMax.Length > 0 Then
                    item.FactorMin = CDbl(row.FactorMin.Replace(".", ","))
                    item.FactorMax = CDbl(row.FactorMax.Replace(".", ","))
                End If
                If row.FactorLabel.Length > 0 Then
                    item.FactorLabel = row.FactorLabel
                End If
                item.IsDeleted = row.StartsWithAsterix
                item.Dataset = currentDataset

                If row.Net.Length = 0 Then
                    item.NetLevel = 0
                Else
                    item.NetLevel = CInt(row.Net.Substring(0, row.Net.Length - 1)) / 4
                End If

                If item.NetLevel > 0 Then
                    item.ParentItem = ParentItems(item.NetLevel - 1)
                End If
                ParentItems(item.NetLevel) = item
                item.Variable = currentVariable
                context.Items.AddObject(item)
            ElseIf row.Prefix = "AR" Then
                'Create item (numeric aggregated)
                Dim item As Item = New Item
                item.RowIndex = row.RowIndex
                item.ItemTypeID = WXPDBItemTypes.NumericAggregated
                item.Label = row.Label
                item.IsDeleted = row.StartsWithAsterix
                item.ColumnIndex = CLng(row.ColumnIndex)
                item.Dataset = currentDataset
                item.Variable = currentVariable
                context.Items.AddObject(item)
            ElseIf row.Prefix = "AL" Then
                'Create item (numeric grouped)
                Dim item As Item = New Item
                item.RowIndex = row.RowIndex
                item.ItemTypeID = WXPDBItemTypes.NumericGrouped
                item.Label = row.Label
                item.IsDeleted = row.StartsWithAsterix
                item.Dataset = currentDataset
                item.ColumnIndex = CLng(row.ColumnIndex)
                item.Variable = currentVariable
                context.Items.AddObject(item)
            ElseIf row.RowType = WXPDBRowTypes.CaseData Then
                Dim respondent As Respondent = New Respondent
                respondent.OriginalRespondentID = row.RespondentID
                respondent.RowIndex = row.RowIndex
                respondent.Dataset = currentDataset
                context.Respondents.AddObject(respondent)
            End If

            If rowCount = ROW_COUNT_FOR_EF Then
                context.SaveChanges()
                rowCount = 0
            End If

        Next

        context.SaveChanges()
        context.UpdateFKColumns(currentDataset.ID)

    End Sub

    Public Sub PopulateHeaders(ByRef line As String)

        'Headers

        For a As Long = 2 To line.Length / 17
            Dim currentField As String = line.Substring(a * 17 - 17, 16)
            Dim nextField As String = String.Empty
            If a < line.Length / 17 Then
                nextField = line.Substring((a + 1) * 17 - 17, 16)
            End If
            Dim header As Header = New Header
            header.ColumnIndex = CInt(currentField.Trim)
            header.Position = a * 17 - 17
            If nextField = "                " Then
                header.Length = 33
                a += 1
            Else
                header.Length = 16
            End If
            header.Dataset = currentDataset
            Me.context.Headers.AddObject(header)
        Next
    End Sub

    Public Sub PopulateFacts(ByRef stream As StreamReader)

        Me.InitializeItems()
        Me.InitializeHeaders()

        Me.FastForwardStreamReader(stream)
        Me.LoadRespondents(stream)

        Me.items.Clear()
        Me.items = Nothing
        stream.Dispose()

    End Sub

    Public Sub InitializeItems()

        Me.items = New List(Of Item)

        Dim itemQuery = _
            From item In context.Items
            Where item.DatasetID = Me.currentDataset.ID
            Select item

        For Each i As Item In itemQuery
            Me.items.Add(i)
        Next

    End Sub

    Public Sub InitializeHeaders()

        Me.headers = New List(Of Header)

        Dim headerQuery = _
            From header In context.Headers
            Where header.DatasetID = Me.currentDataset.ID
            Select header

        For Each h As Header In headerQuery
            Me.headers.Add(h)
        Next

    End Sub

    Public Sub FastForwardStreamReader(ByRef stream As StreamReader)

        Dim minRespondentRowIndex As Integer = context.Respondents.Where(Function(r) r.DatasetID = currentDataset.ID).Min(Function(r) r.RowIndex)
        Dim rowIndex As Integer = 0

        Do
            stream.ReadLine()
            rowIndex += 1
            If rowIndex = minRespondentRowIndex - 1 Then Exit Sub
        Loop

    End Sub

    Public Sub LoadRespondents(ByRef stream As StreamReader)

        'Get respondents
        Dim respondents = _
                From respondent In context.Respondents
                Where respondent.DatasetID = Me.currentDataset.ID
                Order By respondent.RowIndex
                Select respondent

        For Each r As Respondent In respondents
            Me.LoadRespondentFacts(r, stream.ReadLine)
        Next

    End Sub

    Public Sub LoadRespondentFacts(respondent As Respondent, ByRef line As String)

        Dim bh As SQLBulkHelper = New SQLBulkHelper
        Dim f1 As DataTable = bh.GetEmptyDataTable(WXPDBDataTables.CategoricalFacts)
        Dim f2 As DataTable = bh.GetEmptyDataTable(WXPDBDataTables.NumericFacts)
        Dim f3 As DataTable = bh.GetEmptyDataTable(WXPDBDataTables.VariableFacts)
        Me.varIDs = New List(Of Integer)

        For Each i As Item In Me.items
            If i.IsDeleted Then
            Else
                Select Case CType(i.ItemTypeID, WXPDBItemTypes)
                    Case WXPDBItemTypes.Categorical
                        If GetBooleanItemValue(i, line) = True Then
                            Dim row As DataRow = f1.NewRow
                            row("ID") = 0
                            row("RespondentID") = respondent.ID
                            row("ItemID") = i.ID
                            row("DatasetID") = i.DatasetID
                            f1.Rows.Add(row)
                        End If
                    Case (WXPDBItemTypes.NumericAggregated)
                        Dim itemValue As Nullable(Of Double) = GetNumericItemValue(i, line)
                        If itemValue.HasValue Then
                            Dim row As DataRow = f2.NewRow
                            row("ID") = 0
                            row("RespondentID") = respondent.ID
                            row("ItemID") = i.ID
                            row("Value") = itemValue
                            row("DatasetID") = i.DatasetID
                            f2.Rows.Add(row)
                        End If
                       
                    Case WXPDBItemTypes.NumericGrouped
                        If Not Me.VariableExists(i.VariableID) Then
                            Dim itemValue As Nullable(Of Double) = GetNumericItemValue(i, line)
                            If itemValue.HasValue Then
                                Dim row As DataRow = f3.NewRow
                                row("ID") = 0
                                row("RespondentID") = respondent.ID
                                row("VariableID") = i.VariableID
                                row("Value") = itemValue
                                row("DatasetID") = i.DatasetID
                                f3.Rows.Add(row)
                            End If

                        End If
                    Case Else

                End Select

            End If
        Next

        bh.BulkInsert(f1)
        bh.BulkInsert(f2)
        bh.BulkInsert(f3)

    End Sub

    Private Function VariableExists(varID As Integer) As Boolean
        For Each id As Integer In Me.varIDs
            If varID = id Then Return True
        Next
        Me.varIDs.Add(varID)
        Return False
    End Function

    Public Function GetBooleanItemValue(item As Item, ByRef line As String) As Boolean
        Dim header As Header = Me.GetHeader(item.ColumnIndex)
        Dim value As Char = line.Substring(header.Position + item.ItemIndex - 1, 1)
        If value = "." Then
            Return False
        ElseIf Char.IsLetterOrDigit(value) Then
            Return True
        Else
            Dim message As String = "Invalid value at ItemID " & item.ID.ToString & ": " & value.ToString
            Throw New Exception(message)
        End If
    End Function

    Public Function GetNumericItemValue(item As Item, ByRef line As String) As Nullable(Of Double)
        Dim header As Header = Me.GetHeader(item.ColumnIndex)
        Dim stringValue As String = line.Substring(header.Position, header.Length).Trim()
        If IsNumeric(stringValue) Then
            Return CDbl(stringValue)
        Else
            Return Nothing
        End If

    End Function

    Public Function GetHeader(columnIndex As Integer) As Header
        For Each h As Header In Me.headers
            If h.ColumnIndex = columnIndex Then
                Return h
            End If
        Next
        Return Nothing
    End Function

    Public Function ConvertItemIndex(index As Char) As Integer
        Select Case index
            Case "A"
                Return 1
            Case "B"
                Return 2
            Case "C"
                Return 3
            Case "D"
                Return 4
            Case "V"
                Return 5
            Case "X"
                Return 6
            Case "0" To "9"
                Return CInt(index.ToString) + 7
            Case Else
                Return 0
        End Select
    End Function

End Class

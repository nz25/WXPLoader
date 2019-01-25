Public Class WXPRow

    Public Source As String
    Public RowIndex As Long
    Public RowType As WXPDBRowTypes
    Public RespondentID As String
    Public StartsWithAsterix As Boolean
    Public LabelStartsWithPercent As Boolean
    Public Prefix As String
    Public Label As String
    Public Level As String
    Public ColumnIndex As String
    Public ItemIndex As Char
    Public Net As String
    Public FactorAvg As String
    Public FactorMin As String
    Public FactorMax As String
    Public FactorLabel As String

    Public Sub Initialize(ByRef line As String, rowIndex As Long, type As WXPDBRowTypes)

        'Source & RowIndex
        Me.Source = line
        Me.RowIndex = rowIndex
        Me.RowType = type

    End Sub

    Public Sub Parse()

        Select Case Me.RowType
            Case WXPDBRowTypes.MetaData
                Me.ParseMetaDataRow()
            Case WXPDBRowTypes.CaseData
                Me.ParseCaseDataRow()
            Case Else

        End Select

    End Sub

    Private Sub ParseMetaDataRow()

        Dim line As String = Me.Source

        'StartsWithAsterix
        If line.StartsWith("*") Then
            Me.StartsWithAsterix = True
            line = line.Substring(1)
        Else
            Me.StartsWithAsterix = False
        End If

        'Prefix
        If line.StartsWith("L") Then
            Me.Prefix = "L"
            line = line.Substring(1)
        Else
            Me.Prefix = line.Substring(0, 2)
            line = line.Substring(2)
        End If

        'Level
        If Me.Prefix = "L" Then
            Me.Level = line.Substring(0, 1)
            line = line.Substring(1)
        Else
            Me.Level = String.Empty
        End If

        'ColumnIndex
        If Me.Prefix.Length = 2 Then
            Me.ColumnIndex = line.Substring(0, 4)
            If (Me.Prefix = "BD" Or Me.Prefix = "RW") And line.Length > 4 Then
                line = line.Substring(5)
            Else
                line = line.Substring(4)
            End If
        Else
            Me.ColumnIndex = String.Empty
        End If

        'ItemIndex
        If (Me.Prefix = "BD" Or Me.Prefix = "RW") And line.Length > 0 Then
            Me.ItemIndex = line.Substring(0, 1)
            line = line.Substring(2)
        Else
            Me.ItemIndex = String.Empty
        End If

        'Net
        If line.StartsWith("[") Then
            Me.Net = line.Substring(1, line.IndexOf("]") - 1)
            line = line.Substring(line.IndexOf("]") + 1)
        Else
            Me.Net = String.Empty
        End If

        'Factor Expression
        If line.Contains("!") Then
            Dim factorExpression As String = line.Substring(line.IndexOf("!") + 1)
            If factorExpression.Contains("P") Then
                FactorLabel = factorExpression.Substring(factorExpression.IndexOf("P") + 2)
                factorExpression = factorExpression.Substring(0, factorExpression.IndexOf("P"))
            Else
                FactorLabel = String.Empty
            End If

            If factorExpression.Contains("^") Then
                FactorAvg = factorExpression.Substring(0, factorExpression.IndexOf("^"))
                FactorMin = factorExpression.Substring(factorExpression.IndexOf("^") + 2, factorExpression.IndexOf(",") - factorExpression.IndexOf("^") - 2)
                FactorMax = factorExpression.Substring(factorExpression.IndexOf(",") + 2)
            Else
                FactorAvg = factorExpression
                FactorMin = String.Empty
                FactorMax = String.Empty
            End If
            line = line.Substring(0, line.IndexOf("!"))
        Else
            FactorAvg = String.Empty
            FactorMin = String.Empty
            FactorMax = String.Empty
            FactorLabel = String.Empty

        End If

        'Label
        If line.Length > 0 AndAlso line.Substring(0, 1) = "%" Then
            Me.LabelStartsWithPercent = True
            Me.Label = line.Substring(1)
        Else
            Me.Label = line
        End If

    End Sub

    Private Sub ParseCaseDataRow()

        Dim line As String = Me.Source
        Me.RespondentID = line.Substring(0, 16)

    End Sub

    Private Function RowBelongsToCaseData(ByRef line As String) As Boolean
        If line.Length >= 16 AndAlso IsNumeric(line.Substring(0, 16)) Then
            Return True
        Else
            Return False
        End If
    End Function

End Class


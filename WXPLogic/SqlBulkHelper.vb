Imports System.Configuration
Imports System.Data.Objects.DataClasses
Imports System.Data.SqlClient

Public Class SQLBulkHelper

    Public Const BulkInsertSize As Integer = 10000

    Private Function GetConnection() As SqlConnection
        Dim connectionString As String = ConfigurationManager.ConnectionStrings("wxp_db").ConnectionString
        Dim connection As SqlConnection = New SqlConnection()
        Return New SqlConnection(connectionString)
    End Function

    Public Function GetEmptyDataTable(tableName As WXPDBDataTables) As DataTable

        Dim sql As String = "select * from " & tableName.ToString & " where 1 = 0"
        Dim cmd As SqlCommand = New SqlCommand(sql, Me.GetConnection)
        Dim adapter As SqlDataAdapter = New SqlDataAdapter(cmd)
        Dim dataset As System.Data.DataSet = New System.Data.DataSet
        adapter.Fill(dataset)
        Dim table As DataTable = dataset.Tables(0)
        table.TableName = tableName.ToString

        Return table

    End Function

    Public Sub BulkInsert(table As DataTable)

        Dim bulkCopy As SqlBulkCopy = New SqlBulkCopy(Me.GetConnection.ConnectionString)
        bulkCopy.DestinationTableName = table.TableName
        bulkCopy.BulkCopyTimeout = 600
        bulkCopy.WriteToServer(table)

    End Sub

End Class


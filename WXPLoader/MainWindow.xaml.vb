Imports Microsoft.Win32
Imports WXPLogic

Class MainWindow

    Private Sub OpenFile_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles OpenFile.Click

        Dim dialog As OpenFileDialog = New OpenFileDialog()
        dialog.Title = "Choose WXP file"
        dialog.Filter = "WXP Files (*.wxp)|*.wxp"

        If dialog.ShowDialog() = True Then
            'Try
            Dim wxpManager As WXPManager = New WXPManager
            wxpManager.LoadWXP(dialog.FileName)

            'Catch ex As Exception
            'MessageBox.Show(ex.Message)
            'End Try

        End If

        MessageBox.Show("Done")

    End Sub



End Class

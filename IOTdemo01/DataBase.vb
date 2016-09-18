Imports System.Configuration
Imports MySql.Data.MySqlClient

Public Class Database
    Dim connectionStr As String
    Dim conn As MySqlConnection
    Dim cmd As MySqlCommand

    Sub New()
        connectionStr = ConfigurationSettings.AppSettings("MysqlConnection")
    End Sub

    Sub openDB()
        conn = New MySqlConnection(connectionStr)
        cmd = New MySqlCommand
        cmd.Connection = conn
        cmd.CommandType = CommandType.Text
        conn.Open()
    End Sub

    Sub AddData(node As Integer, sensortype As Integer, v1 As Single, Optional v2 As Single = 0)
        cmd.CommandText = "insert into data (src,sensortype,value1,value2) values ( " & node & " , " & sensortype & " , " & v1 & " , " & v2 & ")"
        cmd.ExecuteNonQuery()
    End Sub

    Sub closeDB()
        If IsNothing(conn) Then
            Exit Sub
        End If
        If conn.State = ConnectionState.Open Then
            conn.Close()
        End If
    End Sub
End Class

Imports System.IO.Ports
Imports System.Text
Imports System.Threading
Imports System.Net.Sockets
Imports System.Net


Public Class Form1
    Dim stopCommand As Boolean = False

    Dim headerPatten(1) As Integer
    Dim FrameData(100) As Integer
    Dim FrameLength As Integer
    Dim db As New Database
    Dim dbconnect As Boolean = False
    Dim counts As Integer = 0
    Dim NodeDBFlag() As Boolean = {True, True, True, True, True, True, True, True, True, True}

    Dim comm As New SerialPort

    Dim s1 As Socket = Nothing
    Dim t1 As Thread
    Dim t2 As Thread


    '通信端口监听数据
    Public Sub WaitData()
        Dim sendStr As String
        sendStr = "ok"
        s1 = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) '使用TCP协议
        Dim localEndPoint As New IPEndPoint(IPAddress.Parse("127.0.0.1"), 1024)   '指定IP和Port
        s1.Bind(localEndPoint)
        '绑定到该Socket
        s1.Listen(100)
        '侦听，最多接受100个连接
        While (True)
            Dim bytes(1024) As Byte
            Dim bytesBack(3) As Byte
            '用来存储接收到的字节
            Dim ss As Socket = s1.Accept()
            '若接收到,则创建一个新的Socket与之连接
            ss.Receive(bytes)
            '接收数据，若用ss.send(Byte()),则发送数据
            bytesBack = Encoding.Unicode.GetBytes(sendStr)
            ss.Send(bytesBack)
            ' ListBox1.Items.Insert(0, Encoding.Unicode.GetString(bytes))
            TextBox1.Text = Encoding.Unicode.GetString(bytes)
            '将其插入到列表框的第一项之前
            '若使用Encoding.ASCII.GetString(bytes),则接收到的中文字符不能正常显示
        End While
    End Sub
    Sub InitHeader()
        headerPatten = {&H26, &H26}
    End Sub

    '从串口读一个数据帧
    '返回值： 0：正确，aFrameData中存放读到的数据（行业编码+应用编码+应用数据类型码+应用数据）
    '         1：找不到包头
    '         2：包头校验错
    '         3：数据包校验错
    '         4：读超时
    Function ReadLine() As Integer
        Dim i, count As Integer
        Dim currentChar As Integer

        'Dim len1, len2, cmd, checkHeader As Byte
        Dim crc1, crc2 As Integer

        Try
            i = 0
            count = 0
            '包头寻找
            Do While i <= 1
                currentChar = comm.ReadByte
                If currentChar = headerPatten(i) Then
                    i = i + 1
                Else
                    i = 0
                End If
                count = count + 1
                If count > 200 Then
                    Return 1
                End If
            Loop

            'cmd = comm.ReadByte()
            'len1 = comm.ReadByte()
            'len2 = comm.ReadByte()
            'checkHeader = comm.ReadByte

            'If Not (((cmd Xor len1) Xor len2) = checkHeader) Then
            'Return 2
            'End If

            'FrameLength = len1

            'FrameLength = FrameLength * 256 + len2
            FrameLength = 18

            '读取数据
            For i = 0 To FrameLength - 1
                FrameData(i) = comm.ReadByte
            Next
            crc1 = comm.ReadByte()
            crc2 = comm.ReadByte()

            '此处为CRC校验，本程序没有实现
            If 0 Then '如果校验错
                Return 3
            End If
        Catch ex As Exception
            Return 4         '任何读串口错误，包括超时
        End Try
        Return 0  '成功 返回 0
    End Function

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        'FrameData = {&H27, &H1, &H0, &H0, &H1, &H0, &H2, &H0, &H2, &H0, &H0, &H1, &H1A, &HE3, &H6, &HCB}
        'FrameLength = 16
        ReadAndProcess()
    End Sub

    Sub ReadAndProcess()
        Dim retCode As Integer = 0

        If comport.Text.Trim = "" Then
            MsgBox("请选择一个 COM 口！")
            Exit Sub
        End If

        Button1.Enabled = False
        stopCommand = False

        comm.PortName = comport.Text
        comm.Open()

        Do While True
            retCode = ReadLine()

            If retCode > 0 Then
                ShowMsgBox(retCode)
                Exit Do
            End If

            If stopCommand Then
                Exit Do
            End If

            ProcessAFrameData()
            Application.DoEvents()
        Loop
        comm.Close()
        Button1.Enabled = True
     End Sub

    Sub ShowMsgBox(retCode As Integer)
        Select Case retCode
            Case 1
                MsgBox("找不到包头")
            Case 2
                MsgBox("包头校验错")
            Case 3
                MsgBox("数据包校验错")
            Case 4
                MsgBox("读超时")
        End Select
    End Sub

    Sub ProcessAFrameData()
        Dim TypeOfApp As String

        ShowFrameData()

        TypeOfApp = Chr(FrameData(0))
        'System.Console.WriteLine(TypeOfApp)
        Select Case TypeOfApp
            Case "E"
                NodeReport()  '基站报告节点传感器数据
                'Case &H10, &H11
                'LedRemoteControl() 'LED远程控制实验
            Case Else
                '其他功能
        End Select
    End Sub

    Sub NodeReport()
        Dim TypeOfSensor As String


        TypeOfSensor = Chr(FrameData(11))
        'TypeOfSensor = (TypeOfSensor << 8) + FrameData(4)
        System.Console.WriteLine(TypeOfSensor)
        Select Case TypeOfSensor
            Case "D"
                '温湿度
                TemperAndHumi()
            Case "L"
                '光照
                Light()
            Case Else
                '其它传感器
        End Select

    End Sub

    '温湿度传感器数据计算
    Sub TemperAndHumi()
        Dim node As Integer
        Dim t, h As Single

        'node = FrameData(7) * 256 + FrameData(8)
        node = 1


        t = CType(Val(Chr(FrameData(12))), Single) * 10 + CType(Val(Chr(FrameData(13))), Single)
        h = CType(Val(Chr(FrameData(14))), Single) * 10 + CType(Val(Chr(FrameData(15))), Single)

        't = -39.7 + t * 0.01
        'h = (t - 25) * (0.01 + 0.00008 * h) - 2.0468 + 0.0367 * h + (-0.0000015955 * h * h)
        ShowSensorData(1, node, {t, h})
        WriteToDB(node, 1, t, h)
    End Sub

    Sub Light()
        Dim node As Integer
        Dim l As Single
        'Dim ls As String

        'node = FrameData(7) * 256 + FrameData(8)
        node = 2

        l = CType(Val(Chr(FrameData(12))), Single)

        ShowSensorData(3, node, {l})
        WriteToDB(node, 3, l)
        'End If

        'l = 3.3 * l / 32767

    End Sub

    Sub WriteToDB(node As Integer, sensortype As Integer, v1 As Single, Optional v2 As Single = 0)
        If dbconnect Then
            If NodeDBFlag(node) = False Then
                db.AddData(node, sensortype, v1, v2)
                NodeDBFlag(node) = True
                counts += 1
                nos.Text = counts.ToString
            End If
        End If
    End Sub

    Sub ShowSensorData(sensor As Integer, node As Integer, SensorData As Double())
        Static Dim count As Integer = 0
        Static Dim TotalCount As Integer = 0
        Dim s As New StringBuilder

        If count >= 30 Then
            'count = 0
            SensorDataList.Items.RemoveAt(0)
        End If

        TotalCount += 1

        s.Clear()
        s.Append("No:" + TotalCount.ToString("0000000") + " ")
        s.Append("节点：")
        s.Append(Format(node, "00"))

        Select Case sensor
            Case 1
                s.Append(", 温度：")
                s.Append(FormatNumber(SensorData(0), 1))
                s.Append(", 湿度：")
                s.Append(FormatNumber(SensorData(1), 1))
            Case 3
                '光照传感器数据输出
                s.Append(", 光照：")
                s.Append(FormatNumber(SensorData(0), 1))
            Case Else
                '其它传感器数据输出
        End Select
        SensorDataList.Items.Add(s.ToString)
        count += 1
    End Sub

    Sub ShowFrameData()
        Static Dim count As Integer = 0
        Static Dim TotalCount As Integer = 0
        Dim s As New StringBuilder

        If count >= 10 Then
            'count = 0
            FrameDataList.Items.RemoveAt(0)
        End If

        TotalCount += 1
        s.Clear()
        s.Append("No:" + TotalCount.ToString("0000000") + " ")
        For i As Integer = 0 To FrameLength - 1
            If Hex(FrameData(i)).Length = 1 Then
                s.Append("0" + Hex(FrameData(i)) + " ")
            Else
                s.Append(Hex(FrameData(i)) + " ")
            End If
        Next
        FrameDataList.Items.Add(s.ToString)
        count += 1
    End Sub

    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        stopCommand = True
        If comm.IsOpen Then
            comm.Close()
        End If
        Try
            s1.Close()
            t1.Abort()
        Catch
        End Try
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        'ShowCommPorts()
        InitHeader()
        InitCOMport()
        ComboBox1.SelectedIndex = 1
        Control.CheckForIllegalCrossThreadCalls = False
    End Sub

    Sub InitCOMport()
        'comm.BaudRate = 9600
        comm.BaudRate = 115200
        comm.DataBits = 8
        comm.StopBits = 1
        comm.Parity = Parity.None
        comm.ReadBufferSize = 4096   '默认4096
        comm.ReadTimeout = 10000
    End Sub

    Sub ShowCommPorts()
        Dim portNames() As String = SerialPort.GetPortNames()
        comport.Items.Clear()
        For Each Com In portNames
            comport.Items.Add(Com)
        Next
    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        stopCommand = True
    End Sub

    Private Sub comport_DropDown(sender As Object, e As EventArgs) Handles comport.DropDown
        ShowCommPorts()
    End Sub

    Private Sub Button2_Click(sender As System.Object, e As System.EventArgs) Handles Button2.Click
        Try
            If dbconnect = True Then
                db.closeDB()
                dbconnect = False
            Else
                db.openDB()
                dbconnect = True
            End If
        Catch ex As Exception
            MsgBox("原因：" & vbCrLf & ex.Message, MsgBoxStyle.Critical, "连接数据库时发生错误!")
            dbconnect = False
        End Try
        If dbconnect = True Then
            Button2.Text = "数据库已连接"
        Else
            Button2.Text = "数据库已断开"
        End If
    End Sub

    Private Sub ComboBox1_SelectedIndexChanged(sender As System.Object, e As System.EventArgs) Handles ComboBox1.SelectedIndexChanged
        If ComboBox1.SelectedIndex + 1 > 0 Then
            Timer1.Interval = (ComboBox1.SelectedIndex + 1) * 1000
        End If
    End Sub

    Private Sub Timer1_Tick(sender As System.Object, e As System.EventArgs) Handles Timer1.Tick
        NodeDBFlag = {False, False, False, False, False, False, False, False, False, False}
    End Sub

    Private Sub FrameDataList_SelectedIndexChanged(sender As Object, e As EventArgs) Handles FrameDataList.SelectedIndexChanged

    End Sub

    Private Sub Label5_Click(sender As Object, e As EventArgs) Handles Label5.Click

    End Sub

    Private Sub SensorDataList_SelectedIndexChanged(sender As Object, e As EventArgs) Handles SensorDataList.SelectedIndexChanged

    End Sub

    Private Sub BtnStart_Click(sender As Object, e As EventArgs) Handles BtnStart.Click
        t1 = New Thread(AddressOf WaitData)
        '建立新的线程
        t1.Start()
        '启动线程
        BtnStart.Enabled = False
        '按钮不可用，避免另启线程)
    End Sub

    Private Sub BtnStop_Click(sender As Object, e As EventArgs) Handles BtnStop.Click
        Try
            s1.Close()
            '关闭Socket
            t1.Abort()
            '中止线程
        Catch
        Finally
            BtnStart.Enabled =
            True
            '启用BtnStart
        End Try
    End Sub
End Class
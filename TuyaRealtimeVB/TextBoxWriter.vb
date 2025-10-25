Imports System.IO
Imports System.Text
Imports System.Windows.Forms
Imports System.Linq

Public Class TextBoxWriter
    Inherits TextWriter

    Private _textBox As TextBox
    Private _encoding As System.Text.Encoding
    Private _buffer As New StringBuilder()
    Private _isPausedFunc As Func(Of Boolean)
    Private _lastMessage As String = ""
    Private _lastMessageTime As DateTime = DateTime.MinValue
    Private Shared ReadOnly _duplicateThreshold As TimeSpan = TimeSpan.FromMilliseconds(50)

    Public Sub New(textBox As TextBox, isPausedFunc As Func(Of Boolean))
        _textBox = textBox
        _encoding = System.Text.Encoding.UTF8
        _isPausedFunc = isPausedFunc
    End Sub

    Public Overrides ReadOnly Property Encoding As System.Text.Encoding
        Get
            Return _encoding
        End Get
    End Property

    Public Overrides Sub Write(value As Char)
        MyBase.Write(value)
        _buffer.Append(value)
        If value = vbLf OrElse value = vbCr Then
            Flush()
        End If
    End Sub

    Public Overrides Sub Write(value As String)
        MyBase.Write(value)
        _buffer.Append(value)
        Flush()
    End Sub

    Public Overrides Sub WriteLine(value As String)
        MyBase.WriteLine(value)
        _buffer.Append(value)
        Flush()
    End Sub

    Public Overrides Sub WriteLine()
        MyBase.WriteLine()
        Flush()
    End Sub

    Public Overrides Sub Flush()
        MyBase.Flush()
        If _isPausedFunc IsNot Nothing AndAlso _isPausedFunc() Then Return

        If _buffer.Length > 0 Then
            Dim fullText As String = _buffer.ToString()
            _buffer.Clear()

            Dim lines() As String = fullText.Split(New String() {vbCrLf, vbLf, vbCr}, StringSplitOptions.None)

            For Each line As String In lines
                Dim trimmedLine As String = line.Trim()
                If String.IsNullOrWhiteSpace(trimmedLine) Then Continue For

                Dim now As DateTime = DateTime.Now
                If trimmedLine = _lastMessage AndAlso (now - _lastMessageTime) < _duplicateThreshold Then
                    Continue For
                End If

                _lastMessage = trimmedLine
                _lastMessageTime = now

                AppendText(trimmedLine & Environment.NewLine)
            Next
        End If
    End Sub

    Private Sub AppendText(text As String)
        If String.IsNullOrWhiteSpace(text) Then Return

        If _textBox.InvokeRequired Then
            _textBox.Invoke(Sub() AppendText(text))
            Return
        End If

        Try
            Dim timestamp As String = DateTime.Now.ToString("HH:mm:ss.fff")
            Dim logMessage As String = $"[{timestamp}] [CONSOLE] {text}"

            If _textBox.Lines.Length > 10000 Then
                Dim lines = _textBox.Lines.Skip(1000).ToArray()
                _textBox.Lines = lines
            End If

            _textBox.AppendText(logMessage)
            _textBox.SelectionStart = _textBox.Text.Length
            _textBox.ScrollToCaret()
            _textBox.Update()
            Application.DoEvents()
        Catch ex As Exception
        End Try
    End Sub
End Class
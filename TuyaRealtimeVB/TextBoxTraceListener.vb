Imports System.Diagnostics
Imports System.Windows.Forms

Public Class TextBoxTraceListener
    Inherits TraceListener

    Private _textBox As TextBox
    Private _isPausedFunc As Func(Of Boolean)
    Private _lastMessage As String = ""
    Private _lastMessageTime As DateTime = DateTime.MinValue
    Private Shared ReadOnly _duplicateThreshold As TimeSpan = TimeSpan.FromMilliseconds(50)

    Public Sub New(textBox As TextBox, isPausedFunc As Func(Of Boolean))
        _textBox = textBox
        _isPausedFunc = isPausedFunc
    End Sub

    Public Overrides Sub Write(message As String)
        If _isPausedFunc IsNot Nothing AndAlso _isPausedFunc() Then Return
        AppendText(message, False)
    End Sub

    Public Overrides Sub WriteLine(message As String)
        If _isPausedFunc IsNot Nothing AndAlso _isPausedFunc() Then Return
        AppendText(message, True)
    End Sub

    Private Sub AppendText(text As String, addNewLine As Boolean)
        If String.IsNullOrEmpty(text) Then Return

        Dim fullText As String = If(addNewLine, text.Trim(), text)

        If addNewLine Then
            Dim now As DateTime = DateTime.Now
            If fullText = _lastMessage AndAlso (now - _lastMessageTime) < _duplicateThreshold Then
                Return
            End If
            _lastMessage = fullText
            _lastMessageTime = now
        End If

        If _textBox.InvokeRequired Then
            _textBox.Invoke(Sub() AppendText(text, addNewLine))
            Return
        End If

        Try
            Dim timestamp As String = DateTime.Now.ToString("HH:mm:ss.fff")
            Dim logMessage As String = $"[{timestamp}] [DEBUG] {fullText}"
            If addNewLine Then logMessage &= Environment.NewLine

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
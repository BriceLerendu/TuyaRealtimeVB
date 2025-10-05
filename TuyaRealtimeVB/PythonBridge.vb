Imports System.Diagnostics

Public Class PythonBridge
    Private _pythonProcess As Process
    Private _scriptPath As String

    Public Sub New(scriptPath As String)
        _scriptPath = scriptPath
    End Sub

    Public Sub Start()
        Try
            _pythonProcess = New Process()

            ' Configuration du processus Python
            _pythonProcess.StartInfo.FileName = "python"
            _pythonProcess.StartInfo.Arguments = $"""{_scriptPath}"""
            _pythonProcess.StartInfo.UseShellExecute = False
            _pythonProcess.StartInfo.RedirectStandardOutput = True
            _pythonProcess.StartInfo.RedirectStandardError = True
            _pythonProcess.StartInfo.CreateNoWindow = True
            _pythonProcess.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8
            _pythonProcess.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8

            ' Événements pour capturer la sortie Python
            AddHandler _pythonProcess.OutputDataReceived, AddressOf OnPythonOutput
            AddHandler _pythonProcess.ErrorDataReceived, AddressOf OnPythonError

            ' Démarrer Python
            _pythonProcess.Start()
            _pythonProcess.BeginOutputReadLine()
            _pythonProcess.BeginErrorReadLine()

            Console.WriteLine($"Script Python démarré : {_scriptPath}")

        Catch ex As Exception
            Console.WriteLine($"Erreur démarrage Python : {ex.Message}")
            Console.WriteLine("Vérifiez que Python est installé et accessible via la commande 'python'")
        End Try
    End Sub

    Private Sub OnPythonOutput(sender As Object, e As DataReceivedEventArgs)
        If Not String.IsNullOrEmpty(e.Data) Then
            Console.WriteLine($"[Python] {e.Data}")
        End If
    End Sub

    Private Sub OnPythonError(sender As Object, e As DataReceivedEventArgs)
        If Not String.IsNullOrEmpty(e.Data) Then
            Console.WriteLine($"[Python ERROR] {e.Data}")
        End If
    End Sub

    Public Sub [Stop]()
        If _pythonProcess IsNot Nothing AndAlso Not _pythonProcess.HasExited Then
            Console.WriteLine("Arrêt du script Python...")
            _pythonProcess.Kill()
            _pythonProcess.WaitForExit(2000)
            _pythonProcess.Dispose()
            Console.WriteLine("Script Python arrêté")
        End If
    End Sub
End Class
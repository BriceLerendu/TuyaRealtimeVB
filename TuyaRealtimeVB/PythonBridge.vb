Imports System.Diagnostics
Imports Newtonsoft.Json.Linq

Public Class PythonBridge
    Implements ITuyaRealtimeClient

    Private _pythonProcess As Process
    Private _scriptPath As String
    Private _isRunning As Boolean = False

    ' Implémentation de ITuyaRealtimeClient
    Public Event DeviceStatusChanged(deviceId As String, statusData As JObject) Implements ITuyaRealtimeClient.DeviceStatusChanged

    Public ReadOnly Property Mode As RealtimeMode Implements ITuyaRealtimeClient.Mode
        Get
            Return RealtimeMode.PythonBridge
        End Get
    End Property

    Public Sub New(scriptPath As String)
        _scriptPath = scriptPath
    End Sub

    Public Async Function StartAsync() As Task(Of Boolean) Implements ITuyaRealtimeClient.StartAsync
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
            _isRunning = True

            Console.WriteLine($"Script Python démarré : {_scriptPath}")

            ' Attendre un petit moment pour vérifier que le processus démarre bien
            Await Task.Delay(500)

            Return Not _pythonProcess.HasExited

        Catch ex As Exception
            Console.WriteLine($"Erreur démarrage Python : {ex.Message}")
            Console.WriteLine("Vérifiez que Python est installé et accessible via la commande 'python'")
            _isRunning = False
            Return False
        End Try
    End Function

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

    Public Function IsRunning() As Boolean Implements ITuyaRealtimeClient.IsRunning
        Return _isRunning AndAlso _pythonProcess IsNot Nothing AndAlso Not _pythonProcess.HasExited
    End Function

    Public Sub [Stop]() Implements ITuyaRealtimeClient.Stop
        If _pythonProcess IsNot Nothing AndAlso Not _pythonProcess.HasExited Then
            Console.WriteLine("Arrêt du script Python...")
            _pythonProcess.Kill()
            _pythonProcess.WaitForExit(2000)
            _pythonProcess.Dispose()
            Console.WriteLine("Script Python arrêté")
        End If
        _isRunning = False
    End Sub
End Class
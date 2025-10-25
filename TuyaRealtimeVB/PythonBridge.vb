Imports System.Diagnostics
Imports Newtonsoft.Json.Linq

''' <summary>
''' Client Python Bridge - Lance le script Python pour écouter Tuya Pulsar
''' </summary>
Public Class PythonBridge
    Implements ITuyaRealtimeClient

    Private _pythonProcess As Process
    Private _scriptPath As String
    Private _logCallback As Action(Of String)

    ''' <summary>
    ''' Événement déclenché lorsqu'un appareil change d'état
    ''' Note: Pour Python Bridge, les événements passent par TuyaHttpServer
    ''' </summary>
    Public Event DeviceStatusChanged(deviceId As String, statusData As JObject) _
        Implements ITuyaRealtimeClient.DeviceStatusChanged

    Public Sub New(scriptPath As String, Optional logCallback As Action(Of String) = Nothing)
        _scriptPath = scriptPath
        _logCallback = logCallback
    End Sub

    Public Function StartAsync() As Task(Of Boolean) Implements ITuyaRealtimeClient.StartAsync
        Try
            Start()
            Return Task.FromResult(IsRunning())
        Catch ex As Exception
            Log($"❌ Erreur StartAsync: {ex.Message}")
            Return Task.FromResult(False)
        End Try
    End Function

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

            Log($"✅ Script Python démarré : {_scriptPath}")

        Catch ex As Exception
            Log($"❌ Erreur démarrage Python : {ex.Message}")
            Log("Vérifiez que Python est installé et accessible via la commande 'python'")
        End Try
    End Sub

    Private Sub OnPythonOutput(sender As Object, e As DataReceivedEventArgs)
        If Not String.IsNullOrEmpty(e.Data) Then
            Log($"[Python] {e.Data}")
        End If
    End Sub

    Private Sub OnPythonError(sender As Object, e As DataReceivedEventArgs)
        If Not String.IsNullOrEmpty(e.Data) Then
            Log($"[Python ERROR] {e.Data}")
        End If
    End Sub

    Public Sub [Stop]() Implements ITuyaRealtimeClient.Stop
        If _pythonProcess IsNot Nothing AndAlso Not _pythonProcess.HasExited Then
            Log("Arrêt du script Python...")

            Try
                ' OPTIMISÉ : Tentative d'arrêt gracieux d'abord
                If _pythonProcess.CloseMainWindow() Then
                    Log("  Tentative d'arrêt gracieux...")
                    If _pythonProcess.WaitForExit(3000) Then
                        Log("  ✓ Script Python arrêté proprement")
                        _pythonProcess.Dispose()
                        Return
                    End If
                End If

                ' Si l'arrêt gracieux échoue, forcer l'arrêt
                Log("  Arrêt gracieux échoué, arrêt forcé...")
                _pythonProcess.Kill()

                If _pythonProcess.WaitForExit(2000) Then
                    Log("  ✓ Script Python arrêté (forcé)")
                Else
                    Log("  ⚠ Le processus Python ne répond pas")
                End If

            Catch ex As Exception
                Log($"  ⚠ Erreur lors de l'arrêt: {ex.Message}")
            Finally
                Try
                    _pythonProcess?.Dispose()
                Catch
                    ' Ignorer les erreurs de dispose
                End Try
                _pythonProcess = Nothing
            End Try
        End If
    End Sub

    Public Function IsRunning() As Boolean Implements ITuyaRealtimeClient.IsRunning
        Return _pythonProcess IsNot Nothing AndAlso Not _pythonProcess.HasExited
    End Function

    Public ReadOnly Property Mode As RealtimeMode Implements ITuyaRealtimeClient.Mode
        Get
            Return RealtimeMode.PythonBridge
        End Get
    End Property

    Private Sub Log(message As String)
        _logCallback?.Invoke(message)
    End Sub
End Class
Imports System.Collections.Generic

''' <summary>
''' ✅ PHASE 6 - Cache LRU (Least Recently Used) avec taille maximale
''' Éviction automatique des entrées les plus anciennes quand la limite est atteinte
''' Thread-safe pour utilisation concurrente
''' </summary>
Public Class LRUCache(Of TKey, TValue)
    Private ReadOnly _capacity As Integer
    Private ReadOnly _cache As New Dictionary(Of TKey, LinkedListNode(Of CacheItem))
    Private ReadOnly _lruList As New LinkedList(Of CacheItem)
    Private ReadOnly _lockObject As New Object()

    ' Métriques de performance
    Private _hits As Long = 0
    Private _misses As Long = 0

    Private Class CacheItem
        Public Property Key As TKey
        Public Property Value As TValue
        Public Property Expiry As DateTime
    End Class

    Public Sub New(capacity As Integer)
        If capacity <= 0 Then
            Throw New ArgumentException("La capacité doit être supérieure à 0", NameOf(capacity))
        End If
        _capacity = capacity
    End Sub

    ''' <summary>
    ''' Ajoute ou met à jour une entrée dans le cache
    ''' </summary>
    Public Sub Put(key As TKey, value As TValue, expiry As DateTime)
        SyncLock _lockObject
            If _cache.ContainsKey(key) Then
                ' Mettre à jour l'entrée existante et la déplacer en tête
                Dim node = _cache(key)
                node.Value.Value = value
                node.Value.Expiry = expiry
                _lruList.Remove(node)
                _lruList.AddFirst(node)
            Else
                ' Nouvelle entrée
                If _cache.Count >= _capacity Then
                    ' Éviction LRU : retirer le dernier élément (le moins récemment utilisé)
                    Dim lastNode = _lruList.Last
                    _lruList.RemoveLast()
                    _cache.Remove(lastNode.Value.Key)
                End If

                ' Ajouter la nouvelle entrée en tête
                Dim item As New CacheItem With {
                    .Key = key,
                    .Value = value,
                    .Expiry = expiry
                }
                Dim newNode = _lruList.AddFirst(item)
                _cache(key) = newNode
            End If
        End SyncLock
    End Sub

    ''' <summary>
    ''' Tente de récupérer une valeur du cache
    ''' </summary>
    ''' <returns>True si la valeur est trouvée et non expirée, False sinon</returns>
    Public Function TryGet(key As TKey, ByRef value As TValue) As Boolean
        SyncLock _lockObject
            If _cache.ContainsKey(key) Then
                Dim node = _cache(key)

                ' Vérifier l'expiration
                If DateTime.Now < node.Value.Expiry Then
                    ' Hit : déplacer en tête (most recently used)
                    _lruList.Remove(node)
                    _lruList.AddFirst(node)
                    value = node.Value.Value
                    _hits += 1
                    Return True
                Else
                    ' Expiré : retirer du cache
                    _lruList.Remove(node)
                    _cache.Remove(key)
                    _misses += 1
                    Return False
                End If
            Else
                ' Miss
                _misses += 1
                Return False
            End If
        End SyncLock
    End Function

    ''' <summary>
    ''' Retire une entrée du cache
    ''' </summary>
    Public Sub Remove(key As TKey)
        SyncLock _lockObject
            If _cache.ContainsKey(key) Then
                Dim node = _cache(key)
                _lruList.Remove(node)
                _cache.Remove(key)
            End If
        End SyncLock
    End Sub

    ''' <summary>
    ''' Vide complètement le cache
    ''' </summary>
    Public Sub Clear()
        SyncLock _lockObject
            _cache.Clear()
            _lruList.Clear()
        End SyncLock
    End Sub

    ''' <summary>
    ''' Nettoie les entrées expirées
    ''' </summary>
    Public Function ClearExpired() As Integer
        Dim removed As Integer = 0
        SyncLock _lockObject
            Dim now = DateTime.Now
            Dim nodesToRemove As New List(Of LinkedListNode(Of CacheItem))

            ' Identifier les nœuds expirés
            Dim currentNode = _lruList.First
            While currentNode IsNot Nothing
                If now >= currentNode.Value.Expiry Then
                    nodesToRemove.Add(currentNode)
                End If
                currentNode = currentNode.Next
            End While

            ' Retirer les nœuds expirés
            For Each node In nodesToRemove
                _lruList.Remove(node)
                _cache.Remove(node.Value.Key)
                removed += 1
            Next
        End SyncLock
        Return removed
    End Function

    ''' <summary>
    ''' Retourne le nombre d'entrées dans le cache
    ''' </summary>
    Public ReadOnly Property Count As Integer
        Get
            SyncLock _lockObject
                Return _cache.Count
            End Get
        End Property
    End ReadOnly

    ''' <summary>
    ''' Retourne le taux de hit (hits / (hits + misses))
    ''' </summary>
    Public ReadOnly Property HitRate As Double
        Get
            SyncLock _lockObject
                Dim total = _hits + _misses
                If total = 0 Then Return 0
                Return CDbl(_hits) / total
            End Get
        End Property
    End ReadOnly

    ''' <summary>
    ''' Retourne le nombre de hits
    ''' </summary>
    Public ReadOnly Property Hits As Long
        Get
            SyncLock _lockObject
                Return _hits
            End Get
        End Property
    End ReadOnly

    ''' <summary>
    ''' Retourne le nombre de misses
    ''' </summary>
    Public ReadOnly Property Misses As Long
        Get
            SyncLock _lockObject
                Return _misses
            End Get
        End Property
    End ReadOnly

    ''' <summary>
    ''' Réinitialise les métriques
    ''' </summary>
    Public Sub ResetMetrics()
        SyncLock _lockObject
            _hits = 0
            _misses = 0
        End SyncLock
    End Sub
End Class

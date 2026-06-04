using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// NetworkTransform с авторитетом ВЛАДЕЛЬЦА (а не сервера).
/// Нужен игроку: позицию задаёт тот клиент, которому принадлежит объект.
/// Это стабильный способ "Owner Authority" во всех версиях NGO 2.x.
/// </summary>
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    /// <summary>
    /// false = транспонирование не серверное, а от владельца объекта.
    /// </summary>
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}

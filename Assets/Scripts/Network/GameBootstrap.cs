using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Обычный объект сцены (БЕЗ NetworkObject), который при старте сервера
/// динамически спавнит префаб менеджера игры. Так в сцене нет ни одного
/// сетевого объекта — это убирает ошибки синхронизации in-scene NetworkObject.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject gameManagerPrefab; // префаб с NetworkGameManager

    /// <summary>
    /// Подписываемся на событие запуска сервера/хоста.
    /// </summary>
    private void Start()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
    }

    /// <summary>
    /// Отписываемся, чтобы не словить вызов на уничтоженном объекте.
    /// </summary>
    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
        }
    }

    /// <summary>
    /// Сервер поднялся — создаём и спавним менеджер игры в сеть.
    /// </summary>
    private void HandleServerStarted()
    {
        if (gameManagerPrefab == null) return;

        GameObject manager = Instantiate(gameManagerPrefab);
        manager.GetComponent<NetworkObject>().Spawn(true);
    }
}

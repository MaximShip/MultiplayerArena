using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Монетка: вращается для красоты, а при касании игроком (на сервере)
/// начисляет ему очко и исчезает у всех клиентов.
/// Спавнится сервером динамически, поэтому это сетевой объект.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class Coin : NetworkBehaviour
{
    [SerializeField] private int scoreValue = 1;     // сколько очков даёт монетка
    [SerializeField] private float spinSpeed = 90f;  // скорость вращения, град/сек

    /// <summary>
    /// Каждый кадр вращаем монетку вокруг вертикальной оси (визуал у всех).
    /// </summary>
    private void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    /// <summary>
    /// При касании игроком на сервере начисляем очко и убираем монетку.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Логику собирания считает только сервер.
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (!IsSpawned) return;

        if (other.CompareTag("Player"))
        {
            NetworkPlayer player = other.GetComponent<NetworkPlayer>();
            if (player != null)
            {
                player.AddScoreRpc(scoreValue);
                NetworkObject.Despawn(true); // удаляет монетку у всех клиентов
            }
        }
    }
}

using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Простой серверный ИИ-бот: преследует ближайшего игрока через NavMesh
/// и наносит урон при касании. Логика выполняется ТОЛЬКО на сервере,
/// на клиентах компонент отключается.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [SerializeField] private float hp = 50f; // здоровье бота

    private NavMeshAgent agent;       // компонент навигации
    private Transform targetPlayer;   // текущая цель преследования
    private bool aiStarted;           // запущен ли цикл поиска цели

    /// <summary>
    /// true только когда сервер реально поднят и слушает сеть.
    /// Хост запускается кнопкой ПОЗЖE нажатия Play, поэтому проверяем это
    /// каждый кадр, а не один раз в Start.
    /// </summary>
    private bool ServerActive =>
        NetworkManager.Singleton != null
        && NetworkManager.Singleton.IsListening
        && NetworkManager.Singleton.IsServer;

    /// <summary>
    /// Кэшируем и настраиваем агента. Логику ИИ здесь НЕ отключаем —
    /// она «проснётся» в Update, когда стартует хост.
    /// </summary>
    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = 3f;
        agent.stoppingDistance = 1.5f;
    }

    /// <summary>
    /// Находит ближайшего игрока среди всех NetworkPlayer на сцене.
    /// </summary>
    private void UpdateTarget()
    {
        if (!ServerActive) return;

        NetworkPlayer[] players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);

        float closest = float.MaxValue;
        targetPlayer = null;

        foreach (NetworkPlayer p in players)
        {
            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < closest)
            {
                closest = dist;
                targetPlayer = p.transform;
            }
        }
    }

    /// <summary>
    /// Пока сервер не поднят — бездействуем. Как только хост стартовал,
    /// один раз запускаем периодический поиск цели и ведём агента к ней.
    /// </summary>
    private void Update()
    {
        if (!ServerActive)
        {
            // На клиенте позицией бота управляет NetworkTransform (с сервера),
            // поэтому локальный NavMeshAgent надо выключить — иначе он «прибивает»
            // бота на месте и движение не видно.
            if (agent != null && agent.enabled) agent.enabled = false;
            return;
        }

        // Ленивая инициализация: запускаем поиск цели после старта хоста.
        if (!aiStarted)
        {
            aiStarted = true;
            InvokeRepeating(nameof(UpdateTarget), 0f, 0.5f);
        }

        if (targetPlayer != null && agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(targetPlayer.position);
        }
    }

    /// <summary>
    /// При касании игрока бот наносит ему урон через серверный Rpc.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        if (other.CompareTag("Player"))
        {
            NetworkPlayer player = other.GetComponent<NetworkPlayer>();
            if (player != null)
            {
                player.TakeDamageRpc(10f);
            }
        }
    }

    /// <summary>
    /// Получение урона. Считается только на сервере.
    /// При смерти начисляет очко убийце и уничтожает бота через 1 секунду.
    /// </summary>
    public void TakeDamage(float amount, ulong killerClientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        hp -= amount;

        if (hp <= 0f)
        {
            // Находим игрока-убийцу по clientId и начисляем очко.
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(killerClientId, out NetworkClient client)
                && client.PlayerObject != null)
            {
                NetworkPlayer killer = client.PlayerObject.GetComponent<NetworkPlayer>();
                if (killer != null)
                {
                    killer.AddScoreRpc(1);
                }
            }

            // Отключаем ИИ и убираем бота из сети с небольшой задержкой.
            enabled = false;
            NetworkObject netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                Invoke(nameof(DespawnSelf), 1f);
            }
            else
            {
                Destroy(gameObject, 1f);
            }
        }
    }

    /// <summary>
    /// Снимает бота с сети (сервер), что удаляет его и у всех клиентов.
    /// </summary>
    private void DespawnSelf()
    {
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
    }
}

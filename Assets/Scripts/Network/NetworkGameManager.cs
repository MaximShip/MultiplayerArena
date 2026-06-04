using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Менеджер раунда: ведёт таймер матча, точки спавна и завершение игры.
/// Синглтон, доступный через NetworkGameManager.Instance.
/// </summary>
public class NetworkGameManager : NetworkBehaviour
{
    // Глобальная точка доступа к менеджеру.
    public static NetworkGameManager Instance { get; private set; }

    // Оставшееся время раунда в секундах. Пишет только сервер.
    public NetworkVariable<float> timeLeft = new NetworkVariable<float>(
        120f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Флаг окончания игры.
    public NetworkVariable<bool> gameOver = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField] private GameObject enemyPrefab;  // префаб бота для сетевого спавна
    [SerializeField] private int enemyCount = 3;      // сколько ботов создать на старте
    [SerializeField] private GameObject coinPrefab;   // префаб монетки
    [SerializeField] private int startCoins = 6;      // сколько монеток в начале
    [SerializeField] private int maxCoins = 10;       // максимум монеток на арене
    [SerializeField] private float coinInterval = 5f; // период появления новых монеток

    private float nextCoinTime;   // когда спавнить следующую монетку
    private int currentCoins;     // сколько монеток сейчас на арене

    // Точки появления игроков заданы в коде, чтобы менеджер был
    // самодостаточным префабом и не зависел от объектов в сцене.
    private static readonly Vector3[] SpawnPositions =
    {
        new Vector3(10f, 1f, 10f), new Vector3(-10f, 1f, 10f),
        new Vector3(10f, 1f, -10f), new Vector3(-10f, 1f, -10f)
    };

    /// <summary>
    /// Назначаем синглтон как можно раньше.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// При старте сети только сервер создаёт ботов из префаба и спавнит их
    /// в сеть — так они корректно появляются и удаляются у всех клиентов
    /// (в отличие от ботов, расставленных в сцене вручную).
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SpawnEnemies();
            SpawnInitialCoins();
        }
    }

    /// <summary>
    /// Создаёт стартовую партию монеток на арене.
    /// </summary>
    private void SpawnInitialCoins()
    {
        for (int i = 0; i < startCoins; i++)
        {
            SpawnOneCoin();
        }
    }

    /// <summary>
    /// Спавнит одну монетку в случайной точке арены, если есть префаб
    /// и на арене ещё меньше maxCoins монеток.
    /// </summary>
    private void SpawnOneCoin()
    {
        if (coinPrefab == null) return;

        // Текущее число монеток считаем сканированием — надёжно и просто.
        currentCoins = FindObjectsByType<Coin>(FindObjectsSortMode.None).Length;
        if (currentCoins >= maxCoins) return;

        // Случайная позиция в пределах арены, чуть над полом.
        Vector3 pos = new Vector3(Random.Range(-18f, 18f), 1f, Random.Range(-18f, 18f));
        GameObject coin = Instantiate(coinPrefab, pos, Quaternion.identity);
        coin.GetComponent<NetworkObject>().Spawn(true);
    }

    /// <summary>
    /// Создаёт enemyCount ботов в разных точках арены и спавнит их по сети.
    /// </summary>
    private void SpawnEnemies()
    {
        if (enemyPrefab == null) return;

        Vector3[] positions =
        {
            new Vector3(8f, 1f, 8f), new Vector3(-8f, 1f, 8f), new Vector3(0f, 1f, -9f),
            new Vector3(7f, 1f, -6f), new Vector3(-7f, 1f, -5f), new Vector3(9f, 1f, 0f)
        };

        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 pos = positions[i % positions.Length];
            GameObject enemy = Instantiate(enemyPrefab, pos, Quaternion.identity);
            enemy.GetComponent<NetworkObject>().Spawn(true);
        }
    }

    /// <summary>
    /// Сбрасываем ссылку синглтона при уничтожении объекта.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Каждый кадр на сервере уменьшаем таймер; по нулю — конец игры.
    /// </summary>
    private void Update()
    {
        if (!IsServer || gameOver.Value) return;

        timeLeft.Value -= Time.deltaTime;

        // Периодически досыпаем монетки взамен собранных.
        if (Time.time >= nextCoinTime)
        {
            nextCoinTime = Time.time + coinInterval;
            SpawnOneCoin();
        }

        if (timeLeft.Value <= 0f)
        {
            timeLeft.Value = 0f;
            gameOver.Value = true;
            EndGameRpc();
        }
    }

    /// <summary>
    /// Завершение матча на всех клиентах: находим лидера по очкам
    /// и показываем результат через HUD.
    /// </summary>
    [Rpc(SendTo.Everyone)]
    private void EndGameRpc()
    {
        NetworkPlayer[] players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);

        NetworkPlayer winner = null;
        int best = -1;
        foreach (NetworkPlayer p in players)
        {
            if (p.score.Value > best)
            {
                best = p.score.Value;
                winner = p;
            }
        }

        string message = winner != null
            ? $"Игра окончена!\nПобедитель: игрок {winner.OwnerClientId} ({best} очков)"
            : "Игра окончена!";

        // Передаём результат в HUD, если он есть на сцене.
        HUDManager hud = FindFirstObjectByType<HUDManager>();
        if (hud != null)
        {
            hud.ShowGameOver(message);
        }
    }

    /// <summary>
    /// Возвращает случайную точку спавна из заранее заданного списка.
    /// </summary>
    public Vector3 GetRandomSpawnPoint()
    {
        int index = Random.Range(0, SpawnPositions.Length);
        return SpawnPositions[index];
    }
}

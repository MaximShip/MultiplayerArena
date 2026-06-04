using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Сетевое представление игрока: здоровье, очки и цвет.
/// Все значения синхронизируются через NetworkVariable, а изменения
/// состояния выполняются исключительно на сервере через Rpc.
/// </summary>
public class NetworkPlayer : NetworkBehaviour
{
    // Здоровье игрока. Читают все, писать может только сервер.
    public NetworkVariable<float> hp = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Счёт игрока (количество убитых ботов / попаданий).
    public NetworkVariable<int> score = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Цвет игрока — назначается случайно сервером при спавне.
    public NetworkVariable<Color> playerColor = new NetworkVariable<Color>(
        Color.white,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField] private Renderer bodyRenderer; // визуал тела для покраски

    /// <summary>
    /// Вызывается при появлении объекта в сети.
    /// На сервере выдаём случайный цвет, на всех клиентах подписываемся
    /// на изменения цвета и здоровья, чтобы обновлять визуал и UI.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        // Сервер раздаёт каждому игроку случайный цвет.
        if (IsServer)
        {
            playerColor.Value = new Color(Random.value, Random.value, Random.value);
        }

        // Подписываемся на изменение цвета и сразу применяем текущее значение.
        playerColor.OnValueChanged += OnColorChanged;
        ApplyColor(playerColor.Value);

        // Подписываемся на изменение здоровья (для логов/UI владельца).
        hp.OnValueChanged += OnHpChanged;
    }

    /// <summary>
    /// Отписываемся от событий при удалении объекта, чтобы избежать утечек.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        playerColor.OnValueChanged -= OnColorChanged;
        hp.OnValueChanged -= OnHpChanged;
    }

    /// <summary>
    /// Реакция на смену цвета — перекрашиваем материал тела.
    /// </summary>
    private void OnColorChanged(Color oldValue, Color newValue)
    {
        ApplyColor(newValue);
    }

    /// <summary>
    /// Реакция на изменение здоровья — выводим в консоль для владельца.
    /// HUD обновляется отдельно через подписку в HUDManager.
    /// </summary>
    private void OnHpChanged(float oldValue, float newValue)
    {
        if (IsOwner)
        {
            Debug.Log($"[NetworkPlayer] HP: {newValue}");
        }
    }

    /// <summary>
    /// Применяет цвет к рендереру тела, если он назначен.
    /// </summary>
    private void ApplyColor(Color color)
    {
        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = color;
        }
    }

    /// <summary>
    /// Нанесение урона. Выполняется только на сервере.
    /// При достижении 0 здоровья игрок отправляется на респаун.
    /// </summary>
    [Rpc(SendTo.Server)]
    public void TakeDamageRpc(float amount)
    {
        hp.Value -= amount;

        if (hp.Value <= 0f)
        {
            // Берём случайную точку появления у менеджера игры.
            Vector3 spawn = NetworkGameManager.Instance != null
                ? NetworkGameManager.Instance.GetRandomSpawnPoint()
                : Vector3.zero;

            hp.Value = 100f; // восстанавливаем здоровье на сервере
            RespawnRpc(spawn);
        }
    }

    /// <summary>
    /// Команда респауна, исполняемая на владельце:
    /// телепортирует игрока в указанную точку.
    /// </summary>
    [Rpc(SendTo.Owner)]
    public void RespawnRpc(Vector3 position)
    {
        // CharacterController мешает прямой установке позиции — временно выключаем.
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.position = position;

        if (cc != null) cc.enabled = true;
    }

    /// <summary>
    /// Добавление очков игроку. Только на сервере.
    /// </summary>
    [Rpc(SendTo.Server)]
    public void AddScoreRpc(int points)
    {
        score.Value += points;
    }
}

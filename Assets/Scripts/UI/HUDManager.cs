using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Отображение игрового интерфейса локального игрока:
/// полоса здоровья, счёт, таймер раунда и экран окончания игры.
/// </summary>
public class HUDManager : MonoBehaviour
{
    [SerializeField] private Image hpBar;             // полоса здоровья (Image type = Filled)
    [SerializeField] private TMP_Text scoreText;      // текст счёта
    [SerializeField] private TMP_Text timerText;      // текст таймера
    [SerializeField] private GameObject gameOverPanel; // панель окончания игры
    [SerializeField] private TMP_Text gameOverText;   // текст результата

    private NetworkPlayer localPlayer; // ссылка на сетевого игрока этого клиента

    /// <summary>
    /// Прячем панель окончания и пытаемся найти локального игрока.
    /// Игрок может появиться не сразу — добавляем повтор через Invoke.
    /// </summary>
    private void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        TryBindLocalPlayer();
    }

    /// <summary>
    /// Находит PlayerObject локального клиента и подписывается
    /// на изменения здоровья и счёта.
    /// </summary>
    private void TryBindLocalPlayer()
    {
        // Вне сети (нет хоста/клиента) выходим — попробуем позже.
        if (NetworkManager.Singleton == null
            || NetworkManager.Singleton.LocalClient == null
            || NetworkManager.Singleton.LocalClient.PlayerObject == null)
        {
            Invoke(nameof(TryBindLocalPlayer), 0.5f);
            return;
        }

        localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayer>();
        if (localPlayer == null) return;

        // Подписываемся и сразу обновляем значения под текущее состояние.
        localPlayer.hp.OnValueChanged += OnHpChanged;
        localPlayer.score.OnValueChanged += OnScoreChanged;
        OnHpChanged(0f, localPlayer.hp.Value);
        OnScoreChanged(0, localPlayer.score.Value);
    }

    /// <summary>
    /// Отписываемся при уничтожении, чтобы не обращаться к мёртвым объектам.
    /// </summary>
    private void OnDestroy()
    {
        if (localPlayer != null)
        {
            localPlayer.hp.OnValueChanged -= OnHpChanged;
            localPlayer.score.OnValueChanged -= OnScoreChanged;
        }
    }

    /// <summary>
    /// Обновление полосы здоровья (0..1 от 100 HP).
    /// </summary>
    private void OnHpChanged(float oldValue, float newValue)
    {
        if (hpBar != null) hpBar.fillAmount = Mathf.Clamp01(newValue / 100f);
    }

    /// <summary>
    /// Обновление текста счёта.
    /// </summary>
    private void OnScoreChanged(int oldValue, int newValue)
    {
        if (scoreText != null) scoreText.text = $"Счёт: {newValue}";
    }

    /// <summary>
    /// Каждый кадр обновляем таймер из менеджера игры (если он есть).
    /// </summary>
    private void Update()
    {
        if (NetworkGameManager.Instance != null && timerText != null)
        {
            float t = NetworkGameManager.Instance.timeLeft.Value;
            int minutes = Mathf.FloorToInt(t / 60f);
            int seconds = Mathf.FloorToInt(t % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    /// <summary>
    /// Показывает экран окончания игры с переданным сообщением.
    /// </summary>
    public void ShowGameOver(string message)
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (gameOverText != null) gameOverText.text = message;

        // Освобождаем курсор, чтобы можно было взаимодействовать с UI.
        Cursor.lockState = CursorLockMode.None;
    }
}

using System;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Точка входа в сетевую игру: инициализирует Unity Services,
/// поднимает хост/клиента через Relay (по join code) или локально.
/// </summary>
public class NetworkLauncher : MonoBehaviour
{
    [SerializeField] private TMP_InputField joinCodeInput;   // поле ввода кода для клиента
    [SerializeField] private TMP_Text statusText;            // строка статуса/ошибок
    [SerializeField] private TMP_Text joinCodeDisplay;       // показ кода для хоста

    private const int MaxPlayers = 2; // максимум игроков (хост + 1 клиент)

    /// <summary>
    /// При старте инициализируем сервисы Unity и выполняем анонимный вход.
    /// </summary>
    private async void Start()
    {
        try
        {
            // Инициализация ядра сервисов (нужно для Auth и Relay).
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            // Анонимный вход, если ещё не авторизованы.
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            SetStatus("Сервисы готовы. Можно создавать или подключаться к игре.");
        }
        catch (Exception e)
        {
            SetStatus($"Ошибка инициализации сервисов: {e.Message}");
        }
    }

    /// <summary>
    /// Создаёт хост через Relay: аллокация, получение кода,
    /// настройка транспорта и запуск хоста.
    /// </summary>
    public async void StartHostRelay()
    {
        try
        {
            // 1. Резервируем место на Relay-сервере под нужное число игроков.
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);

            // 2. Получаем join code, который введёт второй игрок.
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // 3. Прокидываем данные Relay в транспорт NGO.
            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            GetTransport().SetRelayServerData(relayServerData);

            // 4. Запускаем хост.
            NetworkManager.Singleton.StartHost();

            // 5. Показываем код подключения и прячем кнопки лобби.
            if (joinCodeDisplay != null) joinCodeDisplay.text = $"Код: {joinCode}";
            SetStatus("Хост запущен. Передайте код второму игроку.");
            HideLobbyControls();
        }
        catch (Exception e)
        {
            SetStatus($"Не удалось создать хост: {e.Message}");
        }
    }

    /// <summary>
    /// Подключается к хосту через Relay по введённому join code.
    /// </summary>
    public async void StartClientRelay()
    {
        try
        {
            string code = joinCodeInput != null ? joinCodeInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(code))
            {
                SetStatus("Введите код подключения.");
                return;
            }

            // 1. Присоединяемся к аллокации хоста по коду.
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);

            // 2. Настраиваем транспорт данными Relay.
            RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
            GetTransport().SetRelayServerData(relayServerData);

            // 3. Запускаем клиента.
            NetworkManager.Singleton.StartClient();
            SetStatus("Подключение к хосту...");
            HideLobbyControls();
        }
        catch (Exception e)
        {
            SetStatus($"Не удалось подключиться: {e.Message}");
        }
    }

    /// <summary>
    /// Локальный хост без Relay (для теста на одной машине / Play Mode).
    /// </summary>
    public void StartHostLocal()
    {
        if (NetworkManager.Singleton == null)
        {
            SetStatus("NetworkManager не найден на сцене.");
            return;
        }

        NetworkManager.Singleton.StartHost();
        SetStatus("Локальный хост запущен.");
        HideLobbyControls();
    }

    /// <summary>
    /// Локальный клиент без Relay (подключение к 127.0.0.1).
    /// </summary>
    public void StartClientLocal()
    {
        if (NetworkManager.Singleton == null)
        {
            SetStatus("NetworkManager не найден на сцене.");
            return;
        }

        NetworkManager.Singleton.StartClient();
        SetStatus("Подключение к локальному хосту...");
        HideLobbyControls();
    }

    /// <summary>
    /// Прячет кнопки и поле ввода кода после старта игры,
    /// оставляя на экране статус и код подключения (нужен хосту).
    /// </summary>
    private void HideLobbyControls()
    {
        foreach (Button b in GetComponentsInChildren<Button>(true))
        {
            b.gameObject.SetActive(false);
        }
        if (joinCodeInput != null) joinCodeInput.gameObject.SetActive(false);
    }

    /// <summary>
    /// Возвращает компонент UnityTransport, привязанный к NetworkManager.
    /// </summary>
    private UnityTransport GetTransport()
    {
        return NetworkManager.Singleton.GetComponent<UnityTransport>();
    }

    /// <summary>
    /// Выводит сообщение в UI и в консоль.
    /// </summary>
    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log($"[NetworkLauncher] {message}");
    }
}

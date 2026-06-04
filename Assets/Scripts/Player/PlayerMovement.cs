using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Управление перемещением и поворотом игрока от первого лица.
/// Работает только у владельца объекта (IsOwner).
/// Использует CharacterController и сгенерированный класс InputSystem_Actions.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float speed = 5f;            // скорость ходьбы
    [SerializeField] private float mouseSensitivity = 2f; // чувствительность мыши
    [SerializeField] private Transform cameraTransform;   // дочерняя камера для наклона

    private CharacterController controller;
    private InputSystem_Actions input;
    private float verticalVelocity;   // накопленная вертикальная скорость (гравитация)
    private float cameraPitch;        // текущий угол наклона камеры по X

    /// <summary>
    /// Кэшируем CharacterController при инициализации.
    /// </summary>
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    /// <summary>
    /// При появлении в сети только владелец создаёт и включает ввод,
    /// блокирует курсор и оставляет свою камеру активной. У чужих игроков
    /// камера и звук отключаются, чтобы не было конфликта нескольких камер.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        // Камера и AudioListener активны только у владельца объекта.
        if (cameraTransform != null)
        {
            Camera cam = cameraTransform.GetComponent<Camera>();
            if (cam != null) cam.enabled = IsOwner;

            AudioListener listener = cameraTransform.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = IsOwner;
        }

        if (!IsOwner) return;

        input = new InputSystem_Actions();
        input.Enable();

        Cursor.lockState = CursorLockMode.Locked;
    }

    /// <summary>
    /// Освобождаем ресурсы ввода при удалении объекта владельца.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        if (input != null)
        {
            input.Disable();
            input.Dispose();
            input = null;
        }
    }

    /// <summary>
    /// Каждый кадр обрабатываем ввод только для своего игрока.
    /// </summary>
    private void Update()
    {
        if (!IsOwner || input == null) return;

        HandleLook();
        HandleMovement();
    }

    /// <summary>
    /// Поворот: мышь по X вращает всё тело вокруг оси Y,
    /// мышь по Y наклоняет дочернюю камеру с ограничением ±80°.
    /// </summary>
    private void HandleLook()
    {
        Vector2 look = input.Player.Look.ReadValue<Vector2>() * mouseSensitivity * 0.1f;

        // Горизонтальный поворот тела.
        transform.Rotate(Vector3.up, look.x);

        // Вертикальный наклон камеры с клампом.
        cameraPitch -= look.y;
        cameraPitch = Mathf.Clamp(cameraPitch, -80f, 80f);
        if (cameraTransform != null)
        {
            cameraTransform.localEulerAngles = new Vector3(cameraPitch, 0f, 0f);
        }
    }

    /// <summary>
    /// Перемещение по горизонтали через WASD и ручная гравитация.
    /// </summary>
    private void HandleMovement()
    {
        Vector2 move = input.Player.Move.ReadValue<Vector2>();

        // Локальное направление: вперёд/вбок относительно тела.
        Vector3 horizontal = (transform.right * move.x + transform.forward * move.y) * speed;

        // Ручная гравитация: на земле сбрасываем, в воздухе накапливаем.
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -1f;
        }
        verticalVelocity -= 9.81f * Time.deltaTime;

        Vector3 velocity = horizontal + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }
}

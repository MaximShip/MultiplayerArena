using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Стрельба игрока лучом (raycast) из позиции камеры.
/// Работает только у владельца. Урон наносится через серверные Rpc,
/// чтобы изменения здоровья были авторитетными.
/// </summary>
public class PlayerShoot : NetworkBehaviour
{
    [SerializeField] private float fireRate = 0.3f; // кулдаун между выстрелами
    [SerializeField] private float range = 50f;     // дальность луча
    [SerializeField] private Transform cameraTransform; // источник луча

    private InputSystem_Actions input;
    private float nextFireTime; // время, когда снова можно стрелять

    /// <summary>
    /// Владелец создаёт и включает карту ввода для чтения Fire.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        input = new InputSystem_Actions();
        input.Enable();
    }

    /// <summary>
    /// Очищаем ввод при удалении объекта владельца.
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
    /// Каждый кадр проверяем нажатие Fire только для своего игрока.
    /// </summary>
    private void Update()
    {
        if (!IsOwner || input == null) return;

        // Учитываем кулдаун и факт нажатия кнопки.
        if (Time.time >= nextFireTime && input.Player.Fire.IsPressed())
        {
            nextFireTime = Time.time + fireRate;
            Shoot();
        }
    }

    /// <summary>
    /// Выпускает луч вперёд от камеры и обрабатывает попадание
    /// по игроку или по врагу.
    /// </summary>
    private void Shoot()
    {
        // Если камера не назначена, стреляем из позиции самого объекта.
        Vector3 origin = cameraTransform != null ? cameraTransform.position : transform.position;
        Vector3 direction = cameraTransform != null ? cameraTransform.forward : transform.forward;

        // Визуализация выстрела для прототипа.
        Debug.DrawRay(origin, direction * range, Color.red, 1f);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, range))
        {
            // Попадание по другому игроку — наносим урон через серверный Rpc.
            if (hit.collider.CompareTag("Player"))
            {
                NetworkPlayer target = hit.collider.GetComponent<NetworkPlayer>();
                if (target != null && target != GetComponent<NetworkPlayer>())
                {
                    target.TakeDamageRpc(25f);
                }
            }
            // Попадание по врагу — урон уходит через серверный Rpc стрелка.
            else if (hit.collider.CompareTag("Enemy"))
            {
                EnemyAI enemy = hit.collider.GetComponent<EnemyAI>();
                NetworkObject enemyObj = enemy != null ? enemy.GetComponent<NetworkObject>() : null;

                // Ссылку можно создать только из заспавненного объекта,
                // иначе NetworkObjectReference бросает исключение.
                if (enemyObj != null && enemyObj.IsSpawned)
                {
                    DamageEnemyServerRpc(new NetworkObjectReference(enemyObj), 25f);
                }
            }
        }
    }

    /// <summary>
    /// Серверный Rpc нанесения урона врагу.
    /// Урон по AI должен считаться на сервере (там живёт логика бота).
    /// </summary>
    [Rpc(SendTo.Server)]
    private void DamageEnemyServerRpc(NetworkObjectReference enemyRef, float amount)
    {
        if (enemyRef.TryGet(out NetworkObject enemyObject))
        {
            EnemyAI enemy = enemyObject.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                // Передаём стрелка, чтобы начислить ему очко за убийство.
                enemy.TakeDamage(amount, OwnerClientId);
            }
        }
    }
}

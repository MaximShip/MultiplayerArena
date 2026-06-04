using TMPro;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Автосборщик сцены арены. Один пункт меню "Tools/Build Arena Scene"
/// создаёт новую сцену и программно собирает всё, что в README расписано руками:
/// NetworkManager, префабы игрока и врага, землю с укрытиями, NavMesh,
/// менеджер игры со спавн-точками, HUD и меню подключения (Lobby).
/// Скрипт работает только в редакторе (папка Editor).
/// </summary>
public static class ArenaBuilder
{
    private const string ScenePath = "Assets/Scenes/Arena.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
    private const string GameManagerPrefabPath = "Assets/Prefabs/GameManager.prefab";

    /// <summary>
    /// Точка входа из меню редактора: собирает всю сцену с нуля.
    /// </summary>
    [MenuItem("Tools/Build Arena Scene")]
    public static void Build()
    {
        try
        {
            EnsureFolders();

            // 1. Чистая сцена с дефолтными объектами (свет + камера).
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Главную камеру оставляем как "лобби-вид", но без AudioListener
            // и с низким приоритетом, чтобы её перекрывала камера игрока.
            Camera mainCam = Object.FindFirstObjectByType<Camera>();
            if (mainCam != null)
            {
                mainCam.depth = -1f;
                mainCam.transform.position = new Vector3(0f, 15f, -15f);
                mainCam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
                AudioListener al = mainCam.GetComponent<AudioListener>();
                if (al != null) Object.DestroyImmediate(al);
            }

            // 2. Окружение: земля и укрытия.
            BuildEnvironment(out GameObject ground);

            // 3. Префабы: игрок, враг и менеджер игры.
            //    В сцене НЕ держим ни одного сетевого объекта — всё спавнится
            //    динамически с сервера. Это убирает ошибки синхронизации сцены.
            GameObject playerPrefab = BuildPlayerPrefab();
            GameObject enemyPrefab = BuildEnemyPrefab();
            GameObject gameManagerPrefab = BuildGameManagerPrefab(enemyPrefab);

            // 4. NetworkManager: транспорт + регистрация всех сетевых префабов.
            BuildNetworkManager(playerPrefab, enemyPrefab, gameManagerPrefab);

            // 5. Объект-бутстрап в сцене: при старте хоста спавнит менеджер игры.
            BuildGameBootstrap(gameManagerPrefab);

            // 7. NavMesh для ботов.
            BakeNavMesh(ground);

            // 8. UI: EventSystem, HUD и Lobby.
            BuildEventSystem();
            BuildHud();
            BuildLobby();

            // 9. Сохраняем сцену и добавляем её в Build Settings.
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=lime>[ArenaBuilder] Сцена арены собрана: " + ScenePath + "</color>\n" +
                      "Если боты не двигаются после перезапуска — выделите Ground → NavMesh Surface → Bake.");
            EditorUtility.DisplayDialog("Arena Builder",
                "Готово! Сцена собрана и сохранена в " + ScenePath +
                "\n\nНажмите Play и используйте кнопки Host/Join.", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ArenaBuilder] Ошибка сборки: " + e);
            EditorUtility.DisplayDialog("Arena Builder", "Ошибка: " + e.Message, "OK");
        }
    }

    // ───────────────────────────── Окружение ─────────────────────────────

    /// <summary>
    /// Создаёт плоскую землю 50x50 и несколько кубов-укрытий, помечает статикой.
    /// </summary>
    private static void BuildEnvironment(out GameObject ground)
    {
        ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(5f, 1f, 5f); // 10*5 = 50 метров
        MarkStatic(ground);

        // 6 кубов-укрытий в псевдослучайных местах.
        Vector3[] coverPositions =
        {
            new Vector3(6f, 1f, 4f), new Vector3(-7f, 1f, 3f), new Vector3(3f, 1f, -8f),
            new Vector3(-5f, 1f, -6f), new Vector3(9f, 1f, -2f), new Vector3(-3f, 1f, 9f)
        };
        foreach (Vector3 pos in coverPositions)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Cover";
            cube.transform.position = pos;
            cube.transform.localScale = new Vector3(2f, 2f, 2f);
            MarkStatic(cube);
        }
    }

    // ───────────────────────────── Префаб игрока ─────────────────────────────

    /// <summary>
    /// Собирает капсулу-игрока со всеми сетевыми компонентами и камерой,
    /// связывает внутренние ссылки и сохраняет как префаб.
    /// </summary>
    private static GameObject BuildPlayerPrefab()
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "NetworkPlayer";
        player.tag = "Player";

        // CharacterController сам служит коллайдером — лишний капсульный убираем.
        Collider capsuleCollider = player.GetComponent<Collider>();
        if (capsuleCollider != null) Object.DestroyImmediate(capsuleCollider);

        CharacterController cc = player.AddComponent<CharacterController>();
        cc.center = new Vector3(0f, 0f, 0f);
        cc.height = 2f;
        cc.radius = 0.5f;

        player.AddComponent<NetworkObject>();
        player.AddComponent<ClientNetworkTransform>(); // авторитет владельца

        PlayerMovement movement = player.AddComponent<PlayerMovement>();
        PlayerShoot shoot = player.AddComponent<PlayerShoot>();
        NetworkPlayer netPlayer = player.AddComponent<NetworkPlayer>();

        // Дочерняя камера на уровне "глаз".
        GameObject camObj = new GameObject("PlayerCamera");
        camObj.transform.SetParent(player.transform, false);
        camObj.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        Camera cam = camObj.AddComponent<Camera>();
        cam.depth = 0f;
        camObj.AddComponent<AudioListener>();

        // Связываем приватные [SerializeField] поля через SerializedObject.
        SetRef(movement, "cameraTransform", camObj.transform);
        SetRef(shoot, "cameraTransform", camObj.transform);
        SetRef(netPlayer, "bodyRenderer", player.GetComponent<MeshRenderer>());

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
        Object.DestroyImmediate(player); // убираем экземпляр со сцены
        return prefab;
    }

    // ───────────────────────────── Префаб врага ─────────────────────────────

    /// <summary>
    /// Собирает капсулу-бота с NavMeshAgent, сетевыми компонентами и
    /// триггер-коллайдером, сохраняет как префаб.
    /// </summary>
    private static GameObject BuildEnemyPrefab()
    {
        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemy.name = "Enemy";
        enemy.tag = "Enemy";

        // Коллайдер делаем триггером — бот наносит урон касанием.
        CapsuleCollider col = enemy.GetComponent<CapsuleCollider>();
        if (col != null) col.isTrigger = true;

        NavMeshAgent agent = enemy.AddComponent<NavMeshAgent>();
        agent.speed = 3f;
        agent.stoppingDistance = 1.5f;

        enemy.AddComponent<NetworkObject>();
        // Серверный NetworkTransform — клиенты видят движение бота.
        enemy.AddComponent<Unity.Netcode.Components.NetworkTransform>();
        enemy.AddComponent<EnemyAI>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(enemy, EnemyPrefabPath);
        Object.DestroyImmediate(enemy);
        return prefab;
    }

    // ───────────────────────────── NetworkManager ─────────────────────────────

    /// <summary>
    /// Создаёт NetworkManager + UnityTransport, назначает Player Prefab,
    /// регистрирует сетевые префабы (враг, менеджер) и ВЫКЛЮЧАЕТ управление
    /// сценами — у нас одна статичная сцена и нет сетевых объектов в ней,
    /// поэтому синхронизация сцены не нужна (и не падает).
    /// </summary>
    private static void BuildNetworkManager(GameObject playerPrefab, GameObject enemyPrefab, GameObject gameManagerPrefab)
    {
        GameObject nmObj = new GameObject("NetworkManager");
        NetworkManager nm = nmObj.AddComponent<NetworkManager>();
        UnityTransport utp = nmObj.AddComponent<UnityTransport>();

        if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
        nm.NetworkConfig.NetworkTransport = utp;
        nm.NetworkConfig.PlayerPrefab = playerPrefab;
        nm.NetworkConfig.EnableSceneManagement = false;

        // Регистрируем сетевые префабы (в обёртке try/catch на случай отличий
        // API версий — тогда добавить вручную в инспекторе).
        try
        {
            nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = enemyPrefab });
            nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = gameManagerPrefab });
        }
        catch (System.Exception)
        {
            Debug.LogWarning("[ArenaBuilder] Не удалось авто-зарегистрировать префабы. " +
                             "Добавьте Enemy и GameManager вручную: NetworkManager → Network Prefabs Lists.");
        }

        EditorUtility.SetDirty(nm);
    }

    // ───────────────────────────── GameManager ─────────────────────────────

    /// <summary>
    /// Собирает ПРЕФАБ менеджера игры (NetworkObject + NetworkGameManager) и
    /// прописывает в нём ссылку на префаб врага. Точки спавна заданы в коде
    /// менеджера, поэтому объектов в сцене не требуется.
    /// </summary>
    private static GameObject BuildGameManagerPrefab(GameObject enemyPrefab)
    {
        GameObject gmObj = new GameObject("GameManager");
        gmObj.AddComponent<NetworkObject>();
        NetworkGameManager gm = gmObj.AddComponent<NetworkGameManager>();

        SetRef(gm, "enemyPrefab", enemyPrefab);
        SetInt(gm, "enemyCount", 3);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(gmObj, GameManagerPrefabPath);
        Object.DestroyImmediate(gmObj);
        return prefab;
    }

    /// <summary>
    /// Создаёт в сцене обычный объект GameBootstrap (без NetworkObject),
    /// который при старте хоста заспавнит менеджер игры по сети.
    /// </summary>
    private static void BuildGameBootstrap(GameObject gameManagerPrefab)
    {
        GameObject boot = new GameObject("GameBootstrap");
        GameBootstrap bootstrap = boot.AddComponent<GameBootstrap>();
        SetRef(bootstrap, "gameManagerPrefab", gameManagerPrefab);
    }

    // ───────────────────────────── NavMesh ─────────────────────────────

    /// <summary>
    /// Вешает NavMeshSurface на землю и печёт навигацию для ботов.
    /// </summary>
    private static void BakeNavMesh(GameObject ground)
    {
        try
        {
            NavMeshSurface surface = ground.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.BuildNavMesh();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[ArenaBuilder] Не удалось автоматически запечь NavMesh: " + e.Message +
                             "\nВыделите Ground → NavMesh Surface → Bake вручную.");
        }
    }

    // ───────────────────────────── UI: EventSystem ─────────────────────────────

    /// <summary>
    /// Создаёт EventSystem с модулем ввода нового Input System (нужен для кнопок).
    /// </summary>
    private static void BuildEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    // ───────────────────────────── UI: HUD ─────────────────────────────

    /// <summary>
    /// Создаёт игровой HUD (здоровье, счёт, таймер, экран Game Over)
    /// и связывает ссылки в HUDManager.
    /// </summary>
    private static void BuildHud()
    {
        Canvas canvas = CreateCanvas("HUDCanvas");

        // Полоса здоровья (Image type = Filled) слева снизу.
        Image hpBar = CreateImage("HpBar", canvas.transform, new Vector2(0f, 0f), new Vector2(220f, 24f),
            new Vector2(20f, 20f), new Color(0.85f, 0.2f, 0.2f));
        hpBar.type = Image.Type.Filled;
        hpBar.fillMethod = Image.FillMethod.Horizontal;
        hpBar.fillAmount = 1f;

        // Счёт слева сверху.
        TextMeshProUGUI scoreText = CreateText("ScoreText", canvas.transform, "Счёт: 0",
            new Vector2(0f, 1f), new Vector2(20f, -20f), TextAlignmentOptions.TopLeft);

        // Таймер по центру сверху.
        TextMeshProUGUI timerText = CreateText("TimerText", canvas.transform, "02:00",
            new Vector2(0.5f, 1f), new Vector2(0f, -20f), TextAlignmentOptions.Top);

        // Панель окончания игры (по умолчанию выключена).
        Image panel = CreateImage("GameOverPanel", canvas.transform, new Vector2(0.5f, 0.5f),
            new Vector2(500f, 250f), Vector2.zero, new Color(0f, 0f, 0f, 0.75f));
        TextMeshProUGUI gameOverText = CreateText("GameOverText", panel.transform, "Игра окончена",
            new Vector2(0.5f, 0.5f), Vector2.zero, TextAlignmentOptions.Center);
        panel.gameObject.SetActive(false);

        HUDManager hud = canvas.gameObject.AddComponent<HUDManager>();
        SetRef(hud, "hpBar", hpBar);
        SetRef(hud, "scoreText", scoreText);
        SetRef(hud, "timerText", timerText);
        SetRef(hud, "gameOverPanel", panel.gameObject);
        SetRef(hud, "gameOverText", gameOverText);
    }

    // ───────────────────────────── UI: Lobby ─────────────────────────────

    /// <summary>
    /// Создаёт меню подключения: 4 кнопки, поле кода, статус и показ кода;
    /// связывает ссылки и привязывает кнопки к методам NetworkLauncher.
    /// </summary>
    private static void BuildLobby()
    {
        Canvas canvas = CreateCanvas("LobbyCanvas");

        NetworkLauncher launcher = canvas.gameObject.AddComponent<NetworkLauncher>();

        Button hostRelay = CreateButton("HostRelayButton", canvas.transform, "Host (Relay)", new Vector2(-220f, 80f));
        Button joinRelay = CreateButton("JoinRelayButton", canvas.transform, "Join (Relay)", new Vector2(-220f, 20f));
        Button hostLocal = CreateButton("HostLocalButton", canvas.transform, "Host (Local)", new Vector2(-220f, -40f));
        Button joinLocal = CreateButton("JoinLocalButton", canvas.transform, "Join (Local)", new Vector2(-220f, -100f));

        TMP_InputField codeInput = CreateInputField("JoinCodeInput", canvas.transform, new Vector2(60f, 20f));
        TextMeshProUGUI statusText = CreateText("StatusText", canvas.transform, "Статус: ожидание",
            new Vector2(0.5f, 0f), new Vector2(0f, 40f), TextAlignmentOptions.Bottom);
        TextMeshProUGUI codeDisplay = CreateText("JoinCodeDisplay", canvas.transform, "Код: —",
            new Vector2(0.5f, 1f), new Vector2(0f, -60f), TextAlignmentOptions.Top);

        SetRef(launcher, "joinCodeInput", codeInput);
        SetRef(launcher, "statusText", statusText);
        SetRef(launcher, "joinCodeDisplay", codeDisplay);

        // Привязываем кнопки к публичным методам (сохраняется в сцене).
        BindButton(hostRelay, launcher, "StartHostRelay");
        BindButton(joinRelay, launcher, "StartClientRelay");
        BindButton(hostLocal, launcher, "StartHostLocal");
        BindButton(joinLocal, launcher, "StartClientLocal");
    }

    /// <summary>
    /// Привязка кнопки к методу компонента через постоянный (сериализуемый) слушатель.
    /// </summary>
    private static void BindButton(Button button, MonoBehaviour target, string methodName)
    {
        var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(
            typeof(UnityEngine.Events.UnityAction), target, methodName);
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    // ───────────────────────────── Помощники UI ─────────────────────────────

    private static Canvas CreateCanvas(string name)
    {
        GameObject go = new GameObject(name);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text,
        Vector2 anchor, Vector2 anchoredPos, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = new Vector2(400f, 60f);
        rt.anchoredPosition = anchoredPos;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 32f;
        tmp.alignment = align;
        tmp.color = Color.white;
        return tmp;
    }

    private static Image CreateImage(string name, Transform parent, Vector2 anchor,
        Vector2 size, Vector2 anchoredPos, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        Image img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPos)
    {
        Image bg = CreateImage(name, parent, new Vector2(0.5f, 0.5f), new Vector2(220f, 50f), anchoredPos,
            new Color(0.2f, 0.4f, 0.8f));
        Button button = bg.gameObject.AddComponent<Button>();

        TextMeshProUGUI text = CreateText(name + "Label", bg.transform, label,
            new Vector2(0.5f, 0.5f), Vector2.zero, TextAlignmentOptions.Center);
        text.fontSize = 24f;
        // Растягиваем подпись на всю кнопку.
        RectTransform trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;
        return button;
    }

    private static TMP_InputField CreateInputField(string name, Transform parent, Vector2 anchoredPos)
    {
        Image bg = CreateImage(name, parent, new Vector2(0.5f, 0.5f), new Vector2(260f, 50f), anchoredPos,
            Color.white);
        TMP_InputField field = bg.gameObject.AddComponent<TMP_InputField>();

        // Область текста с маской.
        GameObject area = new GameObject("Text Area");
        area.transform.SetParent(bg.transform, false);
        RectTransform areaRt = area.AddComponent<RectTransform>();
        areaRt.anchorMin = Vector2.zero;
        areaRt.anchorMax = Vector2.one;
        areaRt.sizeDelta = Vector2.zero;
        areaRt.offsetMin = new Vector2(10f, 6f);
        areaRt.offsetMax = new Vector2(-10f, -6f);
        area.AddComponent<RectMask2D>();

        // Текстовый компонент ввода.
        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(area.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        text.color = Color.black;
        text.fontSize = 24f;

        // Плейсхолдер.
        GameObject placeGo = new GameObject("Placeholder");
        placeGo.transform.SetParent(area.transform, false);
        RectTransform placeRt = placeGo.AddComponent<RectTransform>();
        placeRt.anchorMin = Vector2.zero;
        placeRt.anchorMax = Vector2.one;
        placeRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI placeholder = placeGo.AddComponent<TextMeshProUGUI>();
        placeholder.text = "Введите код...";
        placeholder.color = new Color(0.4f, 0.4f, 0.4f);
        placeholder.fontSize = 24f;

        field.textViewport = areaRt;
        field.textComponent = text;
        field.placeholder = placeholder;
        return field;
    }

    // ───────────────────────────── Утилиты ─────────────────────────────

    /// <summary>
    /// Назначает приватное [SerializeField]-поле объекта через SerializedObject.
    /// </summary>
    private static void SetRef(Object component, string field, Object value)
    {
        if (component == null) return;
        SerializedObject so = new SerializedObject(component);
        SerializedProperty prop = so.FindProperty(field);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[ArenaBuilder] Поле '" + field + "' не найдено у " + component.GetType().Name);
        }
    }

    /// <summary>
    /// Назначает приватное int-поле объекта через SerializedObject.
    /// </summary>
    private static void SetInt(Object component, string field, int value)
    {
        if (component == null) return;
        SerializedObject so = new SerializedObject(component);
        SerializedProperty prop = so.FindProperty(field);
        if (prop != null)
        {
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// Заполняет приватный массив Transform[] через SerializedObject.
    /// </summary>
    private static void SetArray(Object component, string field, Transform[] values)
    {
        if (component == null) return;
        SerializedObject so = new SerializedObject(component);
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning("[ArenaBuilder] Массив '" + field + "' не найден у " + component.GetType().Name);
            return;
        }
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Помечает объект статичным (для батчинга и навигации).
    /// </summary>
    private static void MarkStatic(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic);
    }

    /// <summary>
    /// Создаёт папки Assets/Scenes и Assets/Prefabs, если их ещё нет.
    /// </summary>
    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
    }

    /// <summary>
    /// Добавляет собранную сцену первой в список Build Settings.
    /// </summary>
    private static void AddSceneToBuildSettings()
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (s.path != ScenePath) scenes.Add(s);
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}

# Multiplayer Arena — Unity 6

Учебный проект: многопользовательская 3D-арена на двух игроков с ботами, стрельбой,
таймером раунда и подключением через **Unity Relay** (по коду) или локально.

Стек: **Unity 6** (тестировалось на 6000.0.x LTS и **6000.4.x**) · Netcode for GameObjects 2.x ·
Unity Transport · Relay · Authentication · Input System · AI Navigation · Multiplayer Play Mode · TextMeshPro.

> ⚠️ Часть проекта (скрипты, пакеты, настройки, Input Actions) уже сгенерирована.
> Сцену, префабы и UI нужно собрать вручную в редакторе — это описано ниже по шагам.

> ℹ️ **Совместимость версий.** Проект рассчитан на любую версию линейки **Unity 6 (6000.x)**,
> включая **6.4 (6000.4.9f1)**. Особенности, учтённые в `manifest.json`:
> - **Netcode for GameObjects 2.11.2** — версия, идущая с 6000.4 (API `[Rpc]` и Relay не менялся в пределах 2.x).
> - **TextMeshPro** подключается через пакет **`com.unity.ugui`** (в Unity 6 TMP встроен в uGUI;
>   отдельный пакет `com.unity.textmeshpro` устарел).
> - **Unity Transport** не указан явно — он приходит как зависимость Netcode (нужная версия подбирается автоматически).
> - Остальные пакеты указаны в проверенных версиях; на более новом редакторе Package Manager
>   может предложить **обновление (Update)** — соглашайтесь, код от этого не ломается.

---

## Раздел 1. Открытие проекта

1. Установите **Unity Hub** и через него — **Unity 6**: подойдёт **6000.0.x LTS** или
   **6000.4.x** (например, 6000.4.9f1).
   - В Unity Hub: вкладка *Installs* → *Install Editor* → выберите нужную версию **6000.x**.
   - В модулях установки отметьте **Windows Build Support (IL2CPP)**.
2. В Unity Hub: *Projects* → кнопка **Add** → *Add project from disk* → укажите папку
   `MultiplayerArena` (где лежит этот README).
3. Откройте проект двойным кликом. Версия редактора должна быть из линейки **6000.x**.
   При первом открытии на 6.4 Unity сам сгенерирует `Packages/packages-lock.json` под этот редактор.
4. **Дождитесь импорта пакетов** — Unity скачает Netcode, Relay, Input System и др.
   Это может занять **5–10 минут** при первом открытии. Не закрывайте редактор.
5. Если появится окно *Enter Safe Mode* — значит пакеты ещё не докачались.
   Нажмите *Ignore* / дождитесь, пока компиляция пройдёт без ошибок (внизу справа — без красного значка).

> При первом запуске Unity может предложить включить **новый Input System** и
> перезапуститься — соглашайтесь (Active Input Handling уже выставлен в *Input System Package*).

---

## Раздел 2А. ⚡ Быстрая сборка одной кнопкой (рекомендуется)

В проекте есть скрипт-автосборщик `Assets/Editor/ArenaBuilder.cs`. Он создаёт всю сцену
программно, поэтому **ручные шаги из Раздела 2 можно пропустить**.

1. Дождитесь, пока проект **скомпилируется без ошибок** (импорт пакетов завершён).
2. В верхнем меню редактора: **`Tools → Build Arena Scene`**.
3. Скрипт создаст новую сцену `Assets/Scenes/Arena.unity` и соберёт:
   - `NetworkManager` + `UnityTransport`, с назначенным Player Prefab;
   - префаб **NetworkPlayer** (капсула + камера + все сетевые компоненты, Owner Authority);
   - префаб **Enemy** (NavMeshAgent + NetworkObject + триггер) и 3 бота на сцене;
   - землю 50×50 с укрытиями, запечённый **NavMesh**;
   - `GameManager` с 4 точками спавна;
   - **HUD** (здоровье, счёт, таймер, Game Over) и **Lobby** (4 кнопки + поле кода),
     со всеми связанными ссылками и привязанными к кнопкам методами.
4. По окончании появится окно «Готово!». Нажмите **Play** и используйте кнопки Host/Join.

> ⚠️ Что может потребовать ручной правки после автосборки:
> - При первом TMP-объекте Unity попросит **Import TMP Essentials** — нажмите кнопку импорта,
>   иначе текст не отрисуется.
> - Если боты не двигаются (особенно после повторного открытия проекта) — выделите
>   **Ground → NavMesh Surface → Bake**.
> - Если в *NetworkManager → Network Prefabs* нет врага (зависит от версии NGO) — добавьте
>   префаб `Enemy` вручную (в консоли будет предупреждение).
> - Для игры через интернет всё равно нужен **Раздел 3 (Unity Services)**.

> 💡 Запускать `Build Arena Scene` можно повторно — каждый раз создаётся свежая сцена с нуля
> (существующая `Arena.unity` перезапишется).

---

## Раздел 2. Настройка сцены вручную (если не пользуетесь автосборкой)

Создайте новую сцену: *File → New Scene → Basic (URP/Built-in)* и сразу сохраните её
как `Assets/Scenes/Arena.unity` (*File → Save As*).

### 2.1. Terrain (поверхность арены)
1. *GameObject → 3D Object → Terrain*.
2. На объекте Terrain в *Inspector* откройте инструмент кисти (значок горы) →
   **Raise / Lower Terrain**.
3. Поднимите 2–4 холма, проводя кистью по поверхности (зажав ЛКМ).
4. Выделите Terrain → в правом верхнем углу *Inspector* поставьте галочку **Static**.

### 2.2. Укрытия (кубы)
1. *GameObject → 3D Object → Cube*. Масштабируйте под укрытие (например, 2×2×2).
2. Скопируйте (*Ctrl+D*) и расставьте **5–7 кубов** по арене.
3. Выделите все кубы → поставьте галочку **Static** в *Inspector*.

### 2.3. NetworkManager
1. *GameObject → Create Empty* → переименуйте в **NetworkManager**.
2. *Add Component* → **NetworkManager**.
3. *Add Component* → **Unity Transport** (он же UnityTransport).
4. В компоненте *NetworkManager* поле **Network Transport** должно ссылаться на
   только что добавленный *Unity Transport* (обычно подхватывается автоматически).

### 2.4. Префаб игрока (NetworkPlayer)
1. *GameObject → 3D Object → Capsule* → переименуйте в **NetworkPlayer**.
2. Добавьте компоненты (*Add Component*):
   - **NetworkObject**
   - **NetworkTransform** → в его настройках выберите **Authority Mode = Owner**
     (синхронизация позиции от владельца).
   - **Character Controller**
   - **PlayerMovement**
   - **PlayerShoot**
   - **NetworkPlayer**
3. Установите **Tag = Player** (вверху *Inspector* → *Tag → Player*).
4. Добавьте дочернюю камеру: правый клик на NetworkPlayer → *Camera*.
   - Поднимите камеру на уровень «глаз» (Position Y ≈ 0.6, Z ≈ 0).
   - Перетащите этот объект *Camera* в поле **Camera Transform** компонентов
     *PlayerMovement* и *PlayerShoot*.
5. Поле **Body Renderer** в *NetworkPlayer* свяжите с *Mesh Renderer* капсулы
   (перетащите сам объект NetworkPlayer в это поле).
6. Перетащите NetworkPlayer из *Hierarchy* в папку **Assets/Prefabs** → получится префаб.
7. **Удалите** NetworkPlayer со сцены (он будет спавниться сетью).
8. Выделите объект **NetworkManager** → в поле **Player Prefab** перетащите префаб NetworkPlayer.

### 2.5. Префаб врага (Enemy)
1. *GameObject → 3D Object → Capsule* → переименуйте в **Enemy**.
2. Добавьте компоненты:
   - **NavMesh Agent**
   - **EnemyAI**
   - **Capsule Collider** → поставьте галочку **Is Trigger = true**
   - **NetworkObject**
   - **NetworkTransform** (чтобы клиенты видели движение бота; *Authority = Server* по умолчанию)
3. Установите **Tag = Enemy**.
4. Перетащите Enemy в папку **Assets/Prefabs** → получится префаб.
5. Зарегистрируйте префаб в сети: выделите **NetworkManager** → раздел
   *Network Prefabs* (или *NetworkPrefabsList*) → добавьте префаб **Enemy** в список.

> 💡 Враг — обычный `MonoBehaviour`, но ему добавлен **NetworkObject**, чтобы урон по нему,
> вычисленный на клиенте, корректно применялся на сервере. Это требование сетевой авторитетности.

### 2.6. Размещение врагов
1. Перетащите префаб **Enemy** на сцену **3 раза**, расставив в разных местах арены.
2. Убедитесь, что они стоят на поверхности Terrain (не висят в воздухе).

### 2.7. NavMesh (навигация ботов)
1. Выделите Terrain → *Add Component* → **NavMesh Surface** (из пакета AI Navigation).
2. В компоненте *NavMesh Surface* нажмите **Bake**.
3. Должна появиться синяя «заливка» проходимых зон. Без неё боты стоят на месте.

### 2.8. GameManager (менеджер раунда)
1. *GameObject → Create Empty* → **GameManager**.
2. *Add Component* → **NetworkObject**.
3. *Add Component* → **NetworkGameManager**.
4. Создайте 4 точки спавна: правый клик на GameManager → *Create Empty* →
   назовите **SpawnPoint1**, и так до **SpawnPoint4**. Разнесите их по арене
   (задайте им позиции в углах).
5. В компоненте *NetworkGameManager* раскройте массив **Spawn Points** → размер **4** →
   перетащите туда SpawnPoint1…SpawnPoint4.

### 2.9. Игровой HUD (Canvas)
1. *GameObject → UI → Canvas*. На Canvas автоматически добавится EventSystem.
2. Внутри Canvas создайте:
   - **HP Image**: *UI → Image* → в *Inspector* поле **Image Type = Filled**,
     *Fill Method = Horizontal*. Назовите **HpBar**.
   - **Score Text**: *UI → Text - TextMeshPro* → назовите **ScoreText**.
   - **Timer Text**: *UI → Text - TextMeshPro* → назовите **TimerText** (поместите по центру сверху).
   - **GameOver Panel**: *UI → Panel* → назовите **GameOverPanel**, внутрь добавьте
     *Text - TextMeshPro* (**GameOverText**). **Отключите** GameOverPanel галочкой слева от имени.
3. На сам объект **Canvas** добавьте компонент **HUDManager**.
4. В *HUDManager* свяжите поля: **Hp Bar → HpBar**, **Score Text → ScoreText**,
   **Timer Text → TimerText**, **Game Over Panel → GameOverPanel**, **Game Over Text → GameOverText**.

> При первом добавлении TMP-текста Unity предложит **Import TMP Essentials** — нажмите кнопку импорта.

### 2.10. Lobby (меню подключения)
1. *GameObject → UI → Canvas* → назовите **LobbyCanvas**.
2. Внутри добавьте:
   - **Button - TextMeshPro** → «Host (Relay)»
   - **Button - TextMeshPro** → «Join (Relay)»
   - **Button - TextMeshPro** → «Host (Local)»
   - **Button - TextMeshPro** → «Join (Local)»
   - **InputField - TextMeshPro** → **JoinCodeInput** (для ввода кода)
   - **Text - TextMeshPro** → **StatusText** (статус/ошибки)
   - **Text - TextMeshPro** → **JoinCodeDisplay** (показ кода хоста)
3. На объект **LobbyCanvas** добавьте компонент **NetworkLauncher**.
4. Свяжите поля *NetworkLauncher*: **Join Code Input → JoinCodeInput**,
   **Status Text → StatusText**, **Join Code Display → JoinCodeDisplay**.
5. Привяжите кнопки к методам (в *Inspector* кнопки → раздел *On Click ()* → **+** →
   перетащите LobbyCanvas → выберите функцию):
   - «Host (Relay)» → `NetworkLauncher.StartHostRelay`
   - «Join (Relay)» → `NetworkLauncher.StartClientRelay`
   - «Host (Local)» → `NetworkLauncher.StartHostLocal`
   - «Join (Local)» → `NetworkLauncher.StartClientLocal`
6. Сохраните сцену (*Ctrl+S*) и добавьте её в сборку:
   *File → Build Profiles/Settings → Add Open Scenes*.

---

## Раздел 3. Настройка Unity Services (для Relay)

> Этот раздел нужен только для игры через интернет (Relay). Для теста на одной машине
> можно использовать кнопки **Host (Local) / Join (Local)** и пропустить раздел.

1. В редакторе: *Window → General → Services* (или *Edit → Project Settings → Services*).
2. Войдите в аккаунт **Unity** (тот же, что в Unity Hub).
3. Создайте новый проект в облаке или привяжите существующий:
   *Project Settings → Services → Link project* (выберите Organization и Project).
4. Откройте **Unity Cloud Dashboard** (ссылка из окна Services) и включите сервисы:
   - **Authentication** (анонимный вход) — обычно включается автоматически.
   - **Relay**.
5. Вернитесь в редактор и убедитесь, что вверху окна Services отображается связанный проект
   (есть Project ID и Environment).

---

## Раздел 4. Тестирование двух игроков

### Вариант A — Multiplayer Play Mode (в одном редакторе)
1. Пакет **Multiplayer Play Mode** уже подключён в `manifest.json`.
2. Откройте *Window → Multiplayer → Multiplayer Play Mode*.
3. Включите **Player 2** (поставьте галочку напротив второго виртуального игрока).
   Откроется второе игровое окно (клон редактора).
4. Нажмите **Play** в основном редакторе.
   - В основном окне нажмите **Host (Relay)** → скопируйте появившийся **код**.
   - Во втором окне (Player 2) вставьте код в **JoinCodeInput** → нажмите **Join (Relay)**.
5. Оба игрока должны появиться на арене и видеть друг друга и ботов.

### Вариант B — локальный тест без Relay
1. Нажмите **Play**, затем **Host (Local)**.
2. Во втором экземпляре (Play Mode Player 2) нажмите **Join (Local)** — подключение к 127.0.0.1.

### Управление
- **WASD / стрелки** — движение
- **Мышь** — обзор
- **ЛКМ** — выстрел (урон 25 по игрокам и ботам)
- **Space** — прыжок (зарезервировано в Input Actions)

---

## Раздел 5. Сборка проекта

1. *File → Build Profiles* (или *Build Settings*).
2. Platform: **Windows** → при необходимости *Switch Platform*.
3. Убедитесь, что сцена `Arena` добавлена в список *Scenes In Build*.
4. Нажмите **Build**, выберите папку — получится `.exe`.
5. Для теста по сети запустите два экземпляра `.exe` (или `.exe` + редактор) и
   соединитесь через **Relay** по коду.

---

## Раздел 6. Частые ошибки и решения

| Симптом | Причина | Решение |
|---|---|---|
| `Services not initialized` / ошибка при Host (Relay) | Проект не привязан в *Services* | Раздел 3: войдите в аккаунт и свяжите проект, включите Relay/Authentication |
| Игроки не видят движение друг друга | NetworkTransform не настроен | На префабе NetworkPlayer выставьте **NetworkTransform → Authority = Owner** |
| Бот стоит на месте | NavMesh не запечён | Выделите Terrain → *NavMesh Surface* → **Bake** |
| Relay не подключается / таймаут | Сервис Relay выключен в Dashboard | Включите **Relay** в Unity Cloud Dashboard и пересоздайте хост |
| Урон по врагу не проходит | У Enemy нет NetworkObject | Добавьте на префаб Enemy компонент **NetworkObject** и зарегистрируйте префаб в NetworkManager |
| Ошибки компиляции после открытия | Пакеты ещё не докачались | Дождитесь импорта (5–10 мин), затем *Assets → Reimport All* при необходимости |
| Курсор не двигает камеру | Окно не в фокусе / курсор не залочен | Кликните в игровое окно; курсор блокируется автоматически у владельца |
| `InputSystem_Actions` не найден | Файл `.inputactions` не сгенерировал C# | Выделите `Assets/Settings/InputSystem_Actions.inputactions` → в *Inspector* включите **Generate C# Class** → *Apply* |
| Package Manager: `cannot be found` / version conflict | На вашем редакторе пакет нужен другой версии | *Window → Package Manager* → найдите пакет → **Update** (или **Remove** и переустановите). Можно удалить `Packages/packages-lock.json` и переоткрыть проект — Unity пересоберёт версии |
| `TMP_Text` / `TMPro` не найден | TMP не подгрузился из uGUI | Убедитесь, что в `manifest.json` есть `com.unity.ugui`; при первом TMP-объекте нажмите **Import TMP Essentials** |
| Тип `RelayServerData` не найден | Не доехали Relay/Transport | Дождитесь импорта пакетов; Transport приходит как зависимость Netcode — проверьте, что Netcode установлен в *Package Manager* |

---

## Структура проекта

```
MultiplayerArena/
├── Assets/
│   ├── Editor/
│   │   └── ArenaBuilder.cs   ← автосборка сцены (Tools → Build Arena Scene)
│   ├── Scripts/
│   │   ├── Network/   NetworkLauncher.cs, NetworkGameManager.cs, ClientNetworkTransform.cs
│   │   ├── Player/    NetworkPlayer.cs, PlayerMovement.cs, PlayerShoot.cs
│   │   ├── Enemy/     EnemyAI.cs
│   │   └── UI/        HUDManager.cs
│   ├── Settings/      InputSystem_Actions.inputactions
│   ├── Prefabs/  Scenes/  Materials/   (заполняются автосборкой или вручную)
├── Packages/manifest.json
├── ProjectSettings/  (ProjectSettings, InputManager, TagManager, ProjectVersion)
└── README.md
```

> 💡 После открытия проекта выделите `Assets/Settings/InputSystem_Actions.inputactions`,
> в *Inspector* убедитесь, что включён **Generate C# Class** (имя класса `InputSystem_Actions`)
> и нажмите **Apply** — иначе скрипты движения/стрельбы не скомпилируются.

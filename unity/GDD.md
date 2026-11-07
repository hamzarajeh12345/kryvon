# KRYVON – وثيقة تصميم اللعبة ومخطط الكود لـ Unity

> النوع: باتل رويال ثلاثي الأبعاد بمنظور ثالث (TPS)، موبايل وكمبيوتر.
> المحرك: Unity 6 (LTS)، لغة C#، Input System الجديد، URP.
> هاي الوثيقة نسخة Unity من اللعبة اللي بنيناها بالمتصفح؛ نفس الخريطة والأنظمة، بس بمحرك احترافي.

---

## 1. الفكرة والحلقة الأساسية (Core Concept & Loop)

**الفكرة:** 100 لاعب (فردي / ثنائي / فريق 4) بنطّوا من طيارة فوق خريطة بلاد الشام – دمشق، بيروت، حلب، القدس، عمّان، البتراء، البحر الميت – بيجمعوا سلاح وعتاد، والطوق (Al-Tawq) بيضيق كل شوي. آخر فريق عايش بيفوز بـ«كنافة النصر».

**الحلقة (Loop):**
```
لوبي → بحث عن مباراة → ساحة الانتظار (20 ث) → الطيارة → القفز والمظلة
  → الهبوط واللوت → التحرك مع الطوق → القتال / الإسقاط / الإنعاش
  → آخر فريق → شاشة النتائج (XP، نقاط تصنيف، عملات) → لوبي
```

**الهوية:** آثار حقيقية كمعالم (الجامع الأموي، قبة الصخرة، الخزنة، المدرج الروماني…)، مساعدة اسمها سارة، أسلحة بأسماء عربية، شخصيات قابلة للتخصيص (حجاب، كوفية، لحية…).

---

## 2. الميكانيكيات الأساسية وكيف تنبرمج

| الميكانيك | كيف بتشتغل برمجياً |
|---|---|
| **الحركة** | `CharacterController` + سرعات (مشي/ركض/انحناء)، الجاذبية يدوياً، الاتجاه نسبةً لزاوية الكاميرا. → `PlayerController` |
| **الكاميرا** | كاميرا فوق الكتف بتدور حول اللاعب، `SphereCast` لتمنع دخولها بالحيطان، تقريب FOV وقت التصويب. → `ThirdPersonCamera` |
| **الإدخال** | Input System الجديد، الأفعال معرّفة بالكود (كيبورد/ماوس/يد تحكم)، وأزرار شاشة للموبايل بـ `OnScreenStick`/`OnScreenButton`. → `PlayerInputReader` |
| **إطلاق النار** | Hitscan: `Physics.Raycast` من منتصف الشاشة، انتشار عشوائي، ارتداد بيرفع الكاميرا، ضرر بيقل مع المسافة، ضرب الراس ×2. البيانات بـ `ScriptableObject`. → `WeaponController` + `WeaponData` |
| **الصحة والدروع** | `Health` فيها HP ودرع وخوذة بتمتص نسبة من الضرر. بالفرق: بدل الموت بصير «مُسقَط» (Knocked) بنزف لحد ما حدا ينعشه. → `Health` |
| **الطوق** | مراحل (انتظار → تضييق) معرّفة بمصفوفة، الدائرة الجاية جوا الحالية، ضرر بالثانية برا الدائرة. → `SafeZone` |
| **المباراة** | آلة حالات: Lobby → Island → Plane → InMatch → Ended، بتتابع الفرق العايشة وبتعلن الفائز. → `MatchManager` |
| **اللوت** | نقاط لوت (`LootPoint`) بالمباني حسب مستوى المنطقة (Tier 1–3)، جدول احتمالات من `LootTable` (ScriptableObject). |
| **الحقيبة والقطع** | `Inventory` بوزن وسعة، القطع (كاتم، مخزن ممدود، أخمص…) بتعدّل قيم السلاح. |
| **الطيارة والمظلة** | مسار مستقيم عبر الخريطة؛ سقوط حر ثم مظلة بتبطّئ وتسمح بالتوجيه. |
| **البوتات** | `NavMeshAgent` + آلة حالات (لوت / قتال / تحرّك للطوق / إنعاش زميل). |
| **الشخصية** | `CharacterLook` (بيانات: بشرة، شعر، لحية، حجاب، ملابس) + `CharacterAppearance` بيبدّل الموديلات والمواد. |

---

## 3. هيكل السكريبتات (Script Architecture)

```
                    ┌────────────────────┐
                    │   MatchManager     │  (Singleton: حالة المباراة، الفرق، الفائز)
                    └─────────┬──────────┘
          events: OnPhaseChanged, OnTeamEliminated, OnMatchEnded
        ┌──────────────┬──────┴───────┬───────────────┐
        ▼              ▼              ▼               ▼
   SafeZone      PlaneController   LootSpawner     HUDController
 (يضر Health)   (القفز والمظلة)   (يملا LootPoints)  (يقرأ الأحداث)

  Player prefab:
  ┌───────────────────────────────────────────────────────────┐
  │ PlayerInputReader → PlayerController (حركة)               │
  │                   → WeaponController (إطلاق) ← WeaponData │
  │                   → ThirdPersonCamera (نظر/تصويب)          │
  │ Health (IDamageable) → يبلّغ MatchManager عند الإسقاط/الموت │
  │ Inventory, CharacterAppearance ← CharacterLook             │
  └───────────────────────────────────────────────────────────┘

  Bot prefab: نفس Health/WeaponController/Inventory + BotBrain (NavMeshAgent)
```

| الكلاس | الدور |
|---|---|
| `MatchManager` | حالة المباراة، تسجيل اللاعبين والفرق، عدّ العايشين، إعلان الفائز. |
| `SafeZone` | مراحل الطوق، حساب الدائرة الجاية، ضرر برا الدائرة، شكل الحيط الأزرق. |
| `PlayerInputReader` | قراءة الإدخال وتوفيره كقيم بسيطة (Move, Look, Fire…). |
| `PlayerController` | الحركة والقفز والانحناء والجاذبية. |
| `ThirdPersonCamera` | الكاميرا فوق الكتف، الحساسية، التصويب، منع الدخول بالجدران. |
| `Health` + `IDamageable` + `HitBox` | الضرر، الدروع، الإسقاط والإنعاش، الموت؛ `HitBox` بيحدد الراس/الجسم. |
| `WeaponData` (SO) / `WeaponController` | بيانات السلاح / الإطلاق والتعبئة والارتداد. |
| `Inventory`, `ItemData` (SO), `AttachmentData` (SO) | الحقيبة والمواد والقطع. |
| `LootPoint`, `LootTable` (SO), `LootSpawner`, `PickupItem` | توزيع اللوت والتقاطه. |
| `PlaneController`, `ParachuteController` | الطيارة والقفز والمظلة. |
| `BotBrain` | ذكاء البوتات. |
| `SquadManager` | الفرق، الإنعاش، الرسائل السريعة والعلامات. |
| `HUDController`, `MinimapController`, `KillFeed` | الواجهة. |
| `CharacterLook` (data) / `CharacterAppearance` | تخصيص الشخصية. |
| `ApiClient` | الاتصال بالسيرفر الحالي (`server.js`): تسجيل دخول، نتائج، متجر. |

**قاعدة:** الأنظمة بتحكي مع بعض بـ C# events (مش `FindObjectOfType` كل فريم)، والبيانات (أسلحة، لوت، مراحل الطوق) بـ ScriptableObjects عشان المصمم يعدّلها بدون كود.

---

## 4. ترتيب المشروع داخل Unity (Folder Structure)

```
Assets/
└── _Kryvon/
    ├── Scenes/            Boot, Lobby, Map_Sham, Training
    ├── Scripts/
    │   ├── Core/          MatchManager, SafeZone, PlaneController
    │   ├── Player/        PlayerInputReader, PlayerController, ThirdPersonCamera
    │   ├── Combat/        Health, IDamageable, HitBox, WeaponController
    │   ├── Data/          WeaponData, ItemData, AttachmentData, LootTable, CharacterLook
    │   ├── Items/         Inventory, LootPoint, LootSpawner, PickupItem
    │   ├── AI/            BotBrain
    │   ├── UI/            HUDController, MinimapController, KillFeed
    │   └── Net/           ApiClient
    ├── Data/              (ملفات ScriptableObject: Weapons/, Items/, Zones/)
    ├── Prefabs/           Player, Bot, Weapons/, Loot/, Plane, Buildings/, Landmarks/
    ├── Models/            Characters/, Weapons/, Landmarks/, Props/
    ├── Materials/
    ├── Textures/
    ├── Audio/             SFX/, Music/
    ├── Animations/        Player/
    ├── UI/                Sprites/, Fonts/ (خط عربي)
    └── Settings/          URP assets, Input
```

---

## 5. خطوة بخطوة: تشغيل أول نسخة

السكريبتات جاهزة بمجلد `unity/Scripts/` بهالمشروع. انسخ مجلد `Scripts` لـ `Assets/_Kryvon/Scripts`.

1. **مشروع جديد:** Unity Hub → New Project → **Universal 3D** (Unity 6).
2. **Input System:** Window → Package Manager → تأكد إنه **Input System** منزّل. إذا سألك عن تفعيله، اختار Yes (بيعيد تشغيل المحرر).
3. **الأرض:** GameObject → 3D Object → Plane، كبّره (Scale 50, 1, 50). أو Terrain للخريطة الحقيقية لاحقاً.
4. **اللاعب:**
   - GameObject → Create Empty، سمّيه `Player` وحطه على Y = 0 (نقطة الأصل عند الرجلين).
   - جواته: 3D Object → Capsule على Position Y = 1، واحذف منها الـ Capsule Collider (هي بس للشكل).
   - على `Player` نفسه: Add Component → `CharacterController`، `PlayerInputReader`، `PlayerController`، `Health`، `WeaponController` (السكريبت بيظبط ارتفاع ومركز الـ CharacterController لحاله).
   - اعمل Layer اسمه `Player` وحطه على اللاعب، وشيله من **Collision Mask** بالكاميرا.
5. **الكاميرا:** على `Main Camera` ضيف `ThirdPersonCamera` واسحب `Player` لخانة **Target**. وعلى `PlayerController` اسحب الكاميرا لخانة **Camera Transform**، وعلى `WeaponController` كمان.
6. **سلاح:** Project → Create → KRYVON → Weapon Data، سمّيه `AlSaqr` وظبّط القيم (بيجي بقيم افتراضية معقولة). اسحبه لخانة **Weapon** بـ `WeaponController`.
7. **هدف:** Capsule ثانية، ضيف عليها `Health`. صار عندك هدف تضربه.
8. **المباراة والطوق:** GameObject فاضي اسمه `Match`، ضيف `MatchManager` و `SafeZone`. بـ `SafeZone` ضيف مراحل (أو خلي الافتراضية). اعمل Cylinder شفاف أزرق واسحبه لخانة **Wall Visual**.
9. **Play:** WASD حركة، الماوس نظر، Shift ركض، Space قفز، C انحناء، كبسة يسار إطلاق، يمين تصويب، R تعبئة.

**الترتيب المقترح بعدها:** Inventory + اللوت → الطيارة والمظلة → البوتات (NavMesh) → الواجهة → الخريطة الحقيقية (Terrain + المعالم) → الشخصيات والأنيميشن → الأونلاين (Netcode for GameObjects أو Photon Fusion).

---

## ملاحظات مهمة
- **أصول جاهزة:** الموديلات والأنيميشن الواقعية بدها مصممين، أو أصول مرخّصة من Unity Asset Store / Fab. تأكد من رخصة كل أصل قبل ما تنشر.
- **الأونلاين:** للعبة 100 لاعب، الأفضل سيرفر مخصص (Dedicated Server) مع Netcode for GameObjects أو Photon Fusion. خطط له من بدري: خلي المنطق (الضرر، اللوت، الطوق) يتنفّذ على السيرفر.
- **الموبايل:** URP بإعدادات Mobile، LOD لكل موديل، Occlusion Culling، وتجربة على موبايل متوسط من أول أسبوع.

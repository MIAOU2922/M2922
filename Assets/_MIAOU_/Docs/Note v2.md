Component/
├── Health/          (5)  Health, Shield, Armor, DamageReceiver, DeathHandler
├── Modifier/        (3)  ModifierContainer, ModifierApplier, StatBlock
├── Physics/         (3)  Gravity, Movement, PhysicsDriver
├── Weapon/          (5)  Weapon, FireController, Reload, Spread, Recoil
├── Projectile/      (4)  Projectile, HitDetector, DamageSource, VFXProjectile
├── Inventory/       (4)  Inventory, Item, Holder, Pickup
├── Ammo/            (2)  Magazine, AmmoType
├── Vehicle/         (4)  Vehicle, Seat, VehicleMovement, VehicleWeaponMount
├── World/           (4)  Door, Teleporter, Ladder, TriggerZone
├── Player/          (4)  PlayerController, PlayerStats, PlayerInventory, PlayerInputHandler
├── Game/            (5)  GameModeManager, TeamManager, ScoreManager, SpawnManager, RoundManager
├── Network/         (4)  OwnershipManager, SyncComponent, LagCompensation, EventBus
├── UI/              (3)  HUDController, InventoryUI, ScoreboardUI
└── Utils/           (4)  Poolable, PoolManager, TimerComponent, DebugComponent



UdonSharpBehaviour
 └─ M2922_Base              ← debug, manager, identity, gizmo
      └─ TOUS les 54 composants (héritage direct)



Aspect	            Détail
Sync mode	        Manual pour composants réseau, None pour locaux
Références	        Auto-détection via GetComponent<>() au Start() + serialized fallback
Damage pipeline	    DamageSource → HitDetector → DamageReceiver (shield → armor → health)
EventBus	        Centralisé pour OnHit, OnKill, OnDeath, OnRespawn, etc.
Pooling	            PoolManager + Poolable pour projectiles et objets fréquents
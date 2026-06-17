using Unity.Entities;
using CoreDriller.Player.StatSystem;
using R3;

namespace CoreDriller.Player
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerReturnSystem))] // Runs after player statistics update and return refilling
    public partial class PlayerUISyncSystem : SystemBase
    {
        private ComponentLookup<PlayerMovementData> movementLookup;
        private ComponentLookup<PlayerHealthData> healthLookup;
        private ComponentLookup<PlayerDrillData> drillLookup;
        private BufferLookup<InventoryBuffer> inventoryLookup;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerMovementData>();
            movementLookup = GetComponentLookup<PlayerMovementData>(true);
            healthLookup = GetComponentLookup<PlayerHealthData>(true);
            drillLookup = GetComponentLookup<PlayerDrillData>(true);
            inventoryLookup = GetBufferLookup<InventoryBuffer>(true);
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonEntity<PlayerMovementData>(out var statEntity)) return;

            // Update Lookups to the current frame state
            movementLookup.Update(this);
            healthLookup.Update(this);
            drillLookup.Update(this);
            inventoryLookup.Update(this);

            // 1. Fuel and Movement data change detection
            if (movementLookup.HasComponent(statEntity) && movementLookup.DidChange(statEntity, LastSystemVersion))
            {
                var movementData = movementLookup[statEntity];
                //PlayerUIEvents.TriggerFuelChanged(movementData.CurrentFuel, movementData.MaxFuel);
                PlayerUIEvents.Fuel.OnNext((movementData.CurrentFuel, movementData.MaxFuel));
            }

            // 2. Health data change detection
            if (healthLookup.HasComponent(statEntity) && healthLookup.DidChange(statEntity, LastSystemVersion))
            {
                var healthData = healthLookup[statEntity];
                //PlayerUIEvents.TriggerHealthChanged(healthData.CurrentHealth, healthData.MaxHealth);
                PlayerUIEvents.Health.OnNext((healthData.CurrentHealth, healthData.MaxHealth));
            }

            //// 3. Drill timer and state change detection
            //if (drillLookup.HasComponent(statEntity) && drillLookup.DidChange(statEntity, LastSystemVersion))
            //{
            //    var drillData = drillLookup[statEntity];
            //    PlayerUIEvents.TriggerDrillStateChanged(drillData.CurrentTimer, drillData.IsActive);
            //}

            // 4. Inventory Buffer change detection
            if (inventoryLookup.HasBuffer(statEntity) && inventoryLookup.DidChange(statEntity, LastSystemVersion))
            {
                //PlayerUIEvents.TriggerInventoryChanged();
                PlayerUIEvents.InventoryChanged.OnNext(Unit.Default);
            }
        }
    }
}

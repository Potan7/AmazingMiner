using Unity.Entities;
using CoreDriller.Player.StatSystem;
using UnityEngine;

namespace CoreDriller.Player
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PlayerInitializationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (PlayerManager.Instance == null || PlayerManager.Instance.CurrentStats == null)
                return;

            var stats = PlayerManager.Instance.CurrentStats;

            // Get EntityCommandBuffer to defer structural changes and avoid errors during query iteration
            var ecb = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (tag, entity) in SystemAPI.Query<RefRO<PlayerTag>>()
                         .WithNone<PlayerDrillData>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new PlayerDrillData
                {
                    DrillPower = stats.DrillPower,
                    DrillSpeed = stats.DrillSpeed,
                    DrillFuelConsumption = stats.DrillFuelConsumption,
                    DigCooldown = stats.DigCooldown,
                    DrillRange = stats.DrillRange,
                    DrillExplosionRadius = stats.DrillExplosionRadius
                });

                ecb.AddComponent(entity, new PlayerMovementData
                {
                    MoveSpeed = stats.MoveSpeed,
                    MaxFuel = stats.MaxFuel,
                    CurrentFuel = stats.MaxFuel,
                    JetpackThrust = stats.JetpackThrust,
                    JetpackFuelConsumption = stats.JetpackFuelConsumption
                });

                ecb.AddComponent(entity, new PlayerHealthData
                {
                    MaxHealth = stats.MaxHealth,
                    CurrentHealth = stats.MaxHealth
                });

                ecb.AddComponent(entity, new PlayerInventoryData
                {
                    InventorySlotSize = stats.InventorySlotSize,
                    InventorySlotCount = stats.InventorySlotCount,
                    ItemPickupRange = stats.ItemPickupRange
                });

                var invBuffer = ecb.AddBuffer<InventoryBuffer>(entity);
                for (int i = 0; i < stats.InventorySlotCount; i++)
                {
                    invBuffer.Add(new InventoryBuffer { ItemType = 0, Count = 0 });
                }

                PlayerManager.Instance.SetPlayerEntity(entity);
                //Debug.Log($"[PlayerInitializationSystem] Deferred Initialization for Player Entity: {entity} with stats.");
            }
        }
    }
}

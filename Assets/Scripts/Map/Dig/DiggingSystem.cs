using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using CoreDriller.Map.Rendering;
using UnityEngine.InputSystem;

namespace CoreDriller.Map.Dig
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DiggingSystem : SystemBase
    {
        private const int ChunkSize = 16;
        private const float BlockSize = 0.5f; // 블록 크기 반영
        private Camera mainCamera;

        protected override void OnCreate()
        {
            mainCamera = Camera.main;
        }

        protected override void OnUpdate()
        {
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            float2 worldPos = new float2(mouseWorldPos.x, mouseWorldPos.y);

            // 한 청크의 월드 크기 (16 * 0.5 = 8)
            float chunkWorldSize = ChunkSize * BlockSize;

            // 1. 월드 좌표 -> 청크 좌표 변환
            int chunkX = (int)math.floor(worldPos.x / chunkWorldSize);

            int chunkY = 0;
            if (worldPos.y < 0)
            {
                chunkY = (int)math.ceil(math.abs(worldPos.y) / chunkWorldSize);
            }

            int2 targetChunkCoord = new int2(chunkX, chunkY);

            // 2. 청크 내 로컬 좌표 변환
            float chunkWorldPosY = -chunkY * chunkWorldSize;

            // 로컬 좌표 = (월드좌표 - 청크시작좌표) / 블록크기
            int localX = (int)math.floor((worldPos.x - (chunkX * chunkWorldSize)) / BlockSize);
            int localY = (int)math.floor((worldPos.y - chunkWorldPosY) / BlockSize);

            // 범위 검사
            if (localX < 0 || localX >= ChunkSize || localY < 0 || localY >= ChunkSize) return;

            int blockIndex = localX * ChunkSize + localY;

            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            bool found = false;
            foreach (var (chunk, blocks, entity) in SystemAPI.Query<RefRO<ChunkComponent>, DynamicBuffer<BlockBuffer>>().WithEntityAccess())
            {
                if (chunk.ValueRO.Coordinate.Equals(targetChunkCoord))
                {
                    if (blockIndex < 0 || blockIndex >= blocks.Length) continue;

                    var buffer = blocks;
                    var blockData = buffer[blockIndex].Value;

                    if (blockData.BlockType == 0 || blockData.Hardness > 999999f) continue;

                    blockData.BlockType = 0;
                    buffer[blockIndex] = new BlockBuffer { Value = blockData };

                    ecb.AddComponent<MeshNeedsUpdateTag>(entity);

                    Debug.Log($"[Dig] 0.5f 블록 파괴: 청크({chunkX}, {chunkY}), 로컬({localX}, {localY})");
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Debug.LogWarning($"[Dig] 블록 없음: 월드({worldPos.x:F1}, {worldPos.y:F1}) -> 청크({chunkX}, {chunkY}), 로컬({localX}, {localY})");
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}

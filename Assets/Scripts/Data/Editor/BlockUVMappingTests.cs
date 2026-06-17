using NUnit.Framework;
using UnityEngine;
using Unity.Mathematics;
using CoreDriller;
using CoreDriller.Map;

namespace CoreDriller.Tests
{
    public class BlockUVMappingTests
    {
        [Test]
        public void TestFallbackUVCalculations()
        {
            // Fallback index 0:
            // xIdx = 0, yIdx = 0 -> offsetX = 0.0f, offsetY = 1.0f - 0.25f = 0.75f
            float4 uv0 = DataManager.CalculateBlockUV(null, 0);
            Assert.AreEqual(new float4(0.25f, 0.25f, 0.0f, 0.75f), uv0, "Index 0 fallback UV is incorrect");

            // Fallback index 1:
            // xIdx = 1, yIdx = 0 -> offsetX = 0.25f, offsetY = 0.75f
            float4 uv1 = DataManager.CalculateBlockUV(null, 1);
            Assert.AreEqual(new float4(0.25f, 0.25f, 0.25f, 0.75f), uv1, "Index 1 fallback UV is incorrect");

            // Fallback index 5:
            // xIdx = 1, yIdx = 1 -> offsetX = 0.25f, offsetY = 1.0f - 0.5f = 0.50f
            float4 uv5 = DataManager.CalculateBlockUV(null, 5);
            Assert.AreEqual(new float4(0.25f, 0.25f, 0.25f, 0.50f), uv5, "Index 5 fallback UV is incorrect");
        }

        [Test]
        public void TestDynamicSpriteUVCalculations()
        {
            // Create a mock texture (128x128)
            Texture2D mockTexture = new Texture2D(128, 128);

            // Create a mock sprite at x=32, y=64 with size 32x32
            Rect rect = new Rect(32, 64, 32, 32);
            Sprite mockSprite = Sprite.Create(mockTexture, rect, Vector2.zero);

            // Calculate UVs
            float4 uv = DataManager.CalculateBlockUV(mockSprite, 0);

            // Expected values:
            // ScaleX = 32 / 128 = 0.25f
            // ScaleY = 32 / 128 = 0.25f
            // OffsetX = 32 / 128 = 0.25f
            // OffsetY = 64 / 128 = 0.50f
            float4 expected = new float4(0.25f, 0.25f, 0.25f, 0.50f);

            Assert.AreEqual(expected, uv, "Dynamic sprite UV calculation is incorrect");

            // Clean up mock texture
            Object.DestroyImmediate(mockTexture);
        }
    }
}

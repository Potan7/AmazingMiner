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
            // 스프라이트가 없을 때 디폴트 풀 텍스처 영역을 리턴해야 함
            float4 uv = DataManager.CalculateBlockUV(null);
            Assert.AreEqual(new float4(1f, 1f, 0f, 0f), uv, "Fallback UV when sprite is null is incorrect");
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
            float4 uv = DataManager.CalculateBlockUV(mockSprite);

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

        [Test]
        public void TestMultipleSpritesUVCalculations()
        {
            // Create a mock texture (128x128)
            Texture2D mockTexture = new Texture2D(128, 128);

            // Create 2 mock sprites
            Sprite sprite1 = Sprite.Create(mockTexture, new Rect(0, 0, 32, 32), Vector2.zero);
            Sprite sprite2 = Sprite.Create(mockTexture, new Rect(32, 32, 64, 64), Vector2.zero);

            var list = new System.Collections.Generic.List<Sprite> { sprite1, sprite2 };

            // Calculate UVs
            float4[] uvRects = DataManager.CalculateBlockUVs(list, null);

            Assert.AreEqual(2, uvRects.Length);
            Assert.AreEqual(new float4(0.25f, 0.25f, 0.0f, 0.0f), uvRects[0]);
            Assert.AreEqual(new float4(0.50f, 0.50f, 0.25f, 0.25f), uvRects[1]);

            // Clean up
            Object.DestroyImmediate(mockTexture);
        }
    }
}

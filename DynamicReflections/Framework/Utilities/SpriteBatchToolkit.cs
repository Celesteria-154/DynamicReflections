﻿using DynamicReflections.Framework.Patches.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xTile.Dimensions;

namespace DynamicReflections.Framework.Utilities
{
    public static class SpriteBatchToolkit
    {
        // General helpers
        private static bool _hasCache = false;
        private static SpriteSortMode _cachedSpriteSortMode;
        private static BlendState _cachedBlendState;
        private static SamplerState _cachedSamplerState;
        private static DepthStencilState _cachedDepthStencilState;
        private static RasterizerState _cachedRasterizerState;
        private static Effect _cachedSpriteEffect;
        private static Matrix? _cachedMatrix;

        public static void CacheSpriteBatchSettings(SpriteBatch spriteBatch, bool endSpriteBatch = false)
        {
            var reflection = DynamicReflections.modHelper.Reflection;

            _cachedSpriteSortMode = reflection.GetField<SpriteSortMode>(spriteBatch, "_sortMode").GetValue();
            _cachedBlendState = reflection.GetField<BlendState>(spriteBatch, "_blendState").GetValue();
            _cachedSamplerState = reflection.GetField<SamplerState>(spriteBatch, "_samplerState").GetValue();
            _cachedDepthStencilState = reflection.GetField<DepthStencilState>(spriteBatch, "_depthStencilState").GetValue();
            _cachedRasterizerState = reflection.GetField<RasterizerState>(spriteBatch, "_rasterizerState").GetValue();
            _cachedSpriteEffect = reflection.GetField<Effect>(spriteBatch, "_effect").GetValue();
            _cachedMatrix = reflection.GetField<SpriteEffect>(spriteBatch, "_spriteEffect").GetValue().TransformMatrix;

            _hasCache = true;
            if (endSpriteBatch is true)
            {
                spriteBatch.End();
            }
        }

        public static bool ResumeCachedSpriteBatch(SpriteBatch spriteBatch)
        {
            if (_hasCache is false)
            {
                return false;
            }
            _hasCache = false;

            spriteBatch.Begin(_cachedSpriteSortMode, _cachedBlendState, _cachedSamplerState, _cachedDepthStencilState, _cachedRasterizerState, _cachedSpriteEffect, _cachedMatrix);
            return true;
        }

        // LayerPatch helper methods
        // A note on the Render and Draw prefixed methods: These methods assume SpriteBatch has not been started via SpriteBatch.Begin
        internal static void DrawMirrorReflection(Texture2D mask)
        {
            DynamicReflections.mirrorReflectionEffect.Parameters["Mask"].SetValue(mask);
            Game1.spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.NonPremultiplied, SamplerState.PointClamp, effect: DynamicReflections.mirrorReflectionEffect);

            int index = 0;
            foreach (var mirrorPosition in DynamicReflections.activeMirrorPositions)
            {
                var mirror = DynamicReflections.mirrors[mirrorPosition];
                if (mirror.FurnitureLink is null)
                {
                    Game1.spriteBatch.Draw(DynamicReflections.composedPlayerMirrorReflectionRenders[index], Vector2.Zero, Color.White);
                }

                index++;
            }

            Game1.spriteBatch.End();
        }

        internal static void RenderMirrorsLayer()
        {
            // Set the render target
            Game1.graphics.GraphicsDevice.SetRenderTarget(DynamicReflections.mirrorsLayerRenderTarget);

            // Draw the scene
            Game1.graphics.GraphicsDevice.Clear(Color.Transparent);

            if (Game1.currentLocation is not null && Game1.currentLocation.Map is not null)
            {
                if (Game1.currentLocation.Map.GetLayer("Mirrors") is var mirrorsLayer && mirrorsLayer is not null)
                {
                    Game1.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp);

                    // Draw the "Mirrors" layer
                    LayerPatch.DrawReversePatch(mirrorsLayer, Game1.mapDisplayDevice, Game1.viewport, Location.Origin, wrapAround: false, 4);
                    Game1.spriteBatch.End();
                }
            }

            // Drop the render target
            Game1.graphics.GraphicsDevice.SetRenderTarget(null);

            Game1.graphics.GraphicsDevice.Clear(Game1.bgColor);
        }

        internal static void RenderMirrorsFurniture()
        {
            // Set the render target
            Game1.graphics.GraphicsDevice.SetRenderTarget(DynamicReflections.mirrorsFurnitureRenderTarget);

            // Draw the scene
            Game1.graphics.GraphicsDevice.Clear(Color.Transparent);

            if (Game1.currentLocation is not null && Game1.currentLocation.furniture is not null)
            {
                Game1.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp);
                foreach (var mirror in DynamicReflections.mirrors.Values.ToList())
                {
                    if (mirror.IsEnabled is false || mirror.FurnitureLink is null)
                    {
                        continue;
                    }

                    foreach (var furniture in Game1.currentLocation.furniture)
                    {
                        if (mirror.FurnitureLink != furniture)
                        {
                            continue;
                        }

                        DynamicReflections.isFilteringMirror = true;
                        furniture.draw(Game1.spriteBatch, (int)furniture.TileLocation.X, (int)furniture.TileLocation.Y);
                        DynamicReflections.isFilteringMirror = false;
                        break;
                    }
                }
                Game1.spriteBatch.End();
            }

            // Drop the render target
            Game1.graphics.GraphicsDevice.SetRenderTarget(null);

            Game1.graphics.GraphicsDevice.Clear(Game1.bgColor);
        }

        internal static void RenderMirrorReflectionPlayerSprite()
        {
            var oldPosition = Game1.player.Position;
            var oldDirection = Game1.player.FacingDirection;
            var oldSprite = Game1.player.FarmerSprite;

            Dictionary<string, string> modDataCache = new Dictionary<string, string>();
            foreach (var dataKey in Game1.player.modData.Keys)
            {
                modDataCache[dataKey] = Game1.player.modData[dataKey];
            }

            // Note: Current solution is to utilize RenderTarget2Ds as the player sprite is composed of many other sprites layered on top of each other
            // This makes modifying it via shader difficult and even more so difficult with Fashion Sense (as the size of appearances are not bounded)

            // Draw the raw and flattened player sprites
            int index = 0;
            foreach (var mirrorPosition in DynamicReflections.activeMirrorPositions)
            {
                var rawReflectionRender = DynamicReflections.inBetweenRenderTarget;

                // Set the render target
                Game1.graphics.GraphicsDevice.SetRenderTarget(rawReflectionRender);

                // Draw the scene
                Game1.graphics.GraphicsDevice.Clear(Color.Transparent);

                Game1.spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp);

                var mirror = DynamicReflections.mirrors[mirrorPosition];
                var offsetPosition = mirror.PlayerReflectionPosition;
                offsetPosition -= mirror.Settings.ReflectionOffset * 16;

                Game1.player.Position = offsetPosition;
                Game1.player.FacingDirection = DynamicReflections.GetReflectedDirection(oldDirection, true);
                Game1.player.FarmerSprite = oldDirection == 0 ? DynamicReflections.mirrorReflectionSprite : oldSprite;
                Game1.player.modData["FashionSense.Animation.FacingDirection"] = Game1.player.FacingDirection.ToString();

                Game1.player.draw(Game1.spriteBatch);

                Game1.spriteBatch.End();

                Game1.graphics.GraphicsDevice.SetRenderTarget(null);

                // Now use the rawReflectionRender to flip and apply other effects to them
                var composedReflectionRender = DynamicReflections.composedPlayerMirrorReflectionRenders[index];

                // Set the render target
                Game1.graphics.GraphicsDevice.SetRenderTarget(composedReflectionRender);

                // Draw the scene
                Game1.graphics.GraphicsDevice.Clear(Color.Transparent);

                Game1.spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp);

                Game1.player.FacingDirection = DynamicReflections.GetReflectedDirection(oldDirection, true);

                // Determine if we should flip the sprite on the X-axis (if facing front or back)
                var flipEffect = Game1.player.FacingDirection is (0 or 2) ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                // This variable (flipOffset) is required to re-adjust the flipped screen (as the player sprite may not be in the center)
                var flipOffset = Game1.player.FacingDirection is (0 or 2) ? (Game1.viewport.Width - Game1.GlobalToLocal(Game1.viewport, Game1.player.Position).X * 2) - 64 : 0f;

                // TODO: Implement these for Mirror.ReflectionScale
                var scale = new Vector2(1f, 1f);
                var scaleOffset = Vector2.Zero;

                Game1.spriteBatch.Draw(rawReflectionRender, new Vector2(-flipOffset, 0f), rawReflectionRender.Bounds, mirror.Settings.ReflectionOverlay, 0f, scaleOffset, scale, flipEffect, 1f);

                Game1.spriteBatch.End();

                // Drop the render target
                Game1.graphics.GraphicsDevice.SetRenderTarget(null);

                // Now draw the individual furniture mask
                var furnitureMaskRender = DynamicReflections.mirrorsFurnitureRenderTarget;

                // Set the render target
                Game1.graphics.GraphicsDevice.SetRenderTarget(furnitureMaskRender);

                // Draw the scene
                Game1.graphics.GraphicsDevice.Clear(Color.Transparent);

                if (mirror.IsEnabled is true && mirror.FurnitureLink is not null && Game1.currentLocation is not null && Game1.currentLocation.furniture is not null)
                {
                    Game1.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp);

                    foreach (var furniture in Game1.currentLocation.furniture)
                    {
                        if (mirror.FurnitureLink != furniture)
                        {
                            continue;
                        }

                        DynamicReflections.isFilteringMirror = true;
                        furniture.draw(Game1.spriteBatch, (int)furniture.TileLocation.X, (int)furniture.TileLocation.Y);
                        DynamicReflections.isFilteringMirror = false;
                        break;
                    }
                    Game1.spriteBatch.End();
                }

                // Drop the render target
                Game1.graphics.GraphicsDevice.SetRenderTarget(null);

                // Now draw the masked version, for use by the furniture
                var maskedReflectionRender = DynamicReflections.maskedPlayerMirrorReflectionRenders[index];

                // Set the render target
                Game1.graphics.GraphicsDevice.SetRenderTarget(maskedReflectionRender);

                // Draw the scene
                Game1.graphics.GraphicsDevice.Clear(Color.Transparent);

                DynamicReflections.mirrorReflectionEffect.Parameters["Mask"].SetValue(DynamicReflections.mirrorsFurnitureRenderTarget);
                Game1.spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.NonPremultiplied, SamplerState.PointClamp, effect: DynamicReflections.mirrorReflectionEffect);

                Game1.spriteBatch.Draw(composedReflectionRender, Vector2.Zero, Color.White);

                Game1.spriteBatch.End();

                // Drop the render target
                Game1.graphics.GraphicsDevice.SetRenderTarget(null);

                index++;
            }

            Game1.player.Position = oldPosition;
            Game1.player.FacingDirection = oldDirection;
            Game1.player.FarmerSprite = oldSprite;

            // Restore modData for Fashion Sense
            foreach (var dataKey in modDataCache.Keys)
            {
                Game1.player.modData[dataKey] = modDataCache[dataKey];
            }

            Game1.graphics.GraphicsDevice.Clear(Game1.bgColor);
        }

        internal static void RenderWaterReflectionPlayerSprite()
        {
            // Set the render target
            Game1.graphics.GraphicsDevice.SetRenderTarget(DynamicReflections.playerWaterReflectionRender);

            // Draw the scene
            Game1.graphics.GraphicsDevice.Clear(Color.Transparent);

            DrawReflectionViaMatrix();

            // Drop the render target
            Game1.graphics.GraphicsDevice.SetRenderTarget(null);

            Game1.graphics.GraphicsDevice.Clear(Game1.bgColor);
        }

        internal static void DrawReflectionViaMatrix()
        {
            var scale = Matrix.CreateScale(1, -1, 1);
            var position = Matrix.CreateTranslation(0, Game1.GlobalToLocal(Game1.viewport, DynamicReflections.waterReflectionPosition.Value).Y * 2, 0);
            //_monitor.LogOnce($"{Game1.graphics.GraphicsDevice.PresentationParameters.BackBufferHeight} | {Game1.GlobalToLocal(Game1.viewport, ModEntry.farmerReflection.Position).Y}", LogLevel.Debug);

            Game1.spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp, rasterizerState: DynamicReflections.rasterizer, transformMatrix: scale * position);

            var oldPosition = Game1.player.Position;
            Game1.player.Position = DynamicReflections.waterReflectionPosition.Value;
            Game1.player.draw(Game1.spriteBatch);
            Game1.player.Position = oldPosition;

            Game1.spriteBatch.End();
        }

        internal static void DrawRenderedPlayer(bool isWavy = false)
        {
            Game1.spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp, effect: isWavy ? DynamicReflections.waterReflectionEffect : null);
            Game1.spriteBatch.Draw(DynamicReflections.playerWaterReflectionRender, Vector2.Zero, Color.White);

            Game1.spriteBatch.End();
        }
    }
}
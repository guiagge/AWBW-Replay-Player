﻿using AWBWApp.Game.Game.Tile;
using AWBWApp.Game.Helpers;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace AWBWApp.Game.UI.Replay
{
    public class TileCursor : CompositeDrawable
    {
        private Vector2I tilePosition;

        public Vector2I TilePosition
        {
            get => tilePosition;
            set
            {
                if (tilePosition == value)
                    return;

                tilePosition = value;
                moveToPosition(tilePosition);
            }
        }

        private readonly Sprite cursor;

        public TileCursor()
        {
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;

            InternalChild = cursor = new Sprite()
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre
            };
        }

        [BackgroundDependencyLoader]
        private void load(NearestNeighbourTextureStore textureStore)
        {
            cursor.Texture = textureStore.Get("Effects/TileCursor");
            cursor.Size = cursor.Texture.Size;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            this.ScaleTo(0.9f, 1000, Easing.InOutCubic).Then().ScaleTo(0.8f, 1000, Easing.InOutCubic).Loop();
        }

        private void moveToPosition(Vector2I position)
        {
            this.MoveTo(new Vector2(position.X * DrawableTile.BASE_SIZE.X + 16, position.Y * DrawableTile.BASE_SIZE.Y + 48), 125, Easing.OutQuint);
        }
    }
}

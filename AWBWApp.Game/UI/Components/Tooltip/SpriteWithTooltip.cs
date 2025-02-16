﻿using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;

namespace AWBWApp.Game.UI.Components.Tooltip
{
    public class SpriteWithTooltip : Sprite, IHasTooltip
    {
        public string Tooltip { get; set; }

        public LocalisableString TooltipText => Tooltip;
    }
}

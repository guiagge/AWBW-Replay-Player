﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Testing.Drawables.Steps;
using osuTK.Graphics;

namespace AWBWApp.Game.Tests.Visual
{
    public class RepeatUntilStepButton : StepButton
    {
        private readonly int maximumInvocations;
        private int invocations;

        public override int RequiredRepetitions => success ? 0 : int.MaxValue;

        private bool success;

        private string text;

        private readonly Func<bool> isSuccessDelegate;

        public new string Text
        {
            get => text;
            set => base.Text = text = value;
        }

        public RepeatUntilStepButton(Action action, int maximumInvocations, Func<bool> isSuccessDelegate, bool isSetupStep = false)
            : base(isSetupStep)
        {
            this.maximumInvocations = maximumInvocations;
            this.isSuccessDelegate = isSuccessDelegate;
            Action = action;

            updateText();
        }

        public override void PerformStep(bool userTriggered = false)
        {
            if (invocations == maximumInvocations && !userTriggered) throw new InvalidOperationException("Repeat step was invoked too many times");

            if (success && !userTriggered) throw new InvalidOperationException("Repeat step was invoked too many times");

            invocations++;

            base.PerformStep(userTriggered);
            if (isSuccessDelegate())
                Success();

            updateText();
        }

        public override void Reset()
        {
            base.Reset();

            invocations = 0;
            updateText();
        }

        protected override void Success()
        {
            base.Success();
            success = true;
            Light.FadeColour(Color4.YellowGreen);
        }

        private void updateText() => base.Text = $@"{Text} {invocations}/{maximumInvocations}";

        public override string ToString() => "Repeat Until: " + base.ToString();
    }
}

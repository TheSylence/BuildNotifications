﻿using System.Windows.Interactivity;
using System.Windows.Media;
using System.Windows.Shapes;
using BuildNotifications.Resources.Animation;
using TweenSharp.Animation;
using TweenSharp.Factory;

namespace BuildNotifications.Resources.BuildTree.Build
{
    internal class Highlight : TriggerAction<Rectangle>
    {
        public bool DoHighlight { get; set; }

        protected override void Invoke(object parameter)
        {
            var globalTweenHandler = App.GlobalTweenHandler;
            globalTweenHandler.ClearTweensOf(AssociatedObject);

            var brush = new SolidColorBrush();

            if (DoHighlight)
            {
                var targetBrush = AssociatedObject.FindResource("Background3") as SolidColorBrush;
                var background = AssociatedObject.FindResource("Background1") as SolidColorBrush;
                var initialHighlightBrush = AssociatedObject.FindResource("Foreground1") as SolidColorBrush;
                var targetColor = targetBrush?.Color ?? Colors.White;
                var initialColor = initialHighlightBrush?.Color ?? Colors.White;

                brush.Color = background.Color;
                AssociatedObject.Fill = brush;

                var initialTime = 0.5;

                var toInitialColor = brush.Tween(x => x.Color, ColorTween.ColorProgressFunction)
                    .To(initialColor).In(initialTime).Ease(Easing.QuinticEaseIn);
                
                var highlightTween = brush.Tween(x => x.Color, ColorTween.ColorProgressFunction)
                    .To(targetColor).In(0.5).Ease(Easing.QuinticEaseOut).Delay(initialTime + 0.05);
                var opacityTween = AssociatedObject.Tween(x => x.Opacity).To(1).In(0);
                
                globalTweenHandler.Add(new SequenceOfTarget(AssociatedObject, highlightTween, opacityTween, toInitialColor));
            }
            else
            {
                globalTweenHandler.Add(AssociatedObject.Tween(x => x.Opacity).To(0).In(0.25));
            }
        }
    }
}
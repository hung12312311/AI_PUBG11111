using Aimmy2.AILogic;
using Visuality;
using System.Runtime.CompilerServices;
using Dictionary = Aimmy2.Class.Dictionary;
// Frozen coordinate calculation from pre-update commit 409acd3.
internal class LegacyAimReference
{
 public int detectedX, detectedY;
 public double AIConf;
 private int ActiveSlot => AIManager.ActiveSlot;
        public void CalculateCoordinates(DetectedPlayerWindow DetectedPlayerOverlay, Prediction closestPrediction, float scaleX, float scaleY)
        {
            AIConf = closestPrediction.Confidence;

            if (Dictionary.toggleState["Aim Assist"] || Dictionary.DetectedPlayerOverlay != null)
            {
                // We don't call UpdateOverlay here anymore, it's called directly in AiLoop for better responsiveness and ESP
                if (!Dictionary.toggleState["Aim Assist"]) return;
            }

            double YOffset = Dictionary.sliderSettings[ActiveSlot == 1 ? "Slot 1 Y Offset (Up/Down)" : "Y Offset (Up/Down)"];
            double XOffset = Dictionary.sliderSettings[ActiveSlot == 1 ? "Slot 1 X Offset (Left/Right)" : "X Offset (Left/Right)"];

            double YOffsetPercentage = Dictionary.sliderSettings[ActiveSlot == 1 ? "Slot 1 Y Offset (%)" : "Y Offset (%)"];
            double XOffsetPercentage = Dictionary.sliderSettings[ActiveSlot == 1 ? "Slot 1 X Offset (%)" : "X Offset (%)"];

            var rect = closestPrediction.Rectangle;
            
            bool useXPercent = Dictionary.toggleState[ActiveSlot == 1 ? "Slot 1 X Axis Percentage Adjustment" : "X Axis Percentage Adjustment"];
            bool useYPercent = Dictionary.toggleState[ActiveSlot == 1 ? "Slot 1 Y Axis Percentage Adjustment" : "Y Axis Percentage Adjustment"];

            if (useXPercent)
            {
                detectedX = (int)((rect.X + (rect.Width * (XOffsetPercentage / 100))) * scaleX);
            }
            else
            {
                detectedX = (int)((rect.X + rect.Width / 2) * scaleX + XOffset);
            }

            if (useYPercent)
            {
                detectedY = (int)((rect.Y + rect.Height - (rect.Height * (YOffsetPercentage / 100))) * scaleY + YOffset);
            }
            else
            {
                detectedY = CalculateDetectedY(scaleY, YOffset, closestPrediction);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CalculateDetectedY(float scaleY, double YOffset, Prediction closestPrediction)
        {
            var rect = closestPrediction.Rectangle;
            float yBase = rect.Y;
            float yAdjustment = 0;

            switch (Dictionary.dropdownState["Aiming Boundaries Alignment"])
            {
                case "Center":
                    yAdjustment = rect.Height / 2;
                    break;

                case "Top":
                    // yBase is already at the top
                    break;

                case "Bottom":
                    yAdjustment = rect.Height;
                    break;
            }

            return (int)((yBase + yAdjustment) * scaleY + YOffset);
        }


}

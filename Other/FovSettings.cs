using Aimmy2.Class;
using Class;

namespace Other;

internal static class FovSettings
{
    private static int _previousImageSize = 640;
    public static int ImageSize => FileManager.AIManager?.IMAGE_SIZE
        ?? int.Parse((string)Dictionary.dropdownState[Aimmy2.AILogic.AIManager.ActiveSlot == 1 ? "Slot 1 Image Size" : "Slot 2 Image Size"]);

    public static void Synchronize(int imageSize)
    {
        foreach (string key in new[] { "FOV Size", "Dynamic FOV Size" })
        {
            double current = Convert.ToDouble(Dictionary.sliderSettings[key]);
            // A full-size FOV follows the model; a smaller user-selected FOV stays smaller.
            Dictionary.sliderSettings[key] = current >= _previousImageSize
                ? imageSize : Math.Clamp(current, 10, imageSize);
        }
        _previousImageSize = imageSize;
    }
}

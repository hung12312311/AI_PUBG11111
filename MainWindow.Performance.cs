using Aimmy2.AILogic;
using Aimmy2.Class;
using Other;
using System.IO;

namespace Aimmy2;

public partial class MainWindow
{
    internal async Task<bool> ApplyPerformanceRecommendationAsync(PerformanceRecommendation recommendation)
    {
        var manager = FileManager.AIManager;
        if (manager == null) return false;
        try
        {
            int slot = AIManager.ActiveSlot;
            if (recommendation.CanChangeImageSize)
                await Task.Run(() => manager.ConfigureDimensions(slot, recommendation.SuggestedImageSize, recommendation.SuggestedImageSize,
                    Convert.ToInt32(Dictionary.sliderSettings["Capture Size"])));
            Dictionary.sliderSettings["AI FPS Limit"] = recommendation.SuggestedFpsLimit;
            global::Class.SaveDictionary.WriteJSON(Dictionary.sliderSettings, Path.Combine(AppContext.BaseDirectory,"bin","configs","Default.cfg"));
            return true;
        }
        catch (Exception ex) { LogManager.Log(LogManager.LogLevel.Error, "Không áp dụng được benchmark: " + ex.Message, true); return false; }
    }
}

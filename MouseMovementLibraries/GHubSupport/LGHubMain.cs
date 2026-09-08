using Other;
using System.Windows;

namespace Aimmy2.MouseMovementLibraries.GHubSupport
{
    internal class LGHubMain
    {
        public bool Load()
        {
            if (!RequirementsManager.CheckForGhub())
            {
                global::Other.LocalizedMessageBox.Show("Unfortunately, LG HUB Mouse is not here.", "Aimmy");
                return false;
            }

            if (RequirementsManager.IsMemoryIntegrityEnabled())
            {
                try
                {
                    LGMouse.Open();
                    LGMouse.Close();
                    return true;
                }
                catch (Exception ex)
                {
                    global::Other.LocalizedMessageBox.Show("Unfortunately, LG HUB Mouse Movement mode cannot be ran sufficiently.\n" + ex.ToString(), "Aimmy");
                    return false;
                }
            }
            else
            {
                global::Other.LocalizedMessageBox.Show("Memory Integrity is enabled. Please disable it to use LG HUB Mouse Movement mode.", "Aimmy");
                return false;
            }
        }
    }
}
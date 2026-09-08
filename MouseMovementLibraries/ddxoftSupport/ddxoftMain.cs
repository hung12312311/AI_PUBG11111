using Other;
using System.IO;
using System.Security.Principal;
using System.Windows;

namespace MouseMovementLibraries.ddxoftSupport
{
    internal class DdxoftMain
    {
        public static ddxoftMouse ddxoftInstance = new();
        private static readonly string ddxoftpath = Path.Combine(AppContext.BaseDirectory, "ddxoft.dll");
        private static readonly object LoadLock = new();
        private static Task<bool>? _loading;
        public static bool IsReady { get; private set; }
        public static void Move(int x, int y) { if (IsReady) ddxoftInstance.movR(x, y); }
        public static void Button(int button) { if (IsReady) ddxoftInstance.btn(button); }
        public static Task<bool> Load()
        {
            lock (LoadLock)
            {
                if (IsReady) return Task.FromResult(true);
                if (_loading is { IsCompleted: false }) return _loading;
                return _loading = DLLLoading();
            }
        }
        private static async Task<bool> DLLLoading()
        {
            try
            {
                if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator) == false)
                {
                    global::Other.LocalizedMessageBox.Show("The ddxoft Virtual Input Driver requires Aimmy to be run as an administrator, please close Aimmy and run it as administrator to use this movement method.", "Aimmy");
                    return false;
                }

                if (!File.Exists(ddxoftpath))
                {
                    global::Other.LocalizedMessageBox.Show("Thiếu ddxoft.dll trong thư mục app. Khôi phục DLL x64 từ gói ddxoft chính thức: https://github.com/ddxoft/master", "Aimmy");
                    return false;
                }

                var result = await Task.Run(() =>
                {
                    int loadCode = ddxoftInstance.Load(ddxoftpath);
                    int initCode = loadCode == 1 ? ddxoftInstance.btn(0) : 0;
                    return (loadCode, initCode);
                });
                if (result.loadCode != 1 || result.initCode != 1)
                {
                    global::Other.LocalizedMessageBox.Show($"ddxoft khởi tạo không thành công. DLL={result.loadCode}, DD_btn(0)={result.initCode}.\nNếu cửa sổ driver báo Time out, quá trình khởi tạo/xác thực của driver đã hết thời gian; chưa thể kết luận máy không tương thích.\nCó thể dùng Mouse Event hoặc SendInput trong lúc kiểm tra driver.", "Aimmy");
                    return false;
                }
                IsReady = true;

                return true;
            }
            catch (Exception ex)
            {
                global::Other.LocalizedMessageBox.Show("Failed to load ddxoft virtual input driver.\n\n" + ex.ToString(), "Aimmy");
                return false;
            }
        }


    }
}
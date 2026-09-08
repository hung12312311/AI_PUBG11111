using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Other;
public static class LocalizedMessageBox
{
    public static MessageBoxResult Show(string message, string caption = "Aimmy", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None, MessageBoxOptions options = MessageBoxOptions.None)
    {
        if (UiLanguage.Current.Code != "vi" || Application.Current == null)
            return System.Windows.MessageBox.Show(UiLanguage.Text(message), UiLanguage.Text(caption), buttons, icon, defaultResult, options);
        return Application.Current.Dispatcher.Invoke(() => ShowDialog(null,message,caption,buttons,defaultResult));
    }
    public static MessageBoxResult Show(Window owner, string message, string caption = "Aimmy", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None, MessageBoxOptions options = MessageBoxOptions.None)
    {
        if (UiLanguage.Current.Code != "vi") return System.Windows.MessageBox.Show(owner,UiLanguage.Text(message),UiLanguage.Text(caption),buttons,icon,defaultResult,options);
        return owner.Dispatcher.Invoke(() => ShowDialog(owner,message,caption,buttons,defaultResult));
    }
    private static MessageBoxResult ShowDialog(Window? owner,string message,string caption,MessageBoxButton buttons,MessageBoxResult defaultResult)
    {
        var result = buttons == MessageBoxButton.YesNo ? MessageBoxResult.No : buttons == MessageBoxButton.OK ? MessageBoxResult.OK : MessageBoxResult.Cancel;
        var dialog = new Window { Title=UiLanguage.Text(caption),Width=520,SizeToContent=SizeToContent.Height,MaxHeight=650,ResizeMode=ResizeMode.NoResize,Background=new SolidColorBrush(Color.FromRgb(30,29,40)),Foreground=Brushes.White,WindowStartupLocation=owner==null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,ShowInTaskbar=false };
        if(owner?.IsVisible==true)dialog.Owner=owner;
        var panel=new StackPanel { Margin=new Thickness(20) };
        panel.Children.Add(new TextBlock { Text=UiLanguage.Text(caption),FontSize=17,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12) });
        panel.Children.Add(new ScrollViewer { MaxHeight=450,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=new TextBlock { Text=UiLanguage.Text(message),TextWrapping=TextWrapping.Wrap,FontSize=13 } });
        var actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0) };
        var choices=buttons switch { MessageBoxButton.YesNo => new[]{MessageBoxResult.Yes,MessageBoxResult.No}, MessageBoxButton.YesNoCancel => new[]{MessageBoxResult.Yes,MessageBoxResult.No,MessageBoxResult.Cancel}, MessageBoxButton.OKCancel => new[]{MessageBoxResult.OK,MessageBoxResult.Cancel}, _=>new[]{MessageBoxResult.OK} };
        foreach(var choice in choices)
        {
            var button=new Button { Content=UiLanguage.Text(choice.ToString()),MinWidth=85,Padding=new Thickness(12,7,12,7),Margin=new Thickness(6,0,0,0),Background=new SolidColorBrush(Color.FromRgb(112,67,192)),Foreground=Brushes.White,IsDefault=choice==(defaultResult==MessageBoxResult.None ? choices[0] : defaultResult),IsCancel=choice==MessageBoxResult.Cancel };
            button.Click+=(_,_)=>{result=choice;dialog.Close();};actions.Children.Add(button);
        }
        panel.Children.Add(actions);dialog.Content=panel;dialog.ShowDialog();return result;
    }
}

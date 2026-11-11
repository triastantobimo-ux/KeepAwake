using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace KeepAwake;

public partial class AboutWindow : FluentWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
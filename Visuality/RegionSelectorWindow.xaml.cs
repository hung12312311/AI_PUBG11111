using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Aimmy2.Class; // For Dictionary access if needed, or simply pass callback
using Rectangle = System.Windows.Shapes.Rectangle;
using Point = System.Windows.Point; // Explicit to avoid confusion with System.Drawing.Point

namespace Visuality
{
    public partial class RegionSelectorWindow : Window
    {
        private Point _startPoint;
        private bool _isDragging = false;
        public System.Drawing.Rectangle SelectedRegion { get; private set; }
        public bool IsConfirmed { get; private set; } = false;

        public RegionSelectorWindow(string promptText = "Select Region")
        {
            InitializeComponent();
            Loaded += (_, _) => global::Other.UiLanguage.RefreshTree(this);
            TitleText.Text = promptText;
            this.KeyDown += RegionSelectorWindow_KeyDown;
        }

        private void RegionSelectorWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                IsConfirmed = false;
                this.Close();
            }
        }

        private void SelectionCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ConfirmPanel.Visibility == Visibility.Visible) return;

            _startPoint = e.GetPosition(SelectionCanvas);
            _isDragging = true;
            
            Canvas.SetLeft(SelectionRect, _startPoint.X);
            Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
            SelectionRect.Visibility = Visibility.Visible;
        }

        private void SelectionCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            var currentPoint = e.GetPosition(SelectionCanvas);
            
            var x = Math.Min(currentPoint.X, _startPoint.X);
            var y = Math.Min(currentPoint.Y, _startPoint.Y);
            
            var width = Math.Abs(currentPoint.X - _startPoint.X);
            var height = Math.Abs(currentPoint.Y - _startPoint.Y);

            Canvas.SetLeft(SelectionRect, x);
            Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = width;
            SelectionRect.Height = height;

            InfoText.Text = $"Selecting: X={(int)x}, Y={(int)y}, W={(int)width}, H={(int)height}";
        }

        private void SelectionCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;

            // Show confirm panel near the selection
            double panelLeft = Canvas.GetLeft(SelectionRect);
            double panelTop = Canvas.GetTop(SelectionRect) + SelectionRect.Height + 10;
            
            // Adjust if off screen
            if (panelLeft + ConfirmPanel.Width > this.ActualWidth)
                panelLeft = this.ActualWidth - ConfirmPanel.Width - 10;
            
            if (panelTop + ConfirmPanel.Height > this.ActualHeight)
                panelTop = Canvas.GetTop(SelectionRect) - ConfirmPanel.Height - 10;

            Canvas.SetLeft(ConfirmPanel, panelLeft);
            Canvas.SetTop(ConfirmPanel, panelTop);
            ConfirmPanel.Visibility = Visibility.Visible;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            int x = (int)Canvas.GetLeft(SelectionRect);
            int y = (int)Canvas.GetTop(SelectionRect);
            int w = (int)SelectionRect.Width;
            int h = (int)SelectionRect.Height;

            SelectedRegion = new System.Drawing.Rectangle(x, y, w, h);
            IsConfirmed = true;
            this.Close();
        }

        private void Retry_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPanel.Visibility = Visibility.Collapsed;
            SelectionRect.Visibility = Visibility.Collapsed;
            InfoText.Text = "Click and drag to select a region. Press ESC to cancel.";
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            this.Close();
        }
    }
}

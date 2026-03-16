using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Eventask.App.Models;
using Eventask.App.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Eventask.App.Views
{
    public partial class MainView : UserControl
    {
        // 防止滚动事件重入导致的死循环
        private bool _isLoadingYear = false;
        // 缓存 ItemsControl 内部的 Panel，避免频繁查找
        private Panel? _yearItemsPanel;
        // 上一次的视图模式，用于检测模式切换
        private ViewModels.CalendarMode _previousMode = ViewModels.CalendarMode.Month;
        // 标记是否正在等待初始滚动定位
        private bool _isInitialScrollPending = false;
        // 保存滚动位置
        private double _savedScrollOffset = 0;

        public MainView()
        {
            InitializeComponent();
            this.AttachedToVisualTree += OnAttachedToVisualTree;
            this.DetachedFromVisualTree += OnDetachedFromVisualTree;
            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            // 当 DataContext 改变时，订阅 ViewModel 的属性变化
            if (DataContext is MainViewModel vm)
            {
                vm.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentMode) && DataContext is MainViewModel vm)
            {
                // 检测是否切换到了年视图
                if (_previousMode != ViewModels.CalendarMode.Year && vm.CurrentMode == ViewModels.CalendarMode.Year)
                {
                    // 标记开始等待初始滚动
                    _isInitialScrollPending = true;
                    // 延迟执行滚动恢复
                    Dispatcher.UIThread.Post(() => RestoreScrollPosition(), DispatcherPriority.Loaded);
                }
                else if (_previousMode == ViewModels.CalendarMode.Year && vm.CurrentMode != ViewModels.CalendarMode.Year)
                {
                    // 离开年视图时保存滚动位置
                    SaveScrollPosition();
                }
                _previousMode = vm.CurrentMode;
            }
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // 初始加载时，如果是年视图模式，恢复滚动位置
            if (DataContext is MainViewModel vm && vm.CurrentMode == ViewModels.CalendarMode.Year)
            {
                _isInitialScrollPending = true;
                Dispatcher.UIThread.Post(() => RestoreScrollPosition(), DispatcherPriority.Loaded);
            }
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // 导航离开时，如果当前是年视图模式，保存滚动位置
            if (DataContext is MainViewModel vm && vm.CurrentMode == ViewModels.CalendarMode.Year)
            {
                SaveScrollPosition();
            }
        }

        private void SaveScrollPosition()
        {
            var scrollViewer = this.FindControl<ScrollViewer>("MonthGroupsScrollViewer");
            if (scrollViewer != null)
            {
                _savedScrollOffset = scrollViewer.Offset.Y;
            }
        }

        private void RestoreScrollPosition()
        {
            try
            {
                if (DataContext is not MainViewModel vm) return;
            
                var scrollViewer = this.FindControl<ScrollViewer>("MonthGroupsScrollViewer");
                if (scrollViewer == null) return;

                // 如果有保存的滚动位置,优先恢复
                if (_savedScrollOffset > 0)
                {
                    scrollViewer.Offset = scrollViewer.Offset.WithY(_savedScrollOffset);
                    return;
                }

                // 否则滚动到当前年份
                ScrollToCurrentYear(scrollViewer, vm);
            }
            finally
            {
                Dispatcher.UIThread.Post(() => _isInitialScrollPending = false, DispatcherPriority.Background);
            }
        }

        private void ScrollToCurrentYear(ScrollViewer scrollViewer, MainViewModel vm)
        {
            // 查找 ItemsControl 内部的 Panel
            if (scrollViewer.Content is ItemsControl itemsControl)
            {
                var panel = itemsControl.GetVisualDescendants()
                                        .OfType<Panel>()
                                        .FirstOrDefault(p => p is StackPanel || p is VirtualizingStackPanel);
            
                if (panel == null) return;

                // 使用 SelectedDate 而不是 CurrentDate
                int targetYear = vm.SelectedDate.Year;
                bool found = false;

                foreach (var child in panel.Children)
                {
                    if (child is Control control && control.DataContext is YearModel yearModel)
                    {
                        if (yearModel.Year == targetYear)
                        {
                            double targetY = control.Bounds.Top;
                            scrollViewer.Offset = scrollViewer.Offset.WithY(targetY);
                            found = true;
                            return;
                        }
                    }
                }

                // 兜底逻辑
                if (!found && panel.Children.Count > 0)
                {
                    if (TryApproximateYearOffset(scrollViewer, panel, vm, targetYear))
                    {
                        return;
                    }

                    if (panel is not VirtualizingStackPanel)
                    {
                        var firstChild = panel.Children.FirstOrDefault(c => c is Control ctrl && ctrl.DataContext is YearModel) as Control;
                        var lastChild = panel.Children.LastOrDefault(c => c is Control ctrl && ctrl.DataContext is YearModel) as Control;

                        if (firstChild?.DataContext is YearModel firstYear && targetYear < firstYear.Year)
                        {
                            scrollViewer.Offset = scrollViewer.Offset.WithY(0);
                        }
                        else if (lastChild?.DataContext is YearModel lastYear && targetYear > lastYear.Year)
                        {
                            scrollViewer.Offset = scrollViewer.Offset.WithY(scrollViewer.Extent.Height);
                        }
                    }
                }
            }
        }

        private static bool TryApproximateYearOffset(ScrollViewer scrollViewer, Panel panel, MainViewModel vm, int targetYear)
        {
            if (vm.YearGroups.Count == 0)
            {
                return false;
            }

            var orderedYears = vm.YearGroups.OrderBy(y => y.Year).ToList();
            int targetIndex = orderedYears.FindIndex(y => y.Year == targetYear);
            if (targetIndex < 0)
            {
                return false;
            }

            double referenceHeight = panel.Children.OfType<Control>()
                                                   .Select(c => c.Bounds.Height)
                                                   .Where(h => h > 0)
                                                   .DefaultIfEmpty(0)
                                                   .Average();

            if (referenceHeight <= 0 && panel.Children.Count > 0)
            {
                referenceHeight = scrollViewer.Viewport.Height / Math.Max(1, panel.Children.Count);
            }

            if (referenceHeight <= 0)
            {
                return false;
            }

            double targetOffset = targetIndex * referenceHeight;
            double maxOffset = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
            scrollViewer.Offset = scrollViewer.Offset.WithY(Math.Clamp(targetOffset, 0, maxOffset));
            return true;
        }

        private void YearView_ScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (_isLoadingYear || _isInitialScrollPending) return;

            if (DataContext is MainViewModel vm && sender is ScrollViewer scrollViewer)
            {
                // 1. 向上滚动加载
                if (scrollViewer.Offset.Y < 50)
                {
                    _isLoadingYear = true;
                    double oldExtentHeight = scrollViewer.Extent.Height;

                    bool added = vm.LoadPreviousYear();

                    if (added)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            try
                            {
                                double newExtentHeight = scrollViewer.Extent.Height;
                                double heightDiff = newExtentHeight - oldExtentHeight;
                                if (heightDiff > 0)
                                {
                                    scrollViewer.Offset = scrollViewer.Offset.WithY(scrollViewer.Offset.Y + heightDiff * 2 - 20);
                                }
                            }
                            finally
                            {
                                _isLoadingYear = false;
                            }
                        }, DispatcherPriority.Loaded);
                    }
                    else
                    {
                        _isLoadingYear = false;
                    }
                }
                // 2. 向下滚动加载
                else if (scrollViewer.Offset.Y > (scrollViewer.Extent.Height - scrollViewer.Viewport.Height - 50))
                {
                    _isLoadingYear = true;
                    vm.LoadNextYear();
                    Dispatcher.UIThread.Post(() => _isLoadingYear = false, DispatcherPriority.Loaded);
                }

                // 3. 更新年份标题
                if (!_isLoadingYear)
                {
                    UpdateYearHeader(scrollViewer, vm);
                }
            }
        }

        private void UpdateYearHeader(ScrollViewer scrollViewer, MainViewModel vm)
        {
            if (_isInitialScrollPending) return;

            if (_yearItemsPanel == null || _yearItemsPanel.GetVisualRoot() == null)
            {
                if (scrollViewer.Content is ItemsControl itemsControl)
                {
                    _yearItemsPanel = itemsControl.GetVisualDescendants()
                                                  .OfType<Panel>()
                                                  .FirstOrDefault(p => p is StackPanel || p is VirtualizingStackPanel);
                }
            }

            if (_yearItemsPanel == null) return;

            double currentScrollY = scrollViewer.Offset.Y;
            double viewportHeight = scrollViewer.Viewport.Height;
            double viewportBottom = currentScrollY + viewportHeight;

            var visibleYears = new List<int>();

            foreach (var child in _yearItemsPanel.Children)
            {
                if (child is Control control && control.DataContext is YearModel yearModel)
                {
                    if (control.Bounds.Bottom > currentScrollY && control.Bounds.Top < viewportBottom)
                    {
                        visibleYears.Add(yearModel.Year);
                    }
                }
            }

            if (visibleYears.Count == 0) return;

            visibleYears.Sort();

            int targetYear;
            if (visibleYears.Count >= 2)
            {
                targetYear = visibleYears[0];
            }
            else
            {
                targetYear = visibleYears[0] - 1;
            }

            if (vm.CurrentDate.Year != targetYear)
            {
                int newYear = targetYear;
                int month = vm.CurrentDate.Month;
                int day = vm.CurrentDate.Day;
                int daysInMonth = DateTime.DaysInMonth(newYear, month);
                
                vm.UpdateCurrentDateFromScroll(new DateTime(newYear, month, Math.Min(day, daysInMonth)));
            }
        }

        private void OnMonthViewPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                int offset = e.Delta.Y > 0 ? -1 : 1;
                vm.ChangeMonth(offset);
                e.Handled = true;
            }
        }
    }
}
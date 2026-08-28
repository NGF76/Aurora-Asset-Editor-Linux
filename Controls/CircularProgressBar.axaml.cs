using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using System;
using System.ComponentModel;
using AuroraAssetEditorLinux.Controls;

namespace AuroraAssetEditorLinux.Controls
{
    public partial class CircularProgressBar : UserControl
    {
        #region Data

        private readonly DispatcherTimer _animationTimer;
        private bool _isAnimating;

        #endregion

        #region Constructor

        public CircularProgressBar()
        {
            InitializeComponent();

            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(75)
            };
            _animationTimer.Tick += HandleAnimationTick!;

            // ربط الأحداث
            this.AttachedToVisualTree += OnAttachedToVisualTree;
            this.DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        #endregion

        #region Private Methods

        private void Start()
        {
            if (_isAnimating) return;
            
            // تغيير شكل المؤشر إلى "انتظار"
            if (TopLevel.GetTopLevel(this) is TopLevel topLevel)
            {
                topLevel.Cursor = new Cursor(StandardCursorType.Wait);
            }

            _isAnimating = true;
            _animationTimer.Start();
        }

        private void Stop()
        {
            if (!_isAnimating) return;
            
            _animationTimer.Stop();
            _isAnimating = false;

            // إعادة المؤشر إلى الشكل الطبيعي
            if (TopLevel.GetTopLevel(this) is TopLevel topLevel)
            {
                topLevel.Cursor = new Cursor(StandardCursorType.Arrow);
            }
        }

        private void HandleAnimationTick(object? sender, EventArgs e)
        {
            if (SpinnerRotate != null)
            {
                SpinnerRotate.Angle = (SpinnerRotate.Angle + 36) % 360;
            }
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // عند إضافة العنصر إلى الشجرة البصرية
            if (this.IsVisible)
            {
                Start();
            }
            
            // حساب مواقع الدوائر
            CalculatePositions();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // عند إزالة العنصر من الشجرة البصرية
            Stop();
        }

        private void CalculatePositions()
        {
            const double offset = Math.PI;
            const double step = Math.PI * 2 / 10.0;

            SetPosition(C0, offset, 0.0, step);
            SetPosition(C1, offset, 1.0, step);
            SetPosition(C2, offset, 2.0, step);
            SetPosition(C3, offset, 3.0, step);
            SetPosition(C4, offset, 4.0, step);
            SetPosition(C5, offset, 5.0, step);
            SetPosition(C6, offset, 6.0, step);
            SetPosition(C7, offset, 7.0, step);
            SetPosition(C8, offset, 8.0, step);
        }

        private static void SetPosition(Control ellipse, double offset, double posOffSet, double step)
        {
            var left = 50.0 + Math.Sin(offset + posOffSet * step) * 50.0;
            var top = 50.0 + Math.Cos(offset + posOffSet * step) * 50.0;
            
            if (ellipse != null)
            {
                Canvas.SetLeft(ellipse, left);
                Canvas.SetTop(ellipse, top);
            }
        }

        // ✅ الدالة الصحيحة لـ OnIsVisibleChanged في Avalonia
        protected override void OnIsVisibleChanged(AvaloniaPropertyChangedEventArgs e)
        {
            base.OnIsVisibleChanged(e);
            
            if (this.IsVisible)
            {
                Start();
            }
            else
            {
                Stop();
            }
        }

        // ✅ إزالة OnUnloaded واستخدام OnDetachedFromVisualTree بدلاً منه
        // protected override void OnUnloaded(EventArgs e) ← ❌ غير موجود في Avalonia

        #endregion
    }
}
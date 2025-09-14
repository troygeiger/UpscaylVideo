using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using UpscaylVideo.ViewModels;

namespace UpscaylVideo.Views;

public partial class PreviewPageView : UserControl
{
    private Border? _clipBorder;
    private Image? _afterImage;
    private Canvas? _overlay;
    private Border? _splitLine;
    private Border? _splitHandle;
    private bool _dragging;
    private PreviewPageViewModel? _vm;

    public PreviewPageView()
    {
        InitializeComponent();
        this.AttachedToVisualTree += OnAttachedToVisualTree;
        this.DetachedFromVisualTree += OnDetachedFromVisualTree;
        this.DataContextChanged += OnDataContextChanged;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _clipBorder = this.FindControl<Border>("ClipBorder");
        _afterImage = this.FindControl<Image>("AfterImage");
        _overlay = this.FindControl<Canvas>("Overlay");
    _splitLine = this.FindControl<Border>("SplitLine");
        _splitHandle = this.FindControl<Border>("SplitHandle");
        if (_overlay != null && _splitHandle != null)
        {
            _overlay.PointerPressed += OverlayOnPointerPressed;
            _overlay.PointerMoved += OverlayOnPointerMoved;
            _overlay.PointerReleased += OverlayOnPointerReleased;
            _overlay.GetObservable(BoundsProperty).Subscribe(_ => LayoutOverlay());
            this.GetObservable(BoundsProperty).Subscribe(_ => LayoutOverlay());

            // Handle-specific events to make grabbing easier
            _splitHandle.PointerPressed += OverlayOnPointerPressed;
            _splitHandle.PointerMoved += OverlayOnPointerMoved;
            _splitHandle.PointerReleased += OverlayOnPointerReleased;
        }
        if (_afterImage != null)
        {
            // When the image gets real bounds after the first arrange, lay out the overlay/clip
            _afterImage.PropertyChanged += (_, ev) =>
            {
                if (DataContext is PreviewPageViewModel vm && (ev.Property == BoundsProperty))
                {
                    UpdateClip(vm.SplitPosition);
                    // Also ensure the overlay positions once the image has real bounds
                    LayoutOverlay();
                }
            };
            _afterImage.GetObservable(BoundsProperty).Subscribe(_ =>
            {
                if (DataContext is PreviewPageViewModel vm)
                {
                    UpdateClip(vm.SplitPosition);
                    LayoutOverlay();
                }
            });
        }
        if (DataContext is PreviewPageViewModel vm)
        {
            SetViewModel(vm);
        }
        // Defer a second pass until after first render so bounds are available
        // Post at Render priority to ensure bounds are calculated on first show
        Dispatcher.UIThread.Post(() =>
        {
            if (_vm is not null)
            {
                UpdateClip(_vm.SplitPosition);
                LayoutOverlay();
            }
        }, DispatcherPriority.Render);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= VmOnPropertyChanged;
            _vm = null;
        }
        this.DataContextChanged -= OnDataContextChanged;
        if (_overlay != null)
        {
            _overlay.PointerPressed -= OverlayOnPointerPressed;
            _overlay.PointerMoved -= OverlayOnPointerMoved;
            _overlay.PointerReleased -= OverlayOnPointerReleased;
            if (_splitHandle != null)
            {
                _splitHandle.PointerPressed -= OverlayOnPointerPressed;
                _splitHandle.PointerMoved -= OverlayOnPointerMoved;
                _splitHandle.PointerReleased -= OverlayOnPointerReleased;
            }
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is PreviewPageViewModel vm)
        {
            SetViewModel(vm);
        }
    }

    private void SetViewModel(PreviewPageViewModel vm)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= VmOnPropertyChanged;
        }
        _vm = vm;
        _vm.PropertyChanged += VmOnPropertyChanged;
        UpdateClip(_vm.SplitPosition);
        LayoutOverlay();
    }

    private void VmOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not PreviewPageViewModel vm)
            return;
        if (e.PropertyName == nameof(PreviewPageViewModel.SplitPosition))
        {
            UpdateClip(vm.SplitPosition);
            LayoutOverlay();
        }
    }

    private void UpdateClip(double split)
    {
        if (_clipBorder is null || _afterImage is null)
            return;
        // Calculate clip rectangle based on displayed image rectangle and split (0..1)
        var bounds = _afterImage.Bounds;
        var display = GetDisplayedImageRect();
        double height = bounds.Height;
        double clipWidth;
        if (display is Rect dr && dr.Width > 0)
        {
            // Reveal left bars + left portion of the displayed image
            clipWidth = Math.Clamp(dr.X + (dr.Width * split), 0, bounds.Width);
        }
        else
        {
            // Fallback: use entire bounds
            clipWidth = Math.Max(0, Math.Min(bounds.Width, bounds.Width * split));
        }
        _clipBorder.Clip = new RectangleGeometry(new Rect(0, 0, clipWidth, height));
    }

    private void LayoutOverlay()
    {
        if (_overlay is null || _splitLine is null || _splitHandle is null || DataContext is not PreviewPageViewModel vm)
            return;
        // Compute the displayed image rect so the split aligns with what the user sees
        var display = GetDisplayedImageRect();
        double w, h, left, top;
        if (display is Rect dr && dr.Width > 0 && dr.Height > 0)
        {
            w = dr.Width;
            h = dr.Height;
            left = dr.X;
            top = dr.Y;
        }
        else
        {
            // Fallback to the overlay size/container when no image yet
            var ob = _overlay.Bounds;
            w = ob.Width;
            h = ob.Height;
            left = 0;
            top = 0;
            if (w <= 0 || h <= 0)
            {
                var self = this.Bounds;
                w = Math.Max(0, self.Width);
                h = Math.Max(0, self.Height);
            }
        }
        if (w <= 0 || h <= 0) return;

        var x = left + (w * vm.SplitPosition);
        Canvas.SetLeft(_splitLine, x - (_splitLine.Width / 2));
        Canvas.SetTop(_splitLine, top);
        _splitLine.Height = h;

        Canvas.SetLeft(_splitHandle, x - (_splitHandle.Width / 2));
        Canvas.SetTop(_splitHandle, top + (h - _splitHandle.Height) / 2);
    }

    // Computes the actual displayed rectangle of the AfterImage inside its bounds when Stretch=Uniform and centered
    private Rect? GetDisplayedImageRect()
    {
        if (_afterImage is null)
            return null;
        var bounds = _afterImage.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return null;
        // Try to get pixel size from VM's bitmap (same as Image.Source)
        if (DataContext is not PreviewPageViewModel vm || vm.AfterImage is null)
            return new Rect(0, 0, bounds.Width, bounds.Height);
        var pixels = vm.AfterImage.PixelSize;
        if (pixels.Width <= 0 || pixels.Height <= 0)
            return new Rect(0, 0, bounds.Width, bounds.Height);

        double bw = bounds.Width;
        double bh = bounds.Height;
        double iw = pixels.Width;
        double ih = pixels.Height;
        if (iw <= 0 || ih <= 0)
            return new Rect(0, 0, bw, bh);

        var scale = Math.Min(bw / iw, bh / ih);
        var dw = iw * scale;
        var dh = ih * scale;
        var dx = (bw - dw) / 2.0;
        var dy = (bh - dh) / 2.0;
        return new Rect(dx, dy, dw, dh);
    }
    

    private void OverlayOnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (_overlay is null || _afterImage is null || DataContext is not PreviewPageViewModel vm)
            return;
        _dragging = true;
        UpdateSplitFromPointer(e, vm);
    }

    private void OverlayOnPointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (!_dragging || _overlay is null || _afterImage is null || DataContext is not PreviewPageViewModel vm)
            return;
        UpdateSplitFromPointer(e, vm);
    }

    private void OverlayOnPointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        if (_overlay is null)
            return;
        _dragging = false;
    }

    private void UpdateSplitFromPointer(Avalonia.Input.PointerEventArgs e, PreviewPageViewModel vm)
    {
        if (_afterImage is null)
            return;
        var p = e.GetPosition(_afterImage);
        var dr = GetDisplayedImageRect();
        double ratio;
        if (dr is Rect r && r.Width > 0)
        {
            ratio = Math.Clamp((p.X - r.X) / r.Width, 0, 1);
        }
        else
        {
            var w = _afterImage.Bounds.Width;
            if (w <= 0) return;
            ratio = Math.Clamp(p.X / w, 0, 1);
        }
        vm.SplitPosition = ratio;
    }
}

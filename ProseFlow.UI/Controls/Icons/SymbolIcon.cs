using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ProseFlow.UI.Utils;
using System.Collections.Concurrent;

namespace ProseFlow.UI.Controls.Icons;

/// <summary>
/// Renders a vector icon, optimized for performance by caching parsed geometries.
/// </summary>
public sealed class SymbolIcon : Control
{
    // Global cache for parsed icon geometries to avoid expensive reparsing.
    private static readonly ConcurrentDictionary<IconSymbol, Geometry> GeometryCache = new();

    // The native design size of the vector icons.

    private Pen? _pen;
    private Geometry? _geometry;

    #region Avalonia Properties

    public static readonly StyledProperty<double> ViewboxSizeProperty =
        AvaloniaProperty.Register<SymbolIcon, double>(nameof(ViewboxSize), 24.0);
    
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<SymbolIcon, double>(nameof(Size), 18.0);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextBlock.ForegroundProperty.AddOwner<SymbolIcon>();

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<SymbolIcon, double>(nameof(StrokeWidth), 1.5);

    public static readonly StyledProperty<IconSymbol?> SymbolProperty =
        AvaloniaProperty.Register<SymbolIcon, IconSymbol?>(nameof(Symbol));

    #endregion

    #region CLR Accessors

    /// <summary>
    /// Gets or sets the native design size of the vector icons.
    /// </summary>
    public double ViewboxSize
    {
        get => GetValue(ViewboxSizeProperty);
        set => SetValue(ViewboxSizeProperty, value);
    }
    
    
    /// <summary>
    /// Gets or sets the uniform width and height of the icon.
    /// </summary>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used to draw the icon's stroke.
    /// </summary>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the thickness of the icon's stroke.
    /// </summary>
    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the symbol that determines which icon to display.
    /// </summary>
    public IconSymbol? Symbol
    {
        get => GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    #endregion

    static SymbolIcon()
    {
        // Register properties to trigger render/measure updates when they change.
        AffectsRender<SymbolIcon>(SymbolProperty, SizeProperty, ForegroundProperty, StrokeWidthProperty);
        AffectsMeasure<SymbolIcon>(SizeProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SymbolProperty)
            _geometry = GetOrParseGeometry(change.GetNewValue<IconSymbol?>());
        else if (change.Property == ForegroundProperty || change.Property == StrokeWidthProperty) _pen = null; // Invalidate the pen as it will be recreated on the next render pass.
    }

    public override void Render(DrawingContext context)
    {
        if (_geometry is null || Foreground is null || Size <= 0)
            return;

        // Lazily create the rendering pen if it has been invalidated.
        _pen ??= new Pen(Foreground, StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        var scale = Size / ViewboxSize;
        var scaleMatrix = Matrix.CreateScale(scale, scale);

        // Use a transform to scale the icon geometry.
        using (context.PushTransform(scaleMatrix))
        {
            context.DrawGeometry(null, _pen, _geometry);
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    /// <summary>
    /// Retrieves a Geometry from the cache or parses it if not found.
    /// </summary>
    private static Geometry? GetOrParseGeometry(IconSymbol? symbol)
    {
        if (symbol is null)
            return null;

        // Atomically gets from cache or parses and adds, ensuring Geometry.Parse runs only once per symbol.
        return GeometryCache.GetOrAdd(
            symbol.Value,
            s => Geometry.Parse(IconToGeometry.CreateGeometryString(s))
        );
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ControlDeFinanzasIA.UI.Controls;

/// <summary>
/// A decorative line icon: a <see cref="Geometry"/> from <c>Styles/Icons.axaml</c> drawn on a 24 x 24 grid and scaled to
/// <see cref="Size"/>, stroked with the inherited foreground. A pure presentation control; there is no logic here.
/// </summary>
public partial class Icon : UserControl
{
    /// <summary>Identifies the <see cref="Data"/> styled property.</summary>
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<Icon, Geometry?>(nameof(Data));

    /// <summary>Identifies the <see cref="Size"/> styled property.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<Icon, double>(nameof(Size), 18d);

    /// <summary>Identifies the <see cref="Thickness"/> styled property.</summary>
    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<Icon, double>(nameof(Thickness), 1.75d);

    /// <summary>
    /// Initializes a new instance of the <see cref="Icon"/> class.
    /// </summary>
    public Icon()
    {
        InitializeComponent();
    }

    /// <summary>Gets or sets the icon geometry (24 x 24 grid).</summary>
    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>Gets or sets the rendered edge length in device-independent pixels.</summary>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Gets or sets the stroke thickness on the 24 x 24 grid.</summary>
    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }
}

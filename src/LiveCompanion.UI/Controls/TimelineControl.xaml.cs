using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LiveCompanion.Core.Models;

namespace LiveCompanion.UI.Controls;

/// <summary>
/// Contrôle visuel de la timeline : affiche les sections comme des blocs
/// proportionnels et un curseur de position.
/// </summary>
public partial class TimelineControl : UserControl
{
    // Couleurs alternées pour les sections (palette Catppuccin)
    private static readonly Brush[] SectionBrushes =
    [
        new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)), // Surface0
        new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A)), // Surface1
    ];

    private static readonly Brush ActiveSectionBrush =
        new SolidColorBrush(Color.FromRgb(0x2D, 0x3A, 0x5C)); // Bleu foncé

    private static readonly Brush CursorBrush =
        new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8)); // Rouge Catppuccin

    private static readonly Brush SectionTextBrush =
        new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)); // Text

    private static readonly Brush SectionBorderBrush =
        new SolidColorBrush(Color.FromRgb(0x58, 0x5B, 0x70)); // Overlay0

    private double _pixelsPerBar = 40;

    public TimelineControl()
    {
        InitializeComponent();
    }

    // ------------------------------------------------------------------ //
    // Dependency Properties
    // ------------------------------------------------------------------ //

    public static readonly DependencyProperty SectionsProperty =
        DependencyProperty.Register(
            nameof(Sections),
            typeof(IReadOnlyList<Section>),
            typeof(TimelineControl),
            new PropertyMetadata(null, OnTimelineDataChanged));

    public IReadOnlyList<Section>? Sections
    {
        get => (IReadOnlyList<Section>?)GetValue(SectionsProperty);
        set => SetValue(SectionsProperty, value);
    }

    public static readonly DependencyProperty CurrentSectionIndexProperty =
        DependencyProperty.Register(
            nameof(CurrentSectionIndex),
            typeof(int),
            typeof(TimelineControl),
            new PropertyMetadata(0, OnTimelineDataChanged));

    public int CurrentSectionIndex
    {
        get => (int)GetValue(CurrentSectionIndexProperty);
        set => SetValue(CurrentSectionIndexProperty, value);
    }

    public static readonly DependencyProperty CurrentBarProperty =
        DependencyProperty.Register(
            nameof(CurrentBar),
            typeof(int),
            typeof(TimelineControl),
            new PropertyMetadata(1, OnTimelineDataChanged));

    public int CurrentBar
    {
        get => (int)GetValue(CurrentBarProperty);
        set => SetValue(CurrentBarProperty, value);
    }

    public static readonly DependencyProperty CurrentBeatProperty =
        DependencyProperty.Register(
            nameof(CurrentBeat),
            typeof(int),
            typeof(TimelineControl),
            new PropertyMetadata(1, OnTimelineDataChanged));

    public int CurrentBeat
    {
        get => (int)GetValue(CurrentBeatProperty);
        set => SetValue(CurrentBeatProperty, value);
    }

    // ------------------------------------------------------------------ //
    // Rendering
    // ------------------------------------------------------------------ //

    private static void OnTimelineDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TimelineControl tc)
            tc.Redraw();
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _pixelsPerBar = e.NewValue;
        Redraw();
    }

    private void TimelineCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Redraw();
    }

    private void Redraw()
    {
        if (TimelineCanvas is null) return;

        TimelineCanvas.Children.Clear();

        var sections = Sections;
        if (sections is null || sections.Count == 0)
        {
            SectionInfoText.Text = string.Empty;
            TempoInfoText.Text = string.Empty;
            PositionInfoText.Text = string.Empty;
            return;
        }

        double canvasHeight = TimelineCanvas.ActualHeight > 0 ? TimelineCanvas.ActualHeight : 48;
        double x = 0;

        for (int i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            double width = section.BarCount * _pixelsPerBar;
            bool isActive = i == CurrentSectionIndex;

            // Rectangle de la section
            var rect = new Rectangle
            {
                Width = width,
                Height = canvasHeight,
                Fill = isActive ? ActiveSectionBrush : SectionBrushes[i % SectionBrushes.Length],
                Stroke = SectionBorderBrush,
                StrokeThickness = 0.5,
                RadiusX = 3,
                RadiusY = 3,
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, 0);
            TimelineCanvas.Children.Add(rect);

            // Nom de la section
            if (width > 30)
            {
                var label = new TextBlock
                {
                    Text = section.Name,
                    Foreground = SectionTextBrush,
                    FontSize = 10,
                    MaxWidth = width - 8,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                Canvas.SetLeft(label, x + 4);
                Canvas.SetTop(label, 4);
                TimelineCanvas.Children.Add(label);
            }

            // Tempo sous le nom
            if (width > 50)
            {
                var tempoLabel = new TextBlock
                {
                    Text = $"{section.Tempo:0} bpm",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86)),
                    FontSize = 9,
                };
                Canvas.SetLeft(tempoLabel, x + 4);
                Canvas.SetTop(tempoLabel, 18);
                TimelineCanvas.Children.Add(tempoLabel);
            }

            x += width;
        }

        // Curseur de position
        double cursorX = 0;
        for (int i = 0; i < CurrentSectionIndex && i < sections.Count; i++)
            cursorX += sections[i].BarCount * _pixelsPerBar;

        if (CurrentSectionIndex < sections.Count)
        {
            var currentSection = sections[CurrentSectionIndex];
            double barProgress = (CurrentBar - 1) +
                (CurrentBeat - 1.0) / currentSection.TimeSignature.Numerator;
            cursorX += barProgress * _pixelsPerBar;
        }

        var cursor = new Line
        {
            X1 = cursorX,
            Y1 = 0,
            X2 = cursorX,
            Y2 = canvasHeight,
            Stroke = CursorBrush,
            StrokeThickness = 2,
        };
        TimelineCanvas.Children.Add(cursor);

        // Mettre à jour la largeur du canvas pour le scroll
        TimelineCanvas.Width = x;

        // Mettre à jour les infos en en-tête
        if (CurrentSectionIndex < sections.Count)
        {
            var sec = sections[CurrentSectionIndex];
            SectionInfoText.Text = sec.Name;
            TempoInfoText.Text = $"{sec.Tempo:0} BPM — {sec.TimeSignature}";
            PositionInfoText.Text = $"{CurrentBar} : {CurrentBeat}";
        }
    }
}

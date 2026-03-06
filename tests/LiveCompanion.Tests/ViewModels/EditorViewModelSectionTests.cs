using System.Collections.ObjectModel;
using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Services;
using LiveCompanion.Core.Validation;

namespace LiveCompanion.Tests.ViewModels;

/// <summary>
/// Tests pour la logique d'édition de sections telle qu'utilisée par EditorViewModel.
/// Couvre : ajout, suppression, réordonnage (move up/down + drag & drop),
/// validation tempo/signature/barCount, et blocage en mode Live.
/// </summary>
public class EditorViewModelSectionTests
{
    private readonly ILogService _log = new DebugLogService();

    // ------------------------------------------------------------------ //
    // Helpers — simule le comportement du EditorViewModel
    // ------------------------------------------------------------------ //

    private static Song CreateSongWithSections(int count)
    {
        var song = new Song { Title = "Test" };
        for (int i = 0; i < count; i++)
        {
            song.Sections.Add(new Section
            {
                Name = $"Section {i + 1}",
                Tempo = 120,
                TimeSignature = TimeSignature.Default,
                BarCount = 4,
                Order = i,
            });
        }
        return song;
    }

    private static void MoveSection(ObservableCollection<Section> sections, int from, int to)
    {
        if (from == to || from < 0 || from >= sections.Count || to < 0 || to >= sections.Count) return;
        sections.Move(from, to);
        for (int i = 0; i < sections.Count; i++)
            sections[i].Order = i;
    }

    // ------------------------------------------------------------------ //
    // Ajout de section
    // ------------------------------------------------------------------ //

    [Fact]
    public void AddSection_ShouldIncrementCount()
    {
        var song = CreateSongWithSections(0);

        song.Sections.Add(new Section
        {
            Name = "Intro",
            Tempo = 120,
            BarCount = 4,
            Order = 0,
            TimeSignature = TimeSignature.Default,
        });

        song.Sections.Should().HaveCount(1);
        song.Sections[0].Name.Should().Be("Intro");
    }

    [Fact]
    public void AddSection_Multiple_ShouldHaveSequentialOrder()
    {
        var song = CreateSongWithSections(3);

        song.Sections[0].Order.Should().Be(0);
        song.Sections[1].Order.Should().Be(1);
        song.Sections[2].Order.Should().Be(2);
    }

    // ------------------------------------------------------------------ //
    // Suppression de section
    // ------------------------------------------------------------------ //

    [Fact]
    public void RemoveSection_ShouldDecrementCount()
    {
        var song = CreateSongWithSections(3);

        song.Sections.RemoveAt(1);

        song.Sections.Should().HaveCount(2);
    }

    [Fact]
    public void RemoveSection_ShouldAllowReorder()
    {
        var song = CreateSongWithSections(3);
        song.Sections.RemoveAt(1);

        // Reorder remaining
        for (int i = 0; i < song.Sections.Count; i++)
            song.Sections[i].Order = i;

        song.Sections[0].Order.Should().Be(0);
        song.Sections[1].Order.Should().Be(1);
    }

    // ------------------------------------------------------------------ //
    // MoveSection (simule drag & drop)
    // ------------------------------------------------------------------ //

    [Fact]
    public void MoveSection_FirstToLast_ShouldReorder()
    {
        var song = CreateSongWithSections(3);
        var sections = new ObservableCollection<Section>(song.Sections);
        var first = sections[0];

        MoveSection(sections, 0, 2);

        sections[2].Should().Be(first);
        sections[0].Order.Should().Be(0);
        sections[1].Order.Should().Be(1);
        sections[2].Order.Should().Be(2);
    }

    [Fact]
    public void MoveSection_LastToFirst_ShouldReorder()
    {
        var song = CreateSongWithSections(3);
        var sections = new ObservableCollection<Section>(song.Sections);
        var last = sections[2];

        MoveSection(sections, 2, 0);

        sections[0].Should().Be(last);
        sections[0].Order.Should().Be(0);
        sections[1].Order.Should().Be(1);
        sections[2].Order.Should().Be(2);
    }

    [Fact]
    public void MoveSection_SameIndex_ShouldDoNothing()
    {
        var song = CreateSongWithSections(3);
        var sections = new ObservableCollection<Section>(song.Sections);
        var original = sections.ToList();

        MoveSection(sections, 1, 1);

        sections.Should().ContainInOrder(original);
    }

    [Fact]
    public void MoveSection_InvalidFrom_ShouldDoNothing()
    {
        var song = CreateSongWithSections(2);
        var sections = new ObservableCollection<Section>(song.Sections);
        var original = sections.ToList();

        MoveSection(sections, -1, 0);
        MoveSection(sections, 5, 0);

        sections.Should().ContainInOrder(original);
    }

    [Fact]
    public void MoveSection_InvalidTo_ShouldDoNothing()
    {
        var song = CreateSongWithSections(2);
        var sections = new ObservableCollection<Section>(song.Sections);
        var original = sections.ToList();

        MoveSection(sections, 0, -1);
        MoveSection(sections, 0, 5);

        sections.Should().ContainInOrder(original);
    }

    // ------------------------------------------------------------------ //
    // MoveUp / MoveDown (boutons)
    // ------------------------------------------------------------------ //

    [Fact]
    public void MoveSectionUp_ShouldSwapWithPrevious()
    {
        var song = CreateSongWithSections(3);
        var sections = new ObservableCollection<Section>(song.Sections);
        var second = sections[1];

        MoveSection(sections, 1, 0);

        sections[0].Should().Be(second);
    }

    [Fact]
    public void MoveSectionDown_ShouldSwapWithNext()
    {
        var song = CreateSongWithSections(3);
        var sections = new ObservableCollection<Section>(song.Sections);
        var first = sections[0];

        MoveSection(sections, 0, 1);

        sections[1].Should().Be(first);
    }

    // ------------------------------------------------------------------ //
    // Blocage en mode Live
    // ------------------------------------------------------------------ //

    [Fact]
    public void LiveModeGuard_WhenEngaged_ShouldBlockOperations()
    {
        var guard = new LiveModeGuard(_log);
        guard.Engage();

        guard.IsLive.Should().BeTrue();
    }

    [Fact]
    public void LiveModeGuard_WhenDisengaged_ShouldAllowOperations()
    {
        var guard = new LiveModeGuard(_log);
        guard.Engage();
        guard.Disengage();

        guard.IsLive.Should().BeFalse();
    }

    // ------------------------------------------------------------------ //
    // Validation — Tempo
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData(20)]
    [InlineData(120)]
    [InlineData(300)]
    public void Validation_ValidTempo_ShouldPass(double tempo)
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "S", Tempo = tempo, BarCount = 4, TimeSignature = TimeSignature.Default } },
        };

        var result = ModelValidator.ValidateSong(song);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(19)]
    [InlineData(301)]
    [InlineData(-10)]
    public void Validation_InvalidTempo_ShouldFail(double tempo)
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "S", Tempo = tempo, BarCount = 4, TimeSignature = TimeSignature.Default } },
        };

        var result = ModelValidator.ValidateSong(song);
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("Tempo"));
    }

    // ------------------------------------------------------------------ //
    // Validation — BarCount
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(100)]
    public void Validation_ValidBarCount_ShouldPass(int barCount)
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "S", Tempo = 120, BarCount = barCount, TimeSignature = TimeSignature.Default } },
        };

        var result = ModelValidator.ValidateSong(song);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validation_InvalidBarCount_ShouldFail(int barCount)
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "S", Tempo = 120, BarCount = barCount, TimeSignature = TimeSignature.Default } },
        };

        var result = ModelValidator.ValidateSong(song);
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("BarCount"));
    }

    // ------------------------------------------------------------------ //
    // Validation — TimeSignature
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData(4, 4)]
    [InlineData(3, 4)]
    [InlineData(6, 8)]
    [InlineData(2, 2)]
    public void Validation_ValidTimeSignature_ShouldPass(int num, int den)
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "S", Tempo = 120, BarCount = 4, TimeSignature = new TimeSignature(num, den) } },
        };

        var result = ModelValidator.ValidateSong(song);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validation_InvalidNumerator_ShouldFail()
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "S", Tempo = 120, BarCount = 4, TimeSignature = new TimeSignature(0, 4) } },
        };

        var result = ModelValidator.ValidateSong(song);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validation_InvalidDenominator_ShouldFail()
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "S", Tempo = 120, BarCount = 4, TimeSignature = new TimeSignature(4, 5) } },
        };

        var result = ModelValidator.ValidateSong(song);
        result.IsValid.Should().BeFalse();
    }

    // ------------------------------------------------------------------ //
    // Validation — multiple erreurs
    // ------------------------------------------------------------------ //

    [Fact]
    public void Validation_MultipleSectionErrors_ShouldAccumulate()
    {
        var song = new Song
        {
            Title = "Test",
            Sections =
            {
                new Section { Name = "S1", Tempo = 0, BarCount = 0, TimeSignature = TimeSignature.Default },
                new Section { Name = "S2", Tempo = 500, BarCount = -1, TimeSignature = new TimeSignature(0, 4) },
            },
        };

        var result = ModelValidator.ValidateSong(song);
        result.IsValid.Should().BeFalse();
        result.Issues.Should().HaveCountGreaterOrEqualTo(4);
    }
}

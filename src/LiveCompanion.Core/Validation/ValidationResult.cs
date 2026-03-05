namespace LiveCompanion.Core.Validation;

/// <summary>Gravité d'un problème de validation.</summary>
public enum ValidationSeverity { Error, Warning }

/// <summary>Problème individuel détecté lors de la validation.</summary>
public sealed record ValidationIssue(
    ValidationSeverity Severity,
    string Field,
    string Message);

/// <summary>
/// Résultat agrégé d'une validation de modèle.
/// Les <see cref="ValidationSeverity.Error"/> bloquent l'opération,
/// les <see cref="ValidationSeverity.Warning"/> sont informatifs.
/// </summary>
public sealed class ValidationResult
{
    private readonly List<ValidationIssue> _issues = [];

    public IReadOnlyList<ValidationIssue> Issues => _issues.AsReadOnly();
    public bool IsValid => !_issues.Any(i => i.Severity == ValidationSeverity.Error);
    public bool HasWarnings => _issues.Any(i => i.Severity == ValidationSeverity.Warning);

    public void AddError(string field, string message)
        => _issues.Add(new(ValidationSeverity.Error, field, message));

    public void AddWarning(string field, string message)
        => _issues.Add(new(ValidationSeverity.Warning, field, message));

    public void Merge(ValidationResult other)
    {
        foreach (var issue in other.Issues)
            _issues.Add(issue);
    }
}

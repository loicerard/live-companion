namespace LiveCompanion.Core.Validation;

/// <summary>
/// Résultat d'un chargement avec validation intégrée.
/// <see cref="Value"/> est <c>null</c> si le chargement a échoué (erreurs de validation).
/// </summary>
public sealed record LoadResult<T>(T? Value, ValidationResult Validation);

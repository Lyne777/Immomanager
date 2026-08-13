namespace Immomanager.Web.Models;

/// <summary>Versicherungsart - wird sowohl für die Vertragsfakten (<see cref="InsurancePolicy"/>)
/// als auch für die Prüf-Checkliste (<see cref="InsuranceCheckItem"/>) verwendet.</summary>
public enum InsuranceCategory
{
    Gebaeudeversicherung,
    HausUndGrundbesitzerhaftpflicht,
}

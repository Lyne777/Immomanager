using System.ComponentModel.DataAnnotations;

namespace Immomanager.Web.Models;

/// <summary>Eine Position einer <see cref="ShoppingList"/> - abhakbar beim Einkaufen.</summary>
public class ShoppingListItem
{
    public int Id { get; set; }

    public int ShoppingListId { get; set; }

    public ShoppingList? ShoppingList { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public decimal? Quantity { get; set; }

    [StringLength(30)]
    public string? Unit { get; set; }

    [Required, StringLength(50)]
    public string Category { get; set; } = "Sonstiges";

    public bool IsChecked { get; set; }
}

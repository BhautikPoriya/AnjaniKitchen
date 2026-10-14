namespace AnjaniKitchen.Models;

public sealed record CategoryDeleteResult(bool Deleted, string? ImagePublicId, bool HasDishes = false);

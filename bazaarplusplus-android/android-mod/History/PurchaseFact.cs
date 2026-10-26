namespace BazaarPlusPlus.Android;

internal sealed record PurchaseFact(string InstanceId, string TemplateId, string Name, string Type, string Tier, uint Price);

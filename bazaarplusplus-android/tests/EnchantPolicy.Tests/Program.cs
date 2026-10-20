using BazaarGameShared.Domain.Core.Types;
using BazaarPlusPlus.Game.ItemEnchantPreview;

static void Require(bool condition, string reason)
{
    if (!condition) throw new InvalidOperationException(reason);
}

foreach (var section in new EInventorySection?[] { EInventorySection.Hand, EInventorySection.Stash, null })
{
    Require(!ItemEnchantPreviewSectionPolicy.IsEligible(ECardType.Item, section, true), "Combat must suppress ordinary item previews.");
    Require(!ItemEnchantPreviewSectionPolicy.IsEligible(ECardType.Skill, section, false), "Skills must not produce item enchant previews.");
}
Require(ItemEnchantPreviewSectionPolicy.IsEligible(ECardType.Item, EInventorySection.Hand, false), "Board items must be eligible.");
Require(ItemEnchantPreviewSectionPolicy.IsEligible(ECardType.Item, EInventorySection.Stash, false), "Stash items must be eligible.");
Require(!ItemEnchantPreviewSectionPolicy.IsEligible(ECardType.Item, null, false), "An unknown section needs the separate opponent/ownerless rule.");

var choices = new[] { EEnchantmentType.Fiery, EEnchantmentType.Icy, EEnchantmentType.Fiery, EEnchantmentType.Shiny };
var filtered = ItemEnchantPreviewCandidateSelector.SelectCandidates(EEnchantmentType.Fiery, choices);
Require(filtered.SequenceEqual(new[] { EEnchantmentType.Icy, EEnchantmentType.Shiny }), "Exclude current enchantment and duplicates while preserving order.");
var pedestal = ItemEnchantPreviewCandidateSelector.SelectCandidates(null, choices, new[] { "iCy" });
Require(pedestal.SequenceEqual(new[] { EEnchantmentType.Icy }), "Pedestal restrictions must be case insensitive.");
Require(ItemEnchantPreviewCandidateSelector.SelectCandidates(null, choices).Count == 3, "Unenchanted items must offer all distinct candidates.");
Console.WriteLine("Enchant eligibility and candidate scenarios passed.");

/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * 
MIT License

Copyright (c) 2025 Lone DMA

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 *
*/

using LoneEftDmaRadar.Tarkov.GameWorld.Loot;

namespace LoneEftDmaRadar.UI.Loot
{
    /// <summary>
    /// Enumerable Loot Filter Class.
    /// </summary>
    internal static class LootFilter
    {
        public static string SearchString;
        public static bool ShowMeds;
        public static bool ShowFood;
        public static bool ShowBackpacks;
        public static bool ShowQuestItems; // respect config via UI binding when set

        /// <summary>
        /// Creates a loot filter based on current Loot Filter settings.
        /// </summary>
        /// <returns>Loot Filter Predicate.</returns>
        public static Predicate<LootItem> Create()
        {
            var search = SearchString?.Trim();
            bool usePrices = string.IsNullOrEmpty(search);
            bool showMeds = ShowMeds;
            bool showFood = ShowFood;
            bool showBackpacks = ShowBackpacks;
            // Always read quest item setting from config (not from static variable)
            bool showQuestItems = App.Config.Loot.ShowQuestItems;
            bool showWishlisted = App.Config.Loot.ShowWishlistedRadar;
            
            // Get the QuestManager to check if quest items are for active quests
            var questManager = Memory.Game?.QuestManager;
            
            if (usePrices)
            {
                Predicate<LootItem> p = x => // Default Predicate
                {
                    // Quest items: only show if enabled AND for an active quest
                    if (x.IsQuestItem && showQuestItems)
                    {
                        // Check if this quest item is for one of the player's active quests
                        // If no quest manager, show all quest items (fallback)
                        if (questManager == null)
                            return true;
                        
                        // Quest items are stored with their BSG ID in the QuestManager.ItemConditions
                        // The ID in LootItem comes from ItemTemplate._id (BSG ID)
                        return questManager.IsQuestItem(x.ID);
                    }
                    // Wishlist items always pass the filter when enabled
                    if (showWishlisted && x.IsWishlisted)
                        return true;
                    return (x.IsRegularLoot || x.IsValuableLoot || x.IsImportant) ||
                                (showBackpacks && x.IsBackpack) ||
                                (showMeds && x.IsMeds) ||
                                (showFood && x.IsFood);
                };
                return item =>
                {
                    if (item is LootAirdrop)
                    {
                        return true;
                    }
                    if (item is LootCorpse)
                    {
                        return true;
                    }
                    if (item is StaticLootContainer container)
                    {
                        // Show if SelectAll is enabled OR if this specific container is selected
                        return App.Config.Containers.SelectAll || App.Config.Containers.Selected.ContainsKey(container.ID);
                    }
                    if (p(item))
                    {
                        return true;
                    }
                    return false;
                };
            }
            else // Loot Search
            {
                var names = search!.Split(',').Select(a => a.Trim()).ToList(); // Pooled wasnt working well here
                Predicate<LootItem> p = x => // Search Predicate
                {
                    // Wishlist items also show during search when enabled
                    if (showWishlisted && x.IsWishlisted)
                        return true;
                    return names.Any(a => x.Name.Contains(a, StringComparison.OrdinalIgnoreCase));
                };
                return item =>
                {
                    if (item is LootAirdrop)
                    {
                        return true;
                    }
                    if (item is StaticLootContainer container)
                    {
                        // Show if SelectAll is enabled OR if this specific container is selected
                        return App.Config.Containers.SelectAll || App.Config.Containers.Selected.ContainsKey(container.ID);
                    }
                    if (item.ContainsSearchPredicate(p))
                    {
                        return true;
                    }
                    return false;
                };
            }
        }
    }
}

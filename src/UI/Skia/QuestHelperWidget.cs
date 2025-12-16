/*
 * Lone EFT DMA Radar
 * Quest Helper Widget
 * 
 * Displays tracked, active quests for the current map with their objectives.
 */

using LoneEftDmaRadar.Tarkov;
using LoneEftDmaRadar.Tarkov.GameWorld.Quests;
using SkiaSharp.Views.WPF;

namespace LoneEftDmaRadar.UI.Skia
{
    /// <summary>
    /// Widget that displays tracked, active quests with objectives for the current map.
    /// </summary>
    public sealed class QuestHelperWidget : AbstractSKWidget
    {
        #region Cached Data

        private class QuestDisplayEntry
        {
            public string QuestId { get; set; }
            public string TraderName { get; set; }
            public string QuestName { get; set; }
            public string ObjectivesHeader { get; set; }
            public List<ObjectiveDisplayEntry> Objectives { get; set; } = new();
        }

        private class ObjectiveDisplayEntry
        {
            public string TypeIcon { get; set; }
            public string Description { get; set; }
            public string Progress { get; set; }
            public bool IsCompleted { get; set; }
        }

        private List<QuestDisplayEntry> _cachedQuests = new();
        private float _scrollOffset = 0f;
        private float _maxScrollOffset = 0f;
        private DateTime _lastRefresh = DateTime.MinValue;
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

        #endregion

        #region Paints

        private static readonly SKPaint TraderNamePaint = new()
        {
            Color = new SKColor(255, 215, 0), // Gold
            IsStroke = false,
            IsAntialias = true
        };

        private static readonly SKPaint QuestNamePaint = new()
        {
            Color = SKColors.White,
            IsStroke = false,
            IsAntialias = true
        };

        private static readonly SKPaint ObjectivesHeaderPaint = new()
        {
            Color = new SKColor(128, 192, 255), // Light blue
            IsStroke = false,
            IsAntialias = true
        };

        private static readonly SKPaint ObjectiveTextPaint = new()
        {
            Color = SKColors.White,
            IsStroke = false,
            IsAntialias = true
        };

        private static readonly SKPaint ObjectiveCompletedPaint = new()
        {
            Color = new SKColor(50, 205, 50), // Lime green
            IsStroke = false,
            IsAntialias = true
        };

        private static readonly SKPaint TypeIconPaint = new()
        {
            Color = new SKColor(255, 165, 0), // Orange
            IsStroke = false,
            IsAntialias = true
        };

        private static readonly SKPaint ProgressPaint = new()
        {
            Color = new SKColor(128, 192, 255), // Light blue
            IsStroke = false,
            IsAntialias = true
        };

        private static readonly SKPaint SeparatorPaint = new()
        {
            Color = new SKColor(60, 60, 60),
            StrokeWidth = 1f,
            Style = SKPaintStyle.Stroke
        };

        private static readonly SKPaint QuestBackgroundPaint = new()
        {
            Color = new SKColor(30, 30, 30, 200),
            Style = SKPaintStyle.Fill
        };

        #endregion

        public QuestHelperWidget(SKGLElement parent, SKRect location, bool minimized, float scale)
            : base(parent, "Quest Helper", new SKPoint(location.Left, location.Top),
                new SKSize(Math.Max(300, location.Width), Math.Max(200, location.Height)), scale, true)
        {
            Minimized = minimized;
            SetScaleFactor(scale);
        }

        /// <summary>
        /// Draw the quest helper widget.
        /// </summary>
        public void Draw(SKCanvas canvas)
        {
            if (Minimized)
            {
                base.Draw(canvas);
                return;
            }

            // Refresh cached quest data periodically
            if (DateTime.UtcNow - _lastRefresh > RefreshInterval)
            {
                RefreshQuestData();
                _lastRefresh = DateTime.UtcNow;
            }

            base.Draw(canvas);

            if (_cachedQuests.Count == 0)
            {
                DrawNoQuestsMessage(canvas);
                return;
            }

            // Clipping for content area
            canvas.Save();
            canvas.ClipRect(ClientRectangle);

            DrawQuestList(canvas);

            // Draw scrollbar if needed
            if (_maxScrollOffset > 0)
            {
                DrawScrollbar(canvas);
            }

            canvas.Restore();
        }

        private void RefreshQuestData()
        {
            _cachedQuests.Clear();

            // Get config values
            var config = App.Config.QuestHelper;
            if (!config.Enabled)
                return;

            // Get current map
            var mapId = Memory.MapID;
            if (string.IsNullOrEmpty(mapId))
                return;

            // Get quest manager
            var questManager = Memory.Game?.QuestManager;
            if (questManager == null)
                return;

            // Get tracked quest IDs
            var trackedIds = config.TrackedQuests;

            // Process active quests that are tracked
            foreach (var quest in questManager.ActiveQuests)
            {
                // Skip if not tracked (or if no quests are tracked, show all active)
                if (trackedIds.Count > 0 && !trackedIds.Contains(quest.Id))
                    continue;

                // Check if quest has objectives on current map
                if (!HasObjectivesOnMap(quest.Id, mapId))
                    continue;

                var entry = CreateQuestDisplayEntry(quest);
                if (entry != null)
                    _cachedQuests.Add(entry);
            }
        }

        private static bool HasObjectivesOnMap(string questId, string mapId)
        {
            if (string.IsNullOrEmpty(mapId))
                return true;

            if (!TarkovDataManager.TaskData.TryGetValue(questId, out var task))
                return true; // Show if no data

            // Check if task has a specific map assigned
            if (task.Map?.NameId != null)
            {
                return IsMapMatch(task.Map.NameId, mapId);
            }

            // Check objectives for map-specific zones
            bool hasMapSpecificObjective = false;
            bool hasAnyMapObjective = false;

            if (task.Objectives != null)
            {
                foreach (var obj in task.Objectives)
                {
                    // Check objective-level maps
                    if (obj.Maps != null && obj.Maps.Count > 0)
                    {
                        foreach (var objMap in obj.Maps)
                        {
                            if (objMap?.NameId != null)
                            {
                                hasMapSpecificObjective = true;
                                if (IsMapMatch(objMap.NameId, mapId))
                                    hasAnyMapObjective = true;
                            }
                        }
                    }

                    // Check objective zones
                    if (obj.Zones != null)
                    {
                        foreach (var zone in obj.Zones)
                        {
                            if (zone?.Map?.NameId != null)
                            {
                                hasMapSpecificObjective = true;
                                if (IsMapMatch(zone.Map.NameId, mapId))
                                    hasAnyMapObjective = true;
                            }
                        }
                    }
                }
            }

            // If quest has map-specific objectives, only show if at least one matches current map
            if (hasMapSpecificObjective)
                return hasAnyMapObjective;

            // Quest has no map restrictions - show it
            return true;
        }

        private static bool IsMapMatch(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return false;
            if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) return true;
            // Factory aliases
            if (a.Contains("factory", StringComparison.OrdinalIgnoreCase) && 
                b.Contains("factory", StringComparison.OrdinalIgnoreCase)) return true;
            // Ground Zero aliases
            if (a.Contains("sandbox", StringComparison.OrdinalIgnoreCase) && 
                b.Contains("sandbox", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private QuestDisplayEntry CreateQuestDisplayEntry(QuestData quest)
        {
            if (!TarkovDataManager.TaskData.TryGetValue(quest.Id, out var task))
                return null;

            var entry = new QuestDisplayEntry
            {
                QuestId = quest.Id,
                TraderName = task.Trader?.Name ?? "Unknown",
                QuestName = quest.Name ?? task.Name ?? quest.Id
            };

            // Build objectives header (keys/items summary)
            var headerParts = new List<string>();

            // Collect needed keys
            if (task.NeededKeys != null)
            {
                foreach (var keyGroup in task.NeededKeys)
                {
                    if (keyGroup.Keys != null)
                    {
                        foreach (var key in keyGroup.Keys)
                        {
                            if (!string.IsNullOrEmpty(key.ShortName))
                                headerParts.Add($"KEY: {key.ShortName}");
                        }
                    }
                }
            }

            // Collect required items
            if (task.Objectives != null)
            {
                foreach (var obj in task.Objectives)
                {
                    if (obj.Item != null && !string.IsNullOrEmpty(obj.Item.ShortName))
                    {
                        var itemText = obj.Item.ShortName;
                        if (obj.Count > 1) itemText += $" x{obj.Count}";
                        headerParts.Add($"ITEM: {itemText}");
                    }
                    if (obj.QuestItem != null && !string.IsNullOrEmpty(obj.QuestItem.ShortName))
                    {
                        headerParts.Add($"Q: {obj.QuestItem.ShortName}");
                    }
                }
            }

            entry.ObjectivesHeader = headerParts.Count > 0 
                ? "Objectives: | " + string.Join(" | ", headerParts.Take(3)) 
                : "Objectives:";

            // Build objectives list
            if (task.Objectives != null)
            {
                foreach (var obj in task.Objectives)
                {
                    var isCompleted = quest.CompletedConditions.Contains(obj.Id);
                    
                    var objEntry = new ObjectiveDisplayEntry
                    {
                        TypeIcon = GetTypeIcon(obj.Type),
                        Description = obj.Description ?? GetDefaultDescription(obj),
                        Progress = GetProgressText(obj, isCompleted),
                        IsCompleted = isCompleted
                    };
                    
                    entry.Objectives.Add(objEntry);
                }
            }

            return entry;
        }

        private static string GetTypeIcon(QuestObjectiveType type) => type switch
        {
            QuestObjectiveType.FindItem or QuestObjectiveType.FindQuestItem => "FIND",
            QuestObjectiveType.GiveItem or QuestObjectiveType.GiveQuestItem => "GIVE",
            QuestObjectiveType.Visit => "GO",
            QuestObjectiveType.Mark or QuestObjectiveType.PlantItem or QuestObjectiveType.PlantQuestItem => "MARK",
            QuestObjectiveType.Shoot => "KILL",
            QuestObjectiveType.Extract => "EXIT",
            QuestObjectiveType.BuildWeapon => "BUILD",
            QuestObjectiveType.Skill => "SKILL",
            QuestObjectiveType.TraderLevel => "LVL",
            _ => "TASK"
        };

        private static string GetDefaultDescription(TarkovDataManager.TaskElement.ObjectiveElement obj)
        {
            return obj.Type switch
            {
                QuestObjectiveType.FindItem => $"Find {obj.Item?.ShortName ?? "item"}" + (obj.Count > 1 ? $" x{obj.Count}" : ""),
                QuestObjectiveType.GiveItem => $"Hand over {obj.Item?.ShortName ?? "item"}" + (obj.Count > 1 ? $" x{obj.Count}" : ""),
                QuestObjectiveType.Shoot => $"Eliminate targets" + (obj.Count > 0 ? $" ({obj.Count})" : ""),
                QuestObjectiveType.Visit => "Visit location",
                QuestObjectiveType.Mark => "Mark location",
                QuestObjectiveType.Extract => "Survive and extract",
                _ => "Complete objective"
            };
        }

        private static string GetProgressText(TarkovDataManager.TaskElement.ObjectiveElement obj, bool isCompleted)
        {
            if (isCompleted)
                return "DONE";
            
            // For objectives with count, show 0/count
            if (obj.Count > 0)
                return $"0/{obj.Count}";
            
            return "";
        }

        private void DrawNoQuestsMessage(SKCanvas canvas)
        {
            var font = SKFonts.InfoWidgetFont;
            float pad = 5f * ScaleFactor;

            string message = Memory.InRaid 
                ? "No tracked quests for this map" 
                : "Enter raid to see quests";

            var textPt = new SKPoint(
                ClientRectangle.Left + pad,
                ClientRectangle.Top + font.Spacing + pad);

            canvas.DrawText(message, textPt, SKTextAlign.Left, font, ObjectiveTextPaint);
        }

        private void DrawQuestList(SKCanvas canvas)
        {
            var font = SKFonts.InfoWidgetFont;
            float pad = 5f * ScaleFactor;
            float questPadding = 8f * ScaleFactor;
            float objIndent = 20f * ScaleFactor;

            // Calculate total content height
            float contentHeight = CalculateContentHeight(font, questPadding);

            // Calculate max scroll offset
            float availableHeight = ClientRectangle.Height - (pad * 2);
            _maxScrollOffset = Math.Max(0, contentHeight - availableHeight);

            // Clamp scroll offset
            _scrollOffset = Math.Clamp(_scrollOffset, 0f, _maxScrollOffset);

            // Starting position with scroll offset
            float y = ClientRectangle.Top + pad - _scrollOffset;

            foreach (var quest in _cachedQuests)
            {
                float questStartY = y;

                // Skip if completely above visible area
                float estimatedQuestHeight = EstimateQuestHeight(quest, font, questPadding);
                if (y + estimatedQuestHeight < ClientRectangle.Top)
                {
                    y += estimatedQuestHeight + questPadding;
                    continue;
                }

                // Stop if below visible area
                if (y > ClientRectangle.Bottom)
                    break;

                // Draw quest background
                float questHeight = DrawQuestEntry(canvas, quest, font, pad, objIndent, ref y);
                
                // Draw separator
                y += questPadding / 2;
                canvas.DrawLine(
                    ClientRectangle.Left + pad, y,
                    ClientRectangle.Right - pad - (ScaleFactor * 10), y,
                    SeparatorPaint);
                y += questPadding / 2;
            }
        }

        private float DrawQuestEntry(SKCanvas canvas, QuestDisplayEntry quest, SKFont font, float pad, float objIndent, ref float y)
        {
            float startY = y;

            // Draw trader name and quest name
            float x = ClientRectangle.Left + pad;
            
            // Trader name (gold)
            canvas.DrawText(quest.TraderName, new SKPoint(x, y + font.Spacing), SKTextAlign.Left, font, TraderNamePaint);
            float traderWidth = font.MeasureText(quest.TraderName);
            
            // Separator
            canvas.DrawText(" - ", new SKPoint(x + traderWidth, y + font.Spacing), SKTextAlign.Left, font, ObjectiveTextPaint);
            float sepWidth = font.MeasureText(" - ");
            
            // Quest name (white)
            canvas.DrawText(quest.QuestName, new SKPoint(x + traderWidth + sepWidth, y + font.Spacing), SKTextAlign.Left, font, QuestNamePaint);
            y += font.Spacing + 2f * ScaleFactor;

            // Objectives header (light blue)
            canvas.DrawText(quest.ObjectivesHeader, new SKPoint(x, y + font.Spacing), SKTextAlign.Left, font, ObjectivesHeaderPaint);
            y += font.Spacing + 4f * ScaleFactor;

            // Draw objectives
            foreach (var obj in quest.Objectives)
            {
                if (y > ClientRectangle.Bottom)
                    break;

                // Type icon (orange)
                canvas.DrawText(obj.TypeIcon, new SKPoint(x, y + font.Spacing), SKTextAlign.Left, font, TypeIconPaint);
                float iconWidth = font.MeasureText(obj.TypeIcon) + 5f * ScaleFactor;

                // Description
                var descPaint = obj.IsCompleted ? ObjectiveCompletedPaint : ObjectiveTextPaint;
                canvas.DrawText(obj.Description, new SKPoint(x + iconWidth, y + font.Spacing), SKTextAlign.Left, font, descPaint);

                // Progress (right-aligned)
                if (!string.IsNullOrEmpty(obj.Progress))
                {
                    float progressX = ClientRectangle.Right - pad - (ScaleFactor * 10) - font.MeasureText(obj.Progress);
                    var progressPaint = obj.IsCompleted ? ObjectiveCompletedPaint : ProgressPaint;
                    canvas.DrawText(obj.Progress, new SKPoint(progressX, y + font.Spacing), SKTextAlign.Left, font, progressPaint);
                }

                y += font.Spacing + 2f * ScaleFactor;
            }

            return y - startY;
        }

        private float CalculateContentHeight(SKFont font, float questPadding)
        {
            float height = 0;
            foreach (var quest in _cachedQuests)
            {
                height += EstimateQuestHeight(quest, font, questPadding) + questPadding;
            }
            return height;
        }

        private float EstimateQuestHeight(QuestDisplayEntry quest, SKFont font, float questPadding)
        {
            // Title line + objectives header + each objective
            return font.Spacing * (2 + quest.Objectives.Count) + (4f * ScaleFactor * quest.Objectives.Count);
        }

        private void DrawScrollbar(SKCanvas canvas)
        {
            float scrollbarWidth = 6f * ScaleFactor;
            float scrollbarX = ClientRectangle.Right - scrollbarWidth - 2f;
            float scrollbarY = ClientRectangle.Top + 2f;
            float scrollbarHeight = ClientRectangle.Height - 4f;

            // Background track
            var trackPaint = new SKPaint
            {
                Color = new SKColor(60, 60, 60, 180),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRect(scrollbarX, scrollbarY, scrollbarWidth, scrollbarHeight, trackPaint);

            // Calculate thumb size and position
            float contentHeight = _maxScrollOffset + ClientRectangle.Height;
            float thumbHeight = Math.Max(20f, (ClientRectangle.Height / contentHeight) * scrollbarHeight);
            float thumbY = scrollbarY + (_scrollOffset / Math.Max(1, _maxScrollOffset)) * (scrollbarHeight - thumbHeight);

            // Draw thumb
            var thumbPaint = new SKPaint
            {
                Color = new SKColor(120, 120, 120, 220),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRoundRect(scrollbarX, thumbY, scrollbarWidth, thumbHeight, 3f, 3f, thumbPaint);
        }

        /// <summary>
        /// Handle mouse wheel scrolling when widget is focused.
        /// </summary>
        protected override void OnMouseWheel(int delta)
        {
            float scrollAmount = -(delta / 120f) * SKFonts.InfoWidgetFont.Spacing * 3f;
            _scrollOffset = Math.Clamp(_scrollOffset + scrollAmount, 0f, _maxScrollOffset);
        }

        public override void SetScaleFactor(float newScale)
        {
            base.SetScaleFactor(newScale);
        }
    }
}

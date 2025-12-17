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

using Collections.Pooled;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.Unity;
using LoneEftDmaRadar.UI.Radar.Maps;
using LoneEftDmaRadar.UI.Skia;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Quests
{
    /// <summary>
    /// Wraps a Mouseoverable Quest Location marker onto the Map GUI.
    /// </summary>
    public sealed class QuestLocation : IWorldEntity, IMapEntity, IMouseoverEntity
    {
        /// <summary>
        /// Name of this quest.
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// Trader who gives this quest.
        /// </summary>
        public string TraderName { get; }
        
        /// <summary>
        /// Type of quest objective.
        /// </summary>
        public QuestObjectiveType Type { get; }
        
        /// <summary>
        /// Description of the objective.
        /// </summary>
        public string ObjectiveDescription { get; }
        
        /// <summary>
        /// Action text for this objective (e.g., "PICK UP: Item Name").
        /// </summary>
        public string ActionText { get; }
        
        /// <summary>
        /// Mouse-over position for radar interaction.
        /// </summary>
        public Vector2 MouseoverPosition { get; set; }

        private readonly Vector3 _position;
        
        /// <summary>
        /// World position of the quest location.
        /// </summary>
        public ref readonly Vector3 Position => ref _position;

        public QuestLocation(string questID, string objectiveId, Vector3 position)
        {
            _position = position;
            
            QuestObjectiveType foundType = QuestObjectiveType.Unknown;
            string actionText = null;
            string description = null;
            string traderName = null;
            
            // Resolve quest name and objective details from API data
            if (TarkovDataManager.TaskData.TryGetValue(questID, out var task))
            {
                Name = task.Name ?? questID;
                traderName = task.Trader?.Name;

                // Find the objective that corresponds to this location
                try
                {
                    if (task.Objectives is not null)
                    {
                        var obj = task.Objectives.FirstOrDefault(o =>
                            (!string.IsNullOrEmpty(o.Id) && string.Equals(o.Id, objectiveId, StringComparison.OrdinalIgnoreCase))
                            || (o.MarkerItem?.Id is not null && string.Equals(o.MarkerItem.Id, objectiveId, StringComparison.OrdinalIgnoreCase))
                            || (o.Zones?.Any(z => string.Equals(z.Id, objectiveId, StringComparison.OrdinalIgnoreCase)) == true)
                        );
                        
                        if (obj != null)
                        {
                            foundType = obj.Type;
                            description = obj.Description;
                            actionText = GetActionText(obj);
                        }
                    }
                }
                catch
                {
                    // Swallow any unexpected structure errors
                }
            }
            else
            {
                // Fallback when TaskData doesn't contain the quest
                Name = questID;
            }

            Type = foundType;
            TraderName = traderName ?? "Unknown";
            ObjectiveDescription = description;
            ActionText = actionText ?? GetDefaultActionText(foundType);
        }

        /// <summary>
        /// Gets the action text based on the objective type and data.
        /// </summary>
        private static string GetActionText(TarkovDataManager.TaskElement.ObjectiveElement obj)
        {
            return obj.Type switch
            {
                QuestObjectiveType.FindQuestItem => 
                    $"PICK UP: {obj.QuestItem?.Name ?? obj.QuestItem?.ShortName ?? "Quest Item"}",
                
                QuestObjectiveType.FindItem => 
                    $"FIND: {obj.Item?.Name ?? obj.Item?.ShortName ?? "Item"}" + 
                    (obj.Count > 1 ? $" x{obj.Count}" : "") +
                    (obj.FoundInRaid ? " (FIR)" : ""),
                
                QuestObjectiveType.PlantItem => 
                    $"PLANT: {obj.MarkerItem?.Name ?? obj.MarkerItem?.ShortName ?? "Item"}",
                
                QuestObjectiveType.PlantQuestItem => 
                    $"PLANT: {obj.QuestItem?.Name ?? "Quest Item"}",
                
                QuestObjectiveType.Mark => 
                    $"MARK: {obj.MarkerItem?.Name ?? "Location"}",
                
                QuestObjectiveType.Visit => 
                    "GO TO: Location",
                
                QuestObjectiveType.Shoot => 
                    $"ELIMINATE: {(obj.Count > 0 ? $"{obj.Count} targets" : "Targets")}",
                
                QuestObjectiveType.Extract => 
                    "EXTRACT: Survive and leave",
                
                QuestObjectiveType.GiveItem => 
                    $"HAND OVER: {obj.Item?.Name ?? obj.Item?.ShortName ?? "Item"}" +
                    (obj.Count > 1 ? $" x{obj.Count}" : ""),
                
                QuestObjectiveType.GiveQuestItem => 
                    $"HAND OVER: {obj.QuestItem?.Name ?? "Quest Item"}",
                
                _ => GetDefaultActionText(obj.Type)
            };
        }

        /// <summary>
        /// Gets a default action text for the objective type.
        /// </summary>
        private static string GetDefaultActionText(QuestObjectiveType type)
        {
            return type switch
            {
                QuestObjectiveType.FindQuestItem => "PICK UP: Quest Item",
                QuestObjectiveType.FindItem => "FIND: Item",
                QuestObjectiveType.PlantItem or QuestObjectiveType.PlantQuestItem => "PLANT: Item",
                QuestObjectiveType.Mark => "MARK: Location",
                QuestObjectiveType.Visit => "GO TO: Location",
                QuestObjectiveType.Shoot => "ELIMINATE: Targets",
                QuestObjectiveType.Extract => "EXTRACT",
                QuestObjectiveType.GiveItem or QuestObjectiveType.GiveQuestItem => "HAND OVER: Item",
                _ => "OBJECTIVE"
            };
        }

        public void Draw(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            var point = Position.ToMapPos(mapParams.Map).ToZoomedPos(mapParams);
            MouseoverPosition = new Vector2(point.X, point.Y);
            var heightDiff = Position.Y - localPlayer.Position.Y;
            SKPaints.ShapeOutline.StrokeWidth = 2f;
            
            if (heightDiff > 1.45) // marker is above player
            {
                using var path = point.GetUpArrow();
                canvas.DrawPath(path, SKPaints.ShapeOutline);
                canvas.DrawPath(path, SKPaints.PaintQuestZone);
            }
            else if (heightDiff < -1.45) // marker is below player
            {
                using var path = point.GetDownArrow();
                canvas.DrawPath(path, SKPaints.ShapeOutline);
                canvas.DrawPath(path, SKPaints.PaintQuestZone);
            }
            else // marker is level with player
            {
                var squareSize = 8 * App.Config.UI.UIScale;
                canvas.DrawRect(point.X, point.Y, squareSize, squareSize, SKPaints.ShapeOutline);
                canvas.DrawRect(point.X, point.Y, squareSize, squareSize, SKPaints.PaintQuestZone);
            }
        }

        public void DrawMouseover(SKCanvas canvas, EftMapParams mapParams, LocalPlayer localPlayer)
        {
            using var lines = new PooledList<string>();
            
            // Line 1: Quest name with trader
            lines.Add($"[{TraderName}] {Name}");
            
            // Line 2: Action text (what to do here)
            if (!string.IsNullOrEmpty(ActionText))
                lines.Add(ActionText);
            
            // Line 3: Objective description (if available and different from action)
            if (!string.IsNullOrEmpty(ObjectiveDescription) && 
                !ObjectiveDescription.Equals(ActionText, StringComparison.OrdinalIgnoreCase))
            {
                // Truncate long descriptions
                var desc = ObjectiveDescription.Length > 60 
                    ? ObjectiveDescription[..57] + "..." 
                    : ObjectiveDescription;
                lines.Add(desc);
            }
            
            // Line 4: Distance to player
            var distance = MathF.Sqrt(Vector3.DistanceSquared(Position, localPlayer.Position));
            lines.Add($"Distance: {distance:F0}m");
            
            Position.ToMapPos(mapParams.Map).ToZoomedPos(mapParams).DrawMouseoverText(canvas, lines.Span);
        }
    }
}

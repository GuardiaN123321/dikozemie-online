using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Dikozemie.Online
{
    /// <summary>
    /// Identifies a concrete owned item to the hero-menu input fallback.
    /// Gauntlet still renders its normal DragWidget; the marker properties let
    /// the multiplayer-safe layer resolve the source even when the generic
    /// ButtonWidget never emits Command.DragBegin.
    /// </summary>
    public sealed class DikozemieInventoryDragSourceWidget : ButtonWidget
    {
        private string _itemId = string.Empty;
        private int _sourceSlot = -1;
        private int _targetSlot = -1;

        public DikozemieInventoryDragSourceWidget(UIContext context)
            : base(context)
        {
        }

        [Editor(false)]
        public string ItemId
        {
            get => _itemId;
            set
            {
                string safe = value ?? string.Empty;
                if (_itemId == safe)
                {
                    return;
                }
                _itemId = safe;
                OnPropertyChanged(safe, nameof(ItemId));
            }
        }

        [Editor(false)]
        public int SourceSlot
        {
            get => _sourceSlot;
            set
            {
                if (_sourceSlot == value)
                {
                    return;
                }
                _sourceSlot = value;
                OnPropertyChanged(value, nameof(SourceSlot));
            }
        }

        [Editor(false)]
        public int TargetSlot
        {
            get => _targetSlot;
            set
            {
                if (_targetSlot == value)
                {
                    return;
                }
                _targetSlot = value;
                OnPropertyChanged(value, nameof(TargetSlot));
            }
        }
    }
}

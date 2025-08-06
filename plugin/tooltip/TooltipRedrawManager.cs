using UnityEngine;
using static LootValuePlus.TooltipController;

namespace LootValuePlus
{
    public class TooltipRedrawManager : MonoBehaviour
    {
        void Update()
        {
            if (HoverItemController.hoveredItem == null)
                return;

            if (!LootValueMod.ShowTotalFleaValueOfContainedItems.Value)
                return;

            if (ClickItemController.itemSells.Contains(HoverItemController.hoveredItem.Id))
                return;

            if (!ScreenChangeController.CanShowItemPriceTooltipsOnCurrentScreen())
                return;


            if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyUp(KeyCode.LeftAlt))
            {
                if (GameTooltipContext.Tooltip != null)
                {
                    GameTooltipContext.Tooltip?.Show(text: GameTooltipContext.Text, delay: GameTooltipContext.Delay);
                }
            }

        }
    }
}


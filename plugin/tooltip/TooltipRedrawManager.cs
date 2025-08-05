using UnityEngine;
using static LootValuePlus.TooltipController;

namespace LootValuePlus
{
    public class TooltipRedrawManager : MonoBehaviour
    {
        void Update()
        {

            if (HoverItemController.hoveredItem != null
                && LootValueMod.ShowTotalFleaValueOfContainedItems.Value
                && !ClickItemController.itemSells.Contains(HoverItemController.hoveredItem.Id) )
            {
                if (Input.GetKeyDown(KeyCode.LeftAlt))
                {
                    if (GameTooltipContext.Tooltip != null)
                    {
                        GameTooltipContext.Tooltip?.Show(text: GameTooltipContext.Text, delay: GameTooltipContext.Delay);
                    }
                }

                if (Input.GetKeyUp(KeyCode.LeftAlt))
                {
                    if (GameTooltipContext.Tooltip != null)
                    {
                        GameTooltipContext.Tooltip?.Show(text: GameTooltipContext.Text, delay: GameTooltipContext.Delay);
                    }
                }
            }
            
        }
    }
}


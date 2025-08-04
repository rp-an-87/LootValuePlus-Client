using UnityEngine;

namespace LootValuePlus
{
    public class TooltipRedrawManager : MonoBehaviour
    {
        void Update()
        {

            if (HoverItemController.hoveredItem != null && LootValueMod.ShowTotalFleaValueOfContainedItems.Value)
            {
                if (Input.GetKeyDown(KeyCode.LeftAlt))
                {
                    if (TooltipContext.Tooltip != null)
                    {
                        TooltipContext.Tooltip.Show(text: TooltipContext.Text, delay: TooltipContext.Delay);
                    }
                }

                if (Input.GetKeyUp(KeyCode.LeftAlt))
                {
                    if (TooltipContext.Tooltip != null)
                    {
                        TooltipContext.Tooltip.Show(text: TooltipContext.Text, delay: TooltipContext.Delay);
                    }
                }
            }
            
        }
    }
}


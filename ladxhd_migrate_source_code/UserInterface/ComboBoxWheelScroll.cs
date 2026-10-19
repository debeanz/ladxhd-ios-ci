using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace LADXHD_Migrater
{
    public static class ComboBoxWheelScroll
    {
        public static void Enable(params ComboBox[] boxes)
        {
            foreach (ComboBox box in boxes)
            {
                box.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
            }
        }

        private static void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            // We only want comboboxes.
            if (sender is not ComboBox box) 
                return;

            // If the ComboBox is not open and is enabled.
            if (box.IsDropDownOpen || !box.IsEnabled || box.ItemCount == 0) 
                return;

            // Get the mouse vertical scrolling.
            int step = e.Delta.Y > 0 ? -1 : e.Delta.Y < 0 ? 1 : 0;

            // Return if no changes.
            if (step == 0) 
                return;

            // Get the previous or next box based on direction.
            int index = box.SelectedIndex + step;

            // Select the previous or next option.
            if (index >= 0 && index < box.ItemCount)
                box.SelectedIndex = index;

            e.Handled = true;
        }
    }
}
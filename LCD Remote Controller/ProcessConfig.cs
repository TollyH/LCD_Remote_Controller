using System;

namespace LCD_Remote_Controller
{
    [Serializable]
    public readonly record struct ProcessConfig(
        string Path,
        byte LCDWidth = 16,
        byte LCDHeight = 2
    );
}

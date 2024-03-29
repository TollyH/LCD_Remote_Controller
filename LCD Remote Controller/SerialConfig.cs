using System;
using System.IO.Ports;

namespace LCD_Remote_Controller
{
    [Serializable]
    public readonly record struct SerialConfig(
        string Device,
        int BaudRate = 115200,
        Parity ParityBits = Parity.None,
        int DataBits = 8,
        StopBits StopBits = StopBits.One,
        string NewLine = "\r"
    );
}

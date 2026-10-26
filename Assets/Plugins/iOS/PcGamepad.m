// PC platform layer, macOS: game pads through Apple's GameController framework (PcMacPad.cs). NOT shipped code.
//
// Unity 2019.4's legacy Input reads a Mac pad as a raw HID joystick with device-specific button / axis numbers (a DualSense or
// DualShock 4 is no Xbox layout, and gamecontrollerd may switch a DualSense into its extended report, which raw HID readers cannot
// parse). GCExtendedGamepad gives every supported pad (DualSense, DualShock 4, Xbox, MFi, Switch Pro) one layout by POSITION:
// buttonA = bottom (Cross), buttonB = right (Circle), buttonX = left (Square), buttonY = top (Triangle).
// Build: setup/native/pcgamepad/build.sh -> Assets/Plugins/macOS/PcGamepad.bundle (x86_64 + arm64; the editor runs x86_64).
#import <Foundation/Foundation.h>
#import <GameController/GameController.h>
#include <stdint.h>
#include <string.h>

// bit numbers of PcGcState.buttons (mirrored in PcMacPad.cs)
enum
{
    PcGc_South, PcGc_East, PcGc_West, PcGc_North,
    PcGc_L1, PcGc_R1, PcGc_L2, PcGc_R2,
    PcGc_Share, PcGc_Options, PcGc_L3, PcGc_R3,
    PcGc_Up, PcGc_Down, PcGc_Left, PcGc_Right,
    PcGc_Home, PcGc_Touchpad,
};

typedef struct
{
    int32_t pads;               // extended gamepads connected
    uint32_t buttons;           // 1 << PcGc_*, OR-ed over every pad
    float lx, ly, rx, ry;       // -1..1, y up; summed over every pad
    float lt, rt;               // 0..1, the largest over every pad
} PcGcState;

static int started;

static void Start(void)
{
    if (started)
        return;
    started = 1;
    // input reaches the process while another app (Terminal, an editor window of another process) is frontmost
    if (@available(macOS 11.3, *))
        GCController.shouldMonitorBackgroundEvents = YES;
}

static void Bit(uint32_t *b, int bit, GCControllerButtonInput *button)
{
    if (button != nil && button.isPressed)
        *b |= 1u << bit;
}

int PcGc_Read(PcGcState *s)
{
    memset(s, 0, sizeof *s);
    @autoreleasepool
    {
        Start();
        for (GCController *c in [GCController controllers])
        {
            GCExtendedGamepad *g = c.extendedGamepad;
            if (g == nil)
                continue;
            s->pads++;
            uint32_t b = 0;
            Bit(&b, PcGc_South, g.buttonA);
            Bit(&b, PcGc_East, g.buttonB);
            Bit(&b, PcGc_West, g.buttonX);
            Bit(&b, PcGc_North, g.buttonY);
            Bit(&b, PcGc_L1, g.leftShoulder);
            Bit(&b, PcGc_R1, g.rightShoulder);
            Bit(&b, PcGc_L2, g.leftTrigger);
            Bit(&b, PcGc_R2, g.rightTrigger);
            if (@available(macOS 10.15, *))
            {
                Bit(&b, PcGc_Share, g.buttonOptions);
                Bit(&b, PcGc_Options, g.buttonMenu);
            }
            if (@available(macOS 10.14.1, *))
            {
                Bit(&b, PcGc_L3, g.leftThumbstickButton);
                Bit(&b, PcGc_R3, g.rightThumbstickButton);
            }
            Bit(&b, PcGc_Up, g.dpad.up);
            Bit(&b, PcGc_Down, g.dpad.down);
            Bit(&b, PcGc_Left, g.dpad.left);
            Bit(&b, PcGc_Right, g.dpad.right);
            if (@available(macOS 11.0, *))
                Bit(&b, PcGc_Home, g.buttonHome);
            if (@available(macOS 11.3, *))
                if ([g isKindOfClass:[GCDualSenseGamepad class]])
                    Bit(&b, PcGc_Touchpad, ((GCDualSenseGamepad *)g).touchpadButton);
            s->buttons |= b;
            s->lx += g.leftThumbstick.xAxis.value;
            s->ly += g.leftThumbstick.yAxis.value;
            s->rx += g.rightThumbstick.xAxis.value;
            s->ry += g.rightThumbstick.yAxis.value;
            if (g.leftTrigger.value > s->lt)
                s->lt = g.leftTrigger.value;
            if (g.rightTrigger.value > s->rt)
                s->rt = g.rightTrigger.value;
        }
    }
    return s->pads;
}

// One line per controller the framework knows ("<vendor> | <category> | <profile class> | extended yes/no"); returns the length.
int PcGc_Describe(char *buf, int cap)
{
    if (buf == NULL || cap <= 0)
        return 0;
    buf[0] = 0;
    @autoreleasepool
    {
        Start();
        NSMutableString *out = [NSMutableString string];
        for (GCController *c in [GCController controllers])
        {
            NSString *category = @"?";
            if (@available(macOS 10.15, *))
                category = c.productCategory ?: @"?";
            [out appendFormat:@"%@ | %@ | %@ | extended %@\n", c.vendorName ?: @"(unnamed)", category,
                c.extendedGamepad ? NSStringFromClass([c.extendedGamepad class]) : @"-",
                c.extendedGamepad ? @"yes" : @"no"];
        }
        const char *u = out.UTF8String;
        strncpy(buf, u, (size_t)cap - 1);
        buf[cap - 1] = 0;
        return (int)strlen(buf);
    }
}

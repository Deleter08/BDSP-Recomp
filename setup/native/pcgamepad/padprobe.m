// padprobe: what PcGamepad.bundle reads from the connected pads, without Unity. Prints the controllers, then every change of the
// buttons (by POSITION: South = Cross on a DualSense) and of the sticks / triggers, for <seconds> (default 60).
//   arch -x86_64 setup/native/pcgamepad/padprobe [seconds]      (x86_64 = what the Unity editor runs under Rosetta)
#import <Foundation/Foundation.h>
#include <math.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>

typedef struct { int32_t pads; uint32_t buttons; float lx, ly, rx, ry, lt, rt; } PcGcState;
int PcGc_Read(PcGcState *s);
int PcGc_Describe(char *buf, int cap);

static const char *names[] = { "South(Cross)", "East(Circle)", "West(Square)", "North(Triangle)", "L1", "R1", "L2", "R2",
    "Share/Create", "Options", "L3", "R3", "DpadUp", "DpadDown", "DpadLeft", "DpadRight", "Home(PS)", "Touchpad" };

int main(int argc, char **argv)
{
    double seconds = argc > 1 ? atof(argv[1]) : 60;
    PcGcState last = { -1 };
    char desc[2048], lastDesc[2048] = "\x01";
    printf("padprobe: %s, %.0f s - press every button\n", sizeof(void *) == 8 ? (
#if defined(__x86_64__)
        "x86_64"
#else
        "arm64"
#endif
        ) : "?", seconds);
    NSDate *end = [NSDate dateWithTimeIntervalSinceNow:seconds];
    while ([end timeIntervalSinceNow] > 0)
    {
        CFRunLoopRunInMode(kCFRunLoopDefaultMode, 1.0 / 60, false);   // GCController connects pads on the main run loop
        PcGcState s;
        PcGc_Read(&s);
        PcGc_Describe(desc, sizeof desc);
        if (strcmp(desc, lastDesc) != 0)
        {
            printf("controllers (%d extended):\n%s", s.pads, desc[0] ? desc : "  (none)\n");
            strcpy(lastDesc, desc);
        }
        uint32_t down = s.buttons & ~(last.pads < 0 ? 0 : last.buttons), up = (last.pads < 0 ? 0 : last.buttons) & ~s.buttons;
        for (int i = 0; i < 18; i++)
        {
            if (down & (1u << i)) printf("  press   %s\n", names[i]);
            if (up & (1u << i)) printf("  release %s\n", names[i]);
        }
        if (last.pads < 0 || fabsf(s.lx - last.lx) > 0.25f || fabsf(s.ly - last.ly) > 0.25f || fabsf(s.rx - last.rx) > 0.25f
            || fabsf(s.ry - last.ry) > 0.25f || fabsf(s.lt - last.lt) > 0.25f || fabsf(s.rt - last.rt) > 0.25f)
        {
            printf("  sticks L(%+.2f,%+.2f) R(%+.2f,%+.2f) triggers L2 %.2f R2 %.2f\n", s.lx, s.ly, s.rx, s.ry, s.lt, s.rt);
            last.lx = s.lx; last.ly = s.ly; last.rx = s.rx; last.ry = s.ry; last.lt = s.lt; last.rt = s.rt;
        }
        last.pads = s.pads;
        last.buttons = s.buttons;
        fflush(stdout);
    }
    return 0;
}

// PC platform layer (macOS player only): the modal text-entry / confirm dialogs that stand in for the Switch software-keyboard
// applet in a standalone .app (the editor uses PcSwkbdWindow, a Windows player PcSwkbdNative). NOT shipped code.
// NSAlert -runModal blocks the calling thread - Unity's macOS player runs scripts on the AppKit main thread - as the applet
// suspends the game. Built by setup/native/pcmacdialog/build.sh into Assets/Plugins/macOS/PcMacDialog.bundle.
#import <Cocoa/Cocoa.h>

static NSString *Str(const char *s) { return s ? [NSString stringWithUTF8String:s] : @""; }

// The text field refuses input beyond maxChars UTF-16 units (the game's textMaxLength).
@interface PcLimitFormatter : NSFormatter
@property (nonatomic) NSUInteger limit;
@end

@implementation PcLimitFormatter
- (NSString *)stringForObjectValue:(id)obj { return [obj isKindOfClass:[NSString class]] ? obj : @""; }
- (BOOL)getObjectValue:(id *)obj forString:(NSString *)string errorDescription:(NSString **)error { *obj = string; return YES; }
- (BOOL)isPartialStringValid:(NSString *)partial newEditingString:(NSString **)newString errorDescription:(NSString **)error
{
    return self.limit == 0 || partial.length <= self.limit;
}
@end

static void Front(void)
{
    [NSApp activateIgnoringOtherApps:YES];
}

// 1 = OK (out = UTF-8 text, NUL-terminated, at most outBytes - 1 bytes), 0 = Cancel / Escape.
int PcMacAsk(const char *title, const char *message, const char *initial, const char *okText, int maxChars, char *out, int outBytes)
{
    @autoreleasepool
    {
        Front();
        NSAlert *alert = [[NSAlert alloc] init];
        alert.messageText = Str(title).length ? Str(title) : @"Software keyboard";
        alert.informativeText = Str(message);
        [alert addButtonWithTitle:Str(okText).length ? Str(okText) : @"OK"];
        [alert addButtonWithTitle:@"Cancel"];
        alert.buttons[1].keyEquivalent = @"\033";
        NSTextField *field = [[NSTextField alloc] initWithFrame:NSMakeRect(0, 0, 320, 24)];
        PcLimitFormatter *fmt = [[PcLimitFormatter alloc] init];
        fmt.limit = maxChars > 0 ? (NSUInteger)maxChars : 0;
        field.formatter = fmt;
        field.stringValue = Str(initial);
        alert.accessoryView = field;
        alert.window.initialFirstResponder = field;
        [alert layout];
        NSModalResponse r = [alert runModal];
        if (r != NSAlertFirstButtonReturn)
            return 0;
        const char *utf8 = field.stringValue.UTF8String;
        if (!out || outBytes <= 0)
            return 1;
        strlcpy(out, utf8 ? utf8 : "", (size_t)outBytes);
        return 1;
    }
}

// 1 = OK, 0 = Back.
int PcMacConfirm(const char *title, const char *message)
{
    @autoreleasepool
    {
        Front();
        NSAlert *alert = [[NSAlert alloc] init];
        alert.messageText = Str(title).length ? Str(title) : @"Software keyboard";
        alert.informativeText = Str(message);
        [alert addButtonWithTitle:@"OK"];
        [alert addButtonWithTitle:@"Back"];
        alert.buttons[1].keyEquivalent = @"\033";
        return [alert runModal] == NSAlertFirstButtonReturn ? 1 : 0;
    }
}

// Unity 6 frame timer (PcFrameRate, s-arm64 perf). The Unity 6000.6 macOS player runs one PlayerLoop per tick of a repeating
// NSTimer that -[PlayerAppDelegate UpdateTimer] arms through -SetupPlayerTimer: at 1 / (refresh / vSyncCount) - measured 1/59 s
// on a 60 Hz display - or 1 / targetFrameRate. That timer beats against the V-Sync present: 15-25 fps at a GPU cost of 4.5 ms, the
// main thread 75 % idle (Metal HUD + sample, 2026-10-22). With interval 0 (Unity's own uncapped value) the present paces the frames:
// locked 60 fps, 0 missed V-Syncs. So a frame-rate interval (> 20 Hz) becomes 0; PcFrameRate's limiter then holds any cap.
// Longer intervals pass through. The 2019.4 player has no such timer and never calls this.
#import <objc/runtime.h>
#import <objc/message.h>

static IMP pcSetupPlayerTimer;

static void PcSetupPlayerTimer(id self, SEL cmd, double interval)
{
    if (interval > 0 && interval < 0.05)
        interval = 0;
    ((void (*)(id, SEL, double))pcSetupPlayerTimer)(self, cmd, interval);
}

// 1 = applied (the running timer re-armed), 0 = no such Unity method (another player version: nothing changed),
// -1 = the method's signature is not (double) (nothing changed). Main thread only.
int PcMacFrameTimerFix(void)
{
    Class delegateClass = NSClassFromString(@"PlayerAppDelegate");
    Method m = delegateClass ? class_getInstanceMethod(delegateClass, NSSelectorFromString(@"SetupPlayerTimer:")) : NULL;
    if (!m)
        return 0;
    const char *types = method_getTypeEncoding(m);
    if (!types || method_getNumberOfArguments(m) != 3 || types[0] != 'v' || !strchr(types, 'd'))
        return -1;
    if (!pcSetupPlayerTimer)
        pcSetupPlayerTimer = method_setImplementation(m, (IMP)PcSetupPlayerTimer);
    id delegate = [NSApp delegate];
    SEL update = NSSelectorFromString(@"UpdateTimer");
    if ([delegate isKindOfClass:delegateClass] && [delegate respondsToSelector:update])
        ((void (*)(id, SEL))objc_msgSend)(delegate, update);
    return 1;
}

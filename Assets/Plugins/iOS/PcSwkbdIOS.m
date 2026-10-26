// PC platform layer (iOS player only): the modal text-entry / confirm dialogs that stand in for the Switch software-keyboard
// applet in an iOS build (the macOS player uses NSAlert in PcMacDialog.bundle). NOT shipped code. UIAlertController + a text
// field blocks the calling thread - Unity's iOS player runs scripts on the main thread - via run-loop pumping, as the applet
// suspends the game. Done = OK, Cancel / Back = Cancel. If no view controller can be found (or the alert never presents),
// Ask returns 0 (Cancel) and Confirm returns 1 (accept), so the caller never hangs.
//
// While the alert is up the Unity player is paused (UnitySetPlayerPause) and the nested run loop only runs default-mode sources with
// runMode:beforeDate:, with a re-entry guard, so the player loop is not driven from inside the managed call.
#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

// UnityPauseMode / UnityPauseFlags / UnitySetPlayerPause come from the Unity prefix header (UnityInterface.h).

// Mirrors nn.swkbd.KeyboardMode / PasswordMode (Assets/Scripts/nn/nn/swkbd).
enum { PcKbNumeric = 1, PcKbASCII = 2, PcKbLatin = 3, PcKbAlphabet = 4 };
enum { PcPwHide = 1 };

static BOOL sBusy = NO;

static NSString *Str(const char *s) { return s ? [NSString stringWithUTF8String:s] : @""; }

// The key window of the foreground-active window scene (iOS 13+); falls back to any key window, then the legacy list
// (Unity builds without a scene manifest report no connected scenes).
static UIWindow *KeyWindow(void)
{
    UIWindow *fallback = nil;
    if (@available(iOS 13.0, *))
    {
        for (UIScene *scene in [UIApplication sharedApplication].connectedScenes)
        {
            if (![scene isKindOfClass:[UIWindowScene class]])
                continue;
            UIWindowScene *ws = (UIWindowScene *)scene;
            if (ws.activationState != UISceneActivationStateForegroundActive)
                continue;
            for (UIWindow *w in ws.windows)
                if (w.isKeyWindow)
                    return w;
            if (!fallback)
                fallback = ws.windows.firstObject;
        }
        if (fallback)
            return fallback;
    }
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
    UIApplication *app = [UIApplication sharedApplication];
    if (app.keyWindow)
        return app.keyWindow;
    for (UIWindow *w in app.windows)
        if (w.isKeyWindow)
            return w;
    return app.windows.firstObject;
#pragma clang diagnostic pop
}

// The top-most view controller the alert should be presented from (Unity's GL view controller, or whatever is on top).
static UIViewController *TopViewController(void)
{
    UIWindow *window = KeyWindow();
    UIViewController *vc = window ? window.rootViewController : nil;
    while (vc && vc.presentedViewController && !vc.presentedViewController.isBeingDismissed)
        vc = vc.presentedViewController;
    return vc;
}

// Drops trailing composed character sequences (never half a surrogate pair / combining sequence) until the string is at most
// maxUnits UTF-16 units (when > 0) and fits in outBytes - 1 UTF-8 bytes (when > 0).
static NSString *Fit(NSString *s, NSInteger maxUnits, NSInteger outBytes)
{
    while (s.length > 0 &&
           ((maxUnits > 0 && s.length > (NSUInteger)maxUnits) ||
            (outBytes > 0 && [s lengthOfBytesUsingEncoding:NSUTF8StringEncoding] >= (NSUInteger)outBytes)))
    {
        NSRange last = [s rangeOfComposedCharacterSequenceAtIndex:s.length - 1];
        s = [s substringToIndex:last.location];
    }
    return s;
}

// Presents `alert` and pumps the run loop until `*result` leaves -1. Returns NO (nothing shown) if no controller, already
// busy, or the presentation never completed (a presentation already in flight elsewhere), so the caller never hangs.
static BOOL RunModal(UIAlertController *alert, int *result)
{
    if (sBusy)
        return NO;
    UIViewController *root = TopViewController();
    if (root == nil)
        return NO;
    sBusy = YES;
    __block BOOL presented = NO;
    UnitySetPlayerPause(kUnityPauseModePause, kPauseFlagSetEngineRunState | kPauseFlagSendPauseMessage);
    [root presentViewController:alert animated:YES completion:^{ presented = YES; }];
    NSDate *giveUp = [NSDate dateWithTimeIntervalSinceNow:3.0];
    BOOL ok = YES;
    while (*result == -1)
    {
        if (!presented && [giveUp timeIntervalSinceNow] < 0)
        {
            ok = NO;
            break;
        }
        // runMode returns NO at once when the mode has no sources; sleep so a bare loop cannot spin the CPU.
        if (![[NSRunLoop currentRunLoop] runMode:NSDefaultRunLoopMode beforeDate:[NSDate dateWithTimeIntervalSinceNow:0.05]])
            [NSThread sleepForTimeInterval:0.01];
    }
    // Only resume if the app is active: if the user backgrounded it during the alert, Unity's own didBecomeActive resumes it.
    if ([UIApplication sharedApplication].applicationState == UIApplicationStateActive)
        UnitySetPlayerPause(kUnityPauseModeResume, kPauseFlagSetEngineRunState | kPauseFlagSendPauseMessage);
    sBusy = NO;
    return ok;
}

// 1 = OK (out = UTF-8 text, NUL-terminated, at most outBytes - 1 bytes), 0 = Cancel. Blocks until answered.
// keyboardMode / passwordMode are the nn.swkbd enum values; maxChars counts UTF-16 units (the console's unit).
int PcIosAsk(const char *title, const char *message, const char *initial, const char *okText, int maxChars,
             int keyboardMode, int passwordMode, char *out, int outBytes)
{
    __block int result = -1;
    __block NSString *answer = @"";
    @autoreleasepool
    {
        UIAlertController *alert = [UIAlertController alertControllerWithTitle:Str(title).length ? Str(title) : @"Software keyboard"
                                                                       message:Str(message)
                                                                preferredStyle:UIAlertControllerStyleAlert];
        [alert addTextFieldWithConfigurationHandler:^(UITextField *field) {
            field.text = Str(initial);
            field.clearButtonMode = UITextFieldViewModeWhileEditing;
            // Smart punctuation turns ' and - into curly quotes / en dashes, which the game's character whitelist rejects.
            field.autocorrectionType = UITextAutocorrectionTypeNo;
            field.spellCheckingType = UITextSpellCheckingTypeNo;
            field.smartQuotesType = UITextSmartQuotesTypeNo;
            field.smartDashesType = UITextSmartDashesTypeNo;
            field.smartInsertDeleteType = UITextSmartInsertDeleteTypeNo;
            field.autocapitalizationType = UITextAutocapitalizationTypeSentences;
            switch (keyboardMode)
            {
                case PcKbNumeric:
                    field.keyboardType = UIKeyboardTypeNumberPad;
                    field.autocapitalizationType = UITextAutocapitalizationTypeNone;
                    break;
                case PcKbASCII:
                case PcKbAlphabet:
                    field.keyboardType = UIKeyboardTypeASCIICapable;
                    field.autocapitalizationType = UITextAutocapitalizationTypeNone;
                    break;
                default:
                    break;
            }
            if (passwordMode == PcPwHide)
            {
                field.secureTextEntry = YES;
                field.autocapitalizationType = UITextAutocapitalizationTypeNone;
            }
        }];
        __weak UITextField *field = alert.textFields.firstObject;
        [alert addAction:[UIAlertAction actionWithTitle:Str(okText).length ? Str(okText) : @"OK" style:UIAlertActionStyleDefault
            handler:^(UIAlertAction *a) { answer = field.text ?: @""; result = 1; }]];
        [alert addAction:[UIAlertAction actionWithTitle:@"Cancel" style:UIAlertActionStyleCancel
            handler:^(UIAlertAction *a) { result = 0; }]];
        if (!RunModal(alert, &result))
            return 0;
    }
    if (result != 1)
        return 0;
    if (!out || outBytes <= 0)
        return 1;
    answer = Fit(answer, maxChars, outBytes);
    const char *utf8 = answer.UTF8String;
    strlcpy(out, utf8 ? utf8 : "", (size_t)outBytes);
    return 1;
}

// 1 = OK, 0 = Back. Blocks until answered.
int PcIosConfirm(const char *title, const char *message)
{
    __block int result = -1;
    @autoreleasepool
    {
        UIAlertController *alert = [UIAlertController alertControllerWithTitle:Str(title).length ? Str(title) : @"Software keyboard"
                                                                       message:Str(message)
                                                                preferredStyle:UIAlertControllerStyleAlert];
        [alert addAction:[UIAlertAction actionWithTitle:@"OK" style:UIAlertActionStyleDefault
            handler:^(UIAlertAction *a) { result = 1; }]];
        [alert addAction:[UIAlertAction actionWithTitle:@"Back" style:UIAlertActionStyleCancel
            handler:^(UIAlertAction *a) { result = 0; }]];
        if (!RunModal(alert, &result))
            return 1;
    }
    return result == 1 ? 1 : 0;
}

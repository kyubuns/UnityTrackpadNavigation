#import <AppKit/AppKit.h>
#include "BridgeCore.h"

namespace
{
trackpad::BridgeCore core;
id monitor = nil;
id deactivateObserver = nil;
id terminateObserver = nil;
NSPoint previousPointer{};
int32_t previousWindow = 0;
double pointerCheckedAt = 0;

double Now()
{
    return NSProcessInfo.processInfo.systemUptime;
}
double ScreenTop()
{
    return NSMaxY(NSScreen.screens.firstObject.frame);
}

TNEvent ReadEvent(NSEvent *event)
{
    TNEvent value{};
    value.timestamp = event.timestamp;
    NSPoint point = event.window ? [event.window convertPointToScreen:event.locationInWindow] : NSEvent.mouseLocation;
    value.screenX = point.x;
    value.screenY = ScreenTop() - point.y;
    value.windowNumber = static_cast<int32_t>(event.windowNumber);
    value.phase = event.type == NSEventTypeSmartMagnify ? 0 : static_cast<int32_t>(event.phase);
    auto modifiers = event.modifierFlags;
    if (modifiers & NSEventModifierFlagShift)
    {
        value.modifiers |= trackpad::Shift;
    }
    if (modifiers & NSEventModifierFlagControl)
    {
        value.modifiers |= trackpad::Control;
    }
    if (modifiers & NSEventModifierFlagOption)
    {
        value.modifiers |= trackpad::Option;
    }
    if (modifiers & NSEventModifierFlagCommand)
    {
        value.modifiers |= trackpad::Command;
    }
    switch (event.type)
    {
    case NSEventTypeScrollWheel:
        value.kind = trackpad::Scroll;
        value.deltaX = event.scrollingDeltaX;
        value.deltaY = event.scrollingDeltaY;
        value.momentumPhase = static_cast<int32_t>(event.momentumPhase);
        if (event.hasPreciseScrollingDeltas)
        {
            value.flags |= trackpad::Precise;
        }
        if (event.isDirectionInvertedFromDevice)
        {
            value.flags |= trackpad::Natural;
        }
        break;
    case NSEventTypeMagnify:
        value.kind = trackpad::Magnify;
        value.magnification = event.magnification;
        break;
    case NSEventTypeRotate:
        value.kind = trackpad::Rotate;
        value.rotation = event.rotation;
        break;
    case NSEventTypeSmartMagnify:
        value.kind = trackpad::SmartZoom;
        break;
    default:
        break;
    }
    return value;
}
} // namespace

int32_t TN_ApiVersion()
{
    return 1;
}
int32_t TN_EventSize()
{
    return sizeof(TNEvent);
}

int32_t TN_Start()
{
    if (!NSThread.isMainThread || NSApp == nil)
    {
        return 0;
    }
    if (monitor)
    {
        return 1;
    }
    core.Reset();
    monitor = [NSEvent addLocalMonitorForEventsMatchingMask:(NSEventMaskScrollWheel | NSEventMaskMagnify | NSEventMaskRotate | NSEventMaskSmartMagnify)
                                                    handler:^NSEvent *(NSEvent *event) {
                                                      // Public local monitor: only this process, no accessibility permission, no global event posting.
                                                      if (NSApp.modalWindow != nil || event.window.sheetParent != nil)
                                                      {
                                                          core.Reset();
                                                          return event;
                                                      }
                                                      bool consumed = core.Process(ReadEvent(event), Now(), NSApp.isActive, NSEvent.pressedMouseButtons != 0);
                                                      return consumed ? nil : event;
                                                    }];
    deactivateObserver = [NSNotificationCenter.defaultCenter addObserverForName:NSApplicationDidResignActiveNotification
                                                                         object:nil
                                                                          queue:nil
                                                                     usingBlock:^(NSNotification *) {
                                                                       core.Reset();
                                                                     }];
    terminateObserver = [NSNotificationCenter.defaultCenter addObserverForName:NSApplicationWillTerminateNotification
                                                                        object:nil
                                                                         queue:nil
                                                                    usingBlock:^(NSNotification *) {
                                                                      TN_Stop();
                                                                    }];
    return monitor != nil;
}

void TN_Stop()
{
    if (!NSThread.isMainThread)
    {
        return;
    }
    if (monitor)
    {
        [NSEvent removeMonitor:monitor];
    }
    if (deactivateObserver)
    {
        [NSNotificationCenter.defaultCenter removeObserver:deactivateObserver];
    }
    if (terminateObserver)
    {
        [NSNotificationCenter.defaultCenter removeObserver:terminateObserver];
    }
    monitor = deactivateObserver = terminateObserver = nil;
    pointerCheckedAt = 0;
    core.Reset();
}

int32_t TN_Poll(TNEvent *event)
{
    if (!NSThread.isMainThread || !event)
    {
        return 0;
    }
    return core.Poll(*event);
}

void TN_SetCapture(const TNCapture *capture, int32_t observe)
{
    if (!NSThread.isMainThread)
    {
        return;
    }
    core.SetCapture(capture ? *capture : TNCapture{}, observe != 0, Now());
}

int32_t TN_GetPointer(TNPointer *pointer)
{
    if (!NSThread.isMainThread || !pointer || !NSApp)
    {
        return 0;
    }
    *pointer = {};
    pointer->active = NSApp.isActive && NSApp.modalWindow == nil && NSEvent.pressedMouseButtons == 0;
    if (!pointer->active)
    {
        return 1;
    }
    NSPoint point = NSEvent.mouseLocation;
    pointer->x = point.x;
    pointer->y = ScreenTop() - point.y;
    double now = Now();
    // Avoid a WindowServer round-trip every Editor update while the pointer is still.
    // A stale window number fails open when Process checks the event's actual window.
    if (!NSEqualPoints(point, previousPointer) || now - pointerCheckedAt >= 0.05)
    {
        previousWindow = static_cast<int32_t>([NSWindow windowNumberAtPoint:point belowWindowWithWindowNumber:0]);
        previousPointer = point;
        pointerCheckedAt = now;
    }
    pointer->windowNumber = previousWindow;
    return 1;
}

void TN_GetStats(TNStats *stats)
{
    if (!NSThread.isMainThread || !stats)
    {
        return;
    }
    *stats = core.stats;
    stats->queued = core.Count();
    stats->installed = monitor != nil;
}

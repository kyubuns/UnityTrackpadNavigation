#import <AppKit/AppKit.h>
#include <algorithm>
#include <array>

struct LiveTouch
{
    float x, y;
};

struct LiveState
{
    int32_t count, modifiers;
    float width, height;
    uint64_t sequence;
};

namespace
{
id monitor = nil;
NSMapTable<NSView *, NSArray<NSNumber *> *> *originalViews = nil;
std::array<LiveTouch, 10> touches{};
LiveState state{0, 0, 1.6f, 1.f, 0};

void EnableTouches(NSView *view)
{
    if (!view)
    {
        return;
    }
    if (![originalViews objectForKey:view])
    {
        [originalViews setObject:@[@(view.allowedTouchTypes), @(view.wantsRestingTouches)] forKey:view];
        view.allowedTouchTypes |= NSTouchTypeMaskIndirect;
        view.wantsRestingTouches = YES;
    }
    for (NSView *child in view.subviews)
    {
        EnableTouches(child);
    }
}

void ReadTouches(NSEvent *event)
{
    state.count = 0;
    ++state.sequence;
    // Magnify/Scrollの推測値ではなく、raw touchイベントの座標を表示する。
    for (NSTouch *touch in [event touchesMatchingPhase:NSTouchPhaseTouching inView:nil])
    {
        if (touch.type != NSTouchTypeIndirect || state.count == static_cast<int32_t>(touches.size()))
        {
            continue;
        }
        NSPoint point = touch.normalizedPosition;
        touches[state.count++] = {static_cast<float>(point.x), static_cast<float>(point.y)};
        NSSize size = touch.deviceSize;
        if (size.width > 0 && size.height > 0)
        {
            state.width = static_cast<float>(size.width);
            state.height = static_cast<float>(size.height);
        }
    }
}
}

extern "C" __attribute__((visibility("default"))) int32_t TL_Start(int32_t stateSize)
{
    if (!NSThread.isMainThread || !NSApp || stateSize != sizeof(LiveState))
    {
        return 0;
    }
    if (!monitor)
    {
        originalViews = [NSMapTable weakToStrongObjectsMapTable];
        state.count = 0;
        monitor = [NSEvent addLocalMonitorForEventsMatchingMask:NSEventMaskGesture handler:^NSEvent *(NSEvent *event) {
            ReadTouches(event);
            return event;
        }];
    }
    return monitor != nil;
}

extern "C" __attribute__((visibility("default"))) void TL_Stop()
{
    if (!NSThread.isMainThread)
    {
        return;
    }
    if (monitor)
    {
        [NSEvent removeMonitor:monitor];
        monitor = nil;
    }
    // Live Viewを閉じたら、Unityの各ビューを元の設定へ戻す。
    for (NSView *view in originalViews)
    {
        NSArray<NSNumber *> *original = [originalViews objectForKey:view];
        view.allowedTouchTypes = static_cast<NSTouchTypeMask>(original[0].unsignedIntegerValue);
        view.wantsRestingTouches = original[1].boolValue;
    }
    originalViews = nil;
    state.count = state.modifiers = 0;
}

extern "C" __attribute__((visibility("default"))) int32_t TL_Poll(LiveState *result, LiveTouch *points, int32_t capacity)
{
    if (!NSThread.isMainThread || !monitor || !result || !points || capacity < 0)
    {
        return 0;
    }
    if (NSApp.isActive)
    {
        for (NSWindow *window in NSApp.windows)
        {
            if (window.isVisible)
            {
                EnableTouches(window.contentView);
            }
        }
        auto flags = NSEvent.modifierFlags;
        state.modifiers = ((flags & NSEventModifierFlagOption) ? 1 : 0) |
                          ((flags & NSEventModifierFlagCommand) ? 2 : 0) |
                          ((flags & NSEventModifierFlagShift) ? 4 : 0) |
                          ((flags & NSEventModifierFlagControl) ? 8 : 0);
    }
    else
    {
        state.count = state.modifiers = 0;
    }
    *result = state;
    result->count = std::min(state.count, capacity);
    std::copy_n(touches.begin(), result->count, points);
    return 1;
}

static_assert(sizeof(LiveState) == 24 && sizeof(LiveTouch) == 8, "Live View ABI changed");

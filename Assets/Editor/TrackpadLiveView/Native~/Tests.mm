#include "TrackpadLiveView.mm"
#include <cassert>
#include <iostream>

@interface PreviewTouch : NSTouch
@end
@implementation PreviewTouch
- (NSTouchType)type
{
    return NSTouchTypeIndirect;
}
- (NSPoint)normalizedPosition
{
    return NSMakePoint(0.25, 0.75);
}
- (NSSize)deviceSize
{
    return NSMakeSize(160, 100);
}
@end

@interface PreviewEvent : NSEvent
@property NSSet<NSTouch *> *points;
@end
@implementation PreviewEvent
- (NSSet<NSTouch *> *)touchesMatchingPhase:(NSTouchPhase)phase inView:(NSView *)view
{
    assert(phase == NSTouchPhaseTouching && view == nil);
    return self.points;
}
@end

int main()
{
    @autoreleasepool
    {
        [NSApplication sharedApplication];
        assert(TL_Start(1) == 0);
        assert(TL_Start(sizeof(LiveState)) == 1);
        assert(TL_Start(sizeof(LiveState)) == 1);
        NSView *root = [NSView new];
        NSView *child = [NSView new];
        [root addSubview:child];
        root.allowedTouchTypes = child.allowedTouchTypes = 0;
        root.wantsRestingTouches = child.wantsRestingTouches = NO;
        EnableTouches(root);
        assert(child.allowedTouchTypes == NSTouchTypeMaskIndirect && child.wantsRestingTouches);
        PreviewEvent *event = [PreviewEvent new];
        event.points = [NSSet setWithObject:[PreviewTouch new]];
        ReadTouches(event);
        assert(state.count == 1 && touches[0].x == 0.25f && touches[0].y == 0.75f);
        assert(state.width == 160 && state.height == 100);
        event.points = [NSSet set];
        ReadTouches(event);
        assert(state.count == 0);
        TL_Stop();
        TL_Stop();
        assert(root.allowedTouchTypes == 0 && child.allowedTouchTypes == 0);
        assert(!root.wantsRestingTouches && !child.wantsRestingTouches);
        LiveState stopped{};
        LiveTouch points[10]{};
        assert(TL_Poll(&stopped, points, 10) == 0);
        std::cout << "Live View: touch coordinates, release, ABI and view restoration passed.\n";
    }
}

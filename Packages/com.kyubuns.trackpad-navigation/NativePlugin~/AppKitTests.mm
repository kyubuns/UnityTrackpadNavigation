#include "TrackpadBridge.mm"
#include <cassert>
#include <thread>
#include <iostream>

@interface TestPinch : NSEvent
@end
@implementation TestPinch
- (NSEventType)type
{
    return NSEventTypeMagnify;
}
- (NSWindow *)window
{
    return nil;
}
- (NSInteger)windowNumber
{
    return 0;
}
- (NSEventPhase)phase
{
    return NSEventPhaseChanged;
}
- (NSEventModifierFlags)modifierFlags
{
    return 0;
}
- (NSTimeInterval)timestamp
{
    return NSProcessInfo.processInfo.systemUptime;
}
- (CGFloat)magnification
{
    return 0.2;
}
@end

int main()
{
    @autoreleasepool
    {
        [NSApplication sharedApplication];
        assert(TN_ApiVersion() == 1 && TN_EventSize() == 96);
        for (int index = 0; index < 2; ++index)
        {
            assert(TN_Start() == 1 && TN_Start() == 1);
            TNStats stats{};
            TN_GetStats(&stats);
            assert(stats.installed == 1);
            TN_Stop();
            TN_Stop();
            TN_GetStats(&stats);
            assert(stats.installed == 0);
        }
        std::thread worker([] {
            TNEvent value{};
            assert(TN_Start() == 0);
            assert(TN_Poll(&value) == 0);
        });
        worker.join();
        TNPointer pointer{};
        assert(TN_GetPointer(&pointer) == 1);
        TN_Start();
        TNStats before{}, after{};
        TN_GetStats(&before);
        [NSApp sendEvent:[TestPinch new]];
        TN_GetStats(&after);
        assert(after.received == before.received + 1 && after.queued == 0);
        TN_Stop();
        std::cout << "AppKit: lifecycle, main-thread guard and windowless pinch dispatch passed.\n";
    }
}

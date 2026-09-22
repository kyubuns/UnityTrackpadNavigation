#include "BridgeCore.h"
#include <cassert>
#include <iostream>
#include <limits>
using namespace trackpad;

TNEvent ScrollEvent()
{
    TNEvent event{};
    event.kind = Scroll;
    event.phase = Began;
    event.flags = Precise;
    event.screenX = 10;
    event.screenY = 20;
    event.windowNumber = 7;
    event.deltaX = 0.125;
    event.deltaY = -0.25;
    return event;
}
void Arm(BridgeCore &core, double now = 1)
{
    core.SetCapture({9.5, 19.5, 1, 1, 42, 7, 3, 0}, false, now);
}
int main()
{
    BridgeCore core;
    auto event = ScrollEvent();
    assert(!core.Process(event, 1, true, false)); // No approved target.
    Arm(core);
    assert(core.Process(event, 1, true, false));
    TNEvent received{};
    assert(core.Poll(received) && received.target == 42 && received.deltaX == 0.125 && received.deltaY == -0.25);
    assert(received.flags & Captured);
    assert(!core.Poll(received));
    event.phase = Changed;
    event.modifiers = Option;
    assert(core.Process(event, 1.1, true, false));
    core.Poll(received);
    assert(received.modifiers == Option);
    event.phase = Ended;
    assert(core.Process(event, 1.12, true, false));
    event.phase = 0;
    event.momentumPhase = Began;
    assert(core.Process(event, 1.13, true, false));
    event.momentumPhase = Ended;
    assert(core.Process(event, 1.14, true, false));
    event = ScrollEvent();
    assert(!core.Process(event, 1.3, true, false)); // Expired lease fails open.
    Arm(core, 2);
    event.phase = Changed;
    assert(!core.Process(event, 2, true, false)); // Cannot seize a passed-through gesture midway.
    event.phase = Began;
    assert(core.Process(event, 2.01, true, false));
    event.phase = Changed;
    event.screenX = 11;
    assert(core.Process(event, 2.02, true, false)); // A captured gesture retains its canvas when the cursor drifts.
    event.phase = Began;
    assert(!core.Process(event, 2.02, true, false)); // New gestures still need a current hit test.
    event = ScrollEvent();
    event.windowNumber = 8;
    assert(!core.Process(event, 2.03, true, false));
    event = ScrollEvent();
    event.flags = 0;
    assert(!core.Process(event, 2.03, true, false)); // Physical wheel unaffected.
    event = ScrollEvent();
    event.modifiers = Command;
    assert(!core.Process(event, 2.03, true, false));
    event = ScrollEvent();
    event.modifiers = Control;
    assert(!core.Process(event, 2.03, true, false));
    core.SetCapture({9.5, 19.5, 1, 1, 42, 7, 7, 0}, false, 2.03);
    event = ScrollEvent();
    event.modifiers = Command;
    assert(core.Process(event, 2.04, true, false)); // Scene-only camera look.
    event = ScrollEvent();
    assert(!core.Process(event, 2.03, true, true));  // Dragging a selection.
    assert(core.Process(event, 2.03, false, false)); // Background trackpad input is ignored instead of reaching standard zoom.
    assert(core.Count() == 0);
    event.flags = 0;
    assert(!core.Process(event, 2.04, false, false)); // Background mouse wheel is unchanged.
    event.kind = Magnify;
    assert(core.Process(event, 2.05, false, false));
    event.kind = SmartZoom;
    assert(core.Process(event, 2.06, false, false));
    Arm(core, 3);
    event.kind = Magnify;
    event.magnification = 0.0125;
    assert(core.Process(event, 3, true, false));
    core.Poll(received);
    assert(received.magnification == 0.0125 && received.kind == Magnify);
    event.phase = Cancelled;
    assert(core.Process(event, 3.01, true, false));
    event.kind = Rotate;
    assert(!core.Process(event, 3.02, true, false));
    event = ScrollEvent();
    event.deltaX = std::numeric_limits<double>::quiet_NaN();
    assert(!core.Process(event, 3.03, true, false));
    core.Reset();
    core.SetCapture({}, true, 4);
    event = ScrollEvent();
    assert(!core.Process(event, 4, true, false));
    assert(core.Poll(received) && !received.target && !(received.flags & Captured));
    Arm(core, 5);
    event = ScrollEvent();
    for (size_t index = 0; index < BridgeCore::Capacity; ++index)
    {
        assert(core.Process(event, 5, true, false));
    }
    assert(!core.Process(event, 5, true, false) && core.stats.overflow == 1);
    core.Poll(received);
    event.phase = Changed;
    assert(!core.Process(event, 5.01, true, false)); // Overflow releases the whole remaining stream.
    core.Reset();
    assert(!core.Poll(received));
    Arm(core, 6);
    event = ScrollEvent();
    event.phase = 0;
    event.momentumPhase = Began;
    assert(!core.Process(event, 6, true, false)); // Orphan momentum cannot acquire target.

    core.Reset();
    Arm(core, 7);
    event = ScrollEvent();
    assert(core.Process(event, 7, true, false));
    core.SetCapture({9.5, 19.5, 1, 1, 42, 7, 0, 0}, false, 7.01);
    event.phase = Changed;
    assert(core.Process(event, 7.02, true, false)); // A node field moving under the cursor must not leak a wheel zoom.
    assert(core.Process(event, 7.4, true, false));  // A busy Editor must not turn an owned Pan into standard zoom.
    core.SetCapture({9.5, 19.5, 1, 1, 42, 7, 0, 0}, false, 8);
    assert(core.Process(event, 8, true, false)); // A stationary pause does not release a phased gesture.
    event.phase = Ended;
    assert(core.Process(event, 8.01, true, false));
    event.phase = 0;
    event.momentumPhase = Began;
    assert(core.Process(event, 8.02, true, false));
    event.momentumPhase = Ended;
    assert(core.Process(event, 8.03, true, false));
    event = ScrollEvent();
    assert(!core.Process(event, 8.04, true, false)); // New input over the field remains standard.
    core.Reset();
    Arm(core, 9);
    assert(core.Process(event, 9, true, false));
    core.SetCapture({9.5, 19.5, 1, 1, 43, 7, 3, 0}, false, 9.01);
    event.phase = Changed;
    assert(!core.Process(event, 9.02, true, false)); // Changing target still releases ownership.
    core.Reset();
    Arm(core, 10);
    event = ScrollEvent();
    event.kind = SmartZoom;
    event.phase = 0;
    assert(!core.Process(event, 10, true, false)); // Editors without framing support keep their input.
    core.SetCapture({9.5, 19.5, 1, 1, 42, 7, 11, 0}, false, 10.01);
    assert(core.Process(event, 10.02, true, false));
    assert(core.Poll(received) && received.kind == SmartZoom && received.target == 42);
    event = ScrollEvent();
    event.flags = 0;
    assert(!core.Process(event, 10.03, true, false)); // Smart Zoom support does not consume mouse wheel events.
    core.Reset();
    event = ScrollEvent();
    event.kind = Magnify;
    for (int phase : {Began, Changed, Ended, Cancelled})
    {
        event.phase = phase;
        assert(core.Process(event, 11, true, false)); // Unclaimed pinches must not maximize Unity views.
        assert(!core.Poll(received));
    }
    Arm(core, 12);
    event.phase = Began;
    assert(core.Process(event, 12.3, true, false) && !core.Poll(received)); // Expired hit test.
    Arm(core, 13);
    event.screenX = 11;
    assert(core.Process(event, 13, true, false) && !core.Poll(received)); // Cursor drift before begin.
    event.screenX = 10;
    assert(core.Process(event, 13, true, false));
    core.Poll(received);
    core.SetCapture({}, false, 13.01);
    event.phase = Changed;
    assert(core.Process(event, 13.02, true, false) && !core.Poll(received)); // Target lost mid-pinch.
    core.Reset();
    Arm(core, 14);
    event.phase = Began;
    for (size_t index = 0; index < BridgeCore::Capacity; ++index)
    {
        assert(core.Process(event, 14, true, false));
    }
    assert(core.Process(event, 14, true, false)); // Queue overflow must not become maximize either.
    core.Reset();
    event = ScrollEvent();
    event.flags = 0;
    assert(!core.Process(event, 15, true, false));
    std::cout << "Native core: all policy, precision, phase, overflow and lifecycle assertions passed.\n";
}

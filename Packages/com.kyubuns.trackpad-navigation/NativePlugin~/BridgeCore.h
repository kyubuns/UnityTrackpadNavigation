#pragma once
#include "TrackpadBridge.h"
#include <array>
#include <cmath>

namespace trackpad
{
constexpr int Scroll = 1, Magnify = 2, Rotate = 3, SmartZoom = 4;
constexpr int Began = 1, Stationary = 2, Changed = 4, Ended = 8, Cancelled = 16, MayBegin = 32;
constexpr int Precise = 1, Natural = 2, Captured = 4;
constexpr int Shift = 1, Control = 2, Option = 4, Command = 8;
constexpr double LeaseSeconds = 0.2;

struct Stream
{
    bool started = false;
    int target = 0;
    double lastTime = 0;
    TNCapture origin{};
};

class BridgeCore
{
  public:
    static constexpr size_t Capacity = 1024;
    TNCapture capture{};
    bool observe = false;
    TNStats stats{};

    void SetCapture(const TNCapture &value, bool observing, double now)
    {
        capture = value;
        observe = observing;
        leaseUntil = now + LeaseSeconds;
    }

    void Reset()
    {
        capture = {};
        scroll = {};
        magnify = {};
        head = count = 0;
        leaseUntil = 0;
        observe = false;
    }

    // All calls run on the AppKit/Unity main thread. No reverse P/Invoke or retained managed pointers.
    bool Process(TNEvent event, double now, bool active, bool mouseDown)
    {
        ++stats.received;
        if (!active)
        {
            Reset();
            // macOS also delivers scrolling to background windows. Keep Unity stationary until focused.
            return (event.kind == Scroll && (event.flags & Precise)) || event.kind == Magnify || event.kind == SmartZoom;
        }
        event.sequence = stats.received;
        bool precise = event.kind != Scroll || (event.flags & Precise) != 0;
        bool finite = std::isfinite(event.deltaX) && std::isfinite(event.deltaY) &&
                      std::isfinite(event.magnification) && std::isfinite(event.screenX) && std::isfinite(event.screenY);
        auto eligible = [&](const TNCapture &area, bool starting) {
            bool blockedModifier = (event.modifiers & Control) || ((event.modifiers & Command) && !(area.kinds & 4));
            return precise && finite && !mouseDown && !blockedModifier &&
                   area.target != 0 && area.windowNumber == event.windowNumber &&
                   (!starting || (now <= leaseUntil && event.screenX >= area.x && event.screenX < area.x + area.width &&
                                  event.screenY >= area.y && event.screenY < area.y + area.height)) &&
                   ((event.kind == Scroll && (area.kinds & 1)) || (event.kind == Magnify && (area.kinds & 2)) ||
                    (event.kind == SmartZoom && (area.kinds & 8)));
        };

        bool consume = event.kind == SmartZoom && eligible(capture, true);
        if (consume)
        {
            event.target = capture.target;
        }
        if (event.kind == Scroll || event.kind == Magnify)
        {
            auto &stream = event.kind == Scroll ? scroll : magnify;
            bool momentum = event.momentumPhase != 0;
            bool begin = (event.phase & (Began | MayBegin)) != 0;
            // A committed phased gesture stays with its original canvas until end or cancellation.
            bool expired = now - stream.lastTime > 0.75 && (momentum || event.phase == 0 || stream.target == 0);
            // Momentum may only continue a stream we already own; it cannot acquire another canvas.
            if (begin || !stream.started || expired)
            {
                stream.started = true;
                stream.target = eligible(capture, true) && !momentum ? capture.target : 0;
                stream.origin = capture;
            }
            // Canvas content may move a control under the stationary cursor. Re-hit-test only new gestures.
            if (!eligible(stream.origin, false) || stream.target != capture.target)
            {
                stream.target = 0;
            }
            consume = stream.target != 0;
            event.target = consume ? stream.target : 0;
            stream.lastTime = now;
            if ((event.phase & Cancelled) || (event.momentumPhase & (Ended | Cancelled)) ||
                (event.kind == Magnify && (event.phase & Ended)))
            {
                stream = {};
            }
        }

        if (consume || observe)
        {
            if (count == Capacity)
            {
                ++stats.overflow;
                scroll.target = magnify.target = 0;
                return event.kind == Magnify; // Never leak a pinch into Unity's maximize gesture.
            }
            if (consume)
            {
                event.flags |= Captured;
                ++stats.captured;
            }
            queue[(head + count) % Capacity] = event;
            ++count;
        }
        // Unity uses unhandled pinches to maximize views. Even an unclaimed/expired gesture
        // must stop here; only captured events are delivered as navigation input.
        return consume || event.kind == Magnify;
    }

    bool Poll(TNEvent &event)
    {
        if (!count)
        {
            return false;
        }
        event = queue[head];
        head = (head + 1) % Capacity;
        --count;
        return true;
    }

    int Count() const
    {
        return static_cast<int>(count);
    }

  private:
    std::array<TNEvent, Capacity> queue{};
    size_t head = 0, count = 0;
    double leaseUntil = 0;
    Stream scroll{}, magnify{};
};
static_assert(sizeof(TNEvent) == 96, "TNEvent ABI changed");
static_assert(sizeof(TNCapture) == 48, "TNCapture ABI changed");
static_assert(sizeof(TNPointer) == 24, "TNPointer ABI changed");
static_assert(sizeof(TNStats) == 32, "TNStats ABI changed");
} // namespace trackpad

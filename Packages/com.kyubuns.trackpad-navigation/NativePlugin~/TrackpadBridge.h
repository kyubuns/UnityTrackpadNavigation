#pragma once
#include <stdint.h>

#ifdef __cplusplus
extern "C"
{
#endif
#define TN_EXPORT __attribute__((visibility("default")))

    // Fixed-width POD only. ABI layout is checked on both sides before installing a monitor.
    typedef struct TNEvent
    {
        double timestamp, screenX, screenY, deltaX, deltaY, magnification, rotation;
        uint64_t sequence;
        int32_t kind, phase, momentumPhase, modifiers, flags, windowNumber, target, reserved;
    } TNEvent;

    typedef struct TNCapture
    {
        double x, y, width, height;
        int32_t target, windowNumber, kinds, reserved;
    } TNCapture;

    typedef struct TNPointer
    {
        double x, y;
        int32_t windowNumber, active;
    } TNPointer;

    typedef struct TNStats
    {
        uint64_t received, captured, overflow;
        int32_t queued, installed;
    } TNStats;

    TN_EXPORT int32_t TN_ApiVersion(void);
    TN_EXPORT int32_t TN_EventSize(void);
    TN_EXPORT int32_t TN_Start(void);
    TN_EXPORT void TN_Stop(void);
    TN_EXPORT int32_t TN_Poll(TNEvent *event);
    TN_EXPORT void TN_SetCapture(const TNCapture *capture, int32_t observe);
    TN_EXPORT int32_t TN_GetPointer(TNPointer *pointer);
    TN_EXPORT void TN_GetStats(TNStats *stats);

#ifdef __cplusplus
}
#endif

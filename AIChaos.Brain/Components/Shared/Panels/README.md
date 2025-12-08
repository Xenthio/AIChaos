# Component Splitting Pattern

This directory contains example panel components demonstrating the recommended pattern for breaking down large Razor components into smaller, focused, reusable pieces.

## Pattern Overview

### Before: Monolithic Component
```csharp
// StreamControlContent.razor (1,268 lines)
@code {
    // Queue state
    private int blastCount = 1;
    private string blastMessage = "";
    // ... 50+ more fields
    
    // Queue methods
    private async Task ManualBlast() { /* ... */ }
    private async Task LoadQueueStatus() { /* ... */ }
    // ... 30+ more methods
}
```

### After: Modular Components
```csharp
// QueueControlPanel.razor (100 lines)
@code {
    [Parameter] public int ActiveSlots { get; set; }
    [Parameter] public int TotalSlots { get; set; }
    [Parameter] public EventCallback OnStatusChanged { get; set; }
    
    private int blastCount = 3;
    private async Task ManualBlast() { /* ... */ }
}
```

## Benefits

1. **Reduced Cognitive Complexity**: Each panel focuses on one responsibility
2. **Better Testability**: Smaller components are easier to test in isolation
3. **Improved Reusability**: Panels can be used in multiple parent components
4. **Easier Maintenance**: Changes to one panel don't affect others
5. **Clear Boundaries**: Component parameters define the contract

## Example: QueueControlPanel

The `QueueControlPanel.razor` component demonstrates:
- **Props**: `ActiveSlots`, `TotalSlots`, `OnStatusChanged`
- **Internal State**: `blastCount`, `statusMessage`
- **Self-Contained**: Manages own status display and messaging
- **Events**: Communicates with parent via `EventCallback`

## Usage Pattern

```razor
<!-- Parent Component -->
<QueueControlPanel 
    ActiveSlots="@activeSlots" 
    TotalSlots="@totalSlots" 
    OnStatusChanged="@LoadQueueStatus" />

@code {
    private int activeSlots = 0;
    private int totalSlots = 0;
    
    private async Task LoadQueueStatus()
    {
        var status = QueueSlots.GetStatus();
        activeSlots = status.OccupiedSlots;
        totalSlots = status.TotalSlots;
        StateHasChanged();
    }
}
```

## Recommended Split for StreamControlContent

The 1,268-line `StreamControlContent.razor` could be split into:

1. **QueueControlPanel** (✓ Example created)
   - Blast controls, queue status, test buttons
   - ~100 lines

2. **StreamSettingsPanel**
   - YouTube video ID, stream start/stop
   - ~120 lines

3. **ImageModerationPanel**
   - Pending image links, approve/deny
   - ~100 lines

4. **RefundRequestsPanel**
   - Refund requests, approve/deny
   - ~100 lines

5. **CodeModerationPanel**
   - Filtered code review, approve/deny
   - ~120 lines

6. **GlobalHistoryPanel**
   - Command history, undo, save payload
   - ~150 lines

**Result**: 6 focused components (~100-150 lines each) instead of 1 monolithic component (1,268 lines)

## Implementation Notes

- Use `EventCallback` for parent communication
- Keep panels stateless where possible (data flows from parent)
- Include inline styles for panel-specific CSS
- Use dependency injection for services within the panel
- Follow naming convention: `[Feature]Panel.razor`

## Future Work

Complete component splitting should be done as a separate PR with:
- Full parent-child state management
- Comprehensive integration testing
- UI/UX validation
- Performance testing

This example demonstrates the pattern without breaking existing functionality.
